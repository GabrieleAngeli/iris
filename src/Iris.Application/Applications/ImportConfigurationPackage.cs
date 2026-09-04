using System.Text.Json;
using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

/// <summary>Command for <c>POST /applications/{applicationId}/versions/{versionId}/import</c>.</summary>
public sealed record ImportConfigurationPackageCommand(
    Guid ApplicationId,
    Guid VersionId,
    string SchemaVersion,
    IReadOnlyList<ConfigurationKeyInput> ConfigurationKeys,
    IReadOnlyList<DependencyInput> Dependencies,
    IReadOnlyList<PlaceholderInput> Placeholders,
    IReadOnlyList<string>? Warnings,
    IReadOnlyList<ApplicationUnitInput>? ApplicationUnits = null,
    IReadOnlyList<InstallationProfileInput>? InstallationProfiles = null,
    IReadOnlyList<DependencyConstraintInput>? DependencyConstraints = null);

public sealed class ImportConfigurationPackageHandler(IApplicationRepository applications, IClock clock, IUnitOfWork unitOfWork)
{
    public async Task<ApplicationVersionDetailResponse> HandleAsync(
        ImportConfigurationPackageCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.SchemaVersion))
        {
            throw new ValidationException("Package schema version is required.");
        }

        var application = await applications.GetForUpdateAsync(command.ApplicationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Application", command.ApplicationId);

        var version = application.Versions.SingleOrDefault(v => v.Id == command.VersionId)
            ?? throw new NotFoundException("Application version", command.VersionId);

        var configurationKeys = command.ConfigurationKeys
            .Select(k => new NewConfigurationKey(
                Guid.CreateVersion7(),
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
                k.ItemSchemaJson))
            .ToArray();

        var dependencies = command.Dependencies
            .Select(d => new NewDependencyDefinition(
                Guid.CreateVersion7(),
                d.Name,
                d.Category,
                d.Required,
                d.Description,
                d.PlaceholderKey,
                d.ProviderApplicationSlug,
                d.ProviderPlaceholderKey))
            .ToArray();

        var placeholders = command.Placeholders
            .Select(p => new NewPlaceholderDefinition(Guid.CreateVersion7(), p.Key, p.Category, p.Description, p.Required))
            .ToArray();

        var applicationUnits = (command.ApplicationUnits ?? [])
            .Select(u => new NewApplicationUnitDefinition(
                Guid.CreateVersion7(),
                u.Key,
                u.DisplayName,
                u.Kind,
                u.EntryPoint,
                u.ArtifactPath,
                SerializeOrNull(u.ExecutionTargets),
                SerializeOrNull(u.Profiles)))
            .ToArray();

        var installationProfiles = (command.InstallationProfiles ?? [])
            .Select(p => new NewInstallationProfileDefinition(
                Guid.CreateVersion7(),
                p.Key,
                p.DisplayName,
                p.Required,
                p.Multiple,
                SerializeOrNull(p.ConfigurationKeys)))
            .ToArray();

        var dependencyConstraints = (command.DependencyConstraints ?? [])
            .Select(c => new NewDependencyConstraintDefinition(
                Guid.CreateVersion7(),
                c.PlaceholderKey,
                c.ServiceKind,
                c.VersionExpression,
                c.DetailsJson))
            .ToArray();

        var warnings = command.Warnings ?? [];

        // The original package as accepted — kept verbatim for audit/reprocessing, per the
        // Iris Extractor contract ("Important rule: persist the original package content").
        var rawPackageJson = JsonSerializer.Serialize(new
        {
            command.SchemaVersion,
            command.ConfigurationKeys,
            command.Dependencies,
            command.Placeholders,
            command.ApplicationUnits,
            command.InstallationProfiles,
            command.DependencyConstraints,
            Warnings = warnings,
        });

        version.ApplyImport(
            command.SchemaVersion,
            rawPackageJson,
            configurationKeys,
            dependencies,
            placeholders,
            applicationUnits,
            installationProfiles,
            dependencyConstraints,
            warnings,
            clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return version.ToDetailResponse();
    }

    private static string? SerializeOrNull<T>(IReadOnlyList<T>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : null;
}
