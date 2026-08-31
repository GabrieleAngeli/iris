using Iris.Api.Authorization;
using Iris.Application.Access;
using Iris.Application.Governance;
using Iris.Contracts.Governance;
using Iris.Domain.Access;

namespace Iris.Api.Endpoints;

public static class GovernanceEndpoints
{
    public static IEndpointRouteBuilder MapGovernanceEndpoints(this IEndpointRouteBuilder app)
    {
        var governance = app.MapGroup("/governance").WithTags("Governance");

        governance.MapGet("/permissions", async (GetPermissionCatalogHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(new GetPermissionCatalogQuery(), ct).ConfigureAwait(false)))
            .WithName("GetPermissionCatalog")
            .WithSummary("Every permission code Iris recognises.")
            .RequireAuthorization();

        governance.MapGet("/users", async (ListUsersHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(new ListUsersQuery(), ct).ConfigureAwait(false)))
            .WithName("ListUsers")
            .WithSummary("Users and the roles they hold.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Governance.Read));

        governance.MapPost("/users/{userId:guid}/assignments", async (
                Guid userId,
                AssignRoleRequest body,
                AssignRoleHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(
                        new AssignRoleCommand(userId, body.RoleKey, body.ScopeType, body.CustomerId, body.ContextId),
                        ct)
                    .ConfigureAwait(false);
                return Results.Created($"/governance/users/{userId}/assignments/{result.Id}", result);
            })
            .WithName("AssignRole")
            .WithSummary("Grant a role to a user at a scope.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Governance.ManageAssignments));

        governance.MapDelete("/users/{userId:guid}/assignments/{assignmentId:guid}", async (
                Guid userId,
                Guid assignmentId,
                RevokeRoleHandler handler,
                CancellationToken ct) =>
            {
                await handler.HandleAsync(new RevokeRoleCommand(userId, assignmentId), ct).ConfigureAwait(false);
                return Results.NoContent();
            })
            .WithName("RevokeRole")
            .WithSummary("Remove a role assignment from a user.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Governance.ManageAssignments));

        // ----- Tenancy management -----
        var customers = app.MapGroup("/customers").WithTags("Customers");

        customers.MapPost("", async (
                CreateCustomerRequest body,
                CreateCustomerHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new CreateCustomerCommand(body.Key, body.Name), ct)
                    .ConfigureAwait(false);
                return Results.Created($"/customers/{result.Id}", result);
            })
            .WithName("CreateCustomer")
            .WithSummary("Register a new customer.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Governance.ManageCustomers));

        customers.MapPost("/{customerId:guid}/contexts", async (
                Guid customerId,
                AddContextRequest body,
                AddContextHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new AddContextCommand(customerId, body.Name, body.Kind), ct)
                    .ConfigureAwait(false);
                return Results.Created($"/customers/{customerId}/contexts/{result.Id}", result);
            })
            .WithName("AddCustomerContext")
            .WithSummary("Add an environment/context to a customer.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Governance.ManageCustomers));

        return app;
    }
}
