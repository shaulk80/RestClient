using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Abstractions;
using RestClient.Application.Environments;
using RestClient.Application.Secrets;
using RestClient.Domain.Auth;
using RestClient.Domain.Collections;
using RestClient.Domain.Environments;
using RestClient.Domain.Results;

namespace RestClient.Application.Tests.Environments;

[TestFixture]
public class EnvironmentRequestVariablesTests
{
    private static readonly RequestEnvironment Staging = new(
        Guid.NewGuid(),
        "Staging",
        [
            new Variable("host", "example.com", false),
            new Variable("token", "enc:abc", true),
            new Variable("unused", "enc:zzz", true),
        ]);

    private IEnvironmentService _environments = null!;
    private ISecretService _secrets = null!;
    private EnvironmentRequestVariables _variables = null!;

    [SetUp]
    public void SetUp()
    {
        _environments = A.Fake<IEnvironmentService>();
        A.CallTo(() => _environments.GetActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestEnvironment?>.Success(Staging)));
        _secrets = A.Fake<ISecretService>();
        A.CallTo(() => _secrets.IsUnlocked).Returns(true);
        A.CallTo(() => _secrets.Decrypt(A<string>._))
            .ReturnsLazily((string cipher) => Result<string>.Success(cipher.Replace("enc:", string.Empty, StringComparison.Ordinal)));
        _variables = new EnvironmentRequestVariables(_environments, _secrets);
    }

    [Test]
    public async Task ResolvePutsPlainValuesAndDecryptsOnlyReferencedSecrets()
    {
        var result = await _variables.ResolveAsync(DefinitionWithUrl("https://{{host}}/{{token}}"), CancellationToken.None);

        Assert.That(result.Value?.Keys.OrderBy(name => name).ToList(), Is.EqualTo(new List<string> { "host", "token" }));
        Assert.That(result.Value?["token"], Is.EqualTo("abc"));
        A.CallTo(() => _secrets.Decrypt("enc:zzz")).MustNotHaveHappened();
    }

    [Test]
    public async Task ResolveFindsSecretReferencedOnlyInAuth()
    {
        var definition = DefinitionWithUrl("https://example.com") with { Auth = new BearerAuth("{{token}}") };

        var result = await _variables.ResolveAsync(definition, CancellationToken.None);

        Assert.That(result.Value?["token"], Is.EqualTo("abc"));
    }

    [Test]
    public async Task ResolveFailsWithSecurityWhenReferencedSecretIsLocked()
    {
        A.CallTo(() => _secrets.IsUnlocked).Returns(false);

        var result = await _variables.ResolveAsync(DefinitionWithUrl("https://{{token}}"), CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Security));
    }

    [Test]
    public async Task ResolveSucceedsWhileLockedWhenNoSecretIsReferenced()
    {
        A.CallTo(() => _secrets.IsUnlocked).Returns(false);

        var result = await _variables.ResolveAsync(DefinitionWithUrl("https://{{host}}"), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value?["host"], Is.EqualTo("example.com"));
    }

    [Test]
    public async Task ResolveReturnsEmptyWhenNoEnvironmentIsActive()
    {
        A.CallTo(() => _environments.GetActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestEnvironment?>.Success(null)));

        var result = await _variables.ResolveAsync(DefinitionWithUrl("https://{{host}}"), CancellationToken.None);

        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public async Task ResolveFailsWhenActiveEnvironmentCannotBeRead()
    {
        A.CallTo(() => _environments.GetActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestEnvironment?>.Failure(new Error(ErrorCategory.Storage, "Failed."))));

        var result = await _variables.ResolveAsync(DefinitionWithUrl("https://example.com"), CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
    }

    private static RequestDefinition DefinitionWithUrl(string url)
    {
        return new RequestDefinition { Id = Guid.Empty, Url = url };
    }
}
