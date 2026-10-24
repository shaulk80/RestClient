using Microsoft.Data.Sqlite;
using RestClient.Infrastructure.Persistence.Dto;

namespace RestClient.Infrastructure.Persistence.Storage;

public sealed class CollectionDtoWriter
{
    private const string DeleteRequestsSql = "DELETE FROM requests;";
    private const string DeleteFoldersSql = "DELETE FROM folders;";
    private const string DeleteCollectionsSql = "DELETE FROM collections;";
    private const string InsertCollectionSql = "INSERT INTO collections (id, name, position) VALUES ($id, $name, $position);";
    private const string InsertFolderSql = "INSERT INTO folders (id, collection_id, parent_folder_id, name, position) VALUES ($id, $collectionId, $parentId, $name, $position);";
    private const string InsertRequestSql = "INSERT INTO requests (id, folder_id, data, position) VALUES ($id, $folderId, $data, $position);";

    // Replaces the whole workspace. The caller runs this inside a transaction.
    public static async Task WriteAsync(SqliteConnection connection, IReadOnlyList<CollectionDto> collections, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, DeleteRequestsSql, cancellationToken);
        await ExecuteAsync(connection, DeleteFoldersSql, cancellationToken);
        await ExecuteAsync(connection, DeleteCollectionsSql, cancellationToken);

        for (var position = 0; position < collections.Count; position++)
        {
            var collection = collections[position];
            await InsertCollectionAsync(connection, collection, position, cancellationToken);
            await WriteFolderAsync(connection, collection.Root, collection.Id, null, 0, cancellationToken);
        }
    }

    private static async Task InsertCollectionAsync(SqliteConnection connection, CollectionDto collection, int position, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = InsertCollectionSql;
        command.Parameters.AddWithValue("$id", collection.Id);
        command.Parameters.AddWithValue("$name", collection.Name);
        command.Parameters.AddWithValue("$position", position);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task WriteFolderAsync(
        SqliteConnection connection,
        FolderDto folder,
        Guid collectionId,
        Guid? parentId,
        int position,
        CancellationToken cancellationToken)
    {
        await InsertFolderAsync(connection, folder, collectionId, parentId, position, cancellationToken);

        for (var index = 0; index < folder.Folders.Count; index++)
        {
            await WriteFolderAsync(connection, folder.Folders[index], collectionId, folder.Id, index, cancellationToken);
        }

        for (var index = 0; index < folder.Requests.Count; index++)
        {
            await InsertRequestAsync(connection, folder.Requests[index], folder.Id, index, cancellationToken);
        }
    }

    private static async Task InsertFolderAsync(
        SqliteConnection connection,
        FolderDto folder,
        Guid collectionId,
        Guid? parentId,
        int position,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = InsertFolderSql;
        command.Parameters.AddWithValue("$id", folder.Id);
        command.Parameters.AddWithValue("$collectionId", collectionId);
        command.Parameters.AddWithValue("$parentId", (object?)parentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", folder.Name);
        command.Parameters.AddWithValue("$position", position);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertRequestAsync(
        SqliteConnection connection,
        RequestDto request,
        Guid folderId,
        int position,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = InsertRequestSql;
        command.Parameters.AddWithValue("$id", request.Id);
        command.Parameters.AddWithValue("$folderId", folderId);
        command.Parameters.AddWithValue("$data", RequestJson.Serialize(request));
        command.Parameters.AddWithValue("$position", position);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
