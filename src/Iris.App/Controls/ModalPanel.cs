namespace Iris.App.Controls;

/// <summary>
/// A lightweight modal: a dimming scrim plus a centred card. Drop it as the last child of a
/// page-level <see cref="Grid"/> and bind <see cref="IsOpen"/> to a view-model flag. The card
/// body is the control's XAML content; the header shows <see cref="HeaderText"/> and a close
/// button, and both the button and a tap on the scrim invoke <see cref="CloseCommand"/>.
/// </summary>
[ContentProperty(nameof(Body))]
public sealed class ModalPanel : ContentView
{
	public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
		nameof(IsOpen), typeof(bool), typeof(ModalPanel), false,
		propertyChanged: OnIsOpenChanged);

	public static readonly BindableProperty HeaderTextProperty = BindableProperty.Create(
		nameof(HeaderText), typeof(string), typeof(ModalPanel), string.Empty,
		propertyChanged: (b, _, n) => ((ModalPanel)b)._header.Text = (string)n ?? string.Empty);

	public static readonly BindableProperty CloseCommandProperty = BindableProperty.Create(
		nameof(CloseCommand), typeof(System.Windows.Input.ICommand), typeof(ModalPanel));

	public static readonly BindableProperty BodyProperty = BindableProperty.Create(
		nameof(Body), typeof(View), typeof(ModalPanel),
		propertyChanged: (b, _, n) => ((ModalPanel)b)._bodyHost.Content = (View?)n);

	public static readonly BindableProperty CardWidthProperty = BindableProperty.Create(
		nameof(CardWidth), typeof(double), typeof(ModalPanel), 720d,
		propertyChanged: (b, _, n) => ((ModalPanel)b)._card.MaximumWidthRequest = (double)n);

	public bool IsOpen
	{
		get => (bool)GetValue(IsOpenProperty);
		set => SetValue(IsOpenProperty, value);
	}

	public string HeaderText
	{
		get => (string)GetValue(HeaderTextProperty);
		set => SetValue(HeaderTextProperty, value);
	}

	public System.Windows.Input.ICommand? CloseCommand
	{
		get => (System.Windows.Input.ICommand?)GetValue(CloseCommandProperty);
		set => SetValue(CloseCommandProperty, value);
	}

	public View? Body
	{
		get => (View?)GetValue(BodyProperty);
		set => SetValue(BodyProperty, value);
	}

	public double CardWidth
	{
		get => (double)GetValue(CardWidthProperty);
		set => SetValue(CardWidthProperty, value);
	}

	private readonly Label _header;
	private readonly ContentView _bodyHost;
	private readonly Border _card;

	public ModalPanel()
	{
		_header = new Label { FontSize = 17 };
		ApplyStyle(_header, "Subtitle");

		var close = new Button
		{
			Text = "",
			FontFamily = FontIcons(),
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Center,
		};
		ApplyStyle(close, "LinkButton");
		close.Clicked += (_, _) => CloseCommand?.Execute(null);

		_bodyHost = new ContentView();

		var layout = new Grid
		{
			RowDefinitions =
			{
				new RowDefinition(GridLength.Auto),
				new RowDefinition(GridLength.Star),
			},
			RowSpacing = 12,
		};

		var headerRow = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto),
			},
		};
		headerRow.Add(_header, 0, 0);
		headerRow.Add(close, 1, 0);
		layout.Add(headerRow, 0, 0);

		layout.Add(new ScrollView { Content = _bodyHost }, 0, 1);

		_card = new Border
		{
			MaximumWidthRequest = CardWidth,
			MaximumHeightRequest = 620,
			Margin = new Thickness(24),
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			Content = layout,
		};
		ApplyStyle(_card, "Card");

		var scrim = new BoxView { BackgroundColor = Color.FromArgb("#99000000") };
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => CloseCommand?.Execute(null);
		scrim.GestureRecognizers.Add(tap);

		Content = new Grid { Children = { scrim, _card } };

		Opacity = 0;
		IsVisible = false;
	}

	private static string FontIcons() =>
		Application.Current?.Resources.TryGetValue("FontIcons", out var f) == true && f is string s
			? s
			: "Segoe Fluent Icons";

	private static void ApplyStyle(VisualElement element, string key)
	{
		if (Application.Current?.Resources.TryGetValue(key, out var style) == true && style is Style s)
		{
			element.Style = s;
		}
	}

	private static async void OnIsOpenChanged(BindableObject bindable, object oldValue, object newValue)
	{
		var view = (ModalPanel)bindable;
		var open = (bool)newValue;

		if (open)
		{
			view.Opacity = 0;
			view.IsVisible = true;
			await view.FadeTo(1, 150, Easing.CubicOut);
		}
		else
		{
			await view.FadeTo(0, 120, Easing.CubicIn);
			view.IsVisible = false;
		}
	}
}
