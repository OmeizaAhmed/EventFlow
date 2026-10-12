using EventFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventFlow.IntegrationTests;

public sealed class EventFlowApiFactory : WebApplicationFactory<Program>
{
    internal const string JwtIssuer = "EventFlow.IntegrationTests";
    internal const string JwtAudience = "EventFlow.IntegrationTests.Client";
    internal const string JwtSecret = "integration-tests-only-signing-key-at-least-32-bytes";

    // A factory owns one database. Each test creates its own factory, and all request
    // scopes in that test share this connection until the factory is disposed.
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = JwtSecret,
                ["JwtSettings:Issuer"] = JwtIssuer,
                ["JwtSettings:Audience"] = JwtAudience,
                ["JwtSettings:ExpirationInMinutes"] = "60",
                ["ConnectionStrings:DefaultConnection"] = "Unused: replaced by the test SQLite connection",
                ["Serilog:MinimumLevel:Default"] = "Warning"
            }));

        builder.ConfigureTestServices(services =>
        {
            // Remove the provider configuration as well as the options to prevent
            // both Npgsql and SQLite from being registered on the same context.
            services.RemoveAll<DbContextOptions<EventFlowDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<EventFlowDbContext>>();
            _connection.Open();
            services.AddDbContext<EventFlowDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = false
    });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
