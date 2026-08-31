namespace Iris.App.Views;

public partial class AccessPage : ContentPage
{
	private readonly AccessViewModel _vm;

	public AccessPage(AccessViewModel vm)
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
