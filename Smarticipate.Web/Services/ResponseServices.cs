using System.Net.Http.Json;
using Smarticipate.Core;

namespace Smarticipate.Web.Services;

public class ResponseServices(IHttpClientFactory httpClientFactory) : IService
{
    public async Task<bool> CreateResponseAsync(int questionId, ResponseOption selectedOption)
    {
        try
        {
            var client = httpClientFactory.CreateClient("API");

            var request = new
            {
                SelectedOption = (int)selectedOption,
                TimeStamp = DateTime.Now,
                QuestionId = questionId
            };

            var response = await client.PostAsJsonAsync("api/responses", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception when creating response: {ex.Message}");
            return false;
        }
    }

    public async Task<List<ResponseDto>?> GetAllResponsesByQuestionIdAsync(int questionId)
    {
        try
        {
            var client = httpClientFactory.CreateClient("API");
            var response = await client.GetAsync($"api/responses/{questionId}");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<ResponseDto>>();
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception when retrieving responses: {ex.Message}");
            return null;
        }
    }

    public class QuestionDto
    {
        public int Id { get; set; }
        public int QuestionNumber { get; set; }
        public DateTime? TimeStamp { get; set; }
        public int QuestionId { get; set; }
        public List<ResponseDto> Responses { get; set; }
    }

    public class ResponseDto
    {
        public int Id { get; set; }
        public ResponseOption SelectedOption { get; set; }
        public DateTime TimeStamp { get; set; }
        public int QuestionId { get; set; }
    }
}