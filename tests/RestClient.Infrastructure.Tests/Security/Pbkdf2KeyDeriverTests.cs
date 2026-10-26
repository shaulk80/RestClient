using NUnit.Framework;
using RestClient.Infrastructure.Security;

namespace RestClient.Infrastructure.Tests.Security;

[TestFixture]
public class Pbkdf2KeyDeriverTests
{
    private const string Password = "correct horse";
    private const int Iterations = 1000;

    private static readonly byte[] Salt = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
    private static readonly byte[] OtherSalt = Enumerable.Range(50, 16).Select(value => (byte)value).ToArray();

    private readonly Pbkdf2KeyDeriver _deriver = new();

    [Test]
    public void SamePasswordAndSaltGiveSameKey()
    {
        var first = _deriver.Derive(Password, Salt, Iterations);
        var second = _deriver.Derive(Password, Salt, Iterations);

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void DifferentSaltGivesDifferentKey()
    {
        var first = _deriver.Derive(Password, Salt, Iterations);
        var other = _deriver.Derive(Password, OtherSalt, Iterations);

        Assert.That(other, Is.Not.EqualTo(first));
    }

    [Test]
    public void DifferentPasswordGivesDifferentKey()
    {
        var first = _deriver.Derive(Password, Salt, Iterations);
        var other = _deriver.Derive("another", Salt, Iterations);

        Assert.That(other, Is.Not.EqualTo(first));
    }

    [Test]
    public void KeyIsA256BitAesKey()
    {
        var key = _deriver.Derive(Password, Salt, Iterations);

        Assert.That(key, Has.Length.EqualTo(32));
    }
}
