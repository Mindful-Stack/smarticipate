using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Smarticipate.Tests.Integration;

public class ResultsAndLifecycleIntegrationTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Results_are_owner_only()
    {
        var owner = await NewTeacherAsync();
        var other = await NewTeacherAsync();
        var session = await CreateSessionAsync(owner, NewCode());
        var def = await CreateDefAsync(owner, 1, "Pick", options: [new { text = "A" }, new { text = "B" }]);
        var activation = await FireAsync(owner, def, session);

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/question-activations/{activation}/results")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/question-activations/{activation}/results")).StatusCode);
    }

    [Fact]
    public async Task Scale_results_have_full_distribution()
    {
        var teacher = await NewTeacherAsync();
        var session = await CreateSessionAsync(teacher, NewCode());
        var def = await CreateDefAsync(teacher, 4, "Rate", config: new { scaleMin = 1, scaleMax = 5 });
        var activation = await FireAsync(teacher, def, session);

        foreach (var (pk, v) in new[] { ("a", 5), ("b", 5), ("c", 3) })
            (await SubmitAsync(new { activationId = activation, participantKey = pk, numeric = v })).EnsureSuccessStatusCode();

        var results = await ResultsAsync(teacher, activation);
        var numeric = results.GetProperty("numeric");
        Assert.Equal(5, numeric.GetProperty("distribution").GetArrayLength()); // steps 1..5 all present
        Assert.Equal(3, results.GetProperty("responseCount").GetInt32());
    }

    [Fact]
    public async Task Activations_by_session_are_positional_and_owner_only()
    {
        var owner = await NewTeacherAsync();
        var other = await NewTeacherAsync();
        var session = await CreateSessionAsync(owner, NewCode());
        var def = await CreateDefAsync(owner, 1, "Pick", options: [new { text = "A" }, new { text = "B" }]);
        var first = await FireAsync(owner, def, session);
        var second = await FireAsync(owner, def, session);

        var list = await owner.GetFromJsonAsync<JsonElement>($"/api/question-activations/session/{session}");
        var items = list.EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(new[] { 1, 2 }, items.Select(i => i.GetProperty("position").GetInt32()));
        Assert.Equal(new[] { first, second }, items.Select(i => i.GetProperty("id").GetInt32())); // Id order == firing order
        Assert.All(items, i => Assert.True(i.TryGetProperty("responseCount", out _)));

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/question-activations/session/{session}")).StatusCode);
    }

    [Fact]
    public async Task Ending_session_closes_activation_and_blocks_submit()
    {
        var teacher = await NewTeacherAsync();
        var code = NewCode();
        var session = await CreateSessionAsync(teacher, code);
        var def = await CreateDefAsync(teacher, 1, "Pick", options: [new { text = "A" }, new { text = "B" }]);
        var activation = await FireAsync(teacher, def, session);
        var options = await OptionIdsAsync(activation);

        (await teacher.PutAsJsonAsync($"/api/sessions/{code}", new { endTime = DateTime.UtcNow })).EnsureSuccessStatusCode();

        var view = await Anon().GetFromJsonAsync<JsonElement>($"/api/question-activations/{activation}");
        Assert.NotEqual(JsonValueKind.Null, view.GetProperty("endTime").ValueKind); // closed on session end

        var submit = await SubmitAsync(new { activationId = activation, participantKey = "late", optionIds = new[] { options[0] } });
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
    }

    [Fact]
    public async Task Deleting_session_removes_its_activations()
    {
        var teacher = await NewTeacherAsync();
        var code = NewCode();
        var session = await CreateSessionAsync(teacher, code);
        var def = await CreateDefAsync(teacher, 1, "Pick", options: [new { text = "A" }, new { text = "B" }]);
        var activation = await FireAsync(teacher, def, session);

        (await teacher.DeleteAsync($"/api/sessions/{code}")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/question-activations/{activation}/results")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Anon().GetAsync($"/api/question-activations/{activation}")).StatusCode);
    }

    [Fact]
    public async Task Creating_a_second_session_ends_the_first()
    {
        var teacher = await NewTeacherAsync();
        var first = NewCode();
        await CreateSessionAsync(teacher, first);
        await CreateSessionAsync(teacher, NewCode()); // one active session per teacher: ends the first

        var status = await Anon().GetFromJsonAsync<JsonElement>($"/api/sessions/code/{first}/status");
        Assert.False(status.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Stale_activation_is_not_answerable_after_end_then_restart()
    {
        var teacher = await NewTeacherAsync();
        var code = NewCode();
        var session = await CreateSessionAsync(teacher, code);
        var def = await CreateDefAsync(teacher, 1, "Pick", options: [new { text = "A" }, new { text = "B" }]);
        var activation = await FireAsync(teacher, def, session);
        var options = await OptionIdsAsync(activation);

        (await teacher.PutAsJsonAsync($"/api/sessions/{code}", new { endTime = DateTime.UtcNow })).EnsureSuccessStatusCode();
        (await teacher.PutAsync($"/api/sessions/{code}/restart", null)).EnsureSuccessStatusCode();

        var submit = await SubmitAsync(new { activationId = activation, participantKey = "r1", optionIds = new[] { options[0] } });
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
    }
}
