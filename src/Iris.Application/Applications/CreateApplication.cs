using System.Text.RegularExpressions;
using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

/// <summary>Command for <c>POST /applications</c>.</summary>
public sealed record CreateApplicationCommand(
    string Name,
    string? Slug,
    string RuntimeType,
    string RepositoryUrl,
    string DefaultBranch,
    string? Description);

public sealed partial class CreateApplicationHandler(IApplicationRepository applications, IUnitOfWork unitOfWork)
{
    public async Task<ApplicationResponse> HandleAsync(CreateApplicationCommand command, CancellationToken cancellationToken = default)
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

        var slug = string.IsNullOrWhiteSpace(command.Slug) ? Slugify(command.Name) : command.Slug.Trim().ToLowerInvariant();
        if (await applications.ExistsBySlugAsync(slug, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"An application with slug '{slug}' already exists.");
        }

        var application = new ApplicationDefinition(
            Guid.CreateVersion7(), command.Name, slug, runtimeType, command.RepositoryUrl, command.DefaultBranch, command.Description);

        await applications.AddAsync(application, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return application.ToResponse();
    }

    private static string Slugify(string name)
    {
        var lowered = name.Trim().ToLowerInvariant();
        var dashed = NonSlugCharacters().Replace(lowered, "-");
        return dashed.Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();
}
