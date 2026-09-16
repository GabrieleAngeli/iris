namespace Iris.Contracts.Tenancy;

public sealed record ContextSummaryResponse(
    Guid Id,
    string Name,
    string Kind,
    bool IsActive,
    string? AwxContextName = null);

public sealed record CustomerSummaryResponse(
    Guid Id,
    string Key,
    string Name,
    bool IsActive,
    IReadOnlyList<ContextSummaryResponse> Contexts);
