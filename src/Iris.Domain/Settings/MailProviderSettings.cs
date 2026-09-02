using Iris.Domain.Common;

namespace Iris.Domain.Settings;

/// <summary>
/// The SMTP relay Iris sends email through — configured once, in the first-run setup
/// wizard. Single-row: every instance is keyed under <see cref="SingletonId"/>, there is
/// never more than one. <see cref="SmtpPasswordSecretReference"/> is an opaque reference —
/// the real value lives in <c>ISecretStore</c>, never here.
/// </summary>
public sealed class MailProviderSettings : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    /// <summary>The one and only row this table ever holds.</summary>
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    // For the persistence layer.
    private MailProviderSettings()
        : base(Guid.Empty)
    {
        SmtpHost = string.Empty;
        FromAddress = string.Empty;
    }

    private MailProviderSettings(
        string smtpHost,
        int smtpPort,
        string? smtpUsername,
        string? smtpPasswordSecretReference,
        string fromAddress,
        string? fromDisplayName,
        bool enableSsl)
        : base(SingletonId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(smtpHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromAddress);

        SmtpHost = smtpHost.Trim();
        SmtpPort = smtpPort;
        SmtpUsername = string.IsNullOrWhiteSpace(smtpUsername) ? null : smtpUsername.Trim();
        SmtpPasswordSecretReference = smtpPasswordSecretReference;
        FromAddress = fromAddress.Trim();
        FromDisplayName = string.IsNullOrWhiteSpace(fromDisplayName) ? null : fromDisplayName.Trim();
        EnableSsl = enableSsl;
    }

    public string SmtpHost { get; private set; }

    public int SmtpPort { get; private set; }

    public string? SmtpUsername { get; private set; }

    /// <summary>Opaque reference into <c>ISecretStore</c> — never the password itself.</summary>
    public string? SmtpPasswordSecretReference { get; private set; }

    public string FromAddress { get; private set; }

    public string? FromDisplayName { get; private set; }

    public bool EnableSsl { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public static MailProviderSettings Configure(
        string smtpHost,
        int smtpPort,
        string? smtpUsername,
        string? smtpPasswordSecretReference,
        string fromAddress,
        string? fromDisplayName,
        bool enableSsl) =>
        new(smtpHost, smtpPort, smtpUsername, smtpPasswordSecretReference, fromAddress, fromDisplayName, enableSsl);
}
