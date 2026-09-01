using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace Iris.Api.Auth;

public static class AuthenticationSetup
{
    private const string CompositeScheme = "Iris";

    public static WebApplicationBuilder AddIrisAuthentication(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var services = builder.Services;

        var mode = ResolveMode(configuration["Iris:Auth:Mode"], builder.Environment.IsDevelopment());
        var devHeader = configuration["Iris:Auth:DevHeaderName"] ?? "X-Dev-User";
        var devPasswordHeader = configuration["Iris:Auth:DevPasswordHeaderName"] ?? "X-Dev-Password";
        var devUsers = configuration.GetSection("Iris:Auth:DevUsers").Get<List<DevUser>>() ?? [];
        var allowAnyEmail = configuration.GetValue("Iris:Auth:AllowAnyEmail", false);

        var defaultScheme = mode switch
        {
            IrisAuthMode.Dev => DevAuthenticationOptions.SchemeName,
            IrisAuthMode.EntraId => JwtBearerDefaults.AuthenticationScheme,
            _ => CompositeScheme,
        };

        var authBuilder = services.AddAuthentication(defaultScheme);

        if (mode is IrisAuthMode.Dev or IrisAuthMode.Both)
        {
            authBuilder.AddScheme<DevAuthenticationOptions, DevAuthenticationHandler>(
                DevAuthenticationOptions.SchemeName,
                options =>
                {
                    options.HeaderName = devHeader;
                    options.PasswordHeaderName = devPasswordHeader;
                    options.Users = devUsers;
                    options.AllowAnyEmail = allowAnyEmail;
                });
        }

        if (mode is IrisAuthMode.EntraId or IrisAuthMode.Both)
        {
            GuardEntraIdConfiguration(configuration);
            authBuilder.AddMicrosoftIdentityWebApi(configuration, "AzureAd");
        }

        if (mode is IrisAuthMode.Both)
        {
            authBuilder.AddPolicyScheme(CompositeScheme, CompositeScheme, options =>
            {
                options.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(devHeader)
                        ? DevAuthenticationOptions.SchemeName
                        : JwtBearerDefaults.AuthenticationScheme;
            });
        }

        return builder;
    }

    private static void GuardEntraIdConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("AzureAd");
        var tenantId = section["TenantId"];
        var clientId = section["ClientId"];

        static bool Missing(string? value) =>
            string.IsNullOrWhiteSpace(value) ||
            value == "00000000-0000-0000-0000-000000000000";

        if (Missing(tenantId) || Missing(clientId))
        {
            throw new InvalidOperationException(
                "Auth mode requires Entra ID but 'AzureAd:TenantId' / 'AzureAd:ClientId' are not configured. " +
                "Set them via user-secrets or environment variables, or use Iris:Auth:Mode=Dev for local development.");
        }
    }

    private static IrisAuthMode ResolveMode(string? configured, bool isDevelopment)
    {
        if (!string.IsNullOrWhiteSpace(configured) &&
            Enum.TryParse<IrisAuthMode>(configured, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return isDevelopment ? IrisAuthMode.Dev : IrisAuthMode.EntraId;
    }
}
