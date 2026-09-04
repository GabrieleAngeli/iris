using System.Text.Json;
using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;
using Iris.Domain.Infrastructure;

namespace Iris.Application.Applications;

/// <summary>Command for <c>POST /applications/{applicationId}/versions</c>.</summary>
public sealed record AddApplicationVersionCommand(
    Guid ApplicationId,
    string Version,
    string? SourceReference,
    RuntimeMetadataRequest RuntimeMetadata);

public sealed class AddApplicationVersionHandler(IApplicationRepository applications, IUnitOfWork unitOfWork)
{
    public async Task<ApplicationVersionSummaryResponse> HandleAsync(
        AddApplicationVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Version))
        {
            throw new ValidationException("Version is required.");
        }

        if (string.IsNullOrWhiteSpace(command.RuntimeMetadata.RuntimeName))
        {
            throw new ValidationException("Runtime name is required.");
        }

        ServerOs? preferredOs = null;
        if (!string.IsNullOrWhiteSpace(command.RuntimeMetadata.PreferredOs))
        {
            if (!Enum.TryParse<ServerOs>(command.RuntimeMetadata.PreferredOs, ignoreCase: true, out var parsed))
            {
                throw new ValidationException($"Unknown OS '{command.RuntimeMetadata.PreferredOs}'. Expected Linux or Windows.");
            }

            preferredOs = parsed;
        }

        var application = await applications.GetForUpdateAsync(command.ApplicationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Application", command.ApplicationId);

        var runtimeMetadata = new RuntimeMetadata(
            command.RuntimeMetadata.RuntimeName,
            preferredOs,
            command.RuntimeMetadata.RequiredCpuCores,
            command.RuntimeMetadata.RequiredMemoryMb,
            command.RuntimeMetadata.RequiredPorts,
            SerializeOrNull(command.RuntimeMetadata.ExecutionTargets),
            SerializeOrNull(command.RuntimeMetadata.OsSupport),
            command.RuntimeMetadata.MinimumCpuCores,
            command.RuntimeMetadata.MinimumMemoryMb,
            SerializeOrNull(command.RuntimeMetadata.PortKeys));

        ApplicationVersion version;
        try
        {
            version = application.AddVersion(Guid.CreateVersion7(), command.Version, command.SourceReference, runtimeMetadata);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return version.ToSummaryResponse();
    }

    private static string? SerializeOrNull<T>(IReadOnlyList<T>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : null;
}
