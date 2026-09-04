using Iris.App.ViewModels;

namespace Iris.App.Views.Dialogs;

/// <summary>
/// Read-mostly ops console for one <see cref="ApplicationInstallationRowViewModel"/>: runs the
/// Validation Engine, launches an AWX deployment and shows its run history. Not an edit-lock
/// resource — it does not mutate the installation record itself.
/// </summary>
public partial class InstallationOpsDialog : ContentPage
{
	private readonly ApplicationInstallationRowViewModel _row;
	private bool _closing;

	public InstallationOpsDialog(ApplicationInstallationRowViewModel row)
	{
		InitializeComponent();
		BindingContext = _row = row;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

		if (_row.ValidateCommand.CanExecute(null))
		{
			_row.ValidateCommand.Execute(null);
		}

		if (_row.LoadRunsCommand.CanExecute(null))
		{
			_row.LoadRunsCommand.Execute(null);
		}
	}

	private void OnClose(object? sender, EventArgs e) => Close();

	private void Close()
	{
		if (_closing)
		{
			return;
		}

		_closing = true;

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
