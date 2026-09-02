namespace Iris.Contracts.Settings;

public sealed record MailProviderSettingsResponse(
    bool IsConfigured,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUsername,
    string? FromAddress,
    string? FromDisplayName,
    bool EnableSsl);

public sealed record IntegrationLinkResponse(
    string Key,
    string Name,
    string Status,
    string? Endpoint);

public sealed record SystemSettingsResponse(
    bool CanManageSystem,
    MailProviderSettingsResponse? Mail,
    IReadOnlyList<IntegrationLinkResponse> Integrations);
