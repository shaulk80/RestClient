using Microsoft.Extensions.Logging;
using RestDraft.Application.Abstractions;
using RestDraft.Domain.Results;

namespace RestDraft.Infrastructure.Http;

// Deletes only files inside the spill folder. A path that resolves elsewhere is refused, even if the database holds it.
public sealed class BodyFileStore : IBodyFileStore
{
    private static readonly Action<ILogger, string, Exception?> DeleteFailed = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(4, nameof(DeleteFailed)),
        "Response body file could not be deleted: {Reason}");

    private readonly string _spillFolder;
    private readonly ILogger<BodyFileStore> _logger;

    public BodyFileStore(InfrastructureOptions options, ILogger<BodyFileStore> logger)
    {
        _spillFolder = Path.GetFullPath(Path.Combine(options.DataFolder, ResponseBodyReader.TempFolderName));
        _logger = logger;
    }

    public Result Delete(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(_spillFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(new Error(ErrorCategory.Security, "The response body file is outside the data folder."));
        }

        if (!File.Exists(fullPath))
        {
            return Result.Success();
        }

        try
        {
            File.Delete(fullPath);
            return Result.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DeleteFailed(_logger, exception.Message, exception);
            return Result.Failure(new Error(ErrorCategory.Storage, "The response body file could not be deleted.", exception.Message));
        }
    }
}
