using Iris.Domain.Common;
using Iris.Domain.Tenancy;

namespace Iris.Domain.Applications;

public sealed class ApplicationInstallation : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    private readonly List<ApplicationInstallationBinding> _bindings = [];

    private ApplicationInstallation()
        : base(Guid.Empty)
    {
        Name = string.Empty;
    }

    public ApplicationInstallation(
        Guid id,
        string name,
        Guid applicationId,
        Guid applicationVersionId,
        string? applicationUnitKey,
        string? installationProfileKey,
        Guid serverNodeId,
        ContextKind environment,
        string? notes)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        ApplicationId = applicationId;
        ApplicationVersionId = applicationVersionId;
        ApplicationUnitKey = string.IsNullOrWhiteSpace(applicationUnitKey) ? null : applicationUnitKey.Trim();
        InstallationProfileKey = string.IsNullOrWhiteSpace(installationProfileKey) ? null : installationProfileKey.Trim();
        ServerNodeId = serverNodeId;
        Environment = environment;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsActive = true;
    }

    public string Name { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid ApplicationVersionId { get; private set; }

    public string? ApplicationUnitKey { get; private set; }

    public string? InstallationProfileKey { get; private set; }

    public Guid ServerNodeId { get; private set; }

    public ContextKind Environment { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<ApplicationInstallationBinding> Bindings => _bindings.AsReadOnly();

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public void ReplaceBindings(IEnumerable<NewApplicationInstallationBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        _bindings.Clear();
        _bindings.AddRange(bindings.Select(binding => new ApplicationInstallationBinding(
            binding.Id,
            Id,
            binding.PlaceholderKey,
            binding.TargetKind,
            binding.TargetId,
            binding.TargetSlug,
            binding.ValuePreview,
            binding.Notes)));
    }
}

public sealed record NewApplicationInstallationBinding(
    Guid Id,
    string PlaceholderKey,
    string TargetKind,
    Guid? TargetId,
    string? TargetSlug,
    string? ValuePreview,
    string? Notes);
