using Iris.App.ViewModels;

namespace Iris.App.Views.Dialogs;

public partial class SelectApplicationForDeploymentDialog : ContentPage
{
	private readonly SelectApplicationDialogViewModel _vm;
	private bool _closing;

	public SelectApplicationForDeploymentDialog(SelectApplicationDialogViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_vm.CloseRequested += OnCloseRequested;
	}

	// Confirm is bound directly to ConfirmCommand in XAML; Cancel goes through this handler only
	// so both paths end up here, at the same single CloseRequested signal.
	private void OnCloseRequested(object? sender, EventArgs e) => Close();

	private void OnCancel(object? sender, EventArgs e) => _vm.CancelCommand.Execute(null);

	private void Close()
	{
		if (_closing)
		{
			return;
		}

		_closing = true;
		_vm.CloseRequested -= OnCloseRequested;

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
