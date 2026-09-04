using Iris.Application.Access;
using Iris.Application.Applications;
using Iris.Application.Audit;
using Iris.Application.Governance;
using Iris.Application.Infrastructure;
using Iris.Application.Setup;
using Iris.Application.Settings;
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
        services.TryAddScoped<SessionIssuer>();

        // Queries
        services.TryAddScoped<GetMyAccessHandler>();
        services.TryAddScoped<GetMyProfileHandler>();
        services.TryAddScoped<SetMyPasswordHandler>();
        services.TryAddScoped<LoginHandler>();
        services.TryAddScoped<RequestPasswordResetHandler>();
        services.TryAddScoped<GetSetupStatusHandler>();
        services.TryAddScoped<TestMailConnectionHandler>();
        services.TryAddScoped<CompleteSetupHandler>();
        services.TryAddScoped<ClaimSetupAdminHandler>();
        services.TryAddScoped<SkipMyPasswordSetupHandler>();
        services.TryAddScoped<GetPermissionCatalogHandler>();
        services.TryAddScoped<ListRolesHandler>();
        services.TryAddScoped<ListUsersHandler>();
        services.TryAddScoped<ListAccessibleCustomersHandler>();
        services.TryAddScoped<GetSystemSettingsHandler>();
        services.TryAddScoped<ListTransactionLogHandler>();

        // Governance commands
        services.TryAddScoped<CreateCustomerHandler>();
        services.TryAddScoped<UpdateCustomerHandler>();
        services.TryAddScoped<AddContextHandler>();
        services.TryAddScoped<CreateUserHandler>();
        services.TryAddScoped<UpdateUserHandler>();
        services.TryAddScoped<DeleteUserHandler>();
        services.TryAddScoped<IssueUserInvitationHandler>();
        services.TryAddScoped<AcceptInvitationHandler>();
        services.TryAddScoped<AcquireEditLockHandler>();
        services.TryAddScoped<ReleaseEditLockHandler>();
        services.TryAddScoped<GetEditLockHandler>();
        services.TryAddScoped<AssignRoleHandler>();
        services.TryAddScoped<RevokeRoleHandler>();

        // Infrastructure commands
        services.TryAddScoped<ServerCredentialFactory>();
        services.TryAddScoped<CreateServerHandler>();
        services.TryAddScoped<UpdateServerHandler>();
        services.TryAddScoped<UpdateServerCapacityHandler>();
        services.TryAddScoped<DiscoverServerInventoryHandler>();
        services.TryAddScoped<DeleteServerHandler>();
        services.TryAddScoped<AddServerCredentialHandler>();
        services.TryAddScoped<RemoveServerCredentialHandler>();
        services.TryAddScoped<ListServersHandler>();
        services.TryAddScoped<ListDataServicesHandler>();
        services.TryAddScoped<CreateDataServiceHandler>();
        services.TryAddScoped<UpdateDataServiceHandler>();
        services.TryAddScoped<DiscoverDataServiceInventoryHandler>();

        // Applications commands
        services.TryAddScoped<CreateApplicationHandler>();
        services.TryAddScoped<UpdateApplicationHandler>();
        services.TryAddScoped<AddApplicationVersionHandler>();
        services.TryAddScoped<ImportConfigurationPackageHandler>();
        services.TryAddScoped<ListApplicationsHandler>();
        services.TryAddScoped<GetApplicationVersionDetailHandler>();
        services.TryAddScoped<ListApplicationInstallationsHandler>();
        services.TryAddScoped<CreateApplicationInstallationHandler>();
        services.TryAddScoped<GetApplicationInstallationAnsiblePlanHandler>();
        services.TryAddScoped<LaunchApplicationInstallationAwxJobHandler>();

        return services;
    }
}
