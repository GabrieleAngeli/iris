using Iris.App.ViewModels;

namespace Iris.App.Views.Dialogs;

public partial class NewServerDialog : ContentPage
{
	private readonly ServersViewModel _vm;
	private bool _closing;

	public NewServerDialog(ServersViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_vm.NewServerCompleted += OnCompleted;
		Unloaded += (_, _) => Detach();
	}

	private void OnCompleted(object? sender, EventArgs e) => Close();

	private void OnCancel(object? sender, EventArgs e) => Close();

	private void Detach() => _vm.NewServerCompleted -= OnCompleted;

	private void Close()
	{
		if (_closing)
		{
			return;
		}

		_closing = true;
		Detach();

		try
		{
			if (Window is { } window)
			{
				Application.Current?.CloseWindow(window);
			}
		}
		catch (Exception)
		{
			// window may already be closing
		}
	}
}
