using Iris.App.ViewModels;

namespace Iris.App.Views;

public partial class ActionsPage : ContentPage
{
	private readonly ActionsViewModel _vm;

	public ActionsPage(ActionsViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
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
