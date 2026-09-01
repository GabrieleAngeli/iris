using Iris.Application.Access;
using Iris.Application.Governance;
using Iris.Application.Infrastructure;
using Iris.Application.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Iris.Application;

/// <summary>
/// Composition entry point for the application layer: use case handlers,
/// the validation pipeline and cross-cutting behaviours are registered here.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddIrisApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IUserAccessService, UserAccessService>();
        services.TryAddScoped<IPermissionAuthorizer, PermissionAuthorizer>();
        services.TryAddScoped<IUserProvisioningService, UserProvisioningService>();

        // Queries
        services.TryAddScoped<GetMyAccessHandler>();
        services.TryAddScoped<GetPermissionCatalogHandler>();
        services.TryAddScoped<ListRolesHandler>();
        services.TryAddScoped<ListUsersHandler>();
        services.TryAddScoped<ListAccessibleCustomersHandler>();

        // Governance commands
        services.TryAddScoped<CreateCustomerHandler>();
        services.TryAddScoped<AddContextHandler>();
        services.TryAddScoped<CreateUserHandler>();
        services.TryAddScoped<UpdateUserHandler>();
        services.TryAddScoped<DeleteUserHandler>();
        services.TryAddScoped<IssueUserInvitationHandler>();
        services.TryAddScoped<AcquireEditLockHandler>();
        services.TryAddScoped<ReleaseEditLockHandler>();
        services.TryAddScoped<GetEditLockHandler>();
        services.TryAddScoped<AssignRoleHandler>();
        services.TryAddScoped<RevokeRoleHandler>();

        // Infrastructure commands
        services.TryAddScoped<ServerCredentialFactory>();
        services.TryAddScoped<CreateServerHandler>();
        services.TryAddScoped<UpdateServerHandler>();
        services.TryAddScoped<DeleteServerHandler>();
        services.TryAddScoped<AddServerCredentialHandler>();
        services.TryAddScoped<RemoveServerCredentialHandler>();
        services.TryAddScoped<ListServersHandler>();

        return services;
    }
}
