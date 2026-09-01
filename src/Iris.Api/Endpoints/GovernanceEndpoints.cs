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

        governance.MapPost("/users", async (
                CreateUserRequest body,
                CreateUserHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new CreateUserCommand(body.Email, body.DisplayName), ct)
                    .ConfigureAwait(false);
                return Results.Created($"/governance/users/{result.Id}", result);
            })
            .WithName("CreateUser")
            .WithSummary("Pre-provision a user ahead of their first sign-in.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Governance.ManageAssignments));

        governance.MapPut("/users/{userId:guid}", async (
                Guid userId,
                UpdateUserRequest body,
                UpdateUserHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new UpdateUserCommand(userId, body.Email, body.DisplayName, body.IsActive), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("UpdateUser")
            .WithSummary("Edit a user's profile and active flag.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Governance.ManageAssignments));

        governance.MapDelete("/users/{userId:guid}", async (
                Guid userId,
                DeleteUserHandler handler,
                CancellationToken ct) =>
            {
                await handler.HandleAsync(new DeleteUserCommand(userId), ct).ConfigureAwait(false);
                return Results.NoContent();
            })
            .WithName("DeleteUser")
            .WithSummary("Delete a user and their role assignments.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Governance.ManageAssignments));

        governance.MapPost("/users/{userId:guid}/invitation", async (
                Guid userId,
                IssueUserInvitationHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new IssueUserInvitationCommand(userId), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("IssueUserInvitation")
            .WithSummary("Mint a one-time invitation link for a user; supersedes any earlier one.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Governance.ManageAssignments));

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

        // ----- Advisory edit locks -----
        var locks = app.MapGroup("/locks").WithTags("Edit locks");

        locks.MapGet("/{resourceType}/{resourceId:guid}", async (
                string resourceType,
                Guid resourceId,
                GetEditLockHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new GetEditLockQuery(resourceType, resourceId), ct)
                    .ConfigureAwait(false);
                return result is null ? Results.NoContent() : Results.Ok(result);
            })
            .WithName("GetEditLock")
            .WithSummary("Who, if anyone, is currently editing a resource.")
            .RequireAuthorization();

        locks.MapPost("/{resourceType}/{resourceId:guid}", async (
                string resourceType,
                Guid resourceId,
                AcquireEditLockHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new AcquireEditLockCommand(resourceType, resourceId), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("AcquireEditLock")
            .WithSummary("Take or refresh the advisory lock on a resource (also the editor heartbeat).")
            .RequireAuthorization();

        locks.MapDelete("/{resourceType}/{resourceId:guid}", async (
                string resourceType,
                Guid resourceId,
                bool? force,
                ReleaseEditLockHandler handler,
                CancellationToken ct) =>
            {
                await handler
                    .HandleAsync(new ReleaseEditLockCommand(resourceType, resourceId, force ?? false), ct)
                    .ConfigureAwait(false);
                return Results.NoContent();
            })
            .WithName("ReleaseEditLock")
            .WithSummary("Release the lock you hold on a resource (platform admins may force with ?force=true).")
            .RequireAuthorization();

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
