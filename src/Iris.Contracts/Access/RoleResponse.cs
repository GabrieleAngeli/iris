namespace Iris.Contracts.Access;

public sealed record RoleResponse(
    string Key,
    string Name,
    string? Description,
    bool IsBuiltIn,
    IReadOnlyList<string> Permissions);
