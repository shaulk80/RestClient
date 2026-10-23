using RestClient.Domain.Results;

namespace RestClient.Infrastructure.Persistence;

public sealed class CodeMigrationSource : IMigrationSource
{
    private readonly IEnumerable<IMigration> _migrations;

    public CodeMigrationSource(IEnumerable<IMigration> migrations)
    {
        _migrations = migrations;
    }

    public Result<IReadOnlyList<IMigration>> GetMigrations()
    {
        return Result<IReadOnlyList<IMigration>>.Success([.. _migrations]);
    }
}
