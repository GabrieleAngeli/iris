namespace Iris.App.Views;

public partial class SystemSettingsPage : ContentPage
{
	private readonly SystemSettingsViewModel _vm;

	public SystemSettingsPage(SystemSettingsViewModel vm)
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
