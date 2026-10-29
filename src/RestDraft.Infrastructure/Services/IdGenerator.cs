using RestDraft.Application.Abstractions;

namespace RestDraft.Infrastructure.Services;

public sealed class IdGenerator : IIdGenerator
{
    public Guid NewId()
    {
        return Guid.NewGuid();
    }
}
