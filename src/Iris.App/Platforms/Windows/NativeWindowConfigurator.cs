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
			if (!args.DidPositionChange && !args.DidSizeChange)
			{
				return;
			}

			Safe(() => store.Set(key, new WindowRect(
				win.Position.X, win.Position.Y, win.Size.Width, win.Size.Height)));
		};
	}

	private void RestoreGeometry(AppWindow appWindow, string key, IntPtr centreOverOwner)
	{
		if (store.TryGet(key, out var rect) && IsOnAScreen(appWindow, rect))
		{
			appWindow.MoveAndResize(new RectInt32(rect.X, rect.Y, rect.Width, rect.Height));
			return;
		}

		if (centreOverOwner != IntPtr.Zero && GetWindowRect(centreOverOwner, out var owner))
		{
			var size = appWindow.Size;
			var x = owner.Left + (((owner.Right - owner.Left) - size.Width) / 2);
			var y = owner.Top + (((owner.Bottom - owner.Top) - size.Height) / 2);
			appWindow.Move(new PointInt32(Math.Max(owner.Left, x), Math.Max(owner.Top, y)));
		}
	}

	private static bool IsOnAScreen(AppWindow appWindow, WindowRect rect)
	{
		try
		{
			var area = (DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest)
				?? DisplayArea.Primary).OuterBounds;

			var visibleX = rect.X + rect.Width - 80 > area.X && rect.X + 80 < area.X + area.Width;
			var visibleY = rect.Y + 40 > area.Y && rect.Y + 40 < area.Y + area.Height;
			return visibleX && visibleY;
		}
		catch (Exception)
		{
			return true;
		}
	}

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
