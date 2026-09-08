using Iris.Api.Authorization;
using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Application.Setup;
using Iris.Application.Settings;
using Iris.Contracts.Setup;
using Iris.Contracts.Settings;
using Iris.Domain.Access;

namespace Iris.Api.Endpoints;

public static class SystemSettingsEndpoints
{
    public static IEndpointRouteBuilder MapSystemSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var system = app.MapGroup("/system").WithTags("System");

        system.MapGet("/settings", async (
                GetMyAccessHandler access,
                GetSystemSettingsHandler handler,
                CancellationToken ct) =>
            {
                var me = await access.HandleAsync(new GetMyAccessQuery(), ct).ConfigureAwait(false);
                var canManageSystem = me?.EffectivePermissions.Contains(Permissions.PlatformAdmin) == true;
                var result = await handler
                    .HandleAsync(new GetSystemSettingsQuery(canManageSystem), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("GetSystemSettings")
            .WithSummary("Current system settings visible to the signed-in user.")
            .RequireAuthorization();

        system.MapPut("/settings/mail", async (
                MailProviderInput body,
                SaveMailProviderSettingsHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(new SaveMailProviderSettingsCommand(body), ct).ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("SaveMailProviderSettings")
            .WithSummary("Change the SMTP relay after setup. Takes effect immediately, no restart needed.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.PlatformAdmin));

        system.MapPost("/settings/mail/test", async (
                TestMailConnectionRequest body,
                TestMailConnectionHandler handler,
                CancellationToken ct) =>
            {
                await handler.HandleAsync(new TestMailConnectionCommand(body.Mail, body.TestRecipient), ct).ConfigureAwait(false);
                return Results.Ok(new { Sent = true });
            })
            .WithName("TestMailSettings")
            .WithSummary("Sends a real test email using the given (not-necessarily-saved) SMTP settings.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.PlatformAdmin));

        system.MapPost("/settings/secrets/unlock", async (
                UnlockFallbackSecretsRequest body,
                UnlockFallbackSecretsHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(new UnlockFallbackSecretsCommand(body.Password), ct).ConfigureAwait(false);
                return Results.Ok(new UnlockFallbackSecretsResponse(
                    result.Persisted,
                    result.Restored,
                    result.OwnershipTransferred,
                    $"{result.Persisted} secret(s) saved, {result.Restored} restored."));
            })
            .WithName("UnlockFallbackSecrets")
            .WithSummary("Persists secrets held only in this process's memory, and restores previously-saved ones — both encrypted with your own password, re-entered here.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.PlatformAdmin));

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

        system.MapPut("/integrations/azure-devops", async (
                SaveAzureDevOpsIntegrationSettingsRequest body,
                SaveAzureDevOpsIntegrationSettingsHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new SaveAzureDevOpsIntegrationSettingsCommand(body.Endpoint, body.Token), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("SaveAzureDevOpsIntegrationSettings")
            .WithSummary("Persist the Azure DevOps organization URL/PAT. Takes effect after an Iris.Api restart.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.PlatformAdmin));

        system.MapPut("/integrations/nexus", async (
                SaveNexusIntegrationSettingsRequest body,
                SaveNexusIntegrationSettingsHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new SaveNexusIntegrationSettingsCommand(body.Endpoint, body.Token), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("SaveNexusIntegrationSettings")
            .WithSummary("Persist the Nexus endpoint/token. Takes effect after an Iris.Api restart.")
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
                IIntegrationHealthMonitor healthMonitor,
                IClock clock,
                CancellationToken ct) =>
            {
                var connector = connectors.FirstOrDefault(item =>
                    string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
                if (connector is null)
                {
                    return Results.NotFound();
                }

                var status = await connector.GetStatusAsync(probe, ct).ConfigureAwait(false);
                var checkedAtUtc = (DateTimeOffset?)null;
                if (probe)
                {
                    // A manual "Test" click is a real probe too — record it into the same
                    // monitor the periodic background check writes to, so the next System
                    // settings reload shows "Checked just now" instead of waiting for the next
                    // scheduled cycle.
                    checkedAtUtc = clock.UtcNow;
                    healthMonitor.Record(status.Key, status.Status, status.Message, checkedAtUtc.Value);
                }

                return Results.Ok(new IntegrationLinkResponse(
                    status.Key,
                    status.Name,
                    status.Status,
                    status.Endpoint,
                    status.Message,
                    checkedAtUtc));
            })
            .WithName("GetIntegrationStatus")
            .WithSummary("Returns the configured connector status, optionally probing the remote service.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.PlatformAdmin));

        return app;
    }
}
