using Iris.Contracts.Applications;
using Iris.Domain.Applications;
using Iris.Domain.Infrastructure;

namespace Iris.Application.Applications;

internal static class ApplicationInstallationMapping
{
    public static ApplicationInstallationResponse ToResponse(
        this ApplicationInstallation installation,
        ApplicationDefinition application,
        ApplicationVersion version,
        ServerNode server) => new(
            installation.Id,
            installation.Name,
            application.Id,
            application.Name,
            application.Slug,
            version.Id,
            version.Version,
            installation.ApplicationUnitKey,
            installation.InstallationProfileKey,
            server.Id,
            server.Name,
            installation.Environment.ToString(),
            installation.Notes,
            installation.IsActive,
            installation.Bindings.Select(binding => new ApplicationInstallationBindingResponse(
                binding.Id,
                binding.PlaceholderKey,
                binding.TargetKind,
                binding.TargetId,
                binding.TargetSlug,
                binding.ValuePreview,
                binding.Notes)).ToArray(),
            installation.CreatedAtUtc,
            installation.UpdatedAtUtc);
}
