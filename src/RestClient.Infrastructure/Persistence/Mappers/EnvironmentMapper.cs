using RestClient.Domain.Environments;
using RestClient.Infrastructure.Persistence.Dto;

namespace RestClient.Infrastructure.Persistence.Mappers;

public static class EnvironmentMapper
{
    public static EnvironmentDto ToDto(RequestEnvironment environment)
    {
        var variables = environment.Variables
            .Select(variable => new VariableDto(variable.Name, variable.Value, variable.IsSecret))
            .ToList();
        return new EnvironmentDto(environment.Id, environment.Name, variables);
    }

    public static RequestEnvironment ToDomain(EnvironmentDto dto)
    {
        var variables = dto.Variables
            .Select(variable => new Variable(variable.Name, variable.Value, variable.IsSecret))
            .ToList();
        return new RequestEnvironment(dto.Id, dto.Name, variables);
    }
}
