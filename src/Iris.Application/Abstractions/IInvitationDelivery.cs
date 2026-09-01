namespace Iris.Application.Abstractions;

/// <summary>Turns a raw invitation token into the link the recipient opens.</summary>
public interface IInvitationLinkBuilder
{
    string BuildAcceptLink(string rawToken);
}

/// <summary>
/// Delivers an invitation to its recipient. The first-increment implementation just writes the
/// link to the log; swap for an SMTP or Microsoft Graph B2B implementation later — callers only
/// see this port.
/// </summary>
public interface IInvitationNotifier
{
    Task SendAsync(InvitationNotification notification, CancellationToken cancellationToken = default);
}

/// <summary>Everything a notifier needs to deliver an invitation.</summary>
public sealed record InvitationNotification(
    string Email,
    string DisplayName,
    string AcceptLink,
    DateTimeOffset ExpiresAtUtc);
