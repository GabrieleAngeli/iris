using Iris.Application.Abstractions;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

public sealed record GetSystemSettingsQuery(
    bool CanManageSystem,
    string? OpenBaoEndpoint,
    string? AnsibleEndpoint,
    string? AzureDevOpsEndpoint,
    string? NexusEndpoint);

public sealed class GetSystemSettingsHandler(
    IMailProviderSettingsRepository mailSettings)
{
    public async Task<SystemSettingsResponse> HandleAsync(
        GetSystemSettingsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var integrations = new[]
        {
            Link("openbao", "OpenBao", query.OpenBaoEndpoint),
            Link("ansible", "Ansible / AWX", query.AnsibleEndpoint),
            Link("azure-devops", "Azure DevOps", query.AzureDevOpsEndpoint),
            Link("nexus", "Nexus Repository", query.NexusEndpoint),
        };

        if (!query.CanManageSystem)
        {
            return new SystemSettingsResponse(false, null, integrations);
        }

        var mail = await mailSettings.GetAsync(cancellationToken).ConfigureAwait(false);
        var response = mail is null
            ? new MailProviderSettingsResponse(false, null, null, null, null, null, false)
            : new MailProviderSettingsResponse(
                true,
                mail.SmtpHost,
                mail.SmtpPort,
                mail.SmtpUsername,
                mail.FromAddress,
                mail.FromDisplayName,
                mail.EnableSsl);

        return new SystemSettingsResponse(true, response, integrations);
    }

    private static IntegrationLinkResponse Link(string key, string name, string? endpoint) =>
        new(key, name, string.IsNullOrWhiteSpace(endpoint) ? "Not configured" : "Configured", endpoint);
}
