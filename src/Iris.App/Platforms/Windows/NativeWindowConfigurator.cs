using System.Runtime.InteropServices;
using Iris.App.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
using MauiWindow = Microsoft.Maui.Controls.Window;
using WinUIColor = Windows.UI.Color;
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
	private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
	private const int DWMWA_BORDER_COLOR = 34;
	private const int DWMWA_CAPTION_COLOR = 35;
	private const int DWMWA_TEXT_COLOR = 36;
	private const string TitleBarBackgroundOverlayName = "IrisTitleBarChromeBackground";
	private static readonly object ChromeHookLock = new();
	private static readonly HashSet<IntPtr> ChromeHookedHandles = [];
	private static readonly Dictionary<IntPtr, bool> ChromeActiveStates = [];
	private static readonly string[] TitleBarElementNames =
	[
		"PART_LayoutRoot",
		"PART_BackButton",
		"PART_PaneToggleButton",
		"PART_TitleText",
		"PART_SubtitleText",
		"PART_ContentPresenterGrid",
		"TopNavArea",
		"TopNavTopPadding",
		"TopNavGrid",
		"PaneToggleButtonGrid",
		"TogglePaneTopPadding",
		"ButtonHolderGrid",
		"NavigationViewBackButton",
		"NavigationViewCloseButton",
		"TogglePaneButton",
		"PaneTitleTextBlock",
		"PaneTitleHolder",
		"PaneTitlePresenter",
		"PaneTitleOnTopPane"
	];

	public void ConfigureMainWindow(MauiWindow window, string persistKey)
	{
		WhenNativeReady(window, (appWindow, hwnd, nativeWindow) =>
		{
			Safe(() => ConfigureTitleBar(appWindow, hwnd, nativeWindow));
			Safe(() => RestoreGeometry(appWindow, persistKey, IntPtr.Zero));
			Safe(() => RestoreMaximizedState(appWindow, persistKey));
			PersistGeometryContinuously(appWindow, persistKey);
		});
	}

	public void RefreshThemeChrome()
	{
		var windows = Microsoft.Maui.Controls.Application.Current?.Windows;
		if (windows is null)
		{
			return;
		}

		foreach (var window in windows)
		{
			if (TryResolve(window, out var appWindow, out var hwnd, out var nativeWindow)
				&& appWindow is not null && nativeWindow is not null)
			{
				Safe(() => ConfigureTitleBar(appWindow, hwnd, nativeWindow));
			}
		}
	}

	public void MakeModalDialog(MauiWindow window, string persistKey, Action onClosed)
	{
		var ownerHandle = PrimaryWindowHandle();

		WhenNativeReady(window, (appWindow, hwnd, nativeWindow) =>
		{
			Safe(() => ConfigureTitleBar(appWindow, hwnd, nativeWindow));
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

	private static void ConfigureTitleBar(AppWindow appWindow, IntPtr hwnd, WinUIWindow nativeWindow)
	{
		var isActive = GetChromeActiveState(hwnd);
		ApplyChrome(appWindow, hwnd, nativeWindow, isActive);
		HookChromeRefresh(appWindow, hwnd, nativeWindow);
	}

	private static void ApplyChrome(AppWindow appWindow, IntPtr hwnd, WinUIWindow nativeWindow, bool isActive)
	{
		SetChromeActiveState(hwnd, isActive);
		ApplyNativeRequestedTheme(nativeWindow);
		ApplyWinUiCaptionResources(isActive);
		ApplyWinUiTitleBarVisuals(nativeWindow, appWindow, isActive);
		ApplyDwmChrome(hwnd, isActive);

		if (AppWindowTitleBar.IsCustomizationSupported())
		{
			ApplyTitleBarColors(appWindow);
		}
	}

	private static void HookChromeRefresh(AppWindow appWindow, IntPtr hwnd, WinUIWindow nativeWindow)
	{
		if (hwnd == IntPtr.Zero)
		{
			return;
		}

		lock (ChromeHookLock)
		{
			if (!ChromeHookedHandles.Add(hwnd))
			{
				return;
			}
		}

		var app = Microsoft.Maui.Controls.Application.Current;
		if (app is not null)
		{
			app.RequestedThemeChanged += (_, _) => Safe(() => ApplyChrome(appWindow, hwnd, nativeWindow, GetChromeActiveState(hwnd)));
		}

		if (nativeWindow.Content is Microsoft.UI.Xaml.FrameworkElement root)
		{
			root.Loaded += (_, _) => Safe(() => ApplyChrome(appWindow, hwnd, nativeWindow, GetChromeActiveState(hwnd)));
			root.ActualThemeChanged += (_, _) => Safe(() => ApplyChrome(appWindow, hwnd, nativeWindow, GetChromeActiveState(hwnd)));
		}

		nativeWindow.Activated += (_, args) => Safe(() =>
		{
			var isActive = args.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated;
			ApplyChrome(appWindow, hwnd, nativeWindow, isActive);
			if (nativeWindow.Content is Microsoft.UI.Xaml.FrameworkElement root)
			{
				_ = root.DispatcherQueue.TryEnqueue(() => Safe(() => ApplyChrome(appWindow, hwnd, nativeWindow, isActive)));
			}
		});

		nativeWindow.Closed += (_, _) =>
		{
			lock (ChromeHookLock)
			{
				ChromeHookedHandles.Remove(hwnd);
				ChromeActiveStates.Remove(hwnd);
			}
		};
	}

	private static void ApplyNativeRequestedTheme(WinUIWindow nativeWindow)
	{
		if (nativeWindow.Content is not Microsoft.UI.Xaml.FrameworkElement root)
		{
			return;
		}

		root.RequestedTheme = AppChromeTheme.IsDark
			? Microsoft.UI.Xaml.ElementTheme.Dark
			: Microsoft.UI.Xaml.ElementTheme.Light;
	}

	private static void ApplyTitleBarColors(AppWindow appWindow)
	{
		var titleBar = appWindow.TitleBar;
		var dark = AppChromeTheme.IsDark;
		var background = WinColor(ChromeBackgroundKey(dark, isActive: true));
		var inactiveBackground = WinColor(ChromeBackgroundKey(dark, isActive: false));
		var foreground = ChromeForegroundColor(dark);
		var hover = ThemeColor("SubtleFillColorSecondary", dark ? "SubtleFillDark" : "SubtleFillLight");
		var pressed = ThemeColor("SubtleFillColorTertiary", dark ? "LayerAltDark" : "Gray200");

		titleBar.BackgroundColor = background;
		titleBar.ForegroundColor = foreground;
		titleBar.InactiveBackgroundColor = inactiveBackground;
		titleBar.InactiveForegroundColor = foreground;
		titleBar.ButtonBackgroundColor = background;
		titleBar.ButtonForegroundColor = foreground;
		titleBar.ButtonInactiveBackgroundColor = inactiveBackground;
		titleBar.ButtonInactiveForegroundColor = foreground;
		titleBar.ButtonHoverBackgroundColor = hover;
		titleBar.ButtonHoverForegroundColor = foreground;
		titleBar.ButtonPressedBackgroundColor = pressed;
		titleBar.ButtonPressedForegroundColor = foreground;
	}

	private static void ApplyWinUiCaptionResources(bool isActive)
	{
		var resources = Microsoft.UI.Xaml.Application.Current?.Resources;
		if (resources is null)
		{
			return;
		}

		var dark = AppChromeTheme.IsDark;
		var background = WinColor(ChromeBackgroundKey(dark, isActive));
		var inactiveBackground = WinColor(ChromeBackgroundKey(dark, isActive: false));
		var foreground = ChromeForegroundColor(dark);
		var secondaryForeground = WinColor(dark ? "TextSecondaryDark" : "TextSecondaryLight");
		var hover = ThemeColor("SubtleFillColorSecondary", dark ? "SubtleFillDark" : "SubtleFillLight");
		var pressed = ThemeColor("SubtleFillColorTertiary", dark ? "LayerAltDark" : "Gray200");
		var backgroundBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(background);
		var inactiveBackgroundBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(inactiveBackground);
		var foregroundBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(foreground);
		var secondaryForegroundBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(secondaryForeground);
		var hoverBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(hover);
		var pressedBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(pressed);

		SetWinUiResource(resources, "WindowCaptionBackground", backgroundBrush);
		SetWinUiResource(resources, "WindowCaptionBackgroundDisabled", inactiveBackgroundBrush);
		SetWinUiResource(resources, "WindowCaptionForeground", foregroundBrush);
		SetWinUiResource(resources, "WindowCaptionForegroundDisabled", foregroundBrush);
		SetWinUiResource(resources, "WindowCaptionButtonBackground", backgroundBrush);
		SetWinUiResource(resources, "WindowCaptionButtonBackgroundPointerOver", hoverBrush);
		SetWinUiResource(resources, "WindowCaptionButtonBackgroundPressed", pressedBrush);
		SetWinUiResource(resources, "WindowCaptionButtonBackgroundDisabled", inactiveBackgroundBrush);
		SetWinUiResource(resources, "WindowCaptionButtonForeground", foreground);
		SetWinUiResource(resources, "WindowCaptionButtonForegroundPointerOver", foreground);
		SetWinUiResource(resources, "WindowCaptionButtonForegroundPressed", foreground);
		SetWinUiResource(resources, "WindowCaptionButtonForegroundDisabled", foreground);
		SetWinUiResource(resources, "WindowCaptionButtonStroke", foreground);
		SetWinUiResource(resources, "WindowCaptionButtonStrokePointerOver", foreground);
		SetWinUiResource(resources, "WindowCaptionButtonStrokePressed", foreground);
		SetWinUiResource(resources, "WindowCaptionButtonStrokeWidth", 0.0);

		SetWinUiResource(resources, "NavigationViewTopPaneBackground", backgroundBrush);
		SetWinUiResource(resources, "NavigationViewItemForeground", foregroundBrush);
		SetWinUiResource(resources, "NavigationViewItemForegroundPointerOver", foregroundBrush);
		SetWinUiResource(resources, "NavigationViewItemForegroundPressed", foregroundBrush);
		SetWinUiResource(resources, "NavigationViewItemForegroundSelected", foregroundBrush);
		SetWinUiResource(resources, "NavigationViewButtonBackgroundPointerOver", hoverBrush);
		SetWinUiResource(resources, "NavigationViewButtonBackgroundPressed", pressedBrush);
		SetWinUiResource(resources, "NavigationViewButtonBackgroundDisabled", backgroundBrush);
		SetWinUiResource(resources, "NavigationViewButtonForegroundPointerOver", foregroundBrush);
		SetWinUiResource(resources, "NavigationViewButtonForegroundPressed", foregroundBrush);
		SetWinUiResource(resources, "NavigationViewButtonForegroundDisabled", foregroundBrush);

		SetWinUiResource(resources, "TitleBarForegroundBrush", foregroundBrush);
		SetWinUiResource(resources, "TitleBarDeactivatedForegroundBrush", foregroundBrush);
		SetWinUiResource(resources, "TitleBarSubtitleForegroundBrush", secondaryForegroundBrush);
		SetWinUiResource(resources, "TitleBarSubtitleDeactivatedForegroundBrush", secondaryForegroundBrush);
		SetWinUiResource(resources, "TitleBarBackButtonBackground", backgroundBrush);
		SetWinUiResource(resources, "TitleBarBackButtonBackgroundPointerOver", hoverBrush);
		SetWinUiResource(resources, "TitleBarBackButtonBackgroundPressed", pressedBrush);
		SetWinUiResource(resources, "TitleBarBackButtonBackgroundDisabled", inactiveBackgroundBrush);
		SetWinUiResource(resources, "TitleBarBackButtonForeground", foregroundBrush);
		SetWinUiResource(resources, "TitleBarBackButtonForegroundPointerOver", foregroundBrush);
		SetWinUiResource(resources, "TitleBarBackButtonForegroundPressed", foregroundBrush);
		SetWinUiResource(resources, "TitleBarBackButtonForegroundDisabled", foregroundBrush);
		SetWinUiResource(resources, "TitleBarPaneToggleButtonBackground", backgroundBrush);
		SetWinUiResource(resources, "TitleBarPaneToggleButtonBackgroundPointerOver", hoverBrush);
		SetWinUiResource(resources, "TitleBarPaneToggleButtonBackgroundPressed", pressedBrush);
		SetWinUiResource(resources, "TitleBarPaneToggleButtonBackgroundDisabled", inactiveBackgroundBrush);
		SetWinUiResource(resources, "TitleBarPaneToggleButtonForeground", foregroundBrush);
		SetWinUiResource(resources, "TitleBarPaneToggleButtonForegroundPointerOver", foregroundBrush);
		SetWinUiResource(resources, "TitleBarPaneToggleButtonForegroundPressed", foregroundBrush);
		SetWinUiResource(resources, "TitleBarPaneToggleButtonForegroundDisabled", foregroundBrush);
		SetWinUiResource(resources, "TitleBarPaneToggleForegroundDisabled", foregroundBrush);
		SetWinUiResource(resources, "TitleBarCaptionButtonForegroundColor", foreground);
		SetWinUiResource(resources, "TitleBarCaptionButtonHoverForegroundColor", foreground);
		SetWinUiResource(resources, "TitleBarCaptionButtonPressedForegroundColor", foreground);
		SetWinUiResource(resources, "TitleBarCaptionButtonInactiveForegroundColor", foreground);
		SetWinUiResource(resources, "TitleBarCaptionButtonBackgroundColor", background);
		SetWinUiResource(resources, "TitleBarCaptionButtonHoverBackgroundColor", hover);
		SetWinUiResource(resources, "TitleBarCaptionButtonPressedBackgroundColor", pressed);
		SetWinUiResource(resources, "TitleBarCaptionButtonInactiveBackgroundColor", inactiveBackground);
		SetWinUiResource(resources, "TitleBarDeactivatedOpacity", 1.0);
	}

	private static void ApplyWinUiTitleBarVisuals(WinUIWindow nativeWindow, AppWindow appWindow, bool isActive)
	{
		if (nativeWindow.Content is not Microsoft.UI.Xaml.FrameworkElement root)
		{
			return;
		}

		var dark = AppChromeTheme.IsDark;
		var background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
			WinColor(ChromeBackgroundKey(dark, isActive)));
		var foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
			ChromeForegroundColor(dark));
		var chromeHeight = AppWindowTitleBar.IsCustomizationSupported()
			? Math.Max(32, appWindow.TitleBar.Height)
			: 32;

		ApplyTitleBarVisuals(root, background, foreground, chromeHeight);

		_ = root.DispatcherQueue.TryEnqueue(() => Safe(() =>
		{
			ApplyTitleBarVisuals(root, background, foreground, chromeHeight);
		}));
		_ = RefreshTitleBarVisualsAfterLayoutAsync(root, background, foreground, chromeHeight);
	}

	private static async Task RefreshTitleBarVisualsAfterLayoutAsync(
		Microsoft.UI.Xaml.FrameworkElement root,
		Microsoft.UI.Xaml.Media.SolidColorBrush background,
		Microsoft.UI.Xaml.Media.SolidColorBrush foreground,
		double chromeHeight)
	{
		await Task.Delay(100).ConfigureAwait(false);
		_ = root.DispatcherQueue.TryEnqueue(() => Safe(() =>
		{
			ApplyTitleBarVisuals(root, background, foreground, chromeHeight);
		}));
	}

	private static void ApplyTitleBarVisuals(
		Microsoft.UI.Xaml.FrameworkElement root,
		Microsoft.UI.Xaml.Media.SolidColorBrush background,
		Microsoft.UI.Xaml.Media.SolidColorBrush foreground,
		double chromeHeight)
	{
		ApplyTitleBarBackgroundOverlay(root, background, chromeHeight);
		foreach (var element in FindNamedElements(root, TitleBarElementNames))
		{
			ApplyTitleBarVisualColors(element, background, foreground);
		}

		ApplyChromeBandTextForegrounds(root, root, foreground, chromeHeight);
	}

	private static void ApplyTitleBarBackgroundOverlay(
		Microsoft.UI.Xaml.FrameworkElement root,
		Microsoft.UI.Xaml.Media.SolidColorBrush background,
		double chromeHeight)
	{
		var rootGrid = FindFirstNamedPanel(root, "RootGrid");
		if (rootGrid is null)
		{
			return;
		}

		var overlay = rootGrid.Children
			.OfType<Microsoft.UI.Xaml.Controls.Border>()
			.FirstOrDefault(child => child.Name == TitleBarBackgroundOverlayName);
		if (overlay is null)
		{
			overlay = new Microsoft.UI.Xaml.Controls.Border
			{
				Name = TitleBarBackgroundOverlayName,
				IsHitTestVisible = false,
				HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
				VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top,
			};
			Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(overlay, 0);

			rootGrid.Children.Insert(0, overlay);
		}

		overlay.Height = chromeHeight;
		overlay.Background = background;
		Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(overlay, 0);
	}

	private static Microsoft.UI.Xaml.Controls.Panel? FindFirstNamedPanel(
		Microsoft.UI.Xaml.DependencyObject current,
		string name)
	{
		var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(current);
		for (var index = 0; index < count; index++)
		{
			var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(current, index);
			if (child is Microsoft.UI.Xaml.Controls.Panel panel && panel.Name == name)
			{
				return panel;
			}

			var nested = FindFirstNamedPanel(child, name);
			if (nested is not null)
			{
				return nested;
			}
		}

		return null;
	}

	private static IEnumerable<Microsoft.UI.Xaml.FrameworkElement> FindNamedElements(
		Microsoft.UI.Xaml.DependencyObject current,
		IReadOnlyCollection<string> names)
	{
		var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(current);
		for (var index = 0; index < count; index++)
		{
			var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(current, index);
			if (child is Microsoft.UI.Xaml.FrameworkElement element && names.Contains(element.Name))
			{
				yield return element;
			}

			foreach (var nested in FindNamedElements(child, names))
			{
				yield return nested;
			}
		}
	}

	private static void ApplyTitleBarVisualColors(
		Microsoft.UI.Xaml.FrameworkElement element,
		Microsoft.UI.Xaml.Media.SolidColorBrush background,
		Microsoft.UI.Xaml.Media.SolidColorBrush foreground)
	{
		Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(element, 20);

		switch (element)
		{
			case Microsoft.UI.Xaml.Controls.Control control:
				control.Background = background;
				control.Foreground = foreground;
				control.BorderBrush = background;
				break;
			case Microsoft.UI.Xaml.Controls.Panel panel:
				panel.Background = background;
				break;
			case Microsoft.UI.Xaml.Controls.Border border:
				border.Background = background;
				border.BorderBrush = background;
				break;
			case Microsoft.UI.Xaml.Controls.TextBlock textBlock:
				textBlock.Foreground = foreground;
				break;
		}

		ApplyTitleBarDescendantForegrounds(element, foreground);
	}

	private static void ApplyTitleBarDescendantForegrounds(
		Microsoft.UI.Xaml.DependencyObject current,
		Microsoft.UI.Xaml.Media.SolidColorBrush foreground)
	{
		var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(current);
		for (var index = 0; index < count; index++)
		{
			var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(current, index);
			switch (child)
			{
				case Microsoft.UI.Xaml.Controls.TextBlock textBlock:
					textBlock.Foreground = foreground;
					break;
				case Microsoft.UI.Xaml.Controls.Control control:
					control.Foreground = foreground;
					break;
			}

			ApplyTitleBarDescendantForegrounds(child, foreground);
		}
	}

	private static void ApplyChromeBandTextForegrounds(
		Microsoft.UI.Xaml.DependencyObject current,
		Microsoft.UI.Xaml.FrameworkElement root,
		Microsoft.UI.Xaml.Media.SolidColorBrush foreground,
		double chromeHeight)
	{
		var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(current);
		for (var index = 0; index < count; index++)
		{
			var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(current, index);
			if (child is Microsoft.UI.Xaml.Controls.TextBlock textBlock
				&& IsInsideChromeBand(textBlock, root, chromeHeight))
			{
				textBlock.Foreground = foreground;
			}

			ApplyChromeBandTextForegrounds(child, root, foreground, chromeHeight);
		}
	}

	private static bool IsInsideChromeBand(
		Microsoft.UI.Xaml.FrameworkElement element,
		Microsoft.UI.Xaml.FrameworkElement root,
		double chromeHeight)
	{
		if (element.ActualHeight <= 0 || string.IsNullOrWhiteSpace((element as Microsoft.UI.Xaml.Controls.TextBlock)?.Text))
		{
			return false;
		}

		try
		{
			var position = element.TransformToVisual(root).TransformPoint(new Windows.Foundation.Point(0, 0));
			return position.Y >= 0 && position.Y < chromeHeight;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private static void SetWinUiResource(Microsoft.UI.Xaml.ResourceDictionary resources, string key, object value)
	{
		if (resources.ContainsKey(key))
		{
			resources[key] = value;
			return;
		}

		resources.Add(key, value);
	}

	private static void ApplyDwmChrome(IntPtr hwnd, bool isActive)
	{
		if (hwnd == IntPtr.Zero)
		{
			return;
		}

		var dark = AppChromeTheme.IsDark;
		var useDarkMode = dark ? 1 : 0;
		var caption = ColorRef(AppChromeTheme.ResourceColor(ChromeBackgroundKey(dark, isActive)));
		var text = ColorRef(ChromeForegroundColor(dark));
		var border = ColorRef(ThemeColor("ControlStrokeColorDefault", dark ? "ControlStrokeDark" : "ControlStrokeLight"));

		_ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, Marshal.SizeOf<int>());
		_ = DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, Marshal.SizeOf<int>());
		_ = DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, Marshal.SizeOf<int>());
		_ = DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, Marshal.SizeOf<int>());
	}

	private static int ColorRef(byte red, byte green, byte blue) =>
		red | (green << 8) | (blue << 16);

	private static int ColorRef(Color color) =>
		ColorRef(Channel(color.Red), Channel(color.Green), Channel(color.Blue));

	private static int ColorRef(WinUIColor color) =>
		ColorRef(color.R, color.G, color.B);

	private static string ChromeBackgroundKey(bool dark, bool isActive) =>
		(dark, isActive) switch
		{
			(true, true) => "AppChromeDark",
			(true, false) => "AppChromeInactiveDark",
			(false, true) => "AppChromeLight",
			_ => "AppChromeInactiveLight",
		};

	private static WinUIColor ChromeForegroundColor(bool dark) =>
		dark
			? ColorHelper.FromArgb(255, 255, 255, 255)
			: WinColor("TextPrimaryLight");

	private static bool GetChromeActiveState(IntPtr hwnd)
	{
		lock (ChromeHookLock)
		{
			return hwnd == IntPtr.Zero
				|| !ChromeActiveStates.TryGetValue(hwnd, out var isActive)
				|| isActive;
		}
	}

	private static void SetChromeActiveState(IntPtr hwnd, bool isActive)
	{
		if (hwnd == IntPtr.Zero)
		{
			return;
		}

		lock (ChromeHookLock)
		{
			ChromeActiveStates[hwnd] = isActive;
		}
	}

	private static WinUIColor ThemeColor(string winUiKey, string fallbackKey)
	{
		var resources = Microsoft.UI.Xaml.Application.Current?.Resources;
		if (resources is not null && TryGetWinUiColor(resources, winUiKey, out var color))
		{
			return color;
		}

		return WinColor(fallbackKey);
	}

	private static bool TryGetWinUiColor(Microsoft.UI.Xaml.ResourceDictionary resources, string key, out WinUIColor color)
	{
		color = default;
		if (!resources.ContainsKey(key))
		{
			return false;
		}

		var value = resources[key];
		switch (value)
		{
			case WinUIColor resolved:
				color = resolved;
				return true;
			case Microsoft.UI.Xaml.Media.SolidColorBrush brush:
				color = brush.Color;
				return true;
			default:
				return false;
		}
	}

	private static WinUIColor WinColor(string key)
	{
		var color = AppChromeTheme.ResourceColor(key);
		return ColorHelper.FromArgb(Channel(color.Alpha), Channel(color.Red), Channel(color.Green), Channel(color.Blue));
	}

	private static byte Channel(float value) =>
		(byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);

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

	[DllImport("dwmapi.dll", PreserveSig = true)]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

	[StructLayout(LayoutKind.Sequential)]
	private struct Rect
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}
}
