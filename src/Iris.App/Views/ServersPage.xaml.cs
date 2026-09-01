namespace Iris.App.Views;

public partial class ServersPage : ContentPage
{
	private readonly ServersViewModel _vm;

	public ServersPage(ServersViewModel vm)
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
