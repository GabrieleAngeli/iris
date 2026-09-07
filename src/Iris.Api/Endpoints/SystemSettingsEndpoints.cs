using Iris.Api.Authorization;
using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Application.Settings;
using Iris.Contracts.Settings;
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

        system.MapGet("/integrations/{key}/status", async (
                string key,
                bool probe,
                IEnumerable<IIntegrationConnector> connectors,
                CancellationToken ct) =>
            {
                var connector = connectors.FirstOrDefault(item =>
                    string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
                if (connector is null)
                {
                    return Results.NotFound();
                }

                var status = await connector.GetStatusAsync(probe, ct).ConfigureAwait(false);
                return Results.Ok(new IntegrationLinkResponse(
                    status.Key,
                    status.Name,
                    status.Status,
                    status.Endpoint,
                    status.Message));
            })
            .WithName("GetIntegrationStatus")
            .WithSummary("Returns the configured connector status, optionally probing the remote service.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.PlatformAdmin));

        return app;
    }
}
