using Iris.Api.Authorization;
using Iris.Application.Audit;
using Iris.Domain.Access;

namespace Iris.Api.Endpoints;

public static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/activity", async (
                string? area,
                int? take,
                ListTransactionLogHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new ListTransactionLogQuery(area, take ?? 50), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("ListTransactionLog")
            .WithSummary("Recent transaction log entries, optionally filtered by area.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.PlatformAdmin));

        return app;
    }
}
