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
                        body.PublicIpAddress, body.PrivateIpAddress, body.Environment,
                        ToInput(body.Credential)), ct)
                    .ConfigureAwait(false);
                return Results.Created($"/servers/{result.Id}", result);
            })
            .WithName("CreateServer")
            .WithSummary("Register a server, optionally with its first OS-login credential.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Infrastructure.Write));

        servers.MapPut("/{serverId:guid}", async (
                Guid serverId,
                UpdateServerRequest body,
                UpdateServerHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new UpdateServerCommand(
                        serverId, body.Name, body.Hostname, body.Os, body.HostingType,
                        body.PublicIpAddress, body.PrivateIpAddress, body.Environment), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("UpdateServer")
            .WithSummary("Update a server's identity and network details.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Infrastructure.Write));

        servers.MapDelete("/{serverId:guid}", async (
                Guid serverId,
                DeleteServerHandler handler,
                CancellationToken ct) =>
            {
                await handler.HandleAsync(new DeleteServerCommand(serverId), ct).ConfigureAwait(false);
                return Results.NoContent();
            })
            .WithName("DeleteServer")
            .WithSummary("Delete a server and every credential it holds.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Infrastructure.Delete));

        servers.MapPut("/{serverId:guid}/capacity", async (
                Guid serverId,
                UpdateServerCapacityRequest body,
                UpdateServerCapacityHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new UpdateServerCapacityCommand(
                        serverId, body.Capabilities, body.Resources, body.UsedPorts), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("UpdateServerCapacity")
            .WithSummary("Replace a server's capability tags, resource hints and known used ports.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Infrastructure.Write));

        servers.MapPost("/{serverId:guid}/discover", async (
                Guid serverId,
                DiscoverServerInventoryHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new DiscoverServerInventoryCommand(serverId), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("DiscoverServerInventory")
            .WithSummary("Discover OS/version/machine resources using the server credentials.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Infrastructure.Write));

        servers.MapPost("/{serverId:guid}/credentials", async (
                Guid serverId,
                AddServerCredentialRequest body,
                AddServerCredentialHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new AddServerCredentialCommand(
                        serverId, body.Username, body.AuthMethod, body.SecretValue,
                        body.Kind, body.OwnerUserId, body.ServiceName, body.Label), ct)
                    .ConfigureAwait(false);
                return Results.Created($"/servers/{serverId}/credentials/{result.Id}", result);
            })
            .WithName("AddServerCredential")
            .WithSummary("Add an OS-login credential to a server (system user or service account).")
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

        var dataServices = app.MapGroup("/data-services").WithTags("Infrastructure");

        dataServices.MapGet("", async (ListDataServicesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(new ListDataServicesQuery(), ct).ConfigureAwait(false)))
            .WithName("ListDataServices")
            .WithSummary("Managed database/cache endpoints such as MSSQL, PostgreSQL and Redis.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Infrastructure.Read));

        dataServices.MapPost("", async (
                UpsertDataServiceRequest body,
                CreateDataServiceHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new CreateDataServiceCommand(
                        body.Name, body.Kind, body.Endpoint, body.Port,
                        body.Version, body.Size, body.StorageGb, body.Environment,
                        body.Username, body.PasswordValue), ct)
                    .ConfigureAwait(false);
                return Results.Created($"/data-services/{result.Id}", result);
            })
            .WithName("CreateDataService")
            .WithSummary("Register a managed MSSQL, PostgreSQL or Redis service.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Infrastructure.Write));

        dataServices.MapPut("/{dataServiceId:guid}", async (
                Guid dataServiceId,
                UpsertDataServiceRequest body,
                UpdateDataServiceHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new UpdateDataServiceCommand(
                        dataServiceId, body.Name, body.Kind, body.Endpoint, body.Port,
                        body.Version, body.Size, body.StorageGb, body.Environment, body.IsActive,
                        body.Username, body.PasswordValue), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("UpdateDataService")
            .WithSummary("Update a managed data service.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Infrastructure.Write));

        dataServices.MapPost("/{dataServiceId:guid}/discover", async (
                Guid dataServiceId,
                DiscoverDataServiceInventoryHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new DiscoverDataServiceInventoryCommand(dataServiceId), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("DiscoverDataServiceInventory")
            .WithSummary("Discover managed database/cache type and version using username/password credentials.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Infrastructure.Write));

        return app;
    }

    private static ServerCredentialInput? ToInput(ServerCredentialInputRequest? request) =>
        request is null
            ? null
            : new ServerCredentialInput(
                request.Username, request.AuthMethod, request.SecretValue,
                request.Kind, request.OwnerUserId, request.ServiceName, request.Label);
}
