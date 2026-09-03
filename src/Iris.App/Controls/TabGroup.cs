using System.Collections;
using System.Collections.Specialized;
using System.Text;
using Microsoft.Maui.Controls.Shapes;

namespace Iris.App.Controls;

public enum TabContentBlockKind
{
	Text,
	Code,
	Note,
}

public sealed class TabContentBlock
{
	public TabContentBlockKind Kind { get; init; } = TabContentBlockKind.Text;

	public string Title { get; init; } = string.Empty;

	public string Language { get; init; } = string.Empty;

	public string Text { get; init; } = string.Empty;
}

public sealed class TabGroupItem
{
	public string Title { get; init; } = string.Empty;

	public string Content { get; init; } = string.Empty;

	public IReadOnlyList<TabContentBlock> Blocks { get; init; } = [];

	public string PlainText()
	{
		if (Blocks.Count == 0)
		{
			return Content;
		}

		var text = new StringBuilder();
		foreach (var block in Blocks)
		{
			if (!string.IsNullOrWhiteSpace(block.Title))
			{
				text.AppendLine(block.Title);
			}

			if (!string.IsNullOrWhiteSpace(block.Text))
			{
				text.AppendLine(block.Text);
			}

			text.AppendLine();
		}

		return text.ToString().TrimEnd();
	}
}

public sealed class TabGroup : ContentView
{
	public static readonly BindableProperty TitleProperty = BindableProperty.Create(
		nameof(Title),
		typeof(string),
		typeof(TabGroup),
		string.Empty,
		propertyChanged: (bindable, _, _) => ((TabGroup)bindable).Rebuild());

	public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
		nameof(ItemsSource),
		typeof(IEnumerable),
		typeof(TabGroup),
		propertyChanged: OnItemsSourceChanged);

	public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
		nameof(SelectedIndex),
		typeof(int),
		typeof(TabGroup),
		0,
		BindingMode.TwoWay,
		propertyChanged: (bindable, _, _) => ((TabGroup)bindable).UpdateSelection());

	private readonly Border _container;
	private readonly Grid _tabs = new() { RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(2)) };
	private readonly VerticalStackLayout _content = new() { Spacing = 14 };
	private readonly List<(Label Label, BoxView Indicator)> _tabVisuals = [];
	private INotifyCollectionChanged? _collectionChanged;

	public TabGroup()
	{
		_container = new Border
		{
			Padding = new Thickness(24, 22),
			StrokeThickness = 1,
			StrokeShape = new RoundRectangle { CornerRadius = 10 },
			Content = BuildLayout(),
		};

		SetAppThemeColor(_container, Border.BackgroundColorProperty, "LayerLight", "LayerDark");
		_container.Stroke = new SolidColorBrush(ThemeColor("ControlStrokeLight", "ControlStrokeDark"));

		Content = _container;
	}

	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public IEnumerable? ItemsSource
	{
		get => (IEnumerable?)GetValue(ItemsSourceProperty);
		set => SetValue(ItemsSourceProperty, value);
	}

	public int SelectedIndex
	{
		get => (int)GetValue(SelectedIndexProperty);
		set => SetValue(SelectedIndexProperty, value);
	}

	private View BuildLayout()
	{
		var header = new Grid
		{
			ColumnDefinitions =
			[
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto),
			],
			ColumnSpacing = 12,
		};

		var title = new Label
		{
			Text = Title,
			FontSize = 14,
			VerticalTextAlignment = TextAlignment.Center,
		};
		SetAppThemeColor(title, Label.TextColorProperty, "TextSecondaryLight", "TextSecondaryDark");
		title.SetBinding(Label.TextProperty, new Binding(nameof(Title), source: this));
		header.Add(title);

		var actions = new HorizontalStackLayout { Spacing = 8 };
		foreach (var (glyph, action) in new (string Glyph, EventHandler? Action)[]
		{
			("\uE71B", null),
			("\uE943", OnCopySelectedContent),
			("\uE8A7", null),
		})
		{
			var button = new Button
			{
				Text = glyph,
				FontFamily = "Segoe Fluent Icons, Segoe MDL2 Assets",
				WidthRequest = 28,
				HeightRequest = 28,
				Padding = 0,
				BackgroundColor = Colors.Transparent,
				BorderWidth = 0,
				TextColor = ThemeColor("TextSecondaryLight", "TextSecondaryDark"),
			};

			if (action is not null)
			{
				button.Clicked += action;
			}

			actions.Add(button);
		}

		header.Add(actions, 1);

		return new VerticalStackLayout
		{
			Spacing = 18,
			Children =
			{
				header,
				_tabs,
				_content,
			},
		};
	}

	private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
	{
		var tabGroup = (TabGroup)bindable;
		if (tabGroup._collectionChanged is not null)
		{
			tabGroup._collectionChanged.CollectionChanged -= tabGroup.OnCollectionChanged;
			tabGroup._collectionChanged = null;
		}

		if (newValue is INotifyCollectionChanged collectionChanged)
		{
			tabGroup._collectionChanged = collectionChanged;
			collectionChanged.CollectionChanged += tabGroup.OnCollectionChanged;
		}

		tabGroup.Rebuild();
	}

	private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

	private void Rebuild()
	{
		if (_tabs is null)
		{
			return;
		}

		var items = Items().ToArray();
		_tabs.Clear();
		_tabs.ColumnDefinitions.Clear();
		_tabVisuals.Clear();

		for (var index = 0; index < items.Length; index++)
		{
			_tabs.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			var item = items[index];
			var tabIndex = index;

			var label = new Label
			{
				Text = item.Title,
				FontSize = 13,
				FontFamily = "Segoe UI Variable Text Semibold, OpenSansSemibold",
				HorizontalTextAlignment = TextAlignment.Center,
				VerticalTextAlignment = TextAlignment.Center,
				HeightRequest = 36,
			};

			var indicator = new BoxView { HeightRequest = 2, Color = Colors.Transparent };
			var button = new Button
			{
				Text = item.Title,
				BackgroundColor = Colors.Transparent,
				TextColor = Colors.Transparent,
				BorderWidth = 0,
				Padding = 0,
			};
			button.Clicked += (_, _) => SelectedIndex = tabIndex;

			var cell = new Grid();
			cell.Add(label);
			cell.Add(button);

			_tabs.Add(cell, index);
			_tabs.Add(indicator, index, 1);
			_tabVisuals.Add((label, indicator));
		}

		UpdateSelection();
	}

	private void UpdateSelection()
	{
		var items = Items().ToArray();
		if (items.Length == 0)
		{
			_content.Clear();
			return;
		}

		var selected = Math.Clamp(SelectedIndex, 0, items.Length - 1);
		if (selected != SelectedIndex)
		{
			SelectedIndex = selected;
			return;
		}

		for (var index = 0; index < _tabVisuals.Count; index++)
		{
			var (label, indicator) = _tabVisuals[index];
			var active = index == selected;
			label.TextColor = active
				? ThemeColor("TextPrimaryLight", "TextPrimaryDark")
				: ThemeColor("TextSecondaryLight", "TextSecondaryDark");
			indicator.Color = active
				? ThemeColor("AccentLight", "AccentDark")
				: ThemeColor("StrokeLight", "StrokeDark");
		}

		RenderContent(items[selected]);
	}

	private void RenderContent(TabGroupItem item)
	{
		_content.Clear();

		if (item.Blocks.Count > 0)
		{
			foreach (var block in item.Blocks)
			{
				_content.Add(BuildContentBlock(block));
			}

			return;
		}

		_content.Add(BuildTextBlock(new TabContentBlock { Text = item.Content }));
	}

	private View BuildContentBlock(TabContentBlock block) =>
		block.Kind switch
		{
			TabContentBlockKind.Code => BuildCodeBlock(block),
			TabContentBlockKind.Note => BuildNoteBlock(block),
			_ => BuildTextBlock(block),
		};

	private View BuildTextBlock(TabContentBlock block)
	{
		var stack = new VerticalStackLayout { Spacing = 6 };
		if (!string.IsNullOrWhiteSpace(block.Title))
		{
			var title = new Label
			{
				Text = block.Title,
				FontSize = 13,
				FontFamily = "Segoe UI Variable Text Semibold, OpenSansSemibold",
			};
			SetAppThemeColor(title, Label.TextColorProperty, "TextPrimaryLight", "TextPrimaryDark");
			stack.Add(title);
		}

		if (!string.IsNullOrWhiteSpace(block.Text))
		{
			var body = new Label
			{
				Text = block.Text.Trim(),
				FontSize = 13,
				LineBreakMode = LineBreakMode.WordWrap,
			};
			SetAppThemeColor(body, Label.TextColorProperty, "TextSecondaryLight", "TextSecondaryDark");
			stack.Add(body);
		}

		return stack;
	}

	private View BuildCodeBlock(TabContentBlock block)
	{
		var stack = new VerticalStackLayout { Spacing = 8 };
		stack.Add(new CodeBlock
		{
			Title = block.Title,
			Text = block.Text,
			Language = block.Language,
		});
		return stack;
	}

	private View BuildNoteBlock(TabContentBlock block)
	{
		var stack = new VerticalStackLayout { Spacing = 6 };
		if (!string.IsNullOrWhiteSpace(block.Title))
		{
			var title = new Label
			{
				Text = block.Title,
				FontSize = 12,
				FontFamily = "Segoe UI Variable Text Semibold, OpenSansSemibold",
			};
			SetAppThemeColor(title, Label.TextColorProperty, "TextPrimaryLight", "TextPrimaryDark");
			stack.Add(title);
		}

		var note = new Label
		{
			Text = block.Text.Trim(),
			FontSize = 13,
			LineBreakMode = LineBreakMode.WordWrap,
		};
		SetAppThemeColor(note, Label.TextColorProperty, "TextPrimaryLight", "TextPrimaryDark");
		stack.Add(note);

		var border = new Border
		{
			Padding = new Thickness(14, 12),
			StrokeThickness = 1,
			StrokeShape = new RoundRectangle { CornerRadius = 8 },
			Content = stack,
		};
		SetAppThemeColor(border, Border.BackgroundColorProperty, "SubtleFillLight", "SubtleFillDark");
		border.Stroke = new SolidColorBrush(ThemeColor("AccentLight", "AccentDark"));

		return border;
	}

	private IEnumerable<TabGroupItem> Items()
	{
		if (ItemsSource is null)
		{
			yield break;
		}

		foreach (var item in ItemsSource)
		{
			if (item is TabGroupItem tab)
			{
				yield return tab;
			}
		}
	}

	private async void OnCopySelectedContent(object? sender, EventArgs e)
	{
		var item = Items().ElementAtOrDefault(Math.Max(0, SelectedIndex));
		var text = item?.PlainText();
		if (!string.IsNullOrEmpty(text))
		{
			await Clipboard.Default.SetTextAsync(text);
		}
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
