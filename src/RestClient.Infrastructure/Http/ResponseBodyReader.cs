using System.Text;
using Microsoft.Extensions.Logging;
using RestClient.Application.Abstractions;
using RestClient.Domain.Results;

namespace RestClient.Infrastructure.Http;

public sealed class ResponseBodyReader
{
    private const int PreviewLimitBytes = 1024 * 1024;
    private const int ChunkSizeBytes = 80 * 1024;
    private const string TempFolderName = "temp";

    private static readonly Action<ILogger, string, Exception?> SpillFailed = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(3, nameof(SpillFailed)),
        "Response body could not be saved to disk: {Reason}");

    private readonly InfrastructureOptions _options;
    private readonly IIdGenerator _idGenerator;
    private readonly ILogger<ResponseBodyReader> _logger;

    public ResponseBodyReader(InfrastructureOptions options, IIdGenerator idGenerator, ILogger<ResponseBodyReader> logger)
    {
        _options = options;
        _idGenerator = idGenerator;
        _logger = logger;
    }

    public async Task<Result<ResponseBody>> ReadAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var exceededLimit = await ReadUntilLimitExceededAsync(stream, buffer, cancellationToken);
        if (!exceededLimit)
        {
            return Result<ResponseBody>.Success(new ResponseBody(
                Preview(buffer),
                buffer.Length,
                null,
                buffer.Length > PreviewLimitBytes));
        }

        return await SpillAsync(stream, buffer, cancellationToken);
    }

    private static async Task<bool> ReadUntilLimitExceededAsync(Stream source, MemoryStream destination, CancellationToken cancellationToken)
    {
        var chunk = new byte[ChunkSizeBytes];
        while (destination.Length <= PreviewLimitBytes)
        {
            var read = await source.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                return false;
            }

            await destination.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return true;
    }

    private async Task<Result<ResponseBody>> SpillAsync(Stream source, MemoryStream buffered, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(_options.DataFolder, TempFolderName);
        var path = Path.Combine(folder, $"{_idGenerator.NewId():N}.body");
        try
        {
            Directory.CreateDirectory(folder);
            await using var file = File.Create(path);
            buffered.Position = 0;
            await buffered.CopyToAsync(file, cancellationToken);
            await source.CopyToAsync(file, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SpillFailed(_logger, exception.Message, exception);
            return Result<ResponseBody>.Failure(new Error(
                ErrorCategory.Storage,
                "The response body could not be saved to disk.",
                exception.Message));
        }

        return Result<ResponseBody>.Success(new ResponseBody(Preview(buffered), new FileInfo(path).Length, path, true));
    }

    private static string Preview(MemoryStream buffer)
    {
        var length = (int)Math.Min(buffer.Length, PreviewLimitBytes);
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, length);
    }
}
