using System.Net.Http.Json;
using Smarticipate.Core;

namespace Smarticipate.Web.Services;

public class QuestionServices(IHttpClientFactory httpClientFactory) : IService
{
    public async Task<bool> CreateQuestionAsync(string sessionCode)
    {
        try
        {
            var client = httpClientFactory.CreateClient("API");
            var sessionResponse = await client.GetFromJsonAsync<SessionResponse>($"api/sessions/code/{sessionCode}");

            if (sessionResponse is null)
            {
                return false;
            }

            int nextQuestionNumber = 1;
            if (sessionResponse.Questions.Any())
            {
                nextQuestionNumber = sessionResponse.Questions.Max(q => q.QuestionNumber) + 1;
            }

            var request = new
            {
                QuestionNumber = nextQuestionNumber,
                StartTime = DateTime.Now,
                SessionId = sessionResponse.Id
            };

            var response = await client.PostAsJsonAsync("api/questions", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception when creating question: {ex.Message}");
            return false;
        }
    }

    public async Task<List<QuestionResponse>?> GetAllQuestionsBySessionIdAsync(int sessionId)
    {
        try
        {
            var client = httpClientFactory.CreateClient("API");
            var response = await client.GetAsync($"api/questions/{sessionId}");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<QuestionResponse>>();
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception when retrieving questions: {ex.Message}");
            return null;
        }
    }

    public async Task<QuestionResponse> GetQuestionBySessionIdAndNumberAsync(int sessionId, int questionId)
    {
        try
        {
            var client = httpClientFactory.CreateClient("API");
            var response = await client.GetAsync($"api/questions/{sessionId}/{questionId}");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<QuestionResponse>();
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception when retrieving questions: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateQuestionEndTimeAsync(int questionId, DateTime endTime)
    {
        try
        {
            var client = httpClientFactory.CreateClient("API");

            var request = new
            {
                EndTime = endTime
            };

            var response = await client.PutAsJsonAsync($"api/questions/{questionId}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception when updating question: {ex.Message}");
            return false;
        }
    }
    
    private class SessionResponse
    {
        public int Id { get; set; }
        public string SessionCode { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string UserId { get; set; }
        public bool IsActive { get; set; }
        public List<QuestionResponse> Questions { get; set; } = new();
    }

    public class QuestionResponse
    {
        public int Id { get; set; }
        public int QuestionNumber { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int SessionId { get; set; }
        public List<ResponseDto> Responses { get; set; }
    }

    // public class QuestionDto
    // {
    //     public int Id { get; set; }
    //     public int QuestionNumber { get; set; }
    //     public List<ResponseDto> Responses { get; set; } = new();
    // }

    public class ResponseDto
    {
        public int Id { get; set; }
        public ResponseOption SelectedOption { get; set; }
        public DateTime TimeStamp { get; set; }
        public int QuestionId { get; set; }
    }
}