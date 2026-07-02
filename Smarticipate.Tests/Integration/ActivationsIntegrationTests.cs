using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Smarticipate.Tests.Integration;

public class ActivationsIntegrationTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Fire_returns_created_with_positive_duration()
    {
        var teacher = await NewTeacherAsync();
        var session = await CreateSessionAsync(teacher, NewCode());
        var def = await CreateDefAsync(teacher, 1, "Pick", options: [new { text = "A" }, new { text = "B" }]);

        var resp = await FireRawAsync(teacher, def, session, 45);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(45, body.GetProperty("durationSeconds").GetInt32());
    }

    [Fact]
    public async Task Fire_uses_config_default_duration()
    {
        var teacher = await NewTeacherAsync();
        var session = await CreateSessionAsync(teacher, NewCode());
        var def = await CreateDefAsync(teacher, 1, "Pick",
            options: [new { text = "A" }, new { text = "B" }], config: new { defaultDurationSeconds = 30 });

        var resp = await FireRawAsync(teacher, def, session, duration: null);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(30, body.GetProperty("durationSeconds").GetInt32());
    }

    [Fact]
    public async Task Fire_without_duration_or_default_is_rejected()
    {
        var teacher = await NewTeacherAsync();
        var session = await CreateSessionAsync(teacher, NewCode());
        var def = await CreateDefAsync(teacher, 3, "Say");

        var resp = await FireRawAsync(teacher, def, session, duration: null);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Fire_into_ended_session_is_rejected()
    {
        var teacher = await NewTeacherAsync();
        var code = NewCode();
        var session = await CreateSessionAsync(teacher, code);
        var def = await CreateDefAsync(teacher, 1, "Pick", options: [new { text = "A" }, new { text = "B" }]);
        (await teacher.PutAsJsonAsync($"/api/sessions/{code}", new { endTime = DateTime.UtcNow })).EnsureSuccessStatusCode();

        var resp = await FireRawAsync(teacher, def, session, 60);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Fire_into_another_teachers_session_is_not_found()
    {
        var owner = await NewTeacherAsync();
        var other = await NewTeacherAsync();
        var session = await CreateSessionAsync(owner, NewCode());
        var def = await CreateDefAsync(other, 1, "Pick", options: [new { text = "A" }, new { text = "B" }]);

        var resp = await FireRawAsync(other, def, session, 60);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Fire_another_teachers_private_definition_is_not_found()
    {
        var owner = await NewTeacherAsync();
        var other = await NewTeacherAsync();
        var ownerDef = await CreateDefAsync(owner, 1, "Pick", options: [new { text = "A" }, new { text = "B" }]);
        var otherSession = await CreateSessionAsync(other, NewCode());

        var resp = await FireRawAsync(other, ownerDef, otherSession, 60);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
