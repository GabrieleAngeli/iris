using Iris.App.Services;
using Iris.App.ViewModels;
using Iris.App.Views.Dialogs;

namespace Iris.App.Views;

public partial class CustomersPage : ContentPage
{
	private readonly CustomersViewModel _vm;
	private readonly IDialogService _dialogs;

	public CustomersPage(CustomersViewModel vm, IDialogService dialogs)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_dialogs = dialogs;

		_vm.NewCustomerRequested += async (_, _) =>
			await _dialogs.ShowAsync(new NewCustomerDialog(_vm), "dlg.new-customer", 520, 440);

		_vm.AddContextRequested += async (_, row) =>
			await _dialogs.ShowAsync(new AddContextDialog(row), "dlg.add-context", 520, 460);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (_vm.LoadCommand.CanExecute(null))
			_vm.LoadCommand.Execute(null);
	}
}
