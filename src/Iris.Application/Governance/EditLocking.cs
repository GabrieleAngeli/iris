using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Application.Common;
using Iris.Contracts.Governance;
using Iris.Domain.Access;

namespace Iris.Application.Governance;

/// <summary>Shared rules for the advisory edit-lock handlers.</summary>
public static class EditLockPolicy
{
    /// <summary>How long a lock lives without a refresh. The client heartbeats well inside this.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    private static readonly HashSet<string> Resources = new(StringComparer.Ordinal) { "user", "server", "customer" };

    public static string NormalizeResourceType(string? resourceType)
    {
        var value = resourceType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Resources.Contains(value))
        {
            throw new ValidationException($"'{resourceType}' is not a lockable resource type.");
        }

        return value;
    }
}

public sealed record AcquireEditLockCommand(string ResourceType, Guid ResourceId);

/// <summary>
/// <c>POST /locks/{resourceType}/{resourceId}</c>. Acquires the lock, or — if the caller already
/// holds it — refreshes it (this is also the heartbeat). If the lock is held by someone else and
/// still live the call succeeds with <see cref="EditLockResponse.Mine"/> = <c>false</c>: nothing
/// is changed and the caller learns who holds it.
/// </summary>
public sealed class AcquireEditLockHandler(
    IEditLockRepository locks,
    IUserAccessService accessService,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<EditLockResponse> HandleAsync(
        AcquireEditLockCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var resourceType = EditLockPolicy.NormalizeResourceType(command.ResourceType);
        var me = await ResolveCallerAsync(accessService, currentUser, cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;

        var existing = await locks.FindAsync(resourceType, command.ResourceId, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            var created = EditLock.Acquire(
                Guid.CreateVersion7(), resourceType, command.ResourceId, me.UserId, me.DisplayName, now, EditLockPolicy.Ttl);
            await locks.AddAsync(created, cancellationToken).ConfigureAwait(false);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return ToResponse(created, me.UserId);
            }
            catch (Exception)
            {
                // Most likely another request inserted the lock first (unique index on
                // resource type + id). If a competing lock is now on file, report its holder;
                // otherwise the failure was something else — let it surface.
                var winner = await locks.FindAsync(resourceType, command.ResourceId, cancellationToken).ConfigureAwait(false);
                if (winner is null)
                {
                    throw;
                }

                return ToResponse(winner, me.UserId);
            }
        }

        if (existing.IsHeldBy(me.UserId))
        {
            existing.Refresh(now, EditLockPolicy.Ttl);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ToResponse(existing, me.UserId);
        }

        if (existing.IsExpired(now))
        {
            existing.TakeOver(me.UserId, me.DisplayName, now, EditLockPolicy.Ttl);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ToResponse(existing, me.UserId);
        }

        return ToResponse(existing, me.UserId);
    }

    internal static async Task<UserAccessSnapshot> ResolveCallerAsync(
        IUserAccessService accessService,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var externalId = currentUser.ExternalId;
        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new ValidationException("The caller is not authenticated.");
        }

        return await accessService.GetSnapshotAsync(externalId, cancellationToken).ConfigureAwait(false)
            ?? throw new ValidationException("The caller has no provisioned Iris account.");
    }

    internal static EditLockResponse ToResponse(EditLock editLock, Guid callerId) => new(
        editLock.ResourceType,
        editLock.ResourceId,
        editLock.IsHeldBy(callerId),
        editLock.HolderUserId,
        editLock.HolderDisplayName,
        editLock.AcquiredAtUtc,
        editLock.ExpiresAtUtc);
}

public sealed record ReleaseEditLockCommand(string ResourceType, Guid ResourceId, bool Force);

/// <summary><c>DELETE /locks/{resourceType}/{resourceId}</c>. Idempotent. Only the holder may release —
/// unless <paramref name="Force"/> is set and the caller is a platform administrator.</summary>
public sealed class ReleaseEditLockHandler(
    IEditLockRepository locks,
    IUserAccessService accessService,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(ReleaseEditLockCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var resourceType = EditLockPolicy.NormalizeResourceType(command.ResourceType);
        var me = await AcquireEditLockHandler
            .ResolveCallerAsync(accessService, currentUser, cancellationToken)
            .ConfigureAwait(false);

        var existing = await locks.FindAsync(resourceType, command.ResourceId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return;
        }

        var mayForce = command.Force &&
            me.EffectivePermissions(AccessScope.Global()).Contains(Permissions.PlatformAdmin);

        if (!existing.IsHeldBy(me.UserId) && !existing.IsExpired(clock.UtcNow) && !mayForce)
        {
            throw new ConflictException($"The edit lock is held by {existing.HolderDisplayName}.");
        }

        locks.Remove(existing);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed record GetEditLockQuery(string ResourceType, Guid ResourceId);

/// <summary><c>GET /locks/{resourceType}/{resourceId}</c>. Returns <c>null</c> when the resource is free
/// (never locked, or the lock has lapsed).</summary>
public sealed class GetEditLockHandler(
    IEditLockRepository locks,
    IUserAccessService accessService,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<EditLockResponse?> HandleAsync(
        GetEditLockQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var resourceType = EditLockPolicy.NormalizeResourceType(query.ResourceType);
        var me = await AcquireEditLockHandler
            .ResolveCallerAsync(accessService, currentUser, cancellationToken)
            .ConfigureAwait(false);

        var existing = await locks.FindAsync(resourceType, query.ResourceId, cancellationToken).ConfigureAwait(false);
        if (existing is null || existing.IsExpired(clock.UtcNow))
        {
            return null;
        }

        return AcquireEditLockHandler.ToResponse(existing, me.UserId);
    }
}
