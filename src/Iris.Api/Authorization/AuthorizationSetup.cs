using Microsoft.AspNetCore.Authorization;

namespace Iris.Api.Authorization;

public static class AuthorizationSetup
{
    public static IServiceCollection AddIrisAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization();
        return services;
    }
}
