using FakeItEasy;
using RestClient.Application.Abstractions;
using RestClient.Domain.Collections;

namespace RestClient.Application.Tests.Collections;

internal static class Fixtures
{
    public static IIdGenerator CreateIdGenerator()
    {
        var idGenerator = A.Fake<IIdGenerator>();
        A.CallTo(() => idGenerator.NewId()).ReturnsLazily(() => Guid.NewGuid());
        return idGenerator;
    }

    public static RequestDefinition Request(string name)
    {
        return new RequestDefinition { Id = Guid.NewGuid(), Name = name };
    }

    public static Folder Folder(string name, IReadOnlyList<Folder>? folders = null, IReadOnlyList<RequestDefinition>? requests = null)
    {
        return new Folder(Guid.NewGuid(), name, folders ?? [], requests ?? []);
    }

    public static Collection Collection(string name, Folder root)
    {
        return new Collection(Guid.NewGuid(), name, root);
    }
}
