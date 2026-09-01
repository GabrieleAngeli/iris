namespace Iris.App.Services;

/// <summary>
/// Gives MSAL's interactive/broker sign-in the native window handle it needs to parent
/// its UI. Implemented per platform (currently Windows only, under Platforms/Windows) so
/// shared code never touches WinUI/Android/iOS-specific window types directly.
/// </summary>
public interface IWindowHandleProvider
{
	/// <summary>The active top-level window handle, resolved on demand (the window may not exist yet at DI-build time).</summary>
	IntPtr GetHandle();
}
