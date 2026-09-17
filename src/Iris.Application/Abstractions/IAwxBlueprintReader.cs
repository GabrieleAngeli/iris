namespace Iris.Application.Abstractions;

/// <summary>One Job Template declared in the AWX automation repo's blueprint manifest —
/// <see cref="ResolvedJobTemplateId"/> is AWX's real numeric id for it, resolved by name
/// (<see cref="IAwxClient.GetJobTemplateAsync"/>), or null when AWX doesn't have a matching
/// template yet (declared in the repo, not yet synced).</summary>
public sealed record AwxBlueprintTemplateOption(
    string Name,
    string? Playbook,
    bool UseFactCache,
    int? ResolvedJobTemplateId);

/// <summary>Reads the AWX automation repo's blueprint manifest (the same one
/// <c>AwxBlueprintDriftConnector</c> compares against real AWX) so a Configure dialog can offer a
/// picker of known Job Template names instead of requiring the operator to type a numeric AWX id
/// from memory.</summary>
public interface IAwxBlueprintReader
{
    Task<IReadOnlyList<AwxBlueprintTemplateOption>> ListDeclaredTemplatesAsync(CancellationToken cancellationToken = default);
}
