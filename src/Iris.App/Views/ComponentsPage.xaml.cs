namespace Iris.App.Views;

public partial class ComponentsPage : ContentPage
{
	private readonly ComponentsViewModel _vm;

	public ComponentsPage(ComponentsViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (_vm.LoadCommand.CanExecute(null))
			_vm.LoadCommand.Execute(null);
	}
}
