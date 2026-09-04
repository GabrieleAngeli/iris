using Iris.Api.Authorization;
using Iris.Application.Applications;
using Iris.Contracts.Applications;
using Iris.Domain.Access;

namespace Iris.Api.Endpoints;

public static class ApplicationsEndpoints
{
    public static IEndpointRouteBuilder MapApplicationsEndpoints(this IEndpointRouteBuilder app)
    {
        var applications = app.MapGroup("/applications").WithTags("Applications");

        applications.MapGet("", async (ListApplicationsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(new ListApplicationsQuery(), ct).ConfigureAwait(false)))
            .WithName("ListApplications")
            .WithSummary("The application catalog, with each version's configuration knowledge in summary form.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Applications.Read));

        applications.MapPost("", async (
                CreateApplicationRequest body,
                CreateApplicationHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new CreateApplicationCommand(
                        body.Name,
                        body.Slug,
                        body.RuntimeType,
                        body.RepositoryUrl,
                        body.DefaultBranch,
                        body.Description,
                        body.ArtifactProvider,
                        body.ArtifactFeed,
                        body.ArtifactName,
                        body.ArtifactPath,
                        body.BuildPipelineUrl), ct)
                    .ConfigureAwait(false);
                return Results.Created($"/applications/{result.Id}", result);
            })
            .WithName("CreateApplication")
            .WithSummary("Register an application in the catalog.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Applications.Write));

        applications.MapPut("/{applicationId:guid}", async (
                Guid applicationId,
                UpdateApplicationRequest body,
                UpdateApplicationHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new UpdateApplicationCommand(
                        applicationId,
                        body.Name,
                        body.RuntimeType,
                        body.RepositoryUrl,
                        body.DefaultBranch,
                        body.Description,
                        body.IsActive,
                        body.ArtifactProvider,
                        body.ArtifactFeed,
                        body.ArtifactName,
                        body.ArtifactPath,
                        body.BuildPipelineUrl), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("UpdateApplication")
            .WithSummary("Update application inventory metadata. The slug remains stable.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Applications.Write));

        applications.MapPost("/{applicationId:guid}/versions", async (
                Guid applicationId,
                AddApplicationVersionRequest body,
                AddApplicationVersionHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new AddApplicationVersionCommand(
                        applicationId, body.Version, body.SourceReference, body.RuntimeMetadata), ct)
                    .ConfigureAwait(false);
                return Results.Created($"/applications/{applicationId}/versions/{result.Id}", result);
            })
            .WithName("AddApplicationVersion")
            .WithSummary("Add a version to an application, with the runtime it needs to run.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Applications.Write));

        applications.MapGet("/{applicationId:guid}/versions/{versionId:guid}", async (
                Guid applicationId,
                Guid versionId,
                GetApplicationVersionDetailHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new GetApplicationVersionDetailQuery(applicationId, versionId), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("GetApplicationVersionDetail")
            .WithSummary("A version's full configuration knowledge: keys, dependencies and placeholders.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Applications.Read));

        applications.MapPost("/{applicationId:guid}/versions/{versionId:guid}/import", async (
                Guid applicationId,
                Guid versionId,
                ImportConfigurationPackageRequest body,
                ImportConfigurationPackageHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new ImportConfigurationPackageCommand(
                        applicationId,
                        versionId,
                        body.SchemaVersion,
                        body.ConfigurationKeys,
                        body.Dependencies,
                        body.Placeholders,
                        body.Warnings,
                        body.ApplicationUnits,
                        body.InstallationProfiles,
                        body.DependencyConstraints), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("ImportApplicationConfigurationPackage")
            .WithSummary("Import an Iris Extractor configuration package, replacing the version's current knowledge.")
            .RequireAuthorization(PermissionPolicy.Name(Permissions.Applications.ImportKnowledge));

        return app;
    }
}
