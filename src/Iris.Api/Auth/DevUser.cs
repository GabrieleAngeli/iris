namespace Iris.Api.Auth;

/// <summary>A local development identity, configured under <c>Iris:Auth:DevUsers</c>.</summary>
public sealed class DevUser
{
    public string Email { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Value surfaced as the identity-provider object id (matches seeded users' <c>ExternalId</c>).</summary>
    public string ObjectId { get; set; } = string.Empty;
}
