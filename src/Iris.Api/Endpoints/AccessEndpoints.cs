using Iris.Api.Authorization;
using Iris.Application.Access;
using Iris.Application.Common;
using Iris.Application.Tenancy;
using Iris.Domain.Access;

namespace Iris.Api.Endpoints;

public static class AccessEndpoints
{
    public static IEndpointRouteBuilder MapAccessEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", async (
                Guid? customerId,
                Guid? contextId,
                GetMyAccessHandler handler,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var result = await handler
                        .HandleAsync(new GetMyAccessQuery(customerId, contextId), cancellationToken)
                        .ConfigureAwait(false);
                    return Results.Ok(result);
                }
                catch (InvalidScopeRequestException ex)
                {
                    return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
                }
            })
            .WithName("GetMyAccess")
            .WithSummary("The caller's identity and effective permissions, optionally scoped to a customer/context.")
            .RequireAuthorization();

        app.MapGet("/customers", async (ListAccessibleCustomersHandler handler, CancellationToken cancellationToken) =>
                Results.Ok(await handler
                    .HandleAsync(new ListAccessibleCustomersQuery(), cancellationToken)
                    .ConfigureAwait(false)))
            .WithName("ListAccessibleCustomers")
            .WithSummary("Customers and contexts visible to the caller.")
            .RequireAuthorization();

        app.MapGet("/governance/roles", async (ListRolesHandler handler, CancellationToken cancellationToken) =>
                Results.Ok(await handler
                    .HandleAsync(new ListRolesQuery(), cancellationToken)
                    .ConfigureAwait(false)))
            .WithName("ListRoles")
            .WithSummary("Role catalog with the permissions each role carries.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Governance.ManageRoles));

        return app;
    }
}
