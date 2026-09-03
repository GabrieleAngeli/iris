using System.CommandLine;
using System.Text.Json;
using Iris.Extractor;
using Iris.Extractor.DotNet;

var rootOption = new Option<string>("--root")
{
    Description = "Path to the .NET application's source tree to scan.",
    Required = true,
};
var outputOption = new Option<string>("--output")
{
    Description = "Where to write the configuration package JSON.",
    DefaultValueFactory = _ => "iris-package.json",
};
var schemaVersionOption = new Option<string>("--schema-version")
{
    DefaultValueFactory = _ => "1.0",
};
var apiOption = new Option<string?>("--api")
{
    Description = "Iris API base URL. Also read from IRIS_API. Upload is skipped unless --api, " +
                  "--application-id, --version-id and --token are all provided.",
};
var applicationIdOption = new Option<string?>("--application-id")
{
    Description = "Target ApplicationDefinition id. Also read from IRIS_APPLICATION_ID.",
};
var versionIdOption = new Option<string?>("--version-id")
{
    Description = "Target ApplicationVersion id. Also read from IRIS_VERSION_ID.",
};
var tokenOption = new Option<string?>("--token")
{
    Description = "Bearer session token with the applications.import permission. Also read from IRIS_TOKEN.",
};

var dotNetCommand = new Command("dotnet", "Extract configuration knowledge from a .NET application's source tree.")
{
    rootOption, outputOption, schemaVersionOption, apiOption, applicationIdOption, versionIdOption, tokenOption,
};

dotNetCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var root = Path.GetFullPath(parseResult.GetValue(rootOption)!);
    if (!Directory.Exists(root))
    {
        Console.Error.WriteLine($"Root path not found: {root}");
        return 1;
    }

    var package = DotNetExtractor.Extract(root, parseResult.GetValue(schemaVersionOption)!);

    var output = parseResult.GetValue(outputOption)!;
    await File.WriteAllTextAsync(
        output,
        JsonSerializer.Serialize(package, PackageJsonOptions.Instance),
        cancellationToken).ConfigureAwait(false);

    Console.WriteLine(
        $"Wrote {Path.GetFullPath(output)}: {package.ConfigurationKeys.Count} configuration key(s), " +
        $"{package.Dependencies.Count} dependenc(y/ies), {package.Warnings?.Count ?? 0} warning(s).");

    var api = parseResult.GetValue(apiOption) ?? Environment.GetEnvironmentVariable("IRIS_API");
    var applicationId = parseResult.GetValue(applicationIdOption) ?? Environment.GetEnvironmentVariable("IRIS_APPLICATION_ID");
    var versionId = parseResult.GetValue(versionIdOption) ?? Environment.GetEnvironmentVariable("IRIS_VERSION_ID");
    var token = parseResult.GetValue(tokenOption) ?? Environment.GetEnvironmentVariable("IRIS_TOKEN");

    var uploadRequested = new[] { api, applicationId, versionId, token }.Any(v => !string.IsNullOrWhiteSpace(v));
    if (!uploadRequested)
    {
        return 0;
    }

    if (string.IsNullOrWhiteSpace(api) || string.IsNullOrWhiteSpace(applicationId) ||
        string.IsNullOrWhiteSpace(versionId) || string.IsNullOrWhiteSpace(token))
    {
        Console.Error.WriteLine(
            "To upload, --api, --application-id, --version-id and --token " +
            "(or IRIS_API/IRIS_APPLICATION_ID/IRIS_VERSION_ID/IRIS_TOKEN) must all be set.");
        return 1;
    }

    var uploaded = await new IrisUploadClient(api)
        .UploadAsync(applicationId, versionId, token, package, cancellationToken)
        .ConfigureAwait(false);
    return uploaded ? 0 : 1;
});

var root = new RootCommand("Iris Extractor — statically extracts configuration knowledge for import into Iris.")
{
    dotNetCommand,
};

return await root.Parse(args).InvokeAsync();
