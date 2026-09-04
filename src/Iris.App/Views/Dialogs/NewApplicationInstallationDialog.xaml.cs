using Iris.App.ViewModels;

namespace Iris.App.Views.Dialogs;

public partial class NewApplicationInstallationDialog : ContentPage
{
	private readonly ApplicationRowViewModel _row;

	public NewApplicationInstallationDialog(ApplicationRowViewModel row)
	{
		InitializeComponent();
		BindingContext = _row = row;
		_row.ApplicationInstallationCompleted += OnApplicationInstallationCompleted;
	}

	private async void OnCancel(object? sender, EventArgs e)
	{
		await Navigation.PopModalAsync();
	}

	private async void OnApplicationInstallationCompleted(object? sender, EventArgs e)
	{
		await Navigation.PopModalAsync();
	}

	protected override void OnDisappearing()
	{
		_row.ApplicationInstallationCompleted -= OnApplicationInstallationCompleted;
		base.OnDisappearing();
	}
}
