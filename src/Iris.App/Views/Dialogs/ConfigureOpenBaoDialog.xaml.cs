using Iris.App.ViewModels;

namespace Iris.App.Views.Dialogs;

public partial class ConfigureOpenBaoDialog : ContentPage
{
	private readonly ConfigureOpenBaoDialogViewModel _vm;
	private bool _closing;

	public ConfigureOpenBaoDialog(ConfigureOpenBaoDialogViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_vm.CloseRequested += OnCloseRequested;
	}

	// Save is bound directly to SaveCommand in XAML; Cancel goes through this handler only
	// so both paths end up here, at the same single CloseRequested signal.
	private void OnCloseRequested(object? sender, EventArgs e) => Close();

	private void OnCancel(object? sender, EventArgs e) => _vm.CancelCommand.Execute(null);

	private void Close()
	{
		if (_closing)
		{
			return;
		}

		_closing = true;
		_vm.CloseRequested -= OnCloseRequested;

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
