using Iris.App.Services;
using Iris.App.ViewModels;
using Iris.App.Views.Dialogs;

namespace Iris.App.Views;

public partial class DeploymentsPage : ContentPage
{
	private readonly DeploymentsViewModel _vm;
	private readonly IDialogService _dialogs;

	public DeploymentsPage(DeploymentsViewModel vm, IDialogService dialogs)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_dialogs = dialogs;

		_vm.NewApplicationInstallationRequested += async (_, row) =>
			await _dialogs.ShowAsync(new NewApplicationInstallationDialog(row), "dlg.new-application-installation", 820, 760);

		_vm.InstallationOpsRequested += async (_, row) =>
			await _dialogs.ShowAsync(new InstallationOpsDialog(row), "dlg.installation-ops", 720, 680);

		_vm.SelectApplicationRequested += async (_, picker) =>
		{
			// Wait for the picker window to be fully closed before letting the ViewModel react —
			// ShowAsync's Task only completes once the native window is actually gone. Acting on
			// picker.WasConfirmed any earlier (e.g. from the dialog's own Confirmed handling) let a
			// second window start opening while this one was still closing, and crashed the app.
			await _dialogs.ShowAsync(new SelectApplicationForDeploymentDialog(picker), "dlg.select-application-for-deployment", 420, 340);
			await _vm.HandleApplicationPickedAsync(picker);
		};
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (_vm.LoadCommand.CanExecute(null))
		{
			_vm.LoadCommand.Execute(null);
		}
	}
}
