using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Persistence;

namespace RestClient.Infrastructure.Tests.Persistence;

[TestFixture]
public class MigrationSetBuilderTests
{
    [Test]
    public void BuildMergesSourcesInVersionOrder()
    {
        var builder = CreateBuilder(
            CreateSource(CreateMigration(3)),
            CreateSource(CreateMigration(1)));
        int[] expected = [1, 3];

        var result = builder.Build();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value?.Select(migration => migration.Version).ToList(), Is.EqualTo(expected));
    }

    [Test]
    public void BuildFailsWhenSameVersionComesFromTwoSources()
    {
        var builder = CreateBuilder(
            CreateSource(CreateMigration(2)),
            CreateSource(CreateMigration(2)));

        var result = builder.Build();

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
    }

    [Test]
    public void BuildFailsWhenSourceFails()
    {
        var failingSource = A.Fake<IMigrationSource>();
        A.CallTo(() => failingSource.GetMigrations())
            .Returns(Result<IReadOnlyList<IMigration>>.Failure(new Error(ErrorCategory.Storage, "The scripts folder is missing.")));
        var builder = CreateBuilder(failingSource);

        var result = builder.Build();

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Message, Is.EqualTo("The scripts folder is missing."));
    }

    private static MigrationSetBuilder CreateBuilder(params IMigrationSource[] sources)
    {
        return new MigrationSetBuilder(sources, NullLogger<MigrationSetBuilder>.Instance);
    }

    private static IMigrationSource CreateSource(IMigration migration)
    {
        var source = A.Fake<IMigrationSource>();
        A.CallTo(() => source.GetMigrations())
            .Returns(Result<IReadOnlyList<IMigration>>.Success([migration]));
        return source;
    }

    private static IMigration CreateMigration(int version)
    {
        var migration = A.Fake<IMigration>();
        A.CallTo(() => migration.Version).Returns(version);
        return migration;
    }
}
