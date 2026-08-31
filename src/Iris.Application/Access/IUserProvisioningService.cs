using Iris.Application.Abstractions;
using Iris.Domain.Access;

namespace Iris.Application.Access;

/// <summary>
/// Just-in-time provisioning: makes sure the authenticated principal has a
/// corresponding <see cref="User"/> row, creating or refreshing it on sign-in.
/// </summary>
public interface IUserProvisioningService
{
    Task<User> EnsureProvisionedAsync(ICurrentUser principal, CancellationToken cancellationToken = default);
}
