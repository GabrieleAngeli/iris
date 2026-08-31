using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Application.Common;
using Iris.Domain.Access;
using Microsoft.AspNetCore.Authorization;

namespace Iris.Api.Authorization;

/// <summary>
/// Resolves the request scope (from <c>customerId</c> / <c>contextId</c> route or
/// query values) and asks the application layer whether the caller holds the
/// required permission there.
/// </summary>
public sealed class PermissionAuthorizationHandler(
    IPermissionAuthorizer authorizer,
    ICurrentUser currentUser,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.ExternalId))
        {
            return;
        }

        PermissionId permission;
        try
        {
            permission = PermissionId.Parse(requirement.Permission);
        }
        catch (FormatException)
        {
            return;
        }

        AccessScope target;
        try
        {
            target = ScopeFactory.From(
                ReadGuid("customerId"),
                ReadGuid("contextId"));
        }
        catch (InvalidScopeRequestException)
        {
            return;
        }

        var allowed = await authorizer
            .IsAllowedAsync(currentUser.ExternalId, permission, target, httpContextAccessor.HttpContext?.RequestAborted ?? default)
            .ConfigureAwait(false);

        if (allowed)
        {
            context.Succeed(requirement);
        }
    }

    private Guid? ReadGuid(string key)
    {
        var http = httpContextAccessor.HttpContext;
        if (http is null)
        {
            return null;
        }

        if (http.Request.RouteValues.TryGetValue(key, out var routeValue) &&
            Guid.TryParse(routeValue?.ToString(), out var fromRoute))
        {
            return fromRoute;
        }

        if (http.Request.Query.TryGetValue(key, out var queryValue) &&
            Guid.TryParse(queryValue.ToString(), out var fromQuery))
        {
            return fromQuery;
        }

        return null;
    }
}
