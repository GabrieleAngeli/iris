using Iris.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Iris.Api.Tests;

/// <summary>
/// Boots the real API against a throwaway SQLite file with dev-header auth enabled.
/// Migration + seeding run on startup, so every fixture gets the demo tenancy.
/// </summary>
public sealed class IrisApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"iris-apitest-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        // Highest-precedence host settings so the throwaway database always wins
        // over appsettings.json / appsettings.Development.json.
        builder.UseSetting("ConnectionStrings:IrisDb", $"Data Source={_databasePath}");
        builder.UseSetting("Iris:Database:Provider", "Sqlite");
        builder.UseSetting("Iris:Database:MigrateOnStartup", "true");
        builder.UseSetting("Iris:Auth:Mode", "Dev");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IrisDb"] = $"Data Source={_databasePath}",
                ["Iris:Database:Provider"] = "Sqlite",
                ["Iris:Database:MigrateOnStartup"] = "true",
                ["Iris:Auth:Mode"] = "Dev",
                ["Iris:Auth:DevHeaderName"] = "X-Dev-User",
                ["Iris:Auth:AllowAnyEmail"] = "false",
                ["Iris:Auth:DevUsers:0:Email"] = "admin@iris.local",
                ["Iris:Auth:DevUsers:0:Name"] = "Iris Platform Admin",
                ["Iris:Auth:DevUsers:0:ObjectId"] = "11111111-1111-1111-1111-111111111101",
                ["Iris:Auth:DevUsers:1:Email"] = "gio@globex.example",
                ["Iris:Auth:DevUsers:1:Name"] = "Giovanni Neri",
                ["Iris:Auth:DevUsers:1:ObjectId"] = "11111111-1111-1111-1111-111111111105",
            });
        });

        // Real SMTP has no place in an automated test run — /setup/complete and
        // /setup/test-mail now genuinely try to send mail; swap in an always-succeeding fake.
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IEmailSender, FakeEmailSender>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temp database file.
        }
    }
}
