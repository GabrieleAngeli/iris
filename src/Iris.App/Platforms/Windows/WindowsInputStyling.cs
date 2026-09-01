using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;
using WBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using WColors = Microsoft.UI.Colors;
using WThickness = Microsoft.UI.Xaml.Thickness;

namespace Iris.App;

/// <summary>
/// The design wraps every <c>Entry</c>/<c>Editor</c> in a styled MAUI <c>Border</c> (see the
/// <c>FieldBorder</c> style). On WinUI the underlying <c>TextBox</c> still paints its own border,
/// corner radius and focus underline, which reads as a box nested inside the field. Flatten it so
/// only the outer <c>Border</c> shows.
/// </summary>
internal static class WindowsInputStyling
{
	public static void Apply()
	{
		EntryHandler.Mapper.AppendToMapping("IrisFlatField", (handler, _) => Flatten(handler.PlatformView));
		EditorHandler.Mapper.AppendToMapping("IrisFlatField", (handler, _) => Flatten(handler.PlatformView));
	}

	private static void Flatten(TextBox textBox)
	{
		textBox.BorderThickness = new WThickness(0);
		textBox.Padding = new WThickness(0);
		textBox.MinHeight = 0;
		textBox.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(0);

		// Kill the theme-level border thickness (incl. the accent focus underline) and the
		// subtly different focused/hover fills, so the outer Border owns the whole look.
		var noBorder = new WThickness(0);
		textBox.Resources["TextControlBorderThemeThickness"] = noBorder;
		textBox.Resources["TextControlBorderThemeThicknessFocused"] = noBorder;

		var transparent = new WBrush(WColors.Transparent);
		textBox.Resources["TextControlBackground"] = transparent;
		textBox.Resources["TextControlBackgroundPointerOver"] = transparent;
		textBox.Resources["TextControlBackgroundFocused"] = transparent;
		textBox.Resources["TextControlBorderBrush"] = transparent;
		textBox.Resources["TextControlBorderBrushPointerOver"] = transparent;
		textBox.Resources["TextControlBorderBrushFocused"] = transparent;
	}
}
