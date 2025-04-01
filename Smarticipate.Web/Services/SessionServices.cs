using System.Net.Http.Json;

namespace Smarticipate.Web.Services;

public class SessionServices(IHttpClientFactory httpClientFactory) : IService
{
    public async Task<bool> CreateSessionAsync(SessionRequest request)
    {
        try
        {
            var client = httpClientFactory.CreateClient("API");
            var response = await client.PostAsJsonAsync("api/sessions", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception when creating session: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateSessionAsync(string sessionCode)
    {
        try
        {
            var client = httpClientFactory.CreateClient("API");
            var request = new { EndTime = DateTime.Now };
            var response = await client.PutAsJsonAsync($"api/sessions/{sessionCode}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception when stopping session: {ex.Message}");
            return false;
        }
    }

    public async Task<string?> GetActiveSessionAsync(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            var client = httpClientFactory.CreateClient("API");
            var response = await client.GetAsync($"api/sessions/active/{Uri.EscapeDataString(userId)}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ActiveSessionResponse>();
                return result?.SessionCode;
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception when retrieving active session: {ex.Message}");
            return null;
        }
    }

    public record SessionRequest(string SessionCode, DateTime? StartTime, DateTime? EndTime, string UserId);

    public record ActiveSessionResponse(string SessionCode);
}