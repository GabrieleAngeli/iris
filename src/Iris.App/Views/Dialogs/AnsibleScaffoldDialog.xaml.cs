using Iris.App.ViewModels;

namespace Iris.App.Views.Dialogs;

/// <summary>
/// Viewer for one <see cref="ApplicationRowViewModel"/>'s generated Ansible scaffold — a list of
/// files/snippets on the left, the selected one's content on the right. Read-only: there is
/// nothing to save here, the operator copies what they need into their own Ansible repo.
/// </summary>
public partial class AnsibleScaffoldDialog : ContentPage
{
	private bool _closing;

	public AnsibleScaffoldDialog(ApplicationRowViewModel row)
	{
		InitializeComponent();
		BindingContext = Row = row;
	}

	public ApplicationRowViewModel Row { get; }

	private void OnClose(object? sender, EventArgs e) => Close();

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
