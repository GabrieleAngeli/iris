namespace Iris.Contracts.Access;

public sealed record AccessHistoryResponse(
    DateTimeOffset SignedInAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string Method,
    bool IsCurrent);

public sealed record MyProfileResponse(
    MeResponse Me,
    IReadOnlyList<AccessHistoryResponse> AccessHistory);

