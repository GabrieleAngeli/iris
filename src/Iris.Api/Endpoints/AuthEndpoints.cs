using Iris.Application.Access;
using Iris.Application.Governance;
using Iris.Contracts.Access;
using Iris.Contracts.Governance;

namespace Iris.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/login", async (
                LoginRequest body,
                LoginHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new LoginCommand(body.Email, body.Password), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("Login")
            .WithSummary("Sign in with a local email and password; returns a bearer session token.")
            .AllowAnonymous();

        app.MapPost("/invitations/accept", async (
                AcceptInvitationRequest body,
                AcceptInvitationHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new AcceptInvitationCommand(body.Token, body.NewPassword), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("AcceptInvitation")
            .WithSummary("Redeem a one-time invitation link: set the account's first local password.")
            .WithTags("Auth")
            .AllowAnonymous();

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
