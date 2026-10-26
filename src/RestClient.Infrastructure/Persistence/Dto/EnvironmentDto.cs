namespace RestClient.Infrastructure.Persistence.Dto;

public sealed record EnvironmentDto(Guid Id, string Name, IReadOnlyList<VariableDto> Variables);
