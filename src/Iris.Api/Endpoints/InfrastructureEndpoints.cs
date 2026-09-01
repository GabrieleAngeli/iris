using Iris.Api.Authorization;
using Iris.Application.Infrastructure;
using Iris.Contracts.Infrastructure;
using Iris.Domain.Access;

namespace Iris.Api.Endpoints;

public static class InfrastructureEndpoints
{
    public static IEndpointRouteBuilder MapInfrastructureEndpoints(this IEndpointRouteBuilder app)
    {
        var servers = app.MapGroup("/servers").WithTags("Infrastructure");

        servers.MapGet("", async (ListServersHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(new ListServersQuery(), ct).ConfigureAwait(false)))
            .WithName("ListServers")
            .WithSummary("Every registered server and the credentials it holds.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Infrastructure.Read));

        servers.MapPost("", async (
                CreateServerRequest body,
                CreateServerHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new CreateServerCommand(
                        body.Name, body.Hostname, body.Os, body.HostingType,
                        body.PublicIpAddress, body.PrivateIpAddress, body.Environment), ct)
                    .ConfigureAwait(false);
                return Results.Created($"/servers/{result.Id}", result);
            })
            .WithName("CreateServer")
            .WithSummary("Register a server.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Infrastructure.Write));

        servers.MapPost("/{serverId:guid}/credentials", async (
                Guid serverId,
                AddServerCredentialRequest body,
                AddServerCredentialHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new AddServerCredentialCommand(serverId, body.Username, body.AuthMethod, body.SecretValue, body.Label), ct)
                    .ConfigureAwait(false);
                return Results.Created($"/servers/{serverId}/credentials/{result.Id}", result);
            })
            .WithName("AddServerCredential")
            .WithSummary("Add an OS-login credential to a server.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Infrastructure.Write));

        servers.MapDelete("/{serverId:guid}/credentials/{credentialId:guid}", async (
                Guid serverId,
                Guid credentialId,
                RemoveServerCredentialHandler handler,
                CancellationToken ct) =>
            {
                await handler.HandleAsync(new RemoveServerCredentialCommand(serverId, credentialId), ct).ConfigureAwait(false);
                return Results.NoContent();
            })
            .WithName("RemoveServerCredential")
            .WithSummary("Remove a credential from a server.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Infrastructure.Delete));

        return app;
    }
}
