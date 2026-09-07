using Iris.Api.Authorization;
using Iris.Application.Deployments;
using Iris.Contracts.Deployments;
using Iris.Domain.Access;

namespace Iris.Api.Endpoints;

/// <summary>
/// Deployment topology: which servers an environment (customer + context) uses, decided before
/// any application is installed on them. See <c>ApplicationsEndpoints</c> for the applications
/// actually installed on those servers (<c>/applications/installations/*</c>).
/// </summary>
public static class DeploymentsEndpoints
{
    public static IEndpointRouteBuilder MapDeploymentsEndpoints(this IEndpointRouteBuilder app)
    {
        var deployments = app.MapGroup("/deployments").WithTags("Deployments");

        deployments.MapGet("/server-assignments", async (ListEnvironmentServerAssignmentsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(new ListEnvironmentServerAssignmentsQuery(), ct).ConfigureAwait(false)))
            .WithName("ListEnvironmentServerAssignments")
            .WithSummary("Servers assigned to every visible customer environment.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Deployments.Read));

        // Route param is "customerContextId", not "contextId": PermissionAuthorizationHandler treats a
        // route/query value literally named "contextId" as a scope-check input that requires a sibling
        // "customerId" (ScopeFactory.From) — this endpoint intentionally stays Global-scoped, same as
        // /applications/installations/*, which also carries no customerId/contextId route parameter.
        deployments.MapPost("/contexts/{customerContextId:guid}/server-assignments", async (
                Guid customerContextId,
                AssignServerToEnvironmentRequest body,
                AssignServerToEnvironmentHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new AssignServerToEnvironmentCommand(customerContextId, body.ServerNodeId, body.Notes), ct)
                    .ConfigureAwait(false);
                return Results.Created($"/deployments/server-assignments/{result.Id}", result);
            })
            .WithName("AssignServerToEnvironment")
            .WithSummary("Assigns a server to host workloads for a customer environment.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Deployments.Write));

        deployments.MapDelete("/server-assignments/{assignmentId:guid}", async (
                Guid assignmentId,
                UnassignServerFromEnvironmentHandler handler,
                CancellationToken ct) =>
            {
                await handler.HandleAsync(new UnassignServerFromEnvironmentCommand(assignmentId), ct).ConfigureAwait(false);
                return Results.NoContent();
            })
            .WithName("UnassignServerFromEnvironment")
            .WithSummary("Unassigns a server from a customer environment. Fails if application installations still target it there.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Deployments.Write));

        return app;
    }
}
