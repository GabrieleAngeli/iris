using Iris.App.ViewModels;

namespace Iris.App.Views.Dialogs;

public partial class InvitationDialog : ContentPage
{
	private bool _closing;

	public InvitationDialog(UserRowViewModel row)
	{
		InitializeComponent();
		BindingContext = row;
	}

	private void OnCancel(object? sender, EventArgs e) => Close();

	private void Close()
	{
		if (_closing)
		{
			return;
		}

		_closing = true;

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
