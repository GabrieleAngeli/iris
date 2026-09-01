using System.Collections.ObjectModel;
using Iris.Contracts.Access;
using Iris.Contracts.Governance;
using Iris.Contracts.Tenancy;

namespace Iris.App.ViewModels;

/// <summary>Governance › Users: every user, the roles they hold, and where — with assign/revoke.</summary>
public partial class UsersViewModel : ObservableObject
{
	private readonly IIrisApiClient _api;

	public UsersViewModel(IIrisApiClient api)
	{
		_api = api;
	}

	public ObservableCollection<UserRowViewModel> Users { get; } = [];

	[ObservableProperty] private bool _isLoading;
	[ObservableProperty] private string? _error;

	public bool HasError => !string.IsNullOrEmpty(Error);

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	private bool _loaded;
	private IReadOnlyList<RoleResponse> _roles = [];
	private IReadOnlyList<CustomerSummaryResponse> _customers = [];

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
			var users = await _api.GetUsersAsync();
			_roles = await _api.GetRolesAsync();
			_customers = await _api.GetCustomersAsync();

			Users.Clear();
			foreach (var user in users)
			{
				Users.Add(new UserRowViewModel(user, _roles, _customers, _api, this));
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

	// ----- New user -----

	/// <summary>Raised when the operator asks to add a user — the page opens the dialog window.</summary>
	public event EventHandler? NewUserRequested;

	/// <summary>Raised after a user is created so its dialog window can close.</summary>
	public event EventHandler? NewUserCompleted;

	/// <summary>Raised (post-create, or from a row's button) to open the assign-role dialog for a user.</summary>
	public event EventHandler<UserRowViewModel>? AssignRoleRequested;

	/// <summary>Raised from a row's edit button to open the edit/delete dialog for a user.</summary>
	public event EventHandler<UserRowViewModel>? EditUserRequested;

	public void RaiseEditRequested(UserRowViewModel row) => EditUserRequested?.Invoke(this, row);

	public void RemoveRow(UserRowViewModel row) => Users.Remove(row);

	[ObservableProperty] private string _newUserEmail = string.Empty;
	[ObservableProperty] private string _newUserDisplayName = string.Empty;
	[ObservableProperty] private bool _isCreatingUser;
	[ObservableProperty] private string? _createUserError;

	public bool HasCreateUserError => !string.IsNullOrEmpty(CreateUserError);

	partial void OnCreateUserErrorChanged(string? value) => OnPropertyChanged(nameof(HasCreateUserError));

	[RelayCommand]
	private void RequestNewUser()
	{
		NewUserEmail = string.Empty;
		NewUserDisplayName = string.Empty;
		CreateUserError = null;
		NewUserRequested?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Opens the assign-role dialog for <paramref name="row"/> (from its button or right after creating a user).</summary>
	public void OpenAssignPanel(UserRowViewModel row)
	{
		row.AssignError = null;
		AssignRoleRequested?.Invoke(this, row);
	}

	[RelayCommand]
	private async Task CreateUserAsync()
	{
		var email = NewUserEmail.Trim();
		var displayName = NewUserDisplayName.Trim();

		if (email.Length == 0 || displayName.Length == 0)
		{
			CreateUserError = "Enter both an email and a display name.";
			return;
		}

		IsCreatingUser = true;
		CreateUserError = null;

		try
		{
			var created = await _api.CreateUserAsync(new CreateUserRequest(email, displayName));

			var row = new UserRowViewModel(created, _roles, _customers, _api, this);
			Users.Insert(0, row);

			NewUserCompleted?.Invoke(this, EventArgs.Empty);

			// A user without a role can't do anything — chain into the assign-role dialog, but on the
			// next UI tick so the new-user window finishes closing before the next one opens.
			var created_row = row;
			MainThread.BeginInvokeOnMainThread(() => OpenAssignPanel(created_row));
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			CreateUserError = ex.Message;
		}
		finally
		{
			IsCreatingUser = false;
		}
	}
}

/// <summary>One user row: its current role assignments, plus the inline "assign a role" form.</summary>
public sealed partial class UserRowViewModel : ObservableObject
{
	private readonly Guid _userId;
	private readonly IIrisApiClient _api;
	private readonly UsersViewModel _parent;

	public UserRowViewModel(
		UserResponse user,
		IReadOnlyList<RoleResponse> roles,
		IReadOnlyList<CustomerSummaryResponse> customers,
		IIrisApiClient api,
		UsersViewModel parent)
	{
		_userId = user.Id;
		_api = api;
		_parent = parent;
		IsProvisioned = user.IsProvisioned;
		Roles = roles;
		Customers = customers;
		// this is only valid once the object exists, so this can't be a field initializer.
		Assignments = new ObservableCollection<AssignmentRowViewModel>(
			user.Assignments.Select(a => new AssignmentRowViewModel(a, this)));
		ApplyFrom(user);
	}

	[ObservableProperty] private string _displayName = string.Empty;
	[ObservableProperty] private string _email = string.Empty;
	[ObservableProperty] private bool _isActive;

	/// <summary>False for a user an admin created ahead of their first sign-in.</summary>
	public bool IsProvisioned { get; }

	private void ApplyFrom(UserResponse user)
	{
		DisplayName = user.DisplayName;
		Email = user.Email;
		IsActive = user.IsActive;
	}

	public ObservableCollection<AssignmentRowViewModel> Assignments { get; }

	public IReadOnlyList<RoleResponse> Roles { get; }

	public IReadOnlyList<CustomerSummaryResponse> Customers { get; }

	public IReadOnlyList<string> ScopeTypes { get; } = ["Global", "Customer", "Context"];

	[ObservableProperty] private RoleResponse? _selectedRole;
	[ObservableProperty] private string _selectedScopeType = "Global";
	[ObservableProperty] private CustomerSummaryResponse? _selectedCustomer;
	[ObservableProperty] private ContextSummaryResponse? _selectedContext;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _assignError;

	/// <summary>Raised after a role is assigned so its dialog window can close.</summary>
	public event EventHandler? AssignCompleted;

	public bool HasAssignError => !string.IsNullOrEmpty(AssignError);

	partial void OnAssignErrorChanged(string? value) => OnPropertyChanged(nameof(HasAssignError));

	public bool NeedsCustomer => SelectedScopeType is "Customer" or "Context";

	public bool NeedsContext => SelectedScopeType is "Context";

	public IReadOnlyList<ContextSummaryResponse> AvailableContexts => SelectedCustomer?.Contexts ?? [];

	partial void OnSelectedScopeTypeChanged(string value)
	{
		OnPropertyChanged(nameof(NeedsCustomer));
		OnPropertyChanged(nameof(NeedsContext));
	}

	partial void OnSelectedCustomerChanged(CustomerSummaryResponse? value)
	{
		SelectedContext = null;
		OnPropertyChanged(nameof(AvailableContexts));
	}

	[RelayCommand]
	private void OpenAssign() => _parent.OpenAssignPanel(this);

	// ----- Edit / delete -----

	/// <summary>Raised after the user is saved or deleted so its edit dialog window can close.</summary>
	public event EventHandler? EditCompleted;

	[ObservableProperty] private string _editDisplayName = string.Empty;
	[ObservableProperty] private string _editEmail = string.Empty;
	[ObservableProperty] private bool _editActive = true;
	[ObservableProperty] private string _deleteConfirmName = string.Empty;
	[ObservableProperty] private bool _isEditBusy;
	[ObservableProperty] private string? _editError;

	public bool HasEditError => !string.IsNullOrEmpty(EditError);

	/// <summary>Delete is armed only once the operator has typed the display name exactly.</summary>
	public bool CanDelete => string.Equals(DeleteConfirmName.Trim(), DisplayName, StringComparison.Ordinal);

	partial void OnEditErrorChanged(string? value) => OnPropertyChanged(nameof(HasEditError));

	partial void OnDisplayNameChanged(string value) => OnPropertyChanged(nameof(CanDelete));

	partial void OnDeleteConfirmNameChanged(string value)
	{
		OnPropertyChanged(nameof(CanDelete));
		DeleteCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand]
	private void OpenEdit()
	{
		EditDisplayName = DisplayName;
		EditEmail = Email;
		EditActive = IsActive;
		DeleteConfirmName = string.Empty;
		EditError = null;
		_parent.RaiseEditRequested(this);
	}

	[RelayCommand]
	private async Task SaveEditAsync()
	{
		var displayName = EditDisplayName.Trim();
		var email = EditEmail.Trim();
		if (displayName.Length == 0 || email.Length == 0)
		{
			EditError = "Enter both an email and a display name.";
			return;
		}

		IsEditBusy = true;
		EditError = null;

		try
		{
			var updated = await _api.UpdateUserAsync(_userId, new UpdateUserRequest(email, displayName, EditActive));
			ApplyFrom(updated);
			EditCompleted?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			EditError = ex.Message;
		}
		finally
		{
			IsEditBusy = false;
		}
	}

	[RelayCommand(CanExecute = nameof(CanDelete))]
	private async Task DeleteAsync()
	{
		IsEditBusy = true;
		EditError = null;

		try
		{
			await _api.DeleteUserAsync(_userId);
			_parent.RemoveRow(this);
			EditCompleted?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			EditError = ex.Message;
		}
		finally
		{
			IsEditBusy = false;
		}
	}

	[RelayCommand]
	private async Task AssignAsync()
	{
		if (SelectedRole is null)
		{
			AssignError = "Choose a role.";
			return;
		}

		if (NeedsCustomer && SelectedCustomer is null)
		{
			AssignError = "Choose a customer.";
			return;
		}

		if (NeedsContext && SelectedContext is null)
		{
			AssignError = "Choose a context.";
			return;
		}

		IsBusy = true;
		AssignError = null;

		try
		{
			var role = SelectedRole;
			var request = new AssignRoleRequest(
				role.Key,
				SelectedScopeType,
				NeedsCustomer ? SelectedCustomer!.Id : null,
				NeedsContext ? SelectedContext!.Id : null);

			var result = await _api.AssignRoleAsync(_userId, request);

			var dto = new UserAssignmentDto(
				result.Id,
				role.Key,
				role.Name,
				result.ScopeType,
				result.CustomerId,
				result.ContextId);
			Assignments.Add(new AssignmentRowViewModel(dto, this));

			AssignCompleted?.Invoke(this, EventArgs.Empty);
			SelectedRole = null;
			SelectedScopeType = "Global";
			SelectedCustomer = null;
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			AssignError = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private async Task RevokeAsync(AssignmentRowViewModel? assignment)
	{
		if (assignment is null)
		{
			return;
		}

		IsBusy = true;
		AssignError = null;

		try
		{
			await _api.RevokeRoleAsync(_userId, assignment.AssignmentId);
			Assignments.Remove(assignment);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			AssignError = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}
}

/// <summary>
/// One role assignment plus a human-readable scope description. A thin UI wrapper around
/// <see cref="UserAssignmentDto"/> that also carries a back-reference to its owning
/// <see cref="UserRowViewModel"/> — the row's <c>RevokeCommand</c> is what the "Revoke"
/// button in the nested assignments list needs to bind to (<c>{Binding Owner.RevokeCommand}</c>),
/// and it resolves customer/context names the API only gives as ids using the row's customer list.
/// </summary>
public sealed class AssignmentRowViewModel(UserAssignmentDto dto, UserRowViewModel owner)
{
	public Guid AssignmentId => dto.AssignmentId;

	public string RoleName => dto.RoleName;

	public UserRowViewModel Owner => owner;

	public string ScopeDescription
	{
		get
		{
			if (dto.ScopeType == "Global" || dto.CustomerId is null)
			{
				return "Global";
			}

			var customer = owner.Customers.FirstOrDefault(c => c.Id == dto.CustomerId);
			var customerName = customer?.Name ?? "(unknown customer)";

			if (dto.ScopeType == "Customer" || dto.ContextId is null)
			{
				return customerName;
			}

			var context = customer?.Contexts.FirstOrDefault(c => c.Id == dto.ContextId);
			return $"{customerName} · {context?.Name ?? "(unknown context)"}";
		}
	}
}
