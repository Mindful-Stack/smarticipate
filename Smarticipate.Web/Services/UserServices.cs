using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Smarticipate.Web.Services;

public class UserServices (IHttpClientFactory httpClientFactory, AuthenticationStateProvider authStateProvider)
{
    public async Task<string> GetAuthenticatedUser()
    {
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        var userEmail = user.FindFirst(ClaimTypes.Email)?.Value;

        var client = httpClientFactory.CreateClient("API");
        string? userId = null;
        if (!string.IsNullOrEmpty(userEmail))
        {

            try
            {
                var userResponse = await client.GetAsync($"api/users/{Uri.EscapeDataString(userEmail)}");
                if (userResponse.IsSuccessStatusCode)
                {
                    userId = await userResponse.Content.ReadFromJsonAsync<string>();
                    return userId;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
            
        }
        return null;
    }
}