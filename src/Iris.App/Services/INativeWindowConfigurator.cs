namespace Iris.App.Services;

/// <summary>
/// Platform hook for the bits shared code can't do: apply/persist a window's native geometry and
/// turn a secondary <see cref="Window"/> into a true owned, blocking dialog (the window below is
/// disabled until it closes, like the Windows elevation prompt). Windows implementation lives under
/// <c>Platforms/Windows</c>; other platforms get <see cref="NullNativeWindowConfigurator"/>.
/// </summary>
public interface INativeWindowConfigurator
{
	/// <summary>Restore the primary window's saved geometry and keep persisting it as it moves/resizes.</summary>
	void ConfigureMainWindow(Window window, string persistKey);

	/// <summary>
	/// Make <paramref name="window"/> a modal dialog owned by the primary window: restore its saved
	/// geometry (or centre it over the owner), block the owner, drop its minimise/maximise, and keep
	/// persisting its geometry. <paramref name="onClosed"/> runs once the native window has closed and
	/// the owner has been handed control back.
	/// </summary>
	void MakeModalDialog(Window window, string persistKey, Action onClosed);
}

/// <summary>No-op fallback for non-Windows targets (the app currently only ships for Windows).</summary>
public sealed class NullNativeWindowConfigurator : INativeWindowConfigurator
{
	public void ConfigureMainWindow(Window window, string persistKey)
	{
	}

	public void MakeModalDialog(Window window, string persistKey, Action onClosed)
	{
		window.Destroying += (_, _) => onClosed();
	}
}
