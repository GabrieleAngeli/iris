using System.Net;
using System.Net.Http.Json;
using Iris.Contracts.Access;
using Iris.Contracts.Tenancy;

namespace Iris.App.Services;

/// <summary>Where the Iris API lives and how this client authenticates against it.</summary>
public sealed class IrisApiOptions
{
	/// <summary>Base URL of the running Iris.Api. Defaults to the Kestrel HTTP profile.</summary>
	public string BaseUrl { get; set; } = "http://localhost:5006";
}

public interface IIrisApiClient
{
	/// <summary>Dev-mode identity sent as <c>X-Dev-User</c> on every request after sign-in.</summary>
	string? DevUserEmail { get; set; }

	Task<MeResponse?> GetMeAsync(string devUserEmail, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<CustomerSummaryResponse>> GetCustomersAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<string>> GetPermissionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Thin typed client over the Iris AAA endpoints. One long-lived <see cref="HttpClient"/>
/// is shared for the life of the app (single target host).
/// </summary>
public sealed class IrisApiClient(HttpClient http) : IIrisApiClient
{
	public string? DevUserEmail { get; set; }

	public async Task<MeResponse?> GetMeAsync(string devUserEmail, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, "/me");
		request.Headers.Add("X-Dev-User", devUserEmail);

		using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
		if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
		{
			return null;
		}

		response.EnsureSuccessStatusCode();
		return await response.Content
			.ReadFromJsonAsync<MeResponse>(cancellationToken)
			.ConfigureAwait(false);
	}

	public Task<IReadOnlyList<CustomerSummaryResponse>> GetCustomersAsync(CancellationToken cancellationToken = default) =>
		GetListAsync<CustomerSummaryResponse>("/customers", cancellationToken);

	public Task<IReadOnlyList<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken = default) =>
		GetListAsync<RoleResponse>("/governance/roles", cancellationToken);

	public Task<IReadOnlyList<string>> GetPermissionsAsync(CancellationToken cancellationToken = default) =>
		GetListAsync<string>("/governance/permissions", cancellationToken);

	private async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, path);
		if (!string.IsNullOrWhiteSpace(DevUserEmail))
		{
			request.Headers.Add("X-Dev-User", DevUserEmail);
		}

		using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();

		return await response.Content
			.ReadFromJsonAsync<List<T>>(cancellationToken)
			.ConfigureAwait(false) ?? [];
	}
}
