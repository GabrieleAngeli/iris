using Iris.Application.Access;

namespace Iris.Api.Endpoints;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/profile", async (
                HttpContext http,
                GetMyProfileHandler handler,
                CancellationToken ct) =>
            {
                var token = ExtractBearerToken(http.Request);
                var result = await handler
                    .HandleAsync(new GetMyProfileQuery(token), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("GetMyProfile")
            .WithSummary("The current user's profile, effective permissions and access history.")
            .RequireAuthorization();

        return app;
    }

    private static string? ExtractBearerToken(HttpRequest request)
    {
        var value = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..].Trim()
            : null;
    }
}

