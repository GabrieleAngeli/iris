using Microsoft.Maui.Controls.Shapes;

namespace Iris.App.Controls;

public sealed class CodeBlock : ContentView
{
	private const string CopyGlyph = "\uE8C8";
	private const string CopiedGlyph = "\uE73E";

	public static readonly BindableProperty TitleProperty = BindableProperty.Create(
		nameof(Title),
		typeof(string),
		typeof(CodeBlock),
		string.Empty,
		propertyChanged: (bindable, _, _) => ((CodeBlock)bindable).UpdateTitle());

	public static readonly BindableProperty TextProperty = BindableProperty.Create(
		nameof(Text),
		typeof(string),
		typeof(CodeBlock),
		string.Empty,
		propertyChanged: (bindable, _, _) => ((CodeBlock)bindable).UpdateText());

	public static readonly BindableProperty LanguageProperty = BindableProperty.Create(
		nameof(Language),
		typeof(string),
		typeof(CodeBlock),
		string.Empty,
		propertyChanged: (bindable, _, _) => ((CodeBlock)bindable).UpdateTitle());

	private readonly Label _title;
	private readonly Label _language;
	private readonly Button _copyButton;
	private readonly Editor _editor;
	private CancellationTokenSource? _copyFeedbackCancellation;

	public CodeBlock()
	{
		_title = new Label
		{
			FontSize = 12,
			FontFamily = "Segoe UI Variable Text Semibold, OpenSansSemibold",
			VerticalTextAlignment = TextAlignment.Center,
		};
		SetAppThemeColor(_title, Label.TextColorProperty, "TextSecondaryLight", "TextSecondaryDark");

		_language = new Label
		{
			FontSize = 11,
			VerticalTextAlignment = TextAlignment.Center,
		};
		SetAppThemeColor(_language, Label.TextColorProperty, "TextSecondaryLight", "TextSecondaryDark");

		_copyButton = new Button
		{
			Text = CopyGlyph,
			FontFamily = "Segoe Fluent Icons, Segoe MDL2 Assets",
			WidthRequest = 30,
			HeightRequest = 30,
			Padding = 0,
			BackgroundColor = Colors.Transparent,
			BorderWidth = 0,
			TextColor = ThemeColor("TextSecondaryLight", "TextSecondaryDark"),
		};
		ToolTipProperties.SetText(_copyButton, "Copy code");
		_copyButton.Clicked += OnCopyClicked;

		var header = new Grid
		{
			ColumnDefinitions =
			[
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(GridLength.Auto),
			],
			ColumnSpacing = 10,
			Padding = new Thickness(0, 0, 0, 8),
		};
		header.Add(_title);
		header.Add(_language, 1);
		header.Add(_copyButton, 2);

		_editor = new Editor
		{
			IsReadOnly = true,
			AutoSize = EditorAutoSizeOption.Disabled,
			FontFamily = "Consolas, Courier New",
			FontSize = 12,
			BackgroundColor = Colors.Transparent,
			Margin = 0,
			MinimumHeightRequest = 44,
			Text = string.Empty,
		};
		SetAppThemeColor(_editor, Editor.TextColorProperty, "TextPrimaryLight", "TextPrimaryDark");

		var body = new ScrollView
		{
			Orientation = ScrollOrientation.Horizontal,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Default,
			Content = _editor,
		};

		var layout = new VerticalStackLayout
		{
			Spacing = 0,
			Children =
			{
				header,
				body,
			},
		};

		var border = new Border
		{
			Padding = new Thickness(16, 12),
			StrokeThickness = 1,
			StrokeShape = new RoundRectangle { CornerRadius = 8 },
			Content = layout,
		};
		SetAppThemeColor(border, Border.BackgroundColorProperty, "LayerAltLight", "LayerAltDark");
		border.Stroke = new SolidColorBrush(ThemeColor("StrokeLight", "StrokeDark"));

		Content = border;
		UpdateTitle();
		UpdateText();
	}

	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public string Text
	{
		get => (string)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public string Language
	{
		get => (string)GetValue(LanguageProperty);
		set => SetValue(LanguageProperty, value);
	}

	private void UpdateTitle()
	{
		_title.Text = Title;
		_title.IsVisible = !string.IsNullOrWhiteSpace(Title);
		_language.Text = Language;
		_language.IsVisible = !string.IsNullOrWhiteSpace(Language);
	}

	private void UpdateText()
	{
		var text = Text.Trim();
		_editor.Text = text;
		_editor.HeightRequest = CalculateHeight(text);
	}

	private static double CalculateHeight(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 44;
		}

		var lines = text.Count(static c => c == '\n') + 1;
		return Math.Clamp(22 + (lines * 18), 56, 420);
	}

	private async void OnCopyClicked(object? sender, EventArgs e)
	{
		if (!string.IsNullOrWhiteSpace(Text))
		{
			await Clipboard.Default.SetTextAsync(Text.Trim());
			await ShowCopiedFeedbackAsync();
		}
	}

	private async Task ShowCopiedFeedbackAsync()
	{
		_copyFeedbackCancellation?.Cancel();
		_copyFeedbackCancellation?.Dispose();

		var feedbackCancellation = new CancellationTokenSource();
		_copyFeedbackCancellation = feedbackCancellation;

		_copyButton.Text = CopiedGlyph;
		_copyButton.TextColor = ThemeColor("Success", "SuccessDark");
		ToolTipProperties.SetText(_copyButton, "Copied");

		try
		{
			await Task.Delay(1400, feedbackCancellation.Token);
		}
		catch (OperationCanceledException)
		{
			return;
		}

		if (_copyFeedbackCancellation == feedbackCancellation)
		{
			ResetCopyButton();
			_copyFeedbackCancellation.Dispose();
			_copyFeedbackCancellation = null;
		}
	}

	private void ResetCopyButton()
	{
		_copyButton.Text = CopyGlyph;
		_copyButton.TextColor = ThemeColor("TextSecondaryLight", "TextSecondaryDark");
		ToolTipProperties.SetText(_copyButton, "Copy code");
	}

	private static void SetAppThemeColor(BindableObject target, BindableProperty property, string lightKey, string darkKey)
	{
		if (Application.Current?.Resources.TryGetValue(lightKey, out var light) == true &&
			Application.Current.Resources.TryGetValue(darkKey, out var dark) == true &&
			light is Color lightColor &&
			dark is Color darkColor)
		{
			target.SetAppThemeColor(property, lightColor, darkColor);
		}
	}

	private static Color ResourceColor(string key) =>
		Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
			? color
			: Colors.Transparent;

	private static Color ThemeColor(string lightKey, string darkKey) =>
		Application.Current?.RequestedTheme == AppTheme.Dark
			? ResourceColor(darkKey)
			: ResourceColor(lightKey);
}
