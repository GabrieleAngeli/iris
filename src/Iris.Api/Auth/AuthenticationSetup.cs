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

        var devRegistered = mode is IrisAuthMode.Dev or IrisAuthMode.Both;
        var entraIdRegistered = mode is IrisAuthMode.EntraId or IrisAuthMode.Both;

        // The local-password session scheme is always registered, independent of Iris:Auth:Mode —
        // it must work even for an org with no Entra ID tenant configured at all. The composite
        // scheme is therefore always the default now, routing every request to whichever of the
        // (up to three) registered schemes actually applies to it.
        var authBuilder = services.AddAuthentication(CompositeScheme);

        authBuilder.AddScheme<IrisSessionAuthenticationOptions, IrisSessionAuthenticationHandler>(
            IrisSessionAuthenticationOptions.SchemeName, _ => { });

        if (devRegistered)
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

        if (entraIdRegistered)
        {
            GuardEntraIdConfiguration(configuration);
            authBuilder.AddMicrosoftIdentityWebApi(configuration, "AzureAd");
        }

        authBuilder.AddPolicyScheme(CompositeScheme, CompositeScheme, options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                if (devRegistered && context.Request.Headers.ContainsKey(devHeader))
                {
                    return DevAuthenticationOptions.SchemeName;
                }

                var authHeader = context.Request.Headers.Authorization.ToString();
                const string bearerPrefix = "Bearer ";
                if (authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var token = authHeader[bearerPrefix.Length..].Trim();
                    // A JWT always has two dots (header.payload.signature); our opaque session
                    // token never does — that alone is enough to route correctly.
                    if (entraIdRegistered && token.Count(c => c == '.') == 2)
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }

                    return IrisSessionAuthenticationOptions.SchemeName;
                }

                return entraIdRegistered ? JwtBearerDefaults.AuthenticationScheme : IrisSessionAuthenticationOptions.SchemeName;
            };
        });

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
