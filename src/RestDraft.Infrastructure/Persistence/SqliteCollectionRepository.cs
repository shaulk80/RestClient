using Microsoft.Extensions.Logging;
using RestDraft.Application.Abstractions;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Results;
using RestDraft.Infrastructure.Persistence.Mappers;
using RestDraft.Infrastructure.Persistence.Storage;

namespace RestDraft.Infrastructure.Persistence;

public sealed class SqliteCollectionRepository : ICollectionRepository
{
    private static readonly Action<ILogger, Exception?> LoadFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1, nameof(LoadFailed)),
        "Collections could not be loaded");

    private static readonly Action<ILogger, Exception?> SaveFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(2, nameof(SaveFailed)),
        "Collections could not be saved");

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ILogger<SqliteCollectionRepository> _logger;

    public SqliteCollectionRepository(SqliteConnectionFactory connectionFactory, ILogger<SqliteCollectionRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<Collection>>> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            var dtos = await CollectionDtoReader.ReadAsync(connection, cancellationToken);
            return dtos.IsSuccess
                ? CollectionMapper.ToDomain(dtos.Value!)
                : Result<IReadOnlyList<Collection>>.Failure(dtos.Error!);
        }
        catch (Exception exception)
        {
            LoadFailed(_logger, exception);
            return Result<IReadOnlyList<Collection>>.Failure(
                new Error(ErrorCategory.Storage, "The collections could not be loaded.", exception.ToString()));
        }
    }

    public async Task<Result> SaveAsync(IReadOnlyList<Collection> collections, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var dtos = collections.Select(CollectionMapper.ToDto).ToList();
            await CollectionDtoWriter.WriteAsync(connection, dtos, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception)
        {
            SaveFailed(_logger, exception);
            return Result.Failure(new Error(ErrorCategory.Storage, "The collections could not be saved.", exception.ToString()));
        }
    }
}
