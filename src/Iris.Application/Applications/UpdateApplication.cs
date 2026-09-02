using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

/// <summary>Command for <c>PUT /applications/{applicationId}</c>.</summary>
public sealed record UpdateApplicationCommand(
    Guid ApplicationId,
    string Name,
    string RuntimeType,
    string RepositoryUrl,
    string DefaultBranch,
    string? Description,
    bool IsActive,
    string? ArtifactProvider = null,
    string? ArtifactFeed = null,
    string? ArtifactName = null,
    string? ArtifactPath = null,
    string? BuildPipelineUrl = null);

public sealed class UpdateApplicationHandler(IApplicationRepository applications, IUnitOfWork unitOfWork)
{
    public async Task<ApplicationResponse> HandleAsync(UpdateApplicationCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ValidationException("Application name is required.");
        }

        if (string.IsNullOrWhiteSpace(command.RepositoryUrl))
        {
            throw new ValidationException("Repository URL is required.");
        }

        if (string.IsNullOrWhiteSpace(command.DefaultBranch))
        {
            throw new ValidationException("Default branch is required.");
        }

        if (!Enum.TryParse<ApplicationRuntimeType>(command.RuntimeType, ignoreCase: true, out var runtimeType))
        {
            throw new ValidationException(
                $"Unknown runtime type '{command.RuntimeType}'. Expected CSharp, JavaScript, Java, Node or Docker.");
        }

        var application = await applications
            .GetForUpdateAsync(command.ApplicationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Application", command.ApplicationId);

        application.UpdateInventory(
            command.Name,
            runtimeType,
            command.RepositoryUrl,
            command.DefaultBranch,
            command.Description,
            command.IsActive,
            command.ArtifactProvider,
            command.ArtifactFeed,
            command.ArtifactName,
            command.ArtifactPath,
            command.BuildPipelineUrl);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return application.ToResponse();
    }
}
