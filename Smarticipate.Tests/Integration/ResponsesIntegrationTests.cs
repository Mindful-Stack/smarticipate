using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Smarticipate.Tests.Integration;

public class ResponsesIntegrationTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<(HttpClient teacher, int activation, int[] options)> LiveSingleChoiceAsync()
    {
        var teacher = await NewTeacherAsync();
        var session = await CreateSessionAsync(teacher, NewCode());
        var def = await CreateDefAsync(teacher, 1, "Pick", options: [new { text = "A" }, new { text = "B" }]);
        var activation = await FireAsync(teacher, def, session);
        return (teacher, activation, await OptionIdsAsync(activation));
    }

    [Fact]
    public async Task Anonymous_submit_then_tally()
    {
        var (teacher, activation, options) = await LiveSingleChoiceAsync();

        var submit = await SubmitAsync(new { activationId = activation, participantKey = "p1", optionIds = new[] { options[0] } });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);

        var results = await ResultsAsync(teacher, activation);
        Assert.Equal(1, results.GetProperty("responseCount").GetInt32());
        Assert.Equal(1, TallyFor(results, options[0]));
    }

    [Fact]
    public async Task Wrong_channel_is_rejected()
    {
        var (_, activation, _) = await LiveSingleChoiceAsync();
        var resp = await SubmitAsync(new { activationId = activation, participantKey = "p1", text = "hello" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Revise_keeps_one_response_and_moves_tally()
    {
        var (teacher, activation, options) = await LiveSingleChoiceAsync();

        var first = await SubmitAsync(new { activationId = activation, participantKey = "p1", optionIds = new[] { options[0] } });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var second = await SubmitAsync(new { activationId = activation, participantKey = "p1", optionIds = new[] { options[1] } });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var results = await ResultsAsync(teacher, activation);
        Assert.Equal(1, results.GetProperty("responseCount").GetInt32());
        Assert.Equal(0, TallyFor(results, options[0]));
        Assert.Equal(1, TallyFor(results, options[1]));
    }

    [Fact]
    public async Task MultipleChoice_dedups_repeated_option()
    {
        var teacher = await NewTeacherAsync();
        var session = await CreateSessionAsync(teacher, NewCode());
        var def = await CreateDefAsync(teacher, 2, "Many", options: [new { text = "A" }, new { text = "B" }, new { text = "C" }]);
        var activation = await FireAsync(teacher, def, session);
        var options = await OptionIdsAsync(activation);

        var resp = await SubmitAsync(new { activationId = activation, participantKey = "p1", optionIds = new[] { options[0], options[0], options[1] } });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var results = await ResultsAsync(teacher, activation);
        Assert.Equal(1, TallyFor(results, options[0]));
        Assert.Equal(1, TallyFor(results, options[1]));
    }

    [Fact]
    public async Task MultipleChoice_enforces_max_selections()
    {
        var teacher = await NewTeacherAsync();
        var session = await CreateSessionAsync(teacher, NewCode());
        var def = await CreateDefAsync(teacher, 2, "Many",
            options: [new { text = "A" }, new { text = "B" }, new { text = "C" }], config: new { maxSelections = 1 });
        var activation = await FireAsync(teacher, def, session);
        var options = await OptionIdsAsync(activation);

        var resp = await SubmitAsync(new { activationId = activation, participantKey = "p1", optionIds = new[] { options[0], options[1] } });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(0)]
    public async Task Scale_rejects_out_of_range(int value)
    {
        var teacher = await NewTeacherAsync();
        var session = await CreateSessionAsync(teacher, NewCode());
        var def = await CreateDefAsync(teacher, 4, "Rate", config: new { scaleMin = 1, scaleMax = 5 });
        var activation = await FireAsync(teacher, def, session);

        var resp = await SubmitAsync(new { activationId = activation, participantKey = "p1", numeric = value });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task FreeText_rejects_empty_and_trims_valid()
    {
        var teacher = await NewTeacherAsync();
        var session = await CreateSessionAsync(teacher, NewCode());
        var def = await CreateDefAsync(teacher, 3, "Say");
        var activation = await FireAsync(teacher, def, session);

        var empty = await SubmitAsync(new { activationId = activation, participantKey = "p1", text = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var ok = await SubmitAsync(new { activationId = activation, participantKey = "p1", text = "  hi  " });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var results = await ResultsAsync(teacher, activation);
        Assert.Equal("hi", results.GetProperty("textAnswers")[0].GetString());
    }

    [Fact]
    public async Task Numeric_rejects_out_of_range_and_accepts_in_range()
    {
        var teacher = await NewTeacherAsync();
        var session = await CreateSessionAsync(teacher, NewCode());
        var def = await CreateDefAsync(teacher, 5, "Num", config: new { numericMin = 0, numericMax = 100 });
        var activation = await FireAsync(teacher, def, session);

        var over = await SubmitAsync(new { activationId = activation, participantKey = "n1", numeric = 150 });
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        var ok = await SubmitAsync(new { activationId = activation, participantKey = "n2", numeric = 50 });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }

    [Fact]
    public async Task Submitting_to_a_closed_activation_is_rejected()
    {
        var (teacher, activation, options) = await LiveSingleChoiceAsync();
        (await teacher.PostAsync($"/api/question-activations/{activation}/close", null)).EnsureSuccessStatusCode();

        var resp = await SubmitAsync(new { activationId = activation, participantKey = "c1", optionIds = new[] { options[0] } });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Missing_participant_key_is_rejected()
    {
        var (_, activation, options) = await LiveSingleChoiceAsync();
        var resp = await SubmitAsync(new { activationId = activation, optionIds = new[] { options[0] } });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Option_not_belonging_to_the_question_is_rejected()
    {
        var (_, activation, options) = await LiveSingleChoiceAsync();
        var foreign = options.Max() + 100000; // an id that is not one of this definition's options
        var resp = await SubmitAsync(new { activationId = activation, participantKey = "pf", optionIds = new[] { foreign } });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Submit_to_nonexistent_activation_is_not_found()
    {
        var resp = await SubmitAsync(new { activationId = 999999, participantKey = "z", optionIds = new[] { 1 } });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Concurrent_first_submits_yield_one_response()
    {
        var (teacher, activation, options) = await LiveSingleChoiceAsync();

        var body = new { activationId = activation, participantKey = "race", optionIds = new[] { options[0] } };
        var a = SubmitAsync(body);
        var b = SubmitAsync(body);
        await Task.WhenAll(a, b);

        var results = await ResultsAsync(teacher, activation);
        Assert.Equal(1, results.GetProperty("responseCount").GetInt32());
    }

    private static int TallyFor(JsonElement results, int optionId) =>
        results.GetProperty("options").EnumerateArray()
            .Single(o => o.GetProperty("optionId").GetInt32() == optionId)
            .GetProperty("count").GetInt32();
}
