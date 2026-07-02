using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Smarticipate.Tests.Integration;

// Shared helpers for the API integration tests. Each test creates its own teacher(s)
// and session(s); the collection runs serially so the shared database is safe to reuse.
[Collection("api")]
public abstract class IntegrationTestBase(ApiFactory factory)
{
    protected readonly ApiFactory Factory = factory;

    protected HttpClient Anon() => Factory.CreateClient();

    // Creates a fresh teacher (real user row) and returns a client authenticated as them.
    protected async Task<HttpClient> NewTeacherAsync()
    {
        var userId = await Factory.CreateUserAsync();
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
        return client;
    }

    protected static async Task<int> CreateSessionAsync(HttpClient teacher, string code)
    {
        var create = await teacher.PostAsJsonAsync("/api/sessions",
            new { sessionCode = code, name = code, startTime = DateTime.UtcNow, endTime = (DateTime?)null, userId = "ignored" });
        create.EnsureSuccessStatusCode();
        var session = await teacher.GetFromJsonAsync<JsonElement>($"/api/sessions/code/{code}");
        return session.GetProperty("id").GetInt32();
    }

    protected static string NewCode() => "C" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    protected static async Task<HttpResponseMessage> CreateDefRawAsync(
        HttpClient teacher, int type, string prompt, object[]? options = null, object? config = null)
    {
        var body = new Dictionary<string, object?> { ["type"] = type, ["prompt"] = prompt };
        if (options is not null) body["options"] = options;
        if (config is not null) body["config"] = config;
        return await teacher.PostAsJsonAsync("/api/question-definitions", body);
    }

    protected static async Task<int> CreateDefAsync(
        HttpClient teacher, int type, string prompt, object[]? options = null, object? config = null)
    {
        var resp = await CreateDefRawAsync(teacher, type, prompt, options, config);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    protected static async Task<HttpResponseMessage> FireRawAsync(HttpClient teacher, int defId, int sessionId, int? duration = 60)
    {
        var body = new Dictionary<string, object?> { ["definitionId"] = defId, ["sessionId"] = sessionId };
        if (duration.HasValue) body["durationSeconds"] = duration.Value;
        return await teacher.PostAsJsonAsync("/api/question-activations", body);
    }

    protected static async Task<int> FireAsync(HttpClient teacher, int defId, int sessionId, int duration = 60)
    {
        var resp = await FireRawAsync(teacher, defId, sessionId, duration);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    // Anonymous student view; returns the option ids in order.
    protected async Task<int[]> OptionIdsAsync(int activationId)
    {
        var a = await Anon().GetFromJsonAsync<JsonElement>($"/api/question-activations/{activationId}");
        return a.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("id").GetInt32()).ToArray();
    }

    protected Task<HttpResponseMessage> SubmitAsync(object body) =>
        Anon().PostAsJsonAsync("/api/responses", body);

    protected static async Task<JsonElement> ResultsAsync(HttpClient owner, int activationId) =>
        await owner.GetFromJsonAsync<JsonElement>($"/api/question-activations/{activationId}/results");
}
