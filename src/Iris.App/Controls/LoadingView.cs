namespace Iris.App.Controls;

/// <summary>
/// A branded loading overlay. Drop it as the last child of a page-level Grid and
/// bind <see cref="IsActive"/> to a view-model flag; it fades a dimming scrim and
/// a Fluent card with a spinner over the page while work is in progress.
/// </summary>
public sealed class LoadingView : ContentView
{
	public static readonly BindableProperty IsActiveProperty = BindableProperty.Create(
		nameof(IsActive), typeof(bool), typeof(LoadingView), false, propertyChanged: OnIsActiveChanged);

	public static readonly BindableProperty MessageProperty = BindableProperty.Create(
		nameof(Message), typeof(string), typeof(LoadingView), "Loading…",
		propertyChanged: (b, _, n) => ((LoadingView)b)._message.Text = (string)n);

	public bool IsActive
	{
		get => (bool)GetValue(IsActiveProperty);
		set => SetValue(IsActiveProperty, value);
	}

	public string Message
	{
		get => (string)GetValue(MessageProperty);
		set => SetValue(MessageProperty, value);
	}

	private readonly ActivityIndicator _spinner;
	private readonly Label _message;

	public LoadingView()
	{
		_spinner = new ActivityIndicator { WidthRequest = 40, HeightRequest = 40 };

		_message = new Label
		{
			Text = "Loading…",
			HorizontalOptions = LayoutOptions.Center,
			HorizontalTextAlignment = TextAlignment.Center
		};
		ApplyStyle(_message, "Body");

		var caption = new Label
		{
			Text = "Please wait",
			HorizontalOptions = LayoutOptions.Center
		};
		ApplyStyle(caption, "Caption");

		var card = new Border
		{
			Padding = 28,
			WidthRequest = 260,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			Content = new VerticalStackLayout
			{
				Spacing = 14,
				HorizontalOptions = LayoutOptions.Center,
				Children = { _spinner, _message, caption }
			}
		};
		ApplyStyle(card, "Card");

		Content = new Grid
		{
			BackgroundColor = Color.FromArgb("#59000000"),
			Children = { card }
		};

		InputTransparent = false;
		Opacity = 0;
		IsVisible = false;
	}

	private static void ApplyStyle(VisualElement element, string key)
	{
		if (Application.Current?.Resources.TryGetValue(key, out var style) == true && style is Style s)
			element.Style = s;
	}

	private static async void OnIsActiveChanged(BindableObject bindable, object oldValue, object newValue)
	{
		var view = (LoadingView)bindable;
		var active = (bool)newValue;

		view._spinner.IsRunning = active;

		if (active)
		{
			view.Opacity = 0;
			view.IsVisible = true;
			await view.FadeTo(1, 180, Easing.CubicOut);
		}
		else
		{
			await view.FadeTo(0, 180, Easing.CubicIn);
			view.IsVisible = false;
		}
	}
}
