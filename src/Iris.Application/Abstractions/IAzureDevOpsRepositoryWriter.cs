namespace Iris.Application.Abstractions;

/// <summary>One proposed change to an Azure DevOps git repository: a new branch off
/// <see cref="BaseBranch"/>, one commit adding/updating every file in <see cref="Files"/>
/// (path → full new content), and a Pull Request from that branch back into
/// <see cref="BaseBranch"/>. Iris never pushes to <see cref="BaseBranch"/> directly — a human
/// always merges the PR.</summary>
public sealed record AzureDevOpsChangeProposal(
    string Project,
    string Repository,
    string BaseBranch,
    string NewBranchName,
    string CommitMessage,
    string PrTitle,
    string PrDescription,
    IReadOnlyDictionary<string, string> Files);

public sealed record AzureDevOpsPullRequestResult(int PullRequestId, string Url);

/// <summary>Proposes a change to an Azure DevOps git repository as a Pull Request — the only way
/// Iris ever writes to a repository it doesn't own the content of (the AWX automation repo).
/// Read-only otherwise: see <see cref="IAzureDevOpsRepositoryReader"/>.</summary>
public interface IAzureDevOpsRepositoryWriter
{
    Task<AzureDevOpsPullRequestResult> ProposeChangeAsync(
        AzureDevOpsChangeProposal proposal, CancellationToken cancellationToken = default);
}
