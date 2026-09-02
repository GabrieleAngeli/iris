using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

internal static class ApplicationMapping
{
    public static ApplicationResponse ToResponse(this ApplicationDefinition application) => new(
        application.Id,
        application.Name,
        application.Slug,
        application.RuntimeType.ToString(),
        application.RepositoryUrl,
        application.DefaultBranch,
        application.Description,
        application.IsActive,
        application.Versions.Select(v => v.ToSummaryResponse()).ToArray());

    public static ApplicationVersionSummaryResponse ToSummaryResponse(this ApplicationVersion version) => new(
        version.Id,
        version.Version,
        version.SourceReference,
        version.RuntimeMetadata.ToResponse(),
        version.ConfigurationKeys.Count,
        version.Dependencies.Count,
        version.Placeholders.Count,
        version.LastImportedAtUtc);

    public static ApplicationVersionDetailResponse ToDetailResponse(this ApplicationVersion version) => new(
        version.Id,
        version.ApplicationId,
        version.Version,
        version.SourceReference,
        version.RuntimeMetadata.ToResponse(),
        version.ConfigurationKeys.Select(k => new ConfigurationKeyResponse(
            k.Id, k.Key, k.TargetKind, k.Required, k.Secret, k.DefaultValue, k.Description, k.Purpose, k.PlaceholderKey)).ToArray(),
        version.Dependencies.Select(d => new DependencyResponse(
            d.Id, d.Name, d.Category, d.Required, d.Description, d.PlaceholderKey)).ToArray(),
        version.Placeholders.Select(p => new PlaceholderResponse(
            p.Id, p.Key, p.Category, p.Description, p.Required)).ToArray(),
        version.ImportWarnings.ToArray(),
        version.LastImportedAtUtc,
        version.LastImportSchemaVersion);

    public static RuntimeMetadataResponse ToResponse(this RuntimeMetadata metadata) => new(
        metadata.RuntimeName,
        metadata.PreferredOs?.ToString(),
        metadata.RequiredCpuCores,
        metadata.RequiredMemoryMb,
        metadata.RequiredPorts);
}
