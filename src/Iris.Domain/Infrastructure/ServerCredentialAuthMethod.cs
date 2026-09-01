namespace Iris.Domain.Infrastructure;

/// <summary>How a <see cref="ServerCredential"/> authenticates against the server.</summary>
public enum ServerCredentialAuthMethod
{
    Password = 0,
    SshKey = 1,
}
