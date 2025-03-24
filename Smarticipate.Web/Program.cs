using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Smarticipate.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:44397/") });
builder.Services.AddOptions();
builder.Services.AddAuthorizationCore();
// builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();

await builder.Build().RunAsync();