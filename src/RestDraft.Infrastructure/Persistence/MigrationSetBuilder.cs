using Microsoft.Extensions.Logging;
using RestDraft.Domain.Results;

namespace RestDraft.Infrastructure.Persistence;

public sealed class MigrationSetBuilder
{
    private static readonly Action<ILogger, int, Exception?> DuplicateVersion = LoggerMessage.Define<int>(
        LogLevel.Error,
        new EventId(6, nameof(DuplicateVersion)),
        "Migration version {Version} is defined more than once");

    private readonly IEnumerable<IMigrationSource> _sources;
    private readonly ILogger<MigrationSetBuilder> _logger;

    public MigrationSetBuilder(IEnumerable<IMigrationSource> sources, ILogger<MigrationSetBuilder> logger)
    {
        _sources = sources;
        _logger = logger;
    }

    public Result<IReadOnlyList<IMigration>> Build()
    {
        var migrations = new List<IMigration>();
        foreach (var source in _sources)
        {
            var sourceResult = source.GetMigrations();
            if (sourceResult.Error is { } error)
            {
                return Result<IReadOnlyList<IMigration>>.Failure(error);
            }

            migrations.AddRange(sourceResult.Value ?? []);
        }

        var duplicate = migrations
            .GroupBy(migration => migration.Version)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            DuplicateVersion(_logger, duplicate.Key, null);
            return Result<IReadOnlyList<IMigration>>.Failure(
                new Error(ErrorCategory.Storage, "The database could not be migrated.", $"Migration version {duplicate.Key} is defined more than once."));
        }

        return Result<IReadOnlyList<IMigration>>.Success(migrations.OrderBy(migration => migration.Version).ToList());
    }
}
