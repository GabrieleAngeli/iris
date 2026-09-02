using System.Runtime.InteropServices;
using Iris.App.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
using MauiWindow = Microsoft.Maui.Controls.Window;
using WinUIWindow = Microsoft.UI.Xaml.Window;

namespace Iris.App;

/// <summary>
/// Windows implementation: persists window geometry via <see cref="WindowGeometryStore"/> and turns
/// secondary windows into owned, blocking modal dialogs — the owner window is disabled (like the UAC
/// prompt) until the dialog closes.
///
/// Nothing here runs on window teardown: geometry is saved continuously from <c>AppWindow.Changed</c>
/// while the window lives, and the only close-time action is re-enabling the owner, driven off the
/// native <c>Closed</c> event with a handle captured up front — so we never touch a window that has
/// "already been deactivated".
/// </summary>
internal sealed class NativeWindowConfigurator(WindowGeometryStore store) : INativeWindowConfigurator
{
	private const int GWLP_HWNDPARENT = -8;

	public void ConfigureMainWindow(MauiWindow window, string persistKey)
	{
		WhenNativeReady(window, (appWindow, _, _) =>
		{
			Safe(() => RestoreGeometry(appWindow, persistKey, IntPtr.Zero));
			Safe(() => RestoreMaximizedState(appWindow, persistKey));
			PersistGeometryContinuously(appWindow, persistKey);
		});
	}

	public void MakeModalDialog(MauiWindow window, string persistKey, Action onClosed)
	{
		var ownerHandle = PrimaryWindowHandle();

		WhenNativeReady(window, (appWindow, hwnd, nativeWindow) =>
		{
			Safe(() =>
			{
				if (ownerHandle != IntPtr.Zero)
				{
					SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, ownerHandle);
				}

				if (appWindow.Presenter is OverlappedPresenter presenter)
				{
					presenter.IsMinimizable = false;
					presenter.IsMaximizable = false;
					presenter.IsResizable = true;
					try
					{
						presenter.IsModal = true;
					}
					catch (Exception)
					{
						// IsModal needs an owner; EnableWindow below still blocks it.
					}
				}

				RestoreGeometry(appWindow, persistKey, ownerHandle);

				if (ownerHandle != IntPtr.Zero)
				{
					EnableWindow(ownerHandle, false);
				}
			});

			PersistGeometryContinuously(appWindow, persistKey);

			nativeWindow.Closed += (_, _) =>
			{
				Safe(() =>
				{
					if (ownerHandle != IntPtr.Zero)
					{
						EnableWindow(ownerHandle, true);
						SetForegroundWindow(ownerHandle);
					}
				});

				try
				{
					onClosed();
				}
				catch (Exception)
				{
					// caller's completion signal
				}
			};
		});
	}

	// ---- geometry -----------------------------------------------------------

	private void PersistGeometryContinuously(AppWindow appWindow, string key)
	{
		appWindow.Changed += (win, args) =>
		{
			if (TryGetCurrentDisplayRect(win, out var displayRect))
			{
				store.SetDisplay(key, displayRect);
			}

			var isMaximized = win.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
			store.SetMaximized(key, isMaximized);
			if (isMaximized)
			{
				return;
			}

			if (!args.DidPositionChange && !args.DidSizeChange)
			{
				return;
			}

			Safe(() => store.Set(key, new WindowRect(
				win.Position.X, win.Position.Y, win.Size.Width, win.Size.Height)));
		};
	}

	private void RestoreMaximizedState(AppWindow appWindow, string key)
	{
		if (store.IsMaximized(key) && appWindow.Presenter is OverlappedPresenter presenter)
		{
			MoveToStoredDisplay(appWindow, key);
			presenter.Maximize();
		}
	}

	private void MoveToStoredDisplay(AppWindow appWindow, string key)
	{
		var target = store.TryGetDisplay(key, out var savedDisplay)
			? FindDisplay(savedDisplay)
			: null;
		var area = (target ?? PrimaryDisplay())?.WorkArea;
		if (area is null)
		{
			return;
		}

		var size = appWindow.Size;
		appWindow.MoveAndResize(CenteredRect(size.Width, size.Height, area.Value));
	}

	private void RestoreGeometry(AppWindow appWindow, string key, IntPtr centreOverOwner)
	{
		if (store.TryGet(key, out var rect))
		{
			var area = (BestVisibleArea(rect) ?? PrimaryDisplay())?.WorkArea;
			if (area is not null)
			{
				appWindow.MoveAndResize(IsVisibleWithin(rect, area.Value)
					? ClampToWorkArea(rect, area.Value)
					: CenteredRect(rect.Width, rect.Height, area.Value));
			}

			return;
		}

		if (centreOverOwner != IntPtr.Zero && GetWindowRect(centreOverOwner, out var owner))
		{
			var ownerRect = new WindowRect(
				owner.Left,
				owner.Top,
				owner.Right - owner.Left,
				owner.Bottom - owner.Top);
			var area = BestVisibleArea(ownerRect)?.WorkArea ?? PrimaryDisplay()?.WorkArea;
			var size = appWindow.Size;
			var centered = new WindowRect(
				owner.Left + (((owner.Right - owner.Left) - size.Width) / 2),
				owner.Top + (((owner.Bottom - owner.Top) - size.Height) / 2),
				size.Width,
				size.Height);
			var target = area is null
				? new RectInt32(centered.X, centered.Y, centered.Width, centered.Height)
				: ClampToWorkArea(centered, area.Value);
			appWindow.MoveAndResize(target);
			return;
		}

		var primary = PrimaryDisplay()?.WorkArea;
		if (primary is not null)
		{
			var size = appWindow.Size;
			appWindow.MoveAndResize(CenteredRect(size.Width, size.Height, primary.Value));
		}
	}

	private static DisplayArea? BestVisibleArea(WindowRect rect)
	{
		try
		{
			return DisplayArea.FindAll()
				.Where(area => IsVisibleWithin(rect, area.WorkArea))
				.OrderByDescending(area => IntersectionArea(rect, area.WorkArea))
				.FirstOrDefault();
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static DisplayArea? FindDisplay(WindowRect savedDisplay)
	{
		try
		{
			return DisplayArea.FindAll().FirstOrDefault(area => SameBounds(area.OuterBounds, savedDisplay));
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static bool TryGetCurrentDisplayRect(AppWindow appWindow, out WindowRect rect)
	{
		rect = default;
		try
		{
			var bounds = (DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest)
				?? DisplayArea.Primary).OuterBounds;
			rect = ToWindowRect(bounds);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static bool IsVisibleWithin(WindowRect rect, RectInt32 area)
	{
		return IntersectionWidth(rect, area) >= Math.Min(160, rect.Width)
			&& IntersectionHeight(rect, area) >= Math.Min(80, rect.Height);
	}

	private static RectInt32 ClampToWorkArea(WindowRect rect, RectInt32 area)
	{
		var width = Math.Min(rect.Width, area.Width);
		var height = Math.Min(rect.Height, area.Height);
		var x = Math.Clamp(rect.X, area.X, area.X + area.Width - width);
		var y = Math.Clamp(rect.Y, area.Y, area.Y + area.Height - height);
		return new RectInt32(x, y, width, height);
	}

	private static RectInt32 CenteredRect(int requestedWidth, int requestedHeight, RectInt32 area)
	{
		var width = Math.Min(requestedWidth, area.Width);
		var height = Math.Min(requestedHeight, area.Height);
		var x = area.X + Math.Max(0, (area.Width - width) / 2);
		var y = area.Y + Math.Max(0, (area.Height - height) / 2);
		return new RectInt32(x, y, width, height);
	}

	private static int IntersectionArea(WindowRect rect, RectInt32 area) =>
		IntersectionWidth(rect, area) * IntersectionHeight(rect, area);

	private static int IntersectionWidth(WindowRect rect, RectInt32 area) =>
		Math.Max(0, Math.Min(rect.X + rect.Width, area.X + area.Width) - Math.Max(rect.X, area.X));

	private static int IntersectionHeight(WindowRect rect, RectInt32 area) =>
		Math.Max(0, Math.Min(rect.Y + rect.Height, area.Y + area.Height) - Math.Max(rect.Y, area.Y));

	private static DisplayArea? PrimaryDisplay()
	{
		try
		{
			return DisplayArea.Primary;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static bool SameBounds(RectInt32 area, WindowRect saved) =>
		area.X == saved.X && area.Y == saved.Y && area.Width == saved.Width && area.Height == saved.Height;

	private static WindowRect ToWindowRect(RectInt32 rect) =>
		new(rect.X, rect.Y, rect.Width, rect.Height);

	// ---- native plumbing --------------------------------------------------

	private static void Safe(Action action)
	{
		try
		{
			action();
		}
		catch (Exception)
		{
			// Window/native interop races must never surface as unhandled.
		}
	}

	private static void WhenNativeReady(MauiWindow window, Action<AppWindow, IntPtr, WinUIWindow> action)
	{
		if (TryResolve(window, out var appWindow, out var hwnd, out var native)
			&& appWindow is not null && native is not null)
		{
			Safe(() => action(appWindow, hwnd, native));
			return;
		}

		void OnHandlerChanged(object? sender, EventArgs e)
		{
			if (TryResolve(window, out var w, out var h, out var n) && w is not null && n is not null)
			{
				window.HandlerChanged -= OnHandlerChanged;
				Safe(() => action(w, h, n));
			}
		}

		window.HandlerChanged += OnHandlerChanged;
	}

	private static bool TryResolve(MauiWindow window, out AppWindow? appWindow, out IntPtr hwnd, out WinUIWindow? native)
	{
		appWindow = null;
		hwnd = IntPtr.Zero;
		native = null;

		try
		{
			if (window.Handler?.PlatformView is not WinUIWindow n)
			{
				return false;
			}

			native = n;
			hwnd = WindowNative.GetWindowHandle(n);
			appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
			return appWindow is not null;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static IntPtr PrimaryWindowHandle()
	{
		try
		{
			var windows = Microsoft.Maui.Controls.Application.Current?.Windows;
			if (windows is null || windows.Count == 0)
			{
				return IntPtr.Zero;
			}

			return windows[0].Handler?.PlatformView is WinUIWindow native
				? WindowNative.GetWindowHandle(native)
				: IntPtr.Zero;
		}
		catch (Exception)
		{
			return IntPtr.Zero;
		}
	}

	[DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
	private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

	[StructLayout(LayoutKind.Sequential)]
	private struct Rect
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}
}
