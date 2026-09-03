namespace Iris.Extractor.DotNet;

internal static class PathFiltering
{
    private static readonly string[] ExcludedDirectoryNames = ["bin", "obj", ".git", "node_modules"];

    public static bool IsExcluded(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => ExcludedDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }
}
