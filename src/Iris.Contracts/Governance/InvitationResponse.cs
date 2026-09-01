namespace Iris.Contracts.Governance;

/// <summary>
/// Result of <c>POST /governance/users/{userId}/invitation</c>. <see cref="Token"/> and
/// <see cref="AcceptLink"/> are returned exactly once — Iris keeps only a hash of the token —
/// so the caller must hand them to the recipient now.
/// </summary>
public sealed record InvitationResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string Token,
    string AcceptLink,
    DateTimeOffset ExpiresAtUtc);
