using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Iris.Contracts.Access;
using Iris.Contracts.Governance;
using Iris.Contracts.Infrastructure;
using Iris.Contracts.Tenancy;

namespace Iris.App.Services;

/// <summary>Where the Iris API lives and how this client authenticates against it.</summary>
public sealed class IrisApiOptions
{
	/// <summary>Base URL of the running Iris.Api. Defaults to the Kestrel HTTP profile.</summary>
	public string BaseUrl { get; set; } = "http://localhost:5006";
}

/// <summary>An Iris API call failed; <see cref="Message"/> is the RFC 7807 problem detail when the API supplied one.</summary>
public sealed class IrisApiException(string message) : Exception(message);

public interface IIrisApiClient
{
	/// <summary>Dev-mode identity sent as <c>X-Dev-User</c> on every request after sign-in.</summary>
	string? DevUserEmail { get; set; }

	/// <summary>Entra ID access token sent as <c>Authorization: Bearer</c> after single sign-on. Takes precedence over <see cref="DevUserEmail"/> when set.</summary>
	string? BearerToken { get; set; }

	Task<MeResponse?> GetMeAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<CustomerSummaryResponse>> GetCustomersAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<string>> GetPermissionsAsync(CancellationToken cancellationToken = default);

	/// <summary>Every user and the roles they hold. Requires <c>governance.read</c> at Global scope.</summary>
	Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken cancellationToken = default);

	/// <summary>Pre-provisions a user ahead of their first sign-in. Requires <c>governance.assignments.manage</c>.</summary>
	Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

	/// <summary>Grants <paramref name="userId"/> a role at a scope. Requires <c>governance.assignments.manage</c>.</summary>
	Task<AssignmentResponse> AssignRoleAsync(Guid userId, AssignRoleRequest request, CancellationToken cancellationToken = default);

	/// <summary>Revokes a role assignment. Requires <c>governance.assignments.manage</c>.</summary>
	Task RevokeRoleAsync(Guid userId, Guid assignmentId, CancellationToken cancellationToken = default);

	/// <summary>Every registered server and the credentials it holds. Requires <c>infrastructure.read</c> at Global scope.</summary>
	Task<IReadOnlyList<ServerResponse>> GetServersAsync(CancellationToken cancellationToken = default);

	/// <summary>Registers a server. Requires <c>infrastructure.write</c>.</summary>
	Task<ServerResponse> CreateServerAsync(CreateServerRequest request, CancellationToken cancellationToken = default);

	/// <summary>Adds an OS-login credential to a server. Requires <c>infrastructure.write</c>.</summary>
	Task<ServerCredentialResponse> AddServerCredentialAsync(Guid serverId, AddServerCredentialRequest request, CancellationToken cancellationToken = default);

	/// <summary>Removes a credential from a server. Requires <c>infrastructure.delete</c>.</summary>
	Task RemoveServerCredentialAsync(Guid serverId, Guid credentialId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thin typed client over the Iris AAA endpoints. One long-lived <see cref="HttpClient"/>
/// is shared for the life of the app (single target host).
/// </summary>
public sealed class IrisApiClient(HttpClient http) : IIrisApiClient
{
	public string? DevUserEmail { get; set; }

	public string? BearerToken { get; set; }

	public async Task<MeResponse?> GetMeAsync(CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, "/me");
		Authenticate(request);

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

	public Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken cancellationToken = default) =>
		GetListAsync<UserResponse>("/governance/users", cancellationToken);

	public Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default) =>
		PostAsync<UserResponse>("/governance/users", request, cancellationToken);

	public Task<AssignmentResponse> AssignRoleAsync(Guid userId, AssignRoleRequest request, CancellationToken cancellationToken = default) =>
		PostAsync<AssignmentResponse>($"/governance/users/{userId}/assignments", request, cancellationToken);

	public Task RevokeRoleAsync(Guid userId, Guid assignmentId, CancellationToken cancellationToken = default) =>
		DeleteAsync($"/governance/users/{userId}/assignments/{assignmentId}", cancellationToken);

	public Task<IReadOnlyList<ServerResponse>> GetServersAsync(CancellationToken cancellationToken = default) =>
		GetListAsync<ServerResponse>("/servers", cancellationToken);

	public Task<ServerResponse> CreateServerAsync(CreateServerRequest request, CancellationToken cancellationToken = default) =>
		PostAsync<ServerResponse>("/servers", request, cancellationToken);

	public Task<ServerCredentialResponse> AddServerCredentialAsync(Guid serverId, AddServerCredentialRequest request, CancellationToken cancellationToken = default) =>
		PostAsync<ServerCredentialResponse>($"/servers/{serverId}/credentials", request, cancellationToken);

	public Task RemoveServerCredentialAsync(Guid serverId, Guid credentialId, CancellationToken cancellationToken = default) =>
		DeleteAsync($"/servers/{serverId}/credentials/{credentialId}", cancellationToken);

	private async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, path);
		Authenticate(request);

		using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
		await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

		return await response.Content
			.ReadFromJsonAsync<List<T>>(cancellationToken)
			.ConfigureAwait(false) ?? [];
	}

	private async Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
		Authenticate(request);

		using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
		await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

		return (await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false))!;
	}

	private async Task DeleteAsync(string path, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Delete, path);
		Authenticate(request);

		using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
		await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Attaches whichever credential is currently set: a bearer token from Entra ID sign-in, or the dev header.</summary>
	private void Authenticate(HttpRequestMessage request)
	{
		if (!string.IsNullOrWhiteSpace(BearerToken))
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
		}
		else if (!string.IsNullOrWhiteSpace(DevUserEmail))
		{
			request.Headers.Add("X-Dev-User", DevUserEmail);
		}
	}

	/// <summary>Throws <see cref="IrisApiException"/> with the API's RFC 7807 problem detail, when the response carries one.</summary>
	private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		if (response.IsSuccessStatusCode)
		{
			return;
		}

		string? detail = null;
		try
		{
			var problem = await response.Content
				.ReadFromJsonAsync<ProblemDetailsDto>(cancellationToken)
				.ConfigureAwait(false);
			detail = problem?.Detail ?? problem?.Title;
		}
		catch (Exception ex) when (ex is JsonException or NotSupportedException)
		{
			// Not a JSON problem body — fall back to the status code below.
		}

		throw new IrisApiException(detail ?? $"Iris API returned {(int)response.StatusCode} {response.ReasonPhrase}.");
	}

	private sealed record ProblemDetailsDto(string? Title, string? Detail);
}
