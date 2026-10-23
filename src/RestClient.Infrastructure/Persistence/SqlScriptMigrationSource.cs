using System.Globalization;
using Microsoft.Extensions.Logging;
using RestClient.Domain.Results;

namespace RestClient.Infrastructure.Persistence;

public sealed class SqlScriptMigrationSource : IMigrationSource
{
    private const string ScriptSearchPattern = "*.sql";
    private const int VersionFolderNameLength = 4;

    private static readonly Action<ILogger, string, Exception?> ScriptsRootMissing = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(3, nameof(ScriptsRootMissing)),
        "Migration scripts folder {ScriptsRoot} does not exist");

    private static readonly Action<ILogger, string, Exception?> FolderNameInvalid = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(4, nameof(FolderNameInvalid)),
        "Migration folder {FolderPath} is skipped: its name must be a 4-digit version number");

    private static readonly Action<ILogger, string, Exception?> FolderHasNoScripts = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(5, nameof(FolderHasNoScripts)),
        "Migration folder {FolderPath} is skipped: it contains no .sql files");

    private readonly string _scriptsRoot;
    private readonly ILogger<SqlScriptMigrationSource> _logger;

    public SqlScriptMigrationSource(string scriptsRoot, ILogger<SqlScriptMigrationSource> logger)
    {
        _scriptsRoot = scriptsRoot;
        _logger = logger;
    }

    public Result<IReadOnlyList<IMigration>> GetMigrations()
    {
        if (!Directory.Exists(_scriptsRoot))
        {
            ScriptsRootMissing(_logger, _scriptsRoot, null);
            return Result<IReadOnlyList<IMigration>>.Failure(
                new Error(ErrorCategory.Storage, "The database could not be migrated.", $"Migration scripts folder not found: {_scriptsRoot}"));
        }

        var migrations = new List<IMigration>();
        foreach (var folderPath in Directory.EnumerateDirectories(_scriptsRoot))
        {
            var migration = CreateMigration(folderPath);
            if (migration is not null)
            {
                migrations.Add(migration);
            }
        }

        return Result<IReadOnlyList<IMigration>>.Success(migrations);
    }

    private SqlScriptMigration? CreateMigration(string folderPath)
    {
        if (!TryParseVersion(Path.GetFileName(folderPath), out var version))
        {
            FolderNameInvalid(_logger, folderPath, null);
            return null;
        }

        var scriptPaths = Directory.EnumerateFiles(folderPath, ScriptSearchPattern, SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (scriptPaths.Count == 0)
        {
            FolderHasNoScripts(_logger, folderPath, null);
            return null;
        }

        return new SqlScriptMigration(version, scriptPaths, _logger);
    }

    private static bool TryParseVersion(string folderName, out int version)
    {
        version = 0;
        return folderName.Length == VersionFolderNameLength
            && int.TryParse(folderName, NumberStyles.None, CultureInfo.InvariantCulture, out version)
            && version > 0;
    }
}
