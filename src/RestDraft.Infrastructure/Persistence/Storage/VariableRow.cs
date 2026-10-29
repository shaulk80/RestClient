namespace RestDraft.Infrastructure.Persistence.Storage;

// One row of environment_variables. Exactly one of Value and SecretValue is set, as the IsSecret flag says.
public sealed record VariableRow(Guid EnvironmentId, string Name, bool IsSecret, string? Value, string? SecretValue);
