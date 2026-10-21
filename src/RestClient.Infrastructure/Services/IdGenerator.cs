using RestClient.Application.Abstractions;

namespace RestClient.Infrastructure.Services;

public sealed class IdGenerator : IIdGenerator
{
    public Guid NewId()
    {
        return Guid.NewGuid();
    }
}
