using Iris.App.ViewModels;

namespace Iris.App.Views.Dialogs;

public partial class NewApplicationInstallationDialog : ContentPage
{
	private readonly ApplicationRowViewModel _row;
	private bool _closing;

	public NewApplicationInstallationDialog(ApplicationRowViewModel row)
	{
		InitializeComponent();
		BindingContext = _row = row;
		_row.ApplicationInstallationCompleted += OnApplicationInstallationCompleted;
	}

	private void OnCancel(object? sender, EventArgs e) => Close();

	private void OnApplicationInstallationCompleted(object? sender, EventArgs e) => Close();

	protected override void OnDisappearing()
	{
		_row.ApplicationInstallationCompleted -= OnApplicationInstallationCompleted;
		base.OnDisappearing();
	}

	// This dialog is opened as an owned Window via IDialogService (Application.Current.OpenWindow),
	// never pushed onto a Navigation stack — Navigation.PopModalAsync() here was a no-op stack-less
	// pop that threw/errored instead of closing anything (matches every other IDialogService dialog
	// in this app: ConfirmDeleteDialog, InstallationOpsDialog, SelectApplicationForDeploymentDialog).
	private void Close()
	{
		if (_closing)
		{
			return;
		}

		_closing = true;
		_row.ApplicationInstallationCompleted -= OnApplicationInstallationCompleted;

		try
		{
			if (Window is { } window)
			{
				Application.Current?.CloseWindow(window);
			}
		}
		catch (Exception)
		{
			// window may already be closing
		}
	}
}
