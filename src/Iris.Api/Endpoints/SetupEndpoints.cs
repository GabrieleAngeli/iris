using Iris.Application.Setup;
using Iris.Contracts.Setup;

namespace Iris.Api.Endpoints;

public static class SetupEndpoints
{
    public static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder app)
    {
        var setup = app.MapGroup("/setup").WithTags("Setup");

        setup.MapGet("/status", async (GetSetupStatusHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(new GetSetupStatusQuery(), ct).ConfigureAwait(false)))
            .WithName("GetSetupStatus")
            .WithSummary("Whether the first-run setup wizard (mail provider + super-admin) still needs to run.")
            .AllowAnonymous();

        setup.MapPost("/test-mail", async (
                TestMailConnectionRequest body,
                TestMailConnectionHandler handler,
                CancellationToken ct) =>
            {
                await handler
                    .HandleAsync(new TestMailConnectionCommand(body.Mail, body.TestRecipient), ct)
                    .ConfigureAwait(false);
                return Results.NoContent();
            })
            .WithName("TestMailConnection")
            .WithSummary("Tries mail settings before they're saved: connects and sends a real test email.")
            .AllowAnonymous();

        setup.MapPost("/complete", async (
                CompleteSetupRequest body,
                CompleteSetupHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new CompleteSetupCommand(body.Mail, body.AdminEmail, body.AdminDisplayName, body.AdminPassword), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("CompleteSetup")
            .WithSummary("Configures the mail relay and creates the first super-admin. Usable only once.")
            .AllowAnonymous();

        setup.MapPost("/claim-admin", async (
                IConfiguration configuration,
                ClaimSetupAdminHandler handler,
                CancellationToken ct) =>
            {
                var allowedEmails = configuration
                    .GetSection("Iris:Setup:AdminClaimEmails")
                    .GetChildren()
                    .Select(child => child.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .ToArray();

                var result = await handler
                    .HandleAsync(new ClaimSetupAdminCommand(allowedEmails), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("ClaimSetupAdmin")
            .WithSummary("Lets an allow-listed authenticated SSO user claim the first platform-admin role.")
            .RequireAuthorization();

        return app;
    }
}
