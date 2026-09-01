using Iris.App.Services;
using Iris.App.ViewModels;
using Iris.App.Views.Dialogs;

namespace Iris.App.Views;

public partial class UsersPage : ContentPage
{
	private readonly UsersViewModel _vm;
	private readonly IDialogService _dialogs;

	public UsersPage(UsersViewModel vm, IDialogService dialogs)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_dialogs = dialogs;

		_vm.NewUserRequested += async (_, _) =>
			await _dialogs.ShowAsync(new NewUserDialog(_vm), "dlg.new-user", 520, 460);

		_vm.AssignRoleRequested += async (_, row) =>
			await _dialogs.ShowAsync(new AssignRoleDialog(row), "dlg.assign-role", 620, 560);

		_vm.EditUserRequested += async (_, row) =>
			await _dialogs.ShowAsync(new EditUserDialog(row), "dlg.edit-user", 560, 560);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (_vm.LoadCommand.CanExecute(null))
			_vm.LoadCommand.Execute(null);
	}
}
