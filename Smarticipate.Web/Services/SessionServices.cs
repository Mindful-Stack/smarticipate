using System.Net.Http.Json;
using MudBlazor;

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

    public async Task<ActiveSessionResponse?> GetActiveSessionAsync(string userId)
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
                return result;
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception when retrieving active session: {ex.Message}");
            return null;
        }
    }

    public async Task<List<SessionResponse>> GetAllSessionsByUserAsync(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return new List<SessionResponse>();
            }

            var client = httpClientFactory.CreateClient("API");
            var response = await client.GetAsync($"api/sessions/{Uri.EscapeDataString(userId)}");

            if (response.IsSuccessStatusCode)
            {
                var sessions = await response.Content.ReadFromJsonAsync<List<SessionResponse>>();
                return sessions ?? new List<SessionResponse>();
            }

            return new List<SessionResponse>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception when retrieving sessions: {ex.Message}");
            return new List<SessionResponse>();
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

    public record SessionRequest(string SessionCode, DateTime? StartTime, DateTime? EndTime, string UserId);

    public record ActiveSessionResponse
    {
        public int Id { get; set; }
        public string SessionCode { get; set; }
        public DateTime? StartTime { get; set; }
        public string UserId { get; set; }
        public bool IsActive { get; set; }
        public List<QuestionDto> Questions { get; set; }
    }

    public record SessionResponse
    {
        public int Id { get; set; }
        public string SessionCode { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsActive { get; set; }
        public List<QuestionDto> Questions { get; set; }
    }

    public record QuestionDto
    {
        public int Id { get; set; }
        public int QuestionNumber { get; set; }
        public List<ResponseDto> Responses { get; set; } = new();
    }

    public record ResponseDto
    {
        public int Id { get; set; }
        public int SelectedOption { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}