using Iris.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Iris.Infrastructure.Invitations;

/// <summary>
/// Builds the invitation accept link from <c>Iris:Invitations:AcceptUrlBase</c>
/// (default <c>https://localhost:5001/invitations/accept</c>), appending the raw token
/// as a <c>token</c> query-string parameter.
/// </summary>
internal sealed class ConfiguredInvitationLinkBuilder(IConfiguration configuration) : IInvitationLinkBuilder
{
    private const string DefaultBase = "https://localhost:5001/invitations/accept";

    public string BuildAcceptLink(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        var baseUrl = configuration["Iris:Invitations:AcceptUrlBase"] is { Length: > 0 } configured
            ? configured
            : DefaultBase;

        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(rawToken)}";
    }
}
