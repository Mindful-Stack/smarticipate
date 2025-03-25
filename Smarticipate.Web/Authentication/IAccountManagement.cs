namespace Smarticipate.Web.Authentication;

public interface IAccountManagement
{
    public Task<CookieAuthenticationStateProvider.FormResult> LoginAsync(string email, string password);
    public Task<CookieAuthenticationStateProvider.FormResult> RegisterAsync(string email, string password);
}