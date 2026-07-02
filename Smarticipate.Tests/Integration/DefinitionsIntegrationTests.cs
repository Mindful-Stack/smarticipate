using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Smarticipate.Tests.Integration;

public class DefinitionsIntegrationTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Toolbox_includes_system_ready_check()
    {
        var teacher = await NewTeacherAsync();
        var toolbox = await teacher.GetFromJsonAsync<JsonElement>("/api/question-definitions");
        var system = toolbox.EnumerateArray().Where(d => d.GetProperty("isSystem").GetBoolean()).ToList();
        Assert.Contains(system, d => d.GetProperty("name").GetString() == "Ready check");
    }

    [Fact]
    public async Task YesNo_with_no_options_auto_creates_two()
    {
        var teacher = await NewTeacherAsync();
        var id = await CreateDefAsync(teacher, 0, "Ready?");
        var def = await teacher.GetFromJsonAsync<JsonElement>($"/api/question-definitions/{id}");
        Assert.Equal(2, def.GetProperty("options").GetArrayLength());
    }

    [Fact]
    public async Task SingleChoice_needs_at_least_two_options()
    {
        var teacher = await NewTeacherAsync();
        var resp = await CreateDefRawAsync(teacher, 1, "Pick", options: [new { text = "only" }]);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Blank_option_text_is_rejected()
    {
        var teacher = await NewTeacherAsync();
        var resp = await CreateDefRawAsync(teacher, 1, "Pick", options: [new { text = "" }, new { text = "B" }]);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Scale_min_not_below_max_is_rejected()
    {
        var teacher = await NewTeacherAsync();
        var resp = await CreateDefRawAsync(teacher, 4, "Rate", config: new { scaleMin = 5, scaleMax = 5 });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Save_then_delete_toggles_toolbox_membership()
    {
        var teacher = await NewTeacherAsync();
        var id = await CreateDefAsync(teacher, 1, "Pick", options: [new { text = "A" }, new { text = "B" }]);

        (await teacher.PostAsJsonAsync($"/api/question-definitions/{id}/save", new { name = "Saved" }))
            .EnsureSuccessStatusCode();
        var afterSave = await teacher.GetFromJsonAsync<JsonElement>("/api/question-definitions");
        Assert.Contains(afterSave.EnumerateArray(), d => d.GetProperty("id").GetInt32() == id);

        (await teacher.DeleteAsync($"/api/question-definitions/{id}")).EnsureSuccessStatusCode();
        var afterDelete = await teacher.GetFromJsonAsync<JsonElement>("/api/question-definitions");
        Assert.DoesNotContain(afterDelete.EnumerateArray(), d => d.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task Another_teacher_cannot_read_your_definition()
    {
        var owner = await NewTeacherAsync();
        var other = await NewTeacherAsync();
        var id = await CreateDefAsync(owner, 1, "Pick", options: [new { text = "A" }, new { text = "B" }]);

        var resp = await other.GetAsync($"/api/question-definitions/{id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Deferred_types_are_rejected()
    {
        var teacher = await NewTeacherAsync();
        var resp = await CreateDefRawAsync(teacher, 7, "Rank", options: [new { text = "A" }, new { text = "B" }]);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
