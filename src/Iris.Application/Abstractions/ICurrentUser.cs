namespace Iris.Application.Abstractions;

/// <summary>
/// The identity behind the current request, projected from the authenticated
/// principal. Implemented in <c>Iris.Api</c> over <c>HttpContext.User</c>.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Stable identity-provider subject/object id (Entra ID <c>oid</c> or a dev id).</summary>
    string? ExternalId { get; }

    string? Email { get; }

    string? DisplayName { get; }
}
