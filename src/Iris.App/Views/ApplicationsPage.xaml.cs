using Iris.App.Services;
using Iris.App.ViewModels;
using Iris.App.Views.Dialogs;

namespace Iris.App.Views;

public partial class ApplicationsPage : ContentPage
{
	private readonly ApplicationsViewModel _vm;
	private readonly IDialogService _dialogs;

	public ApplicationsPage(ApplicationsViewModel vm, IDialogService dialogs)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_dialogs = dialogs;

		_vm.NewApplicationRequested += async (_, _) =>
			await _dialogs.ShowAsync(new NewApplicationDialog(_vm), "dlg.new-application", 640, 520);

		_vm.EditApplicationRequested += async (_, row) =>
			await _dialogs.ShowAsync(new EditApplicationDialog(row), "dlg.edit-application", 640, 520);

		_vm.ImportManifestRequested += async (_, row) =>
			await _dialogs.ShowAsync(new ImportManifestDialog(row), "dlg.import-manifest", 720, 640);
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
