using NUnit.Framework;
using RestClient.Application.Requests;
using RestClient.Domain.Results;

namespace RestClient.Application.Tests.Requests;

[TestFixture]
public class VariableResolverTests
{
    private static readonly IReadOnlyDictionary<string, string> Variables = new Dictionary<string, string>
    {
        ["host"] = "a",
    };

    [TestCase("{{host}}/x", "a/x")]
    [TestCase("{{ host }}", "a")]
    [TestCase("{{{host}}}", "{a}")]
    [TestCase("no placeholders", "no placeholders")]
    public void ResolveReplacesKnownVariables(string template, string expected)
    {
        var result = VariableResolver.Resolve(template, Variables);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(expected));
    }

    [TestCase("{{missing}}")]
    [TestCase("{{}}")]
    [TestCase("{{host}}{{missing}}")]
    public void ResolveFailsWhenPlaceholderIsUnresolved(string template)
    {
        var result = VariableResolver.Resolve(template, Variables);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [Test]
    public void ResolveListsEveryUnresolvedPlaceholder()
    {
        var result = VariableResolver.Resolve("{{first}} {{second}}", Variables);

        Assert.That(result.Error?.Message, Does.Contain("{{first}}").And.Contain("{{second}}"));
    }

    [Test]
    public void ResolveDoesNotExpandPlaceholdersInsideValues()
    {
        var variables = new Dictionary<string, string> { ["outer"] = "{{host}}" };

        var result = VariableResolver.Resolve("{{outer}}", variables);

        Assert.That(result.Value, Is.EqualTo("{{host}}"));
    }
}
