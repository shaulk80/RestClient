using NUnit.Framework;
using RestDraft.Domain.Environments;
using RestDraft.Domain.Results;

namespace RestDraft.Domain.Tests.Environments;

[TestFixture]
public class VariableNameRulesTests
{
    [TestCase("host", true)]
    [TestCase("  host  ", true)]
    [TestCase("", false)]
    [TestCase("   ", false)]
    [TestCase("bad{name", false)]
    [TestCase("bad}name", false)]
    public void ValidateNameAcceptsOnlyNonEmptyNamesWithoutBraces(string name, bool expected)
    {
        var result = VariableNameRules.ValidateName(name);

        Assert.That(result.IsSuccess, Is.EqualTo(expected));
    }

    [Test]
    public void ValidateUniqueAcceptsDistinctNames()
    {
        var result = VariableNameRules.ValidateUnique([new Variable("a", "1", false), new Variable("b", "2", false)]);

        Assert.That(result.IsSuccess, Is.True);
    }

    [TestCase("a", " a ")]
    [TestCase("token", "token")]
    public void ValidateUniqueRejectsDuplicateNamesAfterTrimming(string first, string second)
    {
        var result = VariableNameRules.ValidateUnique([new Variable(first, "1", false), new Variable(second, "2", true)]);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }
}
