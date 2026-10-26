using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Security;

namespace RestClient.Infrastructure.Tests.Security;

[TestFixture]
public class AesGcmSecretCipherTests
{
    private const string PlainText = "token-123";

    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
    private static readonly byte[] OtherKey = Enumerable.Range(100, 32).Select(value => (byte)value).ToArray();

    private AesGcmSecretCipher _cipher = null!;

    [SetUp]
    public void SetUp()
    {
        _cipher = new AesGcmSecretCipher(NullLogger<AesGcmSecretCipher>.Instance);
    }

    [Test]
    public void EncryptThenDecryptRoundTrips()
    {
        var encrypted = _cipher.Encrypt(PlainText, Key);

        var decrypted = _cipher.Decrypt(encrypted.Value!, Key);

        Assert.That(decrypted.Value, Is.EqualTo(PlainText));
    }

    [Test]
    public void EncryptedTextDoesNotContainPlainText()
    {
        var encrypted = _cipher.Encrypt(PlainText, Key);

        Assert.That(encrypted.Value, Does.Not.Contain(PlainText));
    }

    [Test]
    public void EncryptUsesANewNonceEachTime()
    {
        var first = _cipher.Encrypt(PlainText, Key);
        var second = _cipher.Encrypt(PlainText, Key);

        Assert.That(second.Value, Is.Not.EqualTo(first.Value));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DecryptFailsWithSecurityForWrongKeyOrTamperedCiphertext(bool wrongKey)
    {
        var encrypted = _cipher.Encrypt(PlainText, Key).Value!;
        var cipherText = wrongKey ? encrypted : Tamper(encrypted);
        var key = wrongKey ? OtherKey : Key;

        var result = _cipher.Decrypt(cipherText, key);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Security));
    }

    [TestCase("")]
    [TestCase("AAAA")]
    [TestCase("not base64!")]
    public void DecryptFailsWithSecurityForDamagedPayload(string cipherText)
    {
        var result = _cipher.Decrypt(cipherText, Key);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Security));
    }

    // Flips the last byte of the ciphertext, which is covered by the tag.
    private static string Tamper(string encrypted)
    {
        var payload = Convert.FromBase64String(encrypted);
        payload[^1] ^= 0xFF;
        return Convert.ToBase64String(payload);
    }
}
