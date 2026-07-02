using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Smarticipate.API.Data.Identity;
using Smarticipate.API.Services;
using Xunit;

namespace Smarticipate.Tests.Integration;

// Boots the real API against a fresh, uniquely-named Postgres database on the local
// server (no Docker). The database is created + migrated + seeded on start and dropped
// on dispose, so tests run against real jsonb, real unique indexes, and real FKs.
//
// Auth is exercised via a header-driven test scheme: a request carrying "X-Test-User"
// is authenticated as that user id (which must be a real AspNetUsers row so owner FKs
// hold); a request without it is anonymous. This drives the same owner checks the real
// endpoints use without depending on the Identity cookie surviving the test transport.
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbName = "smarticipate_it_" + Guid.NewGuid().ToString("N")[..12];

    private static string Env(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;

    private string ConnectionString =>
        $"Host={Env("QA_PGHOST", "localhost")};Port={Env("QA_PGPORT", "5432")};" +
        $"Database={_dbName};Username={Env("QA_PGUSER", "postgres")};Password={Env("QA_PGPASSWORD", "postgres")}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // Override the connection string before the app reads it in AddDbContext.
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = TestAuthHandler.SchemeName;
                o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    // Creates a real user row and returns its id (satisfies the OwnerUserId / Session.UserId FKs).
    public async Task<string> CreateUserAsync()
    {
        var id = "t-" + Guid.NewGuid().ToString("N");
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
        var result = await users.CreateAsync(new User { Id = id, UserName = id + "@it.test", Email = id + "@it.test" });
        if (!result.Succeeded)
            throw new InvalidOperationException("Test user creation failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        return id;
    }

    public async Task InitializeAsync()
    {
        // Accessing Services builds the host; ensure the schema and system seed exist
        // (idempotent alongside the app's own startup migrate-then-seed).
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        await db.Database.MigrateAsync();
        await SystemQuestionSeeder.SeedAsync(db);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        var options = new DbContextOptionsBuilder<UserDbContext>().UseNpgsql(ConnectionString).Options;
        await using var db = new UserDbContext(options);
        await db.Database.EnsureDeletedAsync();
    }
}

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var userId) || string.IsNullOrEmpty(userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;
