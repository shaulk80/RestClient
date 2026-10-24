using Microsoft.Data.Sqlite;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Persistence.Dto;

namespace RestClient.Infrastructure.Persistence.Storage;

public sealed class CollectionDtoReader
{
    private const string SelectCollectionsSql = "SELECT id, name FROM collections ORDER BY position;";
    private const string SelectFoldersSql = "SELECT id, collection_id, parent_folder_id, name FROM folders ORDER BY position;";
    private const string SelectRequestsSql = "SELECT folder_id, data FROM requests ORDER BY position;";

    public static async Task<Result<IReadOnlyList<CollectionDto>>> ReadAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var collectionRows = await ReadCollectionRowsAsync(connection, cancellationToken);
        var folderRows = await ReadFolderRowsAsync(connection, cancellationToken);
        var requestRows = await ReadRequestRowsAsync(connection, cancellationToken);
        return Build(collectionRows, folderRows, requestRows);
    }

    private static Result<IReadOnlyList<CollectionDto>> Build(
        IReadOnlyList<CollectionRow> collectionRows,
        IReadOnlyList<FolderRow> folderRows,
        IReadOnlyList<RequestRow> requestRows)
    {
        var foldersById = folderRows.ToDictionary(row => row.Id, row => new FolderDto
        {
            Id = row.Id,
            Name = row.Name,
        });
        var rootsByCollection = new Dictionary<Guid, FolderDto>();
        foreach (var row in folderRows)
        {
            var folder = foldersById[row.Id];
            if (row.ParentFolderId is { } parentId)
            {
                foldersById[parentId].Folders.Add(folder);
            }
            else
            {
                rootsByCollection[row.CollectionId] = folder;
            }
        }

        foreach (var row in requestRows)
        {
            var request = RequestJson.Deserialize(row.Data);
            if (request is null)
            {
                return Result<IReadOnlyList<CollectionDto>>.Failure(new Error(ErrorCategory.Storage, "A stored request is empty."));
            }

            foldersById[row.FolderId].Requests.Add(request);
        }

        var collections = new List<CollectionDto>();
        foreach (var row in collectionRows)
        {
            if (!rootsByCollection.TryGetValue(row.Id, out var root))
            {
                return Result<IReadOnlyList<CollectionDto>>.Failure(new Error(ErrorCategory.Storage, "A stored collection has no root folder."));
            }

            collections.Add(new CollectionDto(row.Id, row.Name, root));
        }

        return Result<IReadOnlyList<CollectionDto>>.Success(collections);
    }

    private static async Task<List<CollectionRow>> ReadCollectionRowsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<CollectionRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = SelectCollectionsSql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CollectionRow(reader.GetGuid(0), reader.GetString(1)));
        }

        return rows;
    }

    private static async Task<List<FolderRow>> ReadFolderRowsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<FolderRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = SelectFoldersSql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var parentId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2);
            rows.Add(new FolderRow(reader.GetGuid(0), reader.GetGuid(1), parentId, reader.GetString(3)));
        }

        return rows;
    }

    private static async Task<List<RequestRow>> ReadRequestRowsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<RequestRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = SelectRequestsSql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new RequestRow(reader.GetGuid(0), reader.GetString(1)));
        }

        return rows;
    }
}
