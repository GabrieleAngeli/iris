namespace Iris.Domain.Infrastructure;

/// <summary>What a <see cref="ServerCredential"/> represents on the server.</summary>
public enum ServerCredentialKind
{
    /// <summary>An OS login used by a named person — optionally linked to an Iris <c>User</c>.</summary>
    SystemUser = 0,

    /// <summary>A shared automation account (e.g. <c>ansible</c>, <c>ci-deploy</c>), identified by a service name.</summary>
    ServiceAccount = 1,
}
