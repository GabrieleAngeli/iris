namespace Iris.App.Services;

/// <summary>Opens a form in a real, separate OS window that blocks the window below until it closes.</summary>
public interface IDialogService
{
	Task ShowAsync(ContentPage content, string persistKey, double defaultWidth, double defaultHeight);
}

public sealed class DialogService(INativeWindowConfigurator native) : IDialogService
{
	public Task ShowAsync(ContentPage content, string persistKey, double defaultWidth, double defaultHeight)
	{
		ArgumentNullException.ThrowIfNull(content);

		var completion = new TaskCompletionSource();

		var window = new Window(content)
		{
			Title = string.IsNullOrWhiteSpace(content.Title) ? "Iris" : content.Title,
			Width = defaultWidth,
			Height = defaultHeight,
			MinimumWidth = 360,
			MinimumHeight = 320,
		};

		window.Created += (_, _) =>
			Try(() => native.MakeModalDialog(window, persistKey, () => completion.TrySetResult()));

		Application.Current!.OpenWindow(window);
		return completion.Task;
	}

	private static void Try(Action action)
	{
		try
		{
			action();
		}
		catch (Exception)
		{
			// The dialog opening/closing must never take the app down.
		}
	}
}
