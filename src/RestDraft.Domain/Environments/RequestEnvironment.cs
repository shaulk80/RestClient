namespace RestDraft.Domain.Environments;

public sealed record RequestEnvironment(Guid Id, string Name, IReadOnlyList<Variable> Variables);
