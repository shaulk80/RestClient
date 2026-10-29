using RestDraft.Domain.Results;

namespace RestDraft.Infrastructure.Persistence;

public interface IMigrationSource
{
    Result<IReadOnlyList<IMigration>> GetMigrations();
}
