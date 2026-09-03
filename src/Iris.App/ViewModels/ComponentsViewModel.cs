using System.Collections.ObjectModel;
using Iris.App.Controls;

namespace Iris.App.ViewModels;

public partial class ComponentsViewModel : ObservableObject
{
	public ObservableCollection<PersonItem> People { get; } = [];
	public ObservableCollection<string> Countries { get; } = [];
	public ObservableCollection<TabGroupItem> GalleryTabs { get; } =
	[
		new() { Title = "First", Content = "Content 1" },
		new() { Title = "Second", Content = "Content 2" },
		new() { Title = "Third", Content = "Content 3" },
	];
	public string CodeBlockSample { get; } = """
		const codeViewer = createCodeViewer(container, report);
		codeViewer.on('cursor', (loc) => {
		  // loc: { line, column, position, start?, end? }
		});
		""";

	[ObservableProperty] private bool _isLoading = true;
	[ObservableProperty] private int _selectedTabIndex;

	[ObservableProperty] private bool _switchOn = true;
	[ObservableProperty] private bool _checkboxChecked = true;
	[ObservableProperty] private double _sliderValue = 42;
	[ObservableProperty] private double _progress = 0.62;
	[ObservableProperty] private bool _isActivityRunning = true;
	[ObservableProperty] private string _selectedCountry = "Italy";
	[ObservableProperty] private DateTime _selectedDate = DateTime.Today;
	[ObservableProperty] private TimeSpan _selectedTime = new(9, 30, 0);
	[ObservableProperty] private string _entryText = string.Empty;
	[ObservableProperty] private string _editorText = "Multi-line editor content…";
	[ObservableProperty] private string _lastAction = "No action yet";

	/// <summary>Simulates fetching the gallery data so the loading splash has something to cover.</summary>
	[RelayCommand]
	private async Task LoadAsync()
	{
		if (People.Count > 0)
			return;

		IsLoading = true;
		await Task.Delay(1400);

		foreach (var person in new[]
		{
			new PersonItem("Giulia Ferri", "Product designer", "GF"),
			new PersonItem("Luca Bianchi", "Backend engineer", "LB"),
			new PersonItem("Sara Conti", "Data analyst", "SC"),
			new PersonItem("Marco De Luca", "Frontend engineer", "MD"),
		})
		{
			People.Add(person);
		}

		foreach (var country in new[] { "Italy", "France", "Germany", "Spain", "Portugal" })
			Countries.Add(country);

		SelectedCountry = "Italy";
		IsLoading = false;
	}

	[RelayCommand]
	private void PrimaryAction() => LastAction = $"Primary clicked at {DateTime.Now:HH:mm:ss}";

	[RelayCommand]
	private void SecondaryAction() => LastAction = $"Secondary clicked at {DateTime.Now:HH:mm:ss}";

	[RelayCommand]
	private void ToggleActivity() => IsActivityRunning = !IsActivityRunning;
}

public sealed record PersonItem(string Name, string Role, string Initials);
