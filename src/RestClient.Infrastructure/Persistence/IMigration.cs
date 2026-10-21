using Microsoft.Data.Sqlite;

namespace RestClient.Infrastructure.Persistence;

public interface IMigration
{
    int Version { get; }

    Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken);
}
