using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace Smarticipate.Web.Authentication;

public class CookieAuthenticationStateProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<CookieAuthenticationStateProvider> logger)
    : AuthenticationStateProvider, IAccountManagement
{
    private readonly AuthenticationState _anonymous;

    private readonly JsonSerializerOptions jsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

    private readonly HttpClient httpClient = httpClientFactory.CreateClient("Auth");
    private bool authenticated = false;
    private readonly ClaimsPrincipal unauthenticated = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        authenticated = false;
        var user = unauthenticated;
        try
        {
            var userResponse = await httpClient.GetAsync("manage/info");
            userResponse.EnsureSuccessStatusCode();

            var userJson = await userResponse.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<AuthenticatedUser>(userJson, jsonSerializerOptions);

            if (userInfo != null)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, userInfo.Email),
                    new(ClaimTypes.Email, userInfo.Email)
                };

                claims.AddRange(
                    userInfo.Claims.Where(c => c.Key != ClaimTypes.Name && c.Key != ClaimTypes.Email)
                        .Select(c => new Claim(c.Key, c.Value)));
                var id = new ClaimsIdentity(claims, nameof(CookieAuthenticationStateProvider));
                user = new ClaimsPrincipal(id);
                authenticated = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "App error");
        }

        // return the state
        return new AuthenticationState(user);
    }

    public async Task<FormResult> RegisterAsync(string email, string password)
    {
        string[] defaultDetail = ["An unknown error prevented registration from succeeding."];
        try
        {
            var result = await httpClient.PostAsJsonAsync("register", new
            {
                email,
                password
            });

            if (result.IsSuccessStatusCode)
            {
                return new FormResult { Succeeded = true };
            }

            return new FormResult
            {
                Succeeded = false,
                ErrorList = defaultDetail
            };
        }
        catch (Exception e)
        {
            logger.LogError("App error");
        }

        return new FormResult
        {
            Succeeded = false,
            ErrorList = defaultDetail
        };
    }

    public async Task<FormResult> LoginAsync(string email, string password)
    {
        try
        {
            var result = await httpClient.PostAsJsonAsync("login?useCookies=true", new
            {
                email,
                password
            });

            if (result.IsSuccessStatusCode)
            {
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                return new FormResult { Succeeded = true };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "App error");
        }

        return new FormResult
        {
            Succeeded = false,
            ErrorList = ["Invalid email and/or password"]
        };
    }

    public void NotifyUserLoggedIn(string username, string email)
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        Console.WriteLine("Authenticated username: " + username);
    }

    public void NotifyUserLoggedOut()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public class AuthenticatedUser
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Dictionary<string, string> Claims { get; set; } = [];
    }

    public class FormResult
    {
        public bool Succeeded { get; set; }
        public string[] ErrorList { get; set; } = [];
    }
}