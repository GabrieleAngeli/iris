using CommunityToolkit.Mvvm.Input;

namespace Iris.App.ViewModels;

/// <summary>
/// A list row (a user, a server) that can only be deleted once the operator retypes its
/// name. Backs the shared <c>ConfirmDeleteDialog</c> window, which is opened from the row's
/// edit dialog and replaces it.
/// </summary>
public interface IConfirmDeletable
{
	/// <summary>Window heading, e.g. "Delete user".</summary>
	string DeleteDialogTitle { get; }

	/// <summary>One sentence describing what the deletion removes.</summary>
	string DeleteDialogPrompt { get; }

	/// <summary>The exact text the operator must retype to arm the Delete button.</summary>
	string DeleteTargetName { get; }

	string DeleteConfirmName { get; set; }

	bool CanDelete { get; }

	bool IsEditBusy { get; }

	string? EditError { get; }

	bool HasEditError { get; }

	IAsyncRelayCommand DeleteCommand { get; }

	/// <summary>Raised once the row has been deleted so the confirmation window can close.</summary>
	event EventHandler? DeleteCompleted;
}
