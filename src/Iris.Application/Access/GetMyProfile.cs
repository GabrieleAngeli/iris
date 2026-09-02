using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Application.Governance;
using Iris.Contracts.Access;

namespace Iris.Application.Access;

public sealed record GetMyProfileQuery(string? CurrentSessionToken);

public sealed class GetMyProfileHandler(
    GetMyAccessHandler access,
    IUserSessionRepository sessions)
{
    public async Task<MyProfileResponse> HandleAsync(
        GetMyProfileQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var me = await access
            .HandleAsync(new GetMyAccessQuery(), cancellationToken)
            .ConfigureAwait(false);

        var currentHash = string.IsNullOrWhiteSpace(query.CurrentSessionToken)
            ? null
            : IssueUserInvitationHandler.HashToken(query.CurrentSessionToken.Trim());

        var history = (await sessions.GetForUserAsync(me.UserId, cancellationToken).ConfigureAwait(false))
            .Select(s => new AccessHistoryResponse(
                s.CreatedAtUtc,
                s.ExpiresAtUtc,
                "Local password",
                currentHash is not null && string.Equals(s.TokenHash, currentHash, StringComparison.Ordinal)))
            .ToList();

        if (history.Count == 0)
        {
            history.Add(new AccessHistoryResponse(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Current authenticated session", true));
        }

        return new MyProfileResponse(me, history);
    }
}
