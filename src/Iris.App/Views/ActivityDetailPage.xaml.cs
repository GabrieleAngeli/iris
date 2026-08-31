using System.Web;

namespace Iris.App.Views;

[QueryProperty(nameof(Title), "Title")]
[QueryProperty(nameof(Description), "Description")]
[QueryProperty(nameof(Timestamp), "Timestamp")]
[QueryProperty(nameof(Category), "Category")]
public partial class ActivityDetailPage : ContentPage
{
	public ActivityDetailPage()
	{
		InitializeComponent();
	}

	private string _title = "Activity";
	public new string Title
	{
		get => _title;
		set { _title = Decode(value); TitleLabel.Text = _title; base.Title = _title; }
	}

	public string Description
	{
		set => DescriptionLabel.Text = Decode(value);
	}

	private string _timestamp = "";
	public string Timestamp
	{
		get => _timestamp;
		set { _timestamp = Decode(value); UpdateMeta(); }
	}

	private string _category = "";
	public string Category
	{
		get => _category;
		set { _category = Decode(value); UpdateMeta(); }
	}

	private void UpdateMeta() => MetaLabel.Text = $"{_category} · {_timestamp}";

	private static string Decode(string? value) =>
		string.IsNullOrEmpty(value) ? string.Empty : HttpUtility.UrlDecode(value);

	private async void OnBackClicked(object sender, EventArgs e) =>
		await Shell.Current.GoToAsync("..");
}
