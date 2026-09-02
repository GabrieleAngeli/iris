using Iris.App.ViewModels;

namespace Iris.App.Views;

public partial class StartupPage : ContentPage
{
	private readonly StartupViewModel _vm;

	public StartupPage(StartupViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.StartAsync();
	}
}
