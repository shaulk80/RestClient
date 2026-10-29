using Microsoft.Data.Sqlite;
using RestDraft.Domain.Results;
using RestDraft.Infrastructure.Persistence.Dto;

namespace RestDraft.Infrastructure.Persistence.Storage;

public sealed class EnvironmentDtoReader
{
    private const string SelectEnvironmentsSql = "SELECT id, name FROM environments ORDER BY position;";
    private const string SelectVariablesSql =
        "SELECT environment_id, name, is_secret, value, secret_value FROM environment_variables ORDER BY environment_id, position;";

    public static async Task<Result<IReadOnlyList<EnvironmentDto>>> ReadAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var environments = await ReadEnvironmentsAsync(connection, cancellationToken);
        var variables = await ReadVariableRowsAsync(connection, cancellationToken);
        return Build(environments, variables);
    }

    private static Result<IReadOnlyList<EnvironmentDto>> Build(
        IReadOnlyList<EnvironmentRow> environments,
        IReadOnlyList<VariableRow> rows)
    {
        var result = new List<EnvironmentDto>();
        foreach (var environment in environments)
        {
            var variables = new List<VariableDto>();
            foreach (var row in rows.Where(candidate => candidate.EnvironmentId == environment.Id))
            {
                var value = row.IsSecret ? row.SecretValue : row.Value;
                if (value is null)
                {
                    return Result<IReadOnlyList<EnvironmentDto>>.Failure(new Error(ErrorCategory.Storage, "A stored variable has no value."));
                }

                variables.Add(new VariableDto(row.Name, value, row.IsSecret));
            }

            result.Add(new EnvironmentDto(environment.Id, environment.Name, variables));
        }

        return Result<IReadOnlyList<EnvironmentDto>>.Success(result);
    }

    private static async Task<List<EnvironmentRow>> ReadEnvironmentsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<EnvironmentRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = SelectEnvironmentsSql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new EnvironmentRow(reader.GetGuid(0), reader.GetString(1)));
        }

        return rows;
    }

    private static async Task<List<VariableRow>> ReadVariableRowsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<VariableRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = SelectVariablesSql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var value = reader.IsDBNull(3) ? null : reader.GetString(3);
            var secretValue = reader.IsDBNull(4) ? null : reader.GetString(4);
            rows.Add(new VariableRow(reader.GetGuid(0), reader.GetString(1), reader.GetInt64(2) != 0, value, secretValue));
        }

        return rows;
    }
}
