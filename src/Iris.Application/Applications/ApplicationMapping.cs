using System.Text.Json;
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
        application.ArtifactProvider,
        application.ArtifactFeed,
        application.ArtifactName,
        application.ArtifactPath,
        application.BuildPipelineUrl,
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
        version.ApplicationUnits.Count,
        version.InstallationProfiles.Count,
        version.DependencyConstraints.Count,
        version.LastImportedAtUtc);

    public static ApplicationVersionDetailResponse ToDetailResponse(this ApplicationVersion version) => new(
        version.Id,
        version.ApplicationId,
        version.Version,
        version.SourceReference,
        version.RuntimeMetadata.ToResponse(),
        version.ConfigurationKeys.Select(k => new ConfigurationKeyResponse(
            k.Id,
            k.Key,
            k.TargetKind,
            k.Required,
            k.Secret,
            k.DefaultValue,
            k.Description,
            k.Purpose,
            k.PlaceholderKey,
            k.ValueType,
            k.ItemType,
            k.Scope,
            k.SerializationJson,
            k.ResolutionJson,
            k.ProfilesJson,
            k.ProfileDefaultsJson,
            k.ItemSchemaJson)).ToArray(),
        version.Dependencies.Select(d => new DependencyResponse(
            d.Id, d.Name, d.Category, d.Required, d.Description, d.PlaceholderKey, d.ProviderApplicationSlug, d.ProviderPlaceholderKey)).ToArray(),
        version.Placeholders.Select(p => new PlaceholderResponse(
            p.Id, p.Key, p.Category, p.Description, p.Required)).ToArray(),
        version.ApplicationUnits.Select(u => new ApplicationUnitResponse(
            u.Id,
            u.Key,
            u.DisplayName,
            u.Kind,
            u.EntryPoint,
            u.ArtifactPath,
            DeserializeList<string>(u.ExecutionTargetsJson),
            DeserializeList<string>(u.ProfilesJson))).ToArray(),
        version.InstallationProfiles.Select(p => new InstallationProfileResponse(
            p.Id,
            p.Key,
            p.DisplayName,
            p.Required,
            p.Multiple,
            DeserializeList<string>(p.ConfigurationKeysJson))).ToArray(),
        version.DependencyConstraints.Select(c => new DependencyConstraintResponse(
            c.Id,
            c.PlaceholderKey,
            c.ServiceKind,
            c.VersionExpression,
            c.DetailsJson)).ToArray(),
        version.ImportWarnings.ToArray(),
        version.LastImportedAtUtc,
        version.LastImportSchemaVersion);

    public static RuntimeMetadataResponse ToResponse(this RuntimeMetadata metadata) => new(
        metadata.RuntimeName,
        metadata.PreferredOs?.ToString(),
        metadata.RequiredCpuCores,
        metadata.RequiredMemoryMb,
        metadata.RequiredPorts,
        DeserializeList<string>(metadata.ExecutionTargetsJson),
        DeserializeList<RuntimeOsSupportInfo>(metadata.OsSupportJson),
        metadata.MinimumCpuCores,
        metadata.MinimumMemoryMb,
        DeserializeList<string>(metadata.PortKeysJson));

    private static IReadOnlyList<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<T>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
