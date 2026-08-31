namespace Iris.Application.Access;

/// <summary>Assembles the <see cref="UserAccessSnapshot"/> for a given external identity.</summary>
public interface IUserAccessService
{
    Task<UserAccessSnapshot?> GetSnapshotAsync(string externalId, CancellationToken cancellationToken = default);
}
