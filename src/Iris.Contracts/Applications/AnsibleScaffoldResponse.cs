namespace Iris.Contracts.Applications;

/// <summary>
/// One generated file/snippet in an <see cref="AnsibleScaffoldResponse"/> — a starting point the
/// operator copies into their own Ansible repository and maintains from there. Iris never writes
/// or re-writes it on disk itself; see <c>GET /applications/{applicationId}/versions/{versionId}/ansible-scaffold</c>.
/// </summary>
public sealed record AnsibleScaffoldFileResponse(
    string RelativePath,
    string Description,
    string Content);

/// <summary>
/// A one-shot Ansible role/playbook scaffold generated from an application version's manifest:
/// Jinja2 config templates plus a starting role/playbook skeleton. Not regenerated on every
/// deploy — a reference the operator commits into their Ansible repo and owns from that point on.
/// </summary>
public sealed record AnsibleScaffoldResponse(
    string ApplicationSlug,
    string Version,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<AnsibleScaffoldFileResponse> Files);

/// <summary>Result of <c>POST /applications/{applicationId}/versions/{versionId}/ansible-scaffold/propose</c>
/// — the Pull Request Iris opened in the AWX automation repo with the generated scaffold.</summary>
public sealed record ProposeAnsibleScaffoldResponse(int PullRequestId, string Url);
