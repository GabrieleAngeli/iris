namespace Iris.App.ViewModels;

/// <summary>Static how-to for downloading/using the Iris Extractor CLI. No API calls — the content
/// mirrors <c>docs/application-assimilation.md</c> so operators without repo access still see it.</summary>
public sealed partial class ExtractorGuideViewModel : ObservableObject
{
    public const string PackCommand = "dotnet pack src/Iris.Extractor -c Release -o ./nupkg";

    public const string InstallCommand = "dotnet tool install --global Iris.Extractor --add-source ./nupkg";

    public const string RunCommand = "iris-extractor dotnet --root src/MyApp --output iris-package.json";

    public const string PipelineSnippet = """
        - script: iris-extractor dotnet --root src/MyApp --output iris-package.json
          env:
            IRIS_API: $(IRIS_API)
            IRIS_APPLICATION_ID: $(IRIS_APPLICATION_ID)
            IRIS_VERSION_ID: $(IRIS_VERSION_ID)
            IRIS_TOKEN: $(IRIS_TOKEN)
        """;

    [RelayCommand]
    private async Task CopyAsync(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            await Clipboard.Default.SetTextAsync(text);
        }
    }
}
