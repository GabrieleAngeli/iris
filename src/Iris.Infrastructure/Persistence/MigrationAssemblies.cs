namespace Iris.Infrastructure.Persistence;

/// <summary>
/// Migrations are provider-specific, so each provider keeps its own set in its own
/// assembly. These names are resolved at runtime (the API project references both).
/// </summary>
public static class MigrationAssemblies
{
    public const string Sqlite = "Iris.Infrastructure";

    public const string Postgres = "Iris.Migrations.Postgres";

    public static string For(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Postgres => Postgres,
        _ => Sqlite,
    };
}
