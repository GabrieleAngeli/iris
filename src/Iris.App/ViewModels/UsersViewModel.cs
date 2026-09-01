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

	/// <summary>Raised from the edit dialog's "Delete this user" button to open the confirm-delete window.</summary>
	public event EventHandler<UserRowViewModel>? DeleteUserRequested;

	/// <summary>Raised from the edit dialog's "Send invitation" button to open the invitation window.</summary>
	public event EventHandler<UserRowViewModel>? InviteUserRequested;

	/// <summary>Opens the invitation window from the edit dialog, on the next UI tick (edit dialog closes first).</summary>
	public void RaiseInviteRequested(UserRowViewModel row) =>
		MainThread.BeginInvokeOnMainThread(() => InviteUserRequested?.Invoke(this, row));

	public void RaiseEditRequested(UserRowViewModel row) => EditUserRequested?.Invoke(this, row);

	/// <summary>
	/// Opens the confirm-delete window for <paramref name="row"/>, on the next UI tick so the edit
	/// dialog it was launched from finishes closing first.
	/// </summary>
	public void RaiseDeleteRequested(UserRowViewModel row) =>
		MainThread.BeginInvokeOnMainThread(() => DeleteUserRequested?.Invoke(this, row));

	/// <summary>Opens the assign-role window from the edit dialog, on the next UI tick (edit dialog closes first).</summary>
	public void RaiseAssignFromEdit(UserRowViewModel row) =>
		MainThread.BeginInvokeOnMainThread(() => OpenAssignPanel(row));

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
public sealed partial class UserRowViewModel : ObservableObject, IConfirmDeletable
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

	/// <summary>Raised after the user is saved so its edit dialog window can close.</summary>
	public event EventHandler? EditCompleted;

	/// <summary>Raised after the user is deleted so the confirm-delete window can close.</summary>
	public event EventHandler? DeleteCompleted;

	/// <summary>Raised from the edit dialog to close it before the confirm-delete window opens.</summary>
	public event EventHandler? DeleteRequested;

	/// <summary>Raised from the edit dialog to close it before the assign-role window opens.</summary>
	public event EventHandler? AssignRequested;

	[ObservableProperty] private string _editDisplayName = string.Empty;
	[ObservableProperty] private string _editEmail = string.Empty;
	[ObservableProperty] private bool _editActive = true;
	[ObservableProperty] private string _deleteConfirmName = string.Empty;
	[ObservableProperty] private bool _isEditBusy;
	[ObservableProperty] private string? _editError;

	public bool HasEditError => !string.IsNullOrEmpty(EditError);

	string IConfirmDeletable.DeleteDialogTitle => "Delete user";

	string IConfirmDeletable.DeleteDialogPrompt =>
		"This permanently removes the user and every role assignment they hold. It cannot be undone.";

	string IConfirmDeletable.DeleteTargetName => DisplayName;

	/// <summary>Delete is armed only once the operator has typed the display name exactly.</summary>
	public bool CanDelete => string.Equals(DeleteConfirmName.Trim(), DisplayName, StringComparison.Ordinal);

	partial void OnEditErrorChanged(string? value) => OnPropertyChanged(nameof(HasEditError));

	partial void OnDisplayNameChanged(string value) => OnPropertyChanged(nameof(CanDelete));

	partial void OnDeleteConfirmNameChanged(string value)
	{
		OnPropertyChanged(nameof(CanDelete));
		DeleteCommand.NotifyCanExecuteChanged();
	}

	// ----- Concurrent-edit lock -----

	private const string LockResource = "user";
	private const int HeartbeatSeconds = 45;
	private CancellationTokenSource? _heartbeatCts;

	[ObservableProperty] private string? _editLockNotice;

	/// <summary>Set when someone else holds the edit lock — the row shows it and the editor stays shut.</summary>
	public bool HasEditLockNotice => !string.IsNullOrEmpty(EditLockNotice);

	partial void OnEditLockNoticeChanged(string? value) => OnPropertyChanged(nameof(HasEditLockNotice));

	[RelayCommand]
	private async Task OpenEditAsync()
	{
		EditLockNotice = null;
		EditError = null;

		try
		{
			var slot = await _api.AcquireEditLockAsync(LockResource, _userId);
			if (!slot.Mine)
			{
				EditLockNotice = $"{slot.HolderDisplayName} is editing this user right now — try again in a moment.";
				return;
			}
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			EditLockNotice = ex.Message;
			return;
		}

		StartHeartbeat();

		EditDisplayName = DisplayName;
		EditEmail = Email;
		EditActive = IsActive;
		DeleteConfirmName = string.Empty;
		_parent.RaiseEditRequested(this);
	}

	private void StartHeartbeat()
	{
		_heartbeatCts?.Cancel();
		var cts = new CancellationTokenSource();
		_heartbeatCts = cts;
		_ = HeartbeatAsync(cts.Token);
	}

	private async Task HeartbeatAsync(CancellationToken token)
	{
		try
		{
			using var timer = new PeriodicTimer(TimeSpan.FromSeconds(HeartbeatSeconds));
			while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
			{
				try
				{
					await _api.AcquireEditLockAsync(LockResource, _userId, token).ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
				{
					// A dropped heartbeat just lets the lock lapse sooner; nothing to surface.
				}
			}
		}
		catch (OperationCanceledException)
		{
			// editor closed
		}
	}

	/// <summary>Called by the edit dialog as it closes: stop the heartbeat and release the lock.</summary>
	public void NotifyEditorClosed()
	{
		if (_heartbeatCts is null)
		{
			return;
		}

		_heartbeatCts.Cancel();
		_heartbeatCts.Dispose();
		_heartbeatCts = null;
		_ = SafeReleaseLockAsync();
	}

	private async Task SafeReleaseLockAsync()
	{
		try
		{
			await _api.ReleaseEditLockAsync(LockResource, _userId).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			// The lock will expire on its own if the release didn't land.
		}
	}

	// ----- Invitation -----

	/// <summary>Raised from the edit dialog to close it before the invitation window opens.</summary>
	public event EventHandler? InviteRequested;

	[ObservableProperty] private bool _isInviteBusy;
	[ObservableProperty] private string? _inviteError;
	[ObservableProperty] private string? _invitationLink;
	[ObservableProperty] private DateTimeOffset? _invitationExpiresAt;

	public bool HasInviteError => !string.IsNullOrEmpty(InviteError);

	public bool HasInvitationLink => !string.IsNullOrEmpty(InvitationLink);

	partial void OnInviteErrorChanged(string? value) => OnPropertyChanged(nameof(HasInviteError));

	partial void OnInvitationLinkChanged(string? value) => OnPropertyChanged(nameof(HasInvitationLink));

	[RelayCommand]
	private void RequestInvite()
	{
		InviteError = null;
		InvitationLink = null;
		InvitationExpiresAt = null;
		InviteRequested?.Invoke(this, EventArgs.Empty);
		_parent.RaiseInviteRequested(this);
	}

	[RelayCommand]
	private async Task IssueInviteAsync()
	{
		IsInviteBusy = true;
		InviteError = null;

		try
		{
			var result = await _api.IssueUserInvitationAsync(_userId);
			InvitationLink = result.AcceptLink;
			InvitationExpiresAt = result.ExpiresAtUtc;
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			InviteError = ex.Message;
		}
		finally
		{
			IsInviteBusy = false;
		}
	}

	[RelayCommand]
	private async Task CopyInvitationLinkAsync()
	{
		if (!string.IsNullOrEmpty(InvitationLink))
		{
			await Clipboard.Default.SetTextAsync(InvitationLink);
		}
	}

	/// <summary>From the edit dialog: close it and hand off to the dedicated confirm-delete window.</summary>
	[RelayCommand]
	private void RequestDelete()
	{
		DeleteConfirmName = string.Empty;
		EditError = null;
		DeleteRequested?.Invoke(this, EventArgs.Empty);
		_parent.RaiseDeleteRequested(this);
	}

	/// <summary>From the edit dialog: close it and hand off to the assign-role window.</summary>
	[RelayCommand]
	private void RequestAssignFromEdit()
	{
		AssignRequested?.Invoke(this, EventArgs.Empty);
		_parent.RaiseAssignFromEdit(this);
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
			DeleteCompleted?.Invoke(this, EventArgs.Empty);
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
