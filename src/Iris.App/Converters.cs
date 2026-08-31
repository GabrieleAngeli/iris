using System.Globalization;

namespace Iris.App;

/// <summary>Returns the logical negation of a bound boolean.</summary>
public sealed class InvertBoolConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is bool b ? !b : value!;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is bool b ? !b : value!;
}

/// <summary>True -&gt; collapsed / False -&gt; visible helper for inverse IsVisible bindings.</summary>
public sealed class RevealConverter : IValueConverter
{
	// isPasswordHidden == true  -> show the "reveal" (eye) glyph
	// isPasswordHidden == false -> show the "hide" glyph
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is bool hidden && hidden ? "" : "";

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

/// <summary>Maps a 0..1 progress value to a display percentage string.</summary>
public sealed class PercentConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is double d ? $"{d * 100:0}%" : "0%";

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

/// <summary>Maps a project status string to a themed colour.</summary>
public sealed class StatusColorConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
		return (value as string) switch
		{
			"On track" => Color.FromArgb(isDark ? "#5EC75E" : "#0E700E"),
			"At risk" => Color.FromArgb(isDark ? "#FF99A4" : "#C42B1C"),
			"Planning" => Color.FromArgb(isDark ? "#FCE100" : "#9D5D00"),
			_ => Color.FromArgb(isDark ? "#C7C7C7" : "#5E5E5E"),
		};
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
