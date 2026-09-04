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
