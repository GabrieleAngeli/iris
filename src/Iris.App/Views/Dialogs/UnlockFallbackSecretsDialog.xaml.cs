using Iris.App.ViewModels;

namespace Iris.App.Views.Dialogs;

public partial class UnlockFallbackSecretsDialog : ContentPage
{
	private readonly UnlockFallbackSecretsDialogViewModel _vm;
	private bool _closing;

	public UnlockFallbackSecretsDialog(UnlockFallbackSecretsDialogViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_vm.CloseRequested += OnCloseRequested;
	}

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
