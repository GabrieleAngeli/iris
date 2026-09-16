using Iris.Api.Authorization;
using Iris.Application.Applications;
using Iris.Contracts.Applications;
using Iris.Domain.Access;

namespace Iris.Api.Endpoints;

/// <summary>
/// The Actions module: Prepare → Review → Execute for an <c>ApplicationInstallation</c> deploy.
/// See <c>ApplicationsEndpoints</c> for the underlying installation/validate/ansible-vars/awx-launch
/// endpoints this builds on.
/// </summary>
public static class ActionsEndpoints
{
    public static IEndpointRouteBuilder MapActionsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/applications/installations/{installationId:guid}/actions/prepare", async (
                Guid installationId,
                ApplicationInstallationAwxLaunchRequest? body,
                PrepareApplicationInstallationActionHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new PrepareApplicationInstallationActionCommand(installationId, body), ct)
                    .ConfigureAwait(false);
                return Results.Created($"/actions/{result.Id}", result);
            })
            .WithTags("Actions")
            .WithName("PrepareApplicationInstallationAction")
            .WithSummary("Freezes a reviewable plan+validation snapshot for an installation — step 1 of Prepare → Review → Execute.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Deployments.Prepare));

        var actions = app.MapGroup("/actions").WithTags("Actions");

        actions.MapGet("", async (
                Guid? customerContextId,
                Guid? applicationId,
                Guid? serverNodeId,
                string? status,
                ListPreparedActionsHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new ListPreparedActionsQuery(customerContextId, applicationId, serverNodeId, status), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("ListPreparedActions")
            .WithSummary("Prepared actions across every installation, filterable by customer context/application/server/status.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Actions.Read));

        actions.MapGet("/{actionId:guid}", async (
                Guid actionId,
                GetPreparedActionHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(new GetPreparedActionQuery(actionId), ct).ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("GetPreparedAction")
            .WithSummary("One prepared action's frozen plan/validation snapshot, plus its linked run once executed.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Actions.Read));

        actions.MapPost("/{actionId:guid}/execute", async (
                Guid actionId,
                ExecutePreparedActionHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(new ExecutePreparedActionCommand(actionId), ct).ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("ExecutePreparedAction")
            .WithSummary("The operator's confirmation — step 3 of Prepare → Review → Execute. Re-validates fresh and launches the AWX job.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Actions.Run));

        actions.MapPost("/{actionId:guid}/cancel", async (
                Guid actionId,
                CancelPreparedActionRequest? body,
                CancelPreparedActionHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new CancelPreparedActionCommand(actionId, body?.Reason), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("CancelPreparedAction")
            .WithSummary("The operator declined a prepared action instead of confirming it.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Actions.Run));

        return app;
    }
}
