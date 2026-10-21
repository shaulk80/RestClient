using Microsoft.Data.Sqlite;

namespace RestClient.Infrastructure.Persistence.Migrations;

public sealed class Migration0001Settings : IMigration
{
    public int Version => 1;

    public async Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE settings (
                key   TEXT NOT NULL PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
