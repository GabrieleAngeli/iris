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
                        configuration["Iris:Integrations:AzureDevOps:Endpoint"],
                        configuration["Iris:Integrations:Nexus:Endpoint"]),
                        ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("GetSystemSettings")
            .WithSummary("Current system settings visible to the signed-in user.")
            .RequireAuthorization();

        system.MapPut("/integrations/openbao", async (
                SaveOpenBaoIntegrationSettingsRequest body,
                SaveOpenBaoIntegrationSettingsHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new SaveOpenBaoIntegrationSettingsCommand(body.Endpoint, body.Token, body.MountPath, body.UseKvV2), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("SaveOpenBaoIntegrationSettings")
            .WithSummary("Persist the OpenBao endpoint/token. Takes effect after an Iris.Api restart.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.PlatformAdmin));

        system.MapPut("/integrations/awx", async (
                SaveAwxIntegrationSettingsRequest body,
                SaveAwxIntegrationSettingsHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new SaveAwxIntegrationSettingsCommand(body.Endpoint, body.Token, body.JobTemplateId), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("SaveAwxIntegrationSettings")
            .WithSummary("Persist the AWX endpoint/token/job template. Takes effect after an Iris.Api restart.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.PlatformAdmin));

        system.MapPut("/integrations/ansible", async (
                SaveAnsibleIntegrationSettingsRequest body,
                SaveAnsibleIntegrationSettingsHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new SaveAnsibleIntegrationSettingsCommand(body.Endpoint, body.Playbook, body.Inventory), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("SaveAnsibleIntegrationSettings")
            .WithSummary("Persist the Ansible endpoint/playbook/inventory. Takes effect after an Iris.Api restart.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.PlatformAdmin));

        system.MapPost("/integrations/openbao/provision", async (
                ProvisionOpenBaoHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(new ProvisionOpenBaoCommand(), ct).ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("ProvisionOpenBao")
            .WithSummary("Starts a dev-mode OpenBao container on this host via Docker and saves its endpoint/token. Convenience/non-production only.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.PlatformAdmin));

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
