using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.API.Endpoints;
using Smarticipate.API.Hubs;
using Smarticipate.API.QuestionTypes;
using Smarticipate.API.Services;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

// Preserve SQL Server-era local-time storage on Postgres timestamptz columns.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
//Identity DbContext
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

//Identity Endpoints
builder.Services.AddIdentityApiEndpoints<User>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
.AddEntityFrameworkStores<UserDbContext>()
.AddDefaultTokenProviders();

//Cookies
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.Name = "YourAuthCookie";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.Path = "/"; // Add this line
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});
    
builder.Services.AddAuthorization();

// Configure JSON options globally => avoid Cycles in Db objects
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    // .NET 10 generates OpenAPI 3.1 by default and is strict about duplicate schema ids.
    // Our endpoints each declare nested `Request`/`Response` records that share simple names,
    // so prefix the schema id with the declaring endpoint type (e.g. "CreateSessionRequest")
    // to keep every schema reference unique. Without this, Scalar fails to render the endpoints.
    options.CreateSchemaReferenceId = jsonTypeInfo =>
    {
        var defaultId = OpenApiOptions.CreateDefaultSchemaReferenceId(jsonTypeInfo);
        var declaringTypeName = jsonTypeInfo.Type.DeclaringType?.Name;
        return defaultId is not null && declaringTypeName is not null
            ? declaringTypeName + defaultId
            : defaultId;
    };
});

//Configure CORS
builder.Services.AddCors(options =>
{
    // Allowed Web origins come from config ("Cors:AllowedOrigins"); falls back to the
    // local dev Web origin so nothing changes locally.
    // TODO: set "Cors:AllowedOrigins" in production config (appsettings/env) before deploy —
    // the hardcoded fallback below is local-dev only.
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                         ?? new[] { "https://localhost:7045" };
    options.AddDefaultPolicy(
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(5);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(15);
});

// Live feedback
builder.Services.AddSingleton<LiveFeedbackStore>();
builder.Services.AddHostedService<FeedbackSnapshotService>();

// Question-type handlers + registry (reflection-registered singletons).
builder.Services.AddQuestionTypeHandlers();

var app = builder.Build();

app.UseCors();
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "text/plain";
        var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;
        await context.Response.WriteAsync($"Error: {exception?.Message ?? "Unknown error"}");
    });
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api/identity")
    .MapIdentityApi<User>()
    .WithTags("Identity");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

}

app.MapPost("/api/identity/logout", async (SignInManager<User> signInManager, [FromBody] object empty) =>
{
    if (empty is not null)
    {
        await signInManager.SignOutAsync();

        return Results.Ok();
    }

    return Results.Unauthorized();
}).RequireAuthorization();

app.MapEndpoints<Program>();

app.MapHub<SessionHub>("/sessionHub");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    await db.Database.MigrateAsync(); // ensure the schema exists before the seeder queries it
    await SystemQuestionSeeder.SeedAsync(db);
}

app.Run();