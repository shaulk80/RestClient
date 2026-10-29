using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace RestDraft.Application.Tests;

[TestFixture]
public class ApplicationServiceCollectionExtensionsTests
{
    [Test]
    public void AddApplicationReturnsTheSameServiceCollection()
    {
        var services = A.Fake<IServiceCollection>();

        var result = services.AddApplication();

        Assert.That(result, Is.SameAs(services));
    }
}
