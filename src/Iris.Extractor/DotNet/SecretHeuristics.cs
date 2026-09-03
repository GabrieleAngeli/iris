namespace Iris.Extractor.DotNet;

/// <summary>Best-effort guess at whether a configuration key holds a sensitive value, from its name alone.</summary>
internal static class SecretHeuristics
{
    private static readonly string[] Hints =
        ["password", "secret", "apikey", "api-key", "token", "connectionstring", "pwd"];

    public static bool LooksSecret(string key) =>
        Hints.Any(hint => key.Contains(hint, StringComparison.OrdinalIgnoreCase));
}
