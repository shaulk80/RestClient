using Microsoft.Extensions.Logging;
using RestDraft.Application.Abstractions;
using RestDraft.Domain.Results;

namespace RestDraft.Infrastructure.Services;

public sealed class LocalFileReader : IFileReader
{
    private static readonly Action<ILogger, string, Exception?> ReadFailed = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(5, nameof(ReadFailed)),
        "File could not be read for a request body: {Reason}");

    private readonly ILogger<LocalFileReader> _logger;

    public LocalFileReader(ILogger<LocalFileReader> logger)
    {
        _logger = logger;
    }

    public async Task<Result<byte[]>> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            return Result<byte[]>.Success(bytes);
        }
        catch (FileNotFoundException)
        {
            return Result<byte[]>.Failure(new Error(ErrorCategory.NotFound, "The file for a multipart part was not found.", path));
        }
        catch (DirectoryNotFoundException)
        {
            return Result<byte[]>.Failure(new Error(ErrorCategory.NotFound, "The file for a multipart part was not found.", path));
        }
        catch (OperationCanceledException)
        {
            return Result<byte[]>.Failure(new Error(ErrorCategory.Cancelled, "Reading the file was cancelled."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            ReadFailed(_logger, exception.Message, exception);
            return Result<byte[]>.Failure(new Error(ErrorCategory.Storage, "The file for a multipart part could not be read.", exception.Message));
        }
    }
}
