using RestClient.Domain.Results;

namespace RestClient.Infrastructure.Persistence;

public interface IMigrationSource
{
    Result<IReadOnlyList<IMigration>> GetMigrations();
}
