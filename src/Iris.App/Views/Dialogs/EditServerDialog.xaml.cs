using Iris.App.ViewModels;

namespace Iris.App.Views.Dialogs;

public partial class EditServerDialog : ContentPage
{
	private readonly ServerRowViewModel _row;
	private bool _closing;

	public EditServerDialog(ServerRowViewModel row)
	{
		InitializeComponent();
		BindingContext = _row = row;
		_row.EditCompleted += OnCompleted;
		_row.DeleteRequested += OnCompleted;
		Unloaded += (_, _) => Detach();
	}

	private void OnCompleted(object? sender, EventArgs e) => Close();

	private void OnCancel(object? sender, EventArgs e) => Close();

	private void Detach()
	{
		_row.EditCompleted -= OnCompleted;
		_row.DeleteRequested -= OnCompleted;
		_row.NotifyEditorClosed();
	}

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
