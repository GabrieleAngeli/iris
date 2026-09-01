using System.Collections.ObjectModel;
using Iris.Contracts.Governance;
using Iris.Contracts.Tenancy;

namespace Iris.App.ViewModels;

/// <summary>Governance › Customers: every customer, its environments/contexts, with create + add-context.</summary>
public partial class CustomersViewModel : ObservableObject
{
	private const string ManagePermission = "governance.customers.manage";

	private readonly IIrisApiClient _api;
	private readonly IAuthService _auth;

	public CustomersViewModel(IIrisApiClient api, IAuthService auth)
	{
		_api = api;
		_auth = auth;
	}

	public ObservableCollection<CustomerRowViewModel> Customers { get; } = [];

	[ObservableProperty] private bool _isLoading;
	[ObservableProperty] private string? _error;

	public bool HasError => !string.IsNullOrEmpty(Error);

	/// <summary>True when the signed-in user may create customers and add contexts (Global <c>governance.customers.manage</c>).</summary>
	public bool CanManageCustomers => _auth.Me?.EffectivePermissions.Contains(ManagePermission) == true;

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	private bool _loaded;

	[RelayCommand]
	private async Task LoadAsync()
	{
		if (_loaded)
		{
			return;
		}

		await RefreshAsync();
		_loaded = true;
	}

	[RelayCommand]
	private async Task RefreshAsync()
	{
		IsLoading = true;
		Error = null;

		try
		{
			var customers = await _api.GetCustomersAsync();
			Customers.Clear();
			foreach (var customer in customers)
			{
				Customers.Add(new CustomerRowViewModel(customer, _api, this));
			}
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			Error = ex.Message;
		}
		finally
		{
			IsLoading = false;
		}
	}

	// ----- New customer -----

	/// <summary>Raised when the operator asks to add a customer — the page opens the dialog window.</summary>
	public event EventHandler? NewCustomerRequested;

	/// <summary>Raised after a customer is created so its dialog window can close.</summary>
	public event EventHandler? NewCustomerCompleted;

	/// <summary>Raised (post-create, or from a row's button) to open the add-context dialog for a customer.</summary>
	public event EventHandler<CustomerRowViewModel>? AddContextRequested;

	[ObservableProperty] private string _newCustomerKey = string.Empty;
	[ObservableProperty] private string _newCustomerName = string.Empty;
	[ObservableProperty] private bool _isCreatingCustomer;
	[ObservableProperty] private string? _createCustomerError;

	public bool HasCreateCustomerError => !string.IsNullOrEmpty(CreateCustomerError);

	partial void OnCreateCustomerErrorChanged(string? value) => OnPropertyChanged(nameof(HasCreateCustomerError));

	[RelayCommand]
	private void RequestNewCustomer()
	{
		NewCustomerKey = string.Empty;
		NewCustomerName = string.Empty;
		CreateCustomerError = null;
		NewCustomerRequested?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Opens the add-context dialog for <paramref name="row"/> (from its button or right after a bare create).</summary>
	public void OpenAddContextPanel(CustomerRowViewModel row)
	{
		row.NewContextName = string.Empty;
		row.NewContextKind = "Test";
		row.ContextError = null;
		AddContextRequested?.Invoke(this, row);
	}

	[RelayCommand]
	private async Task CreateCustomerAsync()
	{
		var key = NewCustomerKey.Trim();
		var name = NewCustomerName.Trim();

		if (key.Length == 0 || name.Length == 0)
		{
			CreateCustomerError = "Enter both a key and a display name.";
			return;
		}

		IsCreatingCustomer = true;
		CreateCustomerError = null;

		try
		{
			var created = await _api.CreateCustomerAsync(new CreateCustomerRequest(key, name));

			var row = new CustomerRowViewModel(created, _api, this);
			Customers.Insert(0, row);

			NewCustomerCompleted?.Invoke(this, EventArgs.Empty);

			// A customer with no environment isn't useful — chain into the add-context dialog on the
			// next UI tick so the new-customer window finishes closing before the next one opens.
			var created_row = row;
			MainThread.BeginInvokeOnMainThread(() => OpenAddContextPanel(created_row));
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			CreateCustomerError = ex.Message;
		}
		finally
		{
			IsCreatingCustomer = false;
		}
	}
}

/// <summary>One customer row: its identity plus the nested list of contexts and the inline "add context" form.</summary>
public sealed partial class CustomerRowViewModel : ObservableObject
{
	private readonly Guid _customerId;
	private readonly IIrisApiClient _api;
	private readonly CustomersViewModel _parent;

	public CustomerRowViewModel(CustomerSummaryResponse customer, IIrisApiClient api, CustomersViewModel parent)
	{
		_customerId = customer.Id;
		_api = api;
		_parent = parent;
		Contexts = new ObservableCollection<ContextRowViewModel>(
			customer.Contexts.Select(c => new ContextRowViewModel(c)));
		ApplyFrom(customer);
	}

	[ObservableProperty] private string _name = string.Empty;
	[ObservableProperty] private string _key = string.Empty;
	[ObservableProperty] private bool _isActive;

	public ObservableCollection<ContextRowViewModel> Contexts { get; }

	public bool HasNoContexts => Contexts.Count == 0;

	public bool CanManageCustomers => _parent.CanManageCustomers;

	public IReadOnlyList<string> ContextKinds { get; } = ["Test", "Staging", "Production"];

	private void ApplyFrom(CustomerSummaryResponse customer)
	{
		Name = customer.Name;
		Key = customer.Key;
		IsActive = customer.IsActive;
	}

	// ----- Add context -----

	/// <summary>Raised after a context is added so its dialog window can close.</summary>
	public event EventHandler? AddContextCompleted;

	[ObservableProperty] private string _newContextName = string.Empty;
	[ObservableProperty] private string _newContextKind = "Test";
	[ObservableProperty] private bool _isAddingContext;
	[ObservableProperty] private string? _contextError;

	public bool HasContextError => !string.IsNullOrEmpty(ContextError);

	partial void OnContextErrorChanged(string? value) => OnPropertyChanged(nameof(HasContextError));

	[RelayCommand]
	private void OpenAddContext() => _parent.OpenAddContextPanel(this);

	[RelayCommand]
	private async Task AddContextAsync()
	{
		var name = NewContextName.Trim();
		if (name.Length == 0)
		{
			ContextError = "Context name is required.";
			return;
		}

		IsAddingContext = true;
		ContextError = null;

		try
		{
			var created = await _api.AddContextAsync(_customerId, new AddContextRequest(name, NewContextKind));
			Contexts.Add(new ContextRowViewModel(created));
			OnPropertyChanged(nameof(HasNoContexts));
			AddContextCompleted?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			ContextError = ex.Message;
		}
		finally
		{
			IsAddingContext = false;
		}
	}
}

/// <summary>One environment/context under a customer. A thin read-only wrapper around <see cref="ContextSummaryResponse"/>.</summary>
public sealed class ContextRowViewModel(ContextSummaryResponse context)
{
	public string Name => context.Name;

	public string Kind => context.Kind;

	public bool IsActive => context.IsActive;
}
