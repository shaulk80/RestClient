using Microsoft.Extensions.Logging;
using RestClient.Application.Abstractions;
using RestClient.Domain.Environments;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Persistence.Mappers;
using RestClient.Infrastructure.Persistence.Storage;

namespace RestClient.Infrastructure.Persistence;

public sealed class SqliteEnvironmentRepository : IEnvironmentRepository
{
    private static readonly Action<ILogger, Exception?> LoadFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(20, nameof(LoadFailed)),
        "Environments could not be loaded");

    private static readonly Action<ILogger, Exception?> SaveFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(21, nameof(SaveFailed)),
        "Environments could not be saved");

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ILogger<SqliteEnvironmentRepository> _logger;

    public SqliteEnvironmentRepository(SqliteConnectionFactory connectionFactory, ILogger<SqliteEnvironmentRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<RequestEnvironment>>> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            var dtos = await EnvironmentDtoReader.ReadAsync(connection, cancellationToken);
            return dtos.IsSuccess
                ? Result<IReadOnlyList<RequestEnvironment>>.Success(dtos.Value!.Select(EnvironmentMapper.ToDomain).ToList())
                : Result<IReadOnlyList<RequestEnvironment>>.Failure(dtos.Error!);
        }
        catch (Exception exception)
        {
            LoadFailed(_logger, exception);
            return Result<IReadOnlyList<RequestEnvironment>>.Failure(
                new Error(ErrorCategory.Storage, "The environments could not be loaded.", exception.ToString()));
        }
    }

    public async Task<Result> SaveAsync(IReadOnlyList<RequestEnvironment> environments, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var dtos = environments.Select(EnvironmentMapper.ToDto).ToList();
            await EnvironmentDtoWriter.WriteAsync(connection, dtos, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception)
        {
            SaveFailed(_logger, exception);
            return Result.Failure(new Error(ErrorCategory.Storage, "The environments could not be saved.", exception.ToString()));
        }
    }
}
