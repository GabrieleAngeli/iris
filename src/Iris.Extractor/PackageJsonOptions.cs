using System.Text.Json;

namespace Iris.Extractor;

/// <summary>camelCase, matching what ASP.NET Core minimal APIs expect on <c>Iris.Api</c>.</summary>
internal static class PackageJsonOptions
{
    public static readonly JsonSerializerOptions Instance = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}
