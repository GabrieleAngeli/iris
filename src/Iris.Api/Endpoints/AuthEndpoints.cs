using Iris.Application.Access;
using Iris.Contracts.Access;

namespace Iris.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/password", async (
                SetPasswordRequest body,
                SetMyPasswordHandler handler,
                CancellationToken ct) =>
            {
                await handler
                    .HandleAsync(new SetMyPasswordCommand(body.NewPassword, body.CurrentPassword), ct)
                    .ConfigureAwait(false);
                return Results.NoContent();
            })
            .WithName("SetMyPassword")
            .WithSummary("Set or change the local password used for non-SSO sign-in.")
            .RequireAuthorization();

        auth.MapPost("/password/skip", async (
                SkipMyPasswordSetupHandler handler,
                CancellationToken ct) =>
            {
                await handler.HandleAsync(new SkipMyPasswordSetupCommand(), ct).ConfigureAwait(false);
                return Results.NoContent();
            })
            .WithName("SkipMyPasswordSetup")
            .WithSummary("Decline to set a local password now; stop being prompted.")
            .RequireAuthorization();

        return app;
    }
}
