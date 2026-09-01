using Iris.App.Services;
using Iris.App.ViewModels;
using Iris.App.Views.Dialogs;

namespace Iris.App.Views;

public partial class ServersPage : ContentPage
{
	private readonly ServersViewModel _vm;
	private readonly IDialogService _dialogs;

	public ServersPage(ServersViewModel vm, IDialogService dialogs)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_dialogs = dialogs;

		_vm.NewServerRequested += async (_, _) =>
			await _dialogs.ShowAsync(new NewServerDialog(_vm), "dlg.new-server", 660, 720);

		_vm.AddCredentialRequested += async (_, row) =>
			await _dialogs.ShowAsync(new AddCredentialDialog(row), "dlg.add-credential", 560, 640);

		_vm.EditServerRequested += async (_, row) =>
			await _dialogs.ShowAsync(new EditServerDialog(row), "dlg.edit-server", 660, 640);

		_vm.DeleteServerRequested += async (_, row) =>
			await _dialogs.ShowAsync(new ConfirmDeleteDialog(row), "dlg.confirm-delete", 460, 340);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (_vm.LoadCommand.CanExecute(null))
			_vm.LoadCommand.Execute(null);
	}
}
