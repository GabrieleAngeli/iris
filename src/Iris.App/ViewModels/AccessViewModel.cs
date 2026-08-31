using System.Collections.ObjectModel;
using Iris.Contracts.Tenancy;

namespace Iris.App.ViewModels;

public partial class AccessViewModel : ObservableObject
{
	private readonly IAuthService _auth;
	private readonly IIrisApiClient _api;

	public AccessViewModel(IAuthService auth, IIrisApiClient api)
	{
		_auth = auth;
		_api = api;
	}

	[ObservableProperty] private string _identity = "Not signed in";
	[ObservableProperty] private string _scope = string.Empty;
	[ObservableProperty] private bool _isLoading;
	[ObservableProperty] private string? _error;

	public ObservableCollection<string> Permissions { get; } = [];
	public ObservableCollection<CustomerSummaryResponse> Customers { get; } = [];

	[RelayCommand]
	private async Task LoadAsync()
	{
		IsLoading = true;
		Error = null;

		try
		{
			var me = _auth.Me;
			if (me is not null)
			{
				Identity = $"{me.DisplayName} · {me.Email}";
				Scope = $"scope: {me.EvaluatedScope}";

				Permissions.Clear();
				foreach (var permission in me.EffectivePermissions)
				{
					Permissions.Add(permission);
				}
			}

			Customers.Clear();
			foreach (var customer in await _api.GetCustomersAsync())
			{
				Customers.Add(customer);
			}
		}
		catch (Exception ex)
		{
			Error = ex.Message;
		}
		finally
		{
			IsLoading = false;
		}
	}
}
