using System.Net.Http.Json;

namespace Smarticipate.Web.Services;

public class UserServices(IHttpClientFactory httpClientFactory) : IService
{
    public async Task<string?> GetAuthenticatedUser()
    {
        try
        {
            var client = httpClientFactory.CreateClient("API");
            var response = await client.GetAsync("api/users/me");
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<string>()
                : null;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }
}
