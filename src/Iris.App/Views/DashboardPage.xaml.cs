namespace Iris.App.Views;

public partial class DashboardPage : ContentPage
{
	public DashboardPage(DashboardViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
		Vm = vm;
	}

	/// <summary>Strongly-typed access to the view model for compiled bindings inside data templates.</summary>
	public DashboardViewModel Vm { get; }

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (Vm.LoadCommand.CanExecute(null))
			Vm.LoadCommand.Execute(null);
	}
}
