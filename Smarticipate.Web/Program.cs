using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Smarticipate.Web;
using Smarticipate.Web.Authentication;
using MudBlazor.Services;
using Smarticipate.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddTransient<CookieHandler>();
builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<AuthenticationStateProvider,CookieAuthenticationStateProvider>();

builder.Services.AddScoped(
    sp => (IAccountManagement)sp.GetRequiredService<AuthenticationStateProvider>());
// builder.Services.AddScoped<SessionServices>();
// builder.Services.AddScoped<UserServices>();
typeof(Program).Assembly
    .GetTypes()
    .Where(t => !t.IsAbstract && t.IsClass && typeof(IService).IsAssignableFrom(t))
    .ToList()
    .ForEach(type => builder.Services.AddScoped(type));

builder.Services.AddOptions();
builder.Services.AddHttpClient(
        "Auth",
        opt => opt.BaseAddress = new Uri(builder.Configuration["BackendUrl"] ?? "https://localhost:44397/api/identity/"))
    .AddHttpMessageHandler<CookieHandler>();

builder.Services.AddHttpClient(
        "API", 
        opt => opt.BaseAddress = new Uri(builder.Configuration["ApiUrl"] ?? "https://localhost:44397/"))
    .AddHttpMessageHandler<CookieHandler>();

await builder.Build().RunAsync();