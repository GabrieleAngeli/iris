using System.Text.Json;
using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

public sealed record LaunchApplicationInstallationAwxJobCommand(
    Guid InstallationId,
    ApplicationInstallationAwxLaunchRequest Request);

public sealed class LaunchApplicationInstallationAwxJobHandler(
    GetApplicationInstallationAnsiblePlanHandler plans,
    IAnsibleExecutionPackageBuilder ansible,
    IAwxClient awx,
    IInstallationRunRepository runs,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<ApplicationInstallationAwxLaunchResponse> HandleAsync(
        LaunchApplicationInstallationAwxJobCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var plan = await plans
            .HandleAsync(new GetApplicationInstallationAnsiblePlanQuery(command.InstallationId), cancellationToken)
            .ConfigureAwait(false);
        var package = ansible.Build(plan, command.Request);

        var preview = package.ExtraVars
            .Where(pair => pair.Value is null or string or bool or int or long)
            .ToDictionary(pair => pair.Key, pair => pair.Value?.ToString(), StringComparer.OrdinalIgnoreCase);
        var previewJson = JsonSerializer.Serialize(preview);

        var run = new InstallationRun(
            Guid.CreateVersion7(),
            command.InstallationId,
            InstallationRunKind.AwxJob,
            previewJson);
        await runs.AddAsync(run, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        AwxJobLaunchResult result;
        try
        {
            result = await awx
                .LaunchAsync(new AwxJobLaunch(command.Request.JobTemplateId, package), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ValidationException ex)
        {
            run.MarkFailed(ex.Message, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        run.MarkSubmitted(
            result.JobId.ToString(),
            result.Url,
            InstallationRunMapping.FromAwxStatus(result.Status),
            result.Message,
            clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ApplicationInstallationAwxLaunchResponse(
            run.Id,
            result.JobId,
            result.Status,
            result.Url,
            result.Message,
            preview);
    }
}
