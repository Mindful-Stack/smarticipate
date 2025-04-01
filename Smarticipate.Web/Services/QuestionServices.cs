using System.Net.Http.Json;

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
                TimeStamp = DateTime.Now,
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

    private class SessionResponse
    {
        public int Id { get; set; }
        public string SessionCode { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string UserId { get; set; }
        public bool IsActive { get; set; }
        public List<QuestionDto> Questions { get; set; } = new();
    }

    private class QuestionDto
    {
        public int Id { get; set; }
        public int QuestionNumber { get; set; }
        public List<ResponseDto> Responses { get; set; } = new();
    }
    
    private class ResponseDto
    {
        public int Id { get; set; }
        public int SelectedOption { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}