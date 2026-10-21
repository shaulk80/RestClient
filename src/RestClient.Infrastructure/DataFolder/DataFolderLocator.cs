using RestClient.Domain.Results;

namespace RestClient.Infrastructure.DataFolder;

public static class DataFolderLocator
{
    public const string DataDirArgument = "--data-dir";
    public const string DataDirEnvironmentVariable = "RESTCLIENT_DATA_DIR";

    private const string DefaultFolderName = "data";

    public static Result<string> Locate(IReadOnlyList<string> args, string? environmentValue, string executableDirectory)
    {
        var configuredPath = ReadArgumentValue(args) ?? NullIfBlank(environmentValue);
        var folderPath = configuredPath ?? Path.Combine(executableDirectory, DefaultFolderName);
        return EnsureFolder(folderPath);
    }

    private static string? ReadArgumentValue(IReadOnlyList<string> args)
    {
        var prefix = DataDirArgument + "=";
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument.StartsWith(prefix, StringComparison.Ordinal))
            {
                return NullIfBlank(argument[prefix.Length..]);
            }

            if (argument == DataDirArgument && index + 1 < args.Count)
            {
                return NullIfBlank(args[index + 1]);
            }
        }

        return null;
    }

    private static Result<string> EnsureFolder(string folderPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(folderPath);
            Directory.CreateDirectory(fullPath);
            return Result<string>.Success(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Result<string>.Failure(new Error(ErrorCategory.Storage, "The data folder cannot be created.", $"{folderPath}: {exception.Message}"));
        }
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
