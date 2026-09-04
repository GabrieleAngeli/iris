using Iris.Application.Abstractions;
using Iris.Contracts.Applications;

namespace Iris.Application.Applications;

public sealed record LaunchApplicationInstallationAwxJobCommand(
    Guid InstallationId,
    ApplicationInstallationAwxLaunchRequest Request);

public sealed class LaunchApplicationInstallationAwxJobHandler(
    GetApplicationInstallationAnsiblePlanHandler plans,
    IAnsibleExecutionPackageBuilder ansible,
    IAwxClient awx)
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
        var result = await awx
            .LaunchAsync(new AwxJobLaunch(command.Request.JobTemplateId, package), cancellationToken)
            .ConfigureAwait(false);

        var preview = package.ExtraVars
            .Where(pair => pair.Value is null or string or bool or int or long)
            .ToDictionary(pair => pair.Key, pair => pair.Value?.ToString(), StringComparer.OrdinalIgnoreCase);

        return new ApplicationInstallationAwxLaunchResponse(
            result.JobId,
            result.Status,
            result.Url,
            result.Message,
            preview);
    }
}
