using Microsoft.Data.Sqlite;
using RestDraft.Infrastructure.Persistence.Dto;

namespace RestDraft.Infrastructure.Persistence.Storage;

public sealed class EnvironmentDtoWriter
{
    private const string DeleteVariablesSql = "DELETE FROM environment_variables;";
    private const string DeleteEnvironmentsSql = "DELETE FROM environments;";
    private const string InsertEnvironmentSql = "INSERT INTO environments (id, name, position) VALUES ($id, $name, $position);";
    private const string InsertVariableSql =
        "INSERT INTO environment_variables (environment_id, position, name, is_secret, value, secret_value) " +
        "VALUES ($environmentId, $position, $name, $isSecret, $value, $secretValue);";

    // Replaces every environment. The caller runs this inside a transaction.
    public static async Task WriteAsync(SqliteConnection connection, IReadOnlyList<EnvironmentDto> environments, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, DeleteVariablesSql, cancellationToken);
        await ExecuteAsync(connection, DeleteEnvironmentsSql, cancellationToken);

        for (var position = 0; position < environments.Count; position++)
        {
            var environment = environments[position];
            await InsertEnvironmentAsync(connection, environment, position, cancellationToken);
            for (var index = 0; index < environment.Variables.Count; index++)
            {
                await InsertVariableAsync(connection, environment.Id, environment.Variables[index], index, cancellationToken);
            }
        }
    }

    private static async Task InsertEnvironmentAsync(
        SqliteConnection connection,
        EnvironmentDto environment,
        int position,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = InsertEnvironmentSql;
        command.Parameters.AddWithValue("$id", environment.Id);
        command.Parameters.AddWithValue("$name", environment.Name);
        command.Parameters.AddWithValue("$position", position);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // A secret goes only to secret_value, so no plain text of a secret reaches the value column.
    private static async Task InsertVariableAsync(
        SqliteConnection connection,
        Guid environmentId,
        VariableDto variable,
        int position,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = InsertVariableSql;
        command.Parameters.AddWithValue("$environmentId", environmentId);
        command.Parameters.AddWithValue("$position", position);
        command.Parameters.AddWithValue("$name", variable.Name);
        command.Parameters.AddWithValue("$isSecret", variable.IsSecret ? 1 : 0);
        command.Parameters.AddWithValue("$value", variable.IsSecret ? DBNull.Value : variable.Value);
        command.Parameters.AddWithValue("$secretValue", variable.IsSecret ? variable.Value : DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
