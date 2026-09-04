using Iris.Domain.Common;

namespace Iris.Domain.Applications;

public sealed class ApplicationInstallationBinding : Entity<Guid>
{
    private ApplicationInstallationBinding()
        : base(Guid.Empty)
    {
        PlaceholderKey = string.Empty;
        TargetKind = string.Empty;
    }

    internal ApplicationInstallationBinding(
        Guid id,
        Guid applicationInstallationId,
        string placeholderKey,
        string targetKind,
        Guid? targetId,
        string? targetSlug,
        string? valuePreview,
        string? notes)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(placeholderKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        ApplicationInstallationId = applicationInstallationId;
        PlaceholderKey = placeholderKey.Trim();
        TargetKind = targetKind.Trim();
        TargetId = targetId;
        TargetSlug = string.IsNullOrWhiteSpace(targetSlug) ? null : targetSlug.Trim();
        ValuePreview = string.IsNullOrWhiteSpace(valuePreview) ? null : valuePreview.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public Guid ApplicationInstallationId { get; private set; }

    public string PlaceholderKey { get; private set; }

    public string TargetKind { get; private set; }

    public Guid? TargetId { get; private set; }

    public string? TargetSlug { get; private set; }

    public string? ValuePreview { get; private set; }

    public string? Notes { get; private set; }
}
