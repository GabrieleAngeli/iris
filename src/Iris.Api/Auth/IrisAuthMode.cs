namespace Iris.Api.Auth;

/// <summary>Which authentication schemes the API accepts, from <c>Iris:Auth:Mode</c>.</summary>
public enum IrisAuthMode
{
    /// <summary>Header-based local development identities only.</summary>
    Dev = 0,

    /// <summary>Microsoft Entra ID bearer tokens only.</summary>
    EntraId = 1,

    /// <summary>Both: the dev header wins when present, otherwise a bearer token is expected.</summary>
    Both = 2,
}
