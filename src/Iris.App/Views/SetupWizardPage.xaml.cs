namespace Iris.App.Views;

public partial class SetupWizardPage : ContentPage
{
	private readonly SetupWizardViewModel _vm;

	public SetupWizardPage(SetupWizardViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;

		_vm.Completed += async (_, _) => await Shell.Current.GoToAsync("//dashboard");
	}
}
