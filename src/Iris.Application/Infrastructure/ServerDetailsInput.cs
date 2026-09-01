using Iris.Application.Common;
using Iris.Domain.Infrastructure;
using Iris.Domain.Tenancy;

namespace Iris.Application.Infrastructure;

/// <summary>The mutable identity/network fields of a server, shared by create and update.</summary>
public sealed record ServerDetailsInput(
    string Name,
    string? Hostname,
    string Os,
    string HostingType,
    string? PublicIpAddress,
    string? PrivateIpAddress,
    string Environment)
{
    /// <summary>The parsed enum triple for this input.</summary>
    public (ServerOs Os, ServerHostingType HostingType, ContextKind Environment) Parse()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ValidationException("Server name is required.");
        }

        if (!Enum.TryParse<ServerOs>(Os, ignoreCase: true, out var os))
        {
            throw new ValidationException($"Unknown OS '{Os}'. Expected Linux or Windows.");
        }

        if (!Enum.TryParse<ServerHostingType>(HostingType, ignoreCase: true, out var hostingType))
        {
            throw new ValidationException($"Unknown hosting type '{HostingType}'. Expected SelfHosted or Cloud.");
        }

        if (!Enum.TryParse<ContextKind>(Environment, ignoreCase: true, out var environment))
        {
            throw new ValidationException($"Unknown environment '{Environment}'. Expected Test, Staging or Production.");
        }

        if (string.IsNullOrWhiteSpace(PublicIpAddress) && string.IsNullOrWhiteSpace(PrivateIpAddress))
        {
            throw new ValidationException("A server needs at least a public or a private IP address.");
        }

        return (os, hostingType, environment);
    }
}
