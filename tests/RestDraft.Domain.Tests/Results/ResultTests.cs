using NUnit.Framework;
using RestDraft.Domain.Results;

namespace RestDraft.Domain.Tests.Results;

[TestFixture]
public class ResultTests
{
    public static IEnumerable<ErrorCategory> ErrorCategories => Enum.GetValues<ErrorCategory>();

    [Test]
    public void SuccessIsSuccessfulWithoutError()
    {
        var result = Result.Success();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Error, Is.Null);
    }

    [TestCaseSource(nameof(ErrorCategories))]
    public void FailureIsNotSuccessfulAndKeepsError(ErrorCategory category)
    {
        var error = new Error(category, "Something failed.", "Details");

        var result = Result.Failure(error);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(error));
    }

    [Test]
    public void GenericSuccessHoldsValue()
    {
        var result = Result<string>.Success("value");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo("value"));
        Assert.That(result.Error, Is.Null);
    }

    [TestCaseSource(nameof(ErrorCategories))]
    public void GenericFailureHasNoValueAndKeepsCategory(ErrorCategory category)
    {
        var result = Result<string>.Failure(new Error(category, "Something failed."));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Value, Is.Null);
        Assert.That(result.Error?.Category, Is.EqualTo(category));
    }
}
