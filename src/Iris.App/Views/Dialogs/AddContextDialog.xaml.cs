using Iris.App.ViewModels;

namespace Iris.App.Views.Dialogs;

public partial class AddContextDialog : ContentPage
{
	private readonly CustomerRowViewModel _row;
	private bool _closing;

	public AddContextDialog(CustomerRowViewModel row)
	{
		InitializeComponent();
		BindingContext = _row = row;
		_row.AddContextCompleted += OnCompleted;
		Unloaded += (_, _) => Detach();
	}

	private void OnCompleted(object? sender, EventArgs e) => Close();

	private void OnCancel(object? sender, EventArgs e) => Close();

	private void Detach() => _row.AddContextCompleted -= OnCompleted;

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
