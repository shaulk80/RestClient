using Microsoft.Data.Sqlite;

namespace RestDraft.Infrastructure.Persistence;

public interface IMigration
{
    int Version { get; }

    Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken);
}
