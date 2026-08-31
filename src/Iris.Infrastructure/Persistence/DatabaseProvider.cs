namespace Iris.Infrastructure.Persistence;

/// <summary>Supported EF Core providers, selected via <c>Iris:Database:Provider</c>.</summary>
public enum DatabaseProvider
{
    Sqlite = 0,
    Postgres = 1,
}

internal static class DatabaseProviderParser
{
    public static DatabaseProvider Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "sqlite" => DatabaseProvider.Sqlite,
        "postgres" or "postgresql" or "npgsql" => DatabaseProvider.Postgres,
        _ => throw new InvalidOperationException(
            $"Unknown database provider '{value}'. Use 'Sqlite' or 'Postgres'."),
    };
}
