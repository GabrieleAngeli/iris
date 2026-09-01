using WinRT.Interop;
using WinUIWindow = Microsoft.UI.Xaml.Window;

namespace Iris.App.Services;

/// <summary>Resolves the HWND of the app's active WinUI window for MSAL's WAM broker.</summary>
public sealed class WindowHandleProvider : IWindowHandleProvider
{
	public IntPtr GetHandle()
	{
		var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
		var nativeWindow = window?.Handler?.PlatformView as WinUIWindow
			?? throw new InvalidOperationException("No active WinUI window to parent the sign-in prompt to.");

		return WindowNative.GetWindowHandle(nativeWindow);
	}
}
