namespace Iris.Contracts.Meta;

/// <summary>Basic identity of the running Iris API instance, returned by <c>GET /</c>.</summary>
/// <param name="Name">Product name.</param>
/// <param name="Version">Informational assembly version.</param>
/// <param name="Environment">Hosting environment name (Development, Staging, Production).</param>
public sealed record ServiceInfoResponse(string Name, string Version, string Environment);
