using Iris.App.ViewModels;

namespace Iris.App.Views.Dialogs;

public partial class ConfirmDeleteDialog : ContentPage
{
	private readonly IConfirmDeletable _target;
	private bool _closing;

	public ConfirmDeleteDialog(IConfirmDeletable target)
	{
		InitializeComponent();
		BindingContext = _target = target;
		_target.DeleteCompleted += OnCompleted;
		Unloaded += (_, _) => Detach();
	}

	private void OnCompleted(object? sender, EventArgs e) => Close();

	private void OnCancel(object? sender, EventArgs e) => Close();

	private void Detach() => _target.DeleteCompleted -= OnCompleted;

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
