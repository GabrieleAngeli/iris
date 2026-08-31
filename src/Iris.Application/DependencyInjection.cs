using Iris.Application.Access;
using Iris.Application.Governance;
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
        services.TryAddScoped<AssignRoleHandler>();
        services.TryAddScoped<RevokeRoleHandler>();

        return services;
    }
}
