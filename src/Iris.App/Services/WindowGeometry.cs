using Microsoft.Maui.Storage;

namespace Iris.App.Services;

/// <summary>Saved on-screen rectangle of a window, in device-independent pixels.</summary>
public readonly record struct WindowRect(int X, int Y, int Width, int Height);

/// <summary>
/// Remembers each window's last position and size in local <see cref="Preferences"/>, keyed by a
/// stable name ("win.main", "dlg.new-server", …). Purely storage — applying the geometry to the
/// native window is the platform configurator's job.
/// </summary>
public sealed class WindowGeometryStore
{
	private const string Prefix = "window.geometry.";
	private const string MaximizedPrefix = "window.maximized.";
	private const string DisplayPrefix = "window.display.";

	public bool TryGet(string key, out WindowRect rect)
	{
		rect = default;
		var raw = Preferences.Default.Get(Prefix + key, string.Empty);
		var parts = raw.Split(';');
		if (parts.Length != 4
			|| !int.TryParse(parts[0], out var x)
			|| !int.TryParse(parts[1], out var y)
			|| !int.TryParse(parts[2], out var w)
			|| !int.TryParse(parts[3], out var h)
			|| w <= 0 || h <= 0)
		{
			return false;
		}

		rect = new WindowRect(x, y, w, h);
		return true;
	}

	public void Set(string key, WindowRect rect)
	{
		if (rect.Width <= 0 || rect.Height <= 0)
		{
			return;
		}

		Preferences.Default.Set(Prefix + key, $"{rect.X};{rect.Y};{rect.Width};{rect.Height}");
	}

	public bool IsMaximized(string key) =>
		Preferences.Default.Get(MaximizedPrefix + key, false);

	public void SetMaximized(string key, bool isMaximized) =>
		Preferences.Default.Set(MaximizedPrefix + key, isMaximized);

	public bool TryGetDisplay(string key, out WindowRect rect)
	{
		rect = default;
		var raw = Preferences.Default.Get(DisplayPrefix + key, string.Empty);
		var parts = raw.Split(';');
		if (parts.Length != 4
			|| !int.TryParse(parts[0], out var x)
			|| !int.TryParse(parts[1], out var y)
			|| !int.TryParse(parts[2], out var w)
			|| !int.TryParse(parts[3], out var h)
			|| w <= 0 || h <= 0)
		{
			return false;
		}

		rect = new WindowRect(x, y, w, h);
		return true;
	}

	public void SetDisplay(string key, WindowRect rect)
	{
		if (rect.Width <= 0 || rect.Height <= 0)
		{
			return;
		}

		Preferences.Default.Set(DisplayPrefix + key, $"{rect.X};{rect.Y};{rect.Width};{rect.Height}");
	}
}
