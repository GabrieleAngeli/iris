using Iris.Application.Abstractions;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

/// <summary>Query for <c>GET /system/integrations/awx-blueprint/templates</c>. Lets the "Configure
/// AWX" dialog offer a picker of Job Template names declared in the AWX automation repo's blueprint
/// manifest, instead of requiring the operator to type a numeric AWX id from memory.</summary>
public sealed record ListAwxBlueprintTemplatesQuery;

public sealed class ListAwxBlueprintTemplatesHandler(IAwxBlueprintReader blueprintReader)
{
    public async Task<IReadOnlyList<AwxBlueprintTemplateResponse>> HandleAsync(
        ListAwxBlueprintTemplatesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var templates = await blueprintReader.ListDeclaredTemplatesAsync(cancellationToken).ConfigureAwait(false);
        return templates
            .Select(t => new AwxBlueprintTemplateResponse(t.Name, t.Playbook, t.UseFactCache, t.ResolvedJobTemplateId))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
