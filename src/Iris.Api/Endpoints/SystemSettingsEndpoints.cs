using Iris.Application.Access;
using Iris.Application.Settings;
using Iris.Domain.Access;

namespace Iris.Api.Endpoints;

public static class SystemSettingsEndpoints
{
    public static IEndpointRouteBuilder MapSystemSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var system = app.MapGroup("/system").WithTags("System");

        system.MapGet("/settings", async (
                IConfiguration configuration,
                GetMyAccessHandler access,
                GetSystemSettingsHandler handler,
                CancellationToken ct) =>
            {
                var me = await access.HandleAsync(new GetMyAccessQuery(), ct).ConfigureAwait(false);
                var canManageSystem = me?.EffectivePermissions.Contains(Permissions.PlatformAdmin) == true;
                var result = await handler
                    .HandleAsync(new GetSystemSettingsQuery(
                        canManageSystem,
                        configuration["Iris:Integrations:OpenBao:Endpoint"],
                        configuration["Iris:Integrations:Ansible:Endpoint"],
                        configuration["Iris:Integrations:AWX:Endpoint"],
                        configuration["Iris:Integrations:AzureDevOps:Endpoint"],
                        configuration["Iris:Integrations:Nexus:Endpoint"]),
                        ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("GetSystemSettings")
            .WithSummary("Current system settings visible to the signed-in user.")
            .RequireAuthorization();

        return app;
    }
}
