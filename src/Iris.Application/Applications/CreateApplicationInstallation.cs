using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;
using Iris.Domain.Tenancy;

namespace Iris.Application.Applications;

public sealed record CreateApplicationInstallationCommand(
    Guid ApplicationId,
    string Name,
    Guid ApplicationVersionId,
    Guid ServerNodeId,
    string Environment,
    string? ApplicationUnitKey,
    string? InstallationProfileKey,
    string? Notes,
    IReadOnlyList<ApplicationInstallationBindingInput>? Bindings);

public sealed class CreateApplicationInstallationHandler(
    IApplicationRepository applications,
    IServerRepository servers,
    IDataServiceRepository dataServices,
    IApplicationInstallationRepository installations,
    IUnitOfWork unitOfWork)
{
    public async Task<ApplicationInstallationResponse> HandleAsync(
        CreateApplicationInstallationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ValidationException("Installation name is required.");
        }

        if (!Enum.TryParse<ContextKind>(command.Environment, ignoreCase: true, out var environment))
        {
            throw new ValidationException($"Unknown environment '{command.Environment}'. Expected Test, Staging or Production.");
        }

        var application = await applications.GetAsync(command.ApplicationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Application", command.ApplicationId);
        var version = application.Versions.SingleOrDefault(v => v.Id == command.ApplicationVersionId)
            ?? throw new ValidationException("Application version does not belong to the selected application.");

        if (!string.IsNullOrWhiteSpace(command.ApplicationUnitKey) &&
            !version.ApplicationUnits.Any(unit => string.Equals(unit.Key, command.ApplicationUnitKey, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException($"Application unit '{command.ApplicationUnitKey}' is not declared by version '{version.Version}'.");
        }

        if (!string.IsNullOrWhiteSpace(command.InstallationProfileKey) &&
            !version.InstallationProfiles.Any(profile => string.Equals(profile.Key, command.InstallationProfileKey, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException($"Installation profile '{command.InstallationProfileKey}' is not declared by version '{version.Version}'.");
        }

        var server = await servers.GetAsync(command.ServerNodeId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Server", command.ServerNodeId);

        var dataServiceBindings = (command.Bindings ?? [])
            .Where(binding => string.Equals(binding.TargetKind, ApplicationInstallationTargetKinds.DataService, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var binding in dataServiceBindings)
        {
            if (binding.TargetId is null)
            {
                throw new ValidationException($"Binding '{binding.PlaceholderKey}' must select a data service.");
            }

            if (await dataServices.GetForUpdateAsync(binding.TargetId.Value, cancellationToken).ConfigureAwait(false) is null)
            {
                throw new NotFoundException("Data service", binding.TargetId.Value);
            }
        }

        var installation = new ApplicationInstallation(
            Guid.CreateVersion7(),
            command.Name,
            application.Id,
            version.Id,
            command.ApplicationUnitKey,
            command.InstallationProfileKey,
            server.Id,
            environment,
            command.Notes);

        installation.ReplaceBindings((command.Bindings ?? []).Select(binding => new NewApplicationInstallationBinding(
            Guid.CreateVersion7(),
            binding.PlaceholderKey,
            binding.TargetKind,
            binding.TargetId,
            binding.TargetSlug,
            binding.ValuePreview,
            binding.Notes)));

        await installations.AddAsync(installation, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return installation.ToResponse(application, version, server);
    }
}

public static class ApplicationInstallationTargetKinds
{
    public const string DataService = "dataService";
    public const string Application = "application";
    public const string Manual = "manual";
}
