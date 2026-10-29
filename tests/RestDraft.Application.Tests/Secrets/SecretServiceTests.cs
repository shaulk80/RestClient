using FakeItEasy;
using NUnit.Framework;
using RestDraft.Application.Abstractions;
using RestDraft.Application.Secrets;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Tests.Secrets;

[TestFixture]
public class SecretServiceTests
{
    private const string MasterPassword = "correct horse";
    private const string PlainSecret = "token-123";

    private static readonly byte[] Salt = new byte[16];

    private InMemorySettingsStore _settings = null!;
    private SecretService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _settings = new InMemorySettingsStore();
        var random = A.Fake<IRandomBytes>();
        A.CallTo(() => random.Generate(16)).Returns(Salt);
        _service = new SecretService(_settings, new FakeKeyDeriver(), new FakeSecretCipher(), random);
    }

    [Test]
    public async Task IsSetUpIsFalseUntilPasswordIsSet()
    {
        var before = await _service.IsSetUpAsync(CancellationToken.None);
        await _service.SetUpAsync(MasterPassword, CancellationToken.None);
        var after = await _service.IsSetUpAsync(CancellationToken.None);

        Assert.That(before.Value, Is.False);
        Assert.That(after.Value, Is.True);
    }

    [Test]
    public async Task SetUpLeavesServiceUnlocked()
    {
        await _service.SetUpAsync(MasterPassword, CancellationToken.None);

        Assert.That(_service.IsUnlocked, Is.True);
    }

    [Test]
    public async Task SetUpFailsWhenPasswordIsEmpty()
    {
        var result = await _service.SetUpAsync(string.Empty, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [Test]
    public async Task SetUpFailsWhenPasswordIsAlreadySet()
    {
        await _service.SetUpAsync(MasterPassword, CancellationToken.None);

        var result = await _service.SetUpAsync("another", CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Conflict));
    }

    [Test]
    public async Task SetUpStoresNoPasswordInSettings()
    {
        await _service.SetUpAsync(MasterPassword, CancellationToken.None);

        Assert.That(_settings.Values.Values.Any(value => value.Contains(MasterPassword, StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public async Task UnlockWithCorrectPasswordAfterLockSucceeds()
    {
        await _service.SetUpAsync(MasterPassword, CancellationToken.None);
        _service.Lock();

        var result = await _service.UnlockAsync(MasterPassword, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_service.IsUnlocked, Is.True);
    }

    [Test]
    public async Task UnlockWithWrongPasswordFailsAndStaysLocked()
    {
        await _service.SetUpAsync(MasterPassword, CancellationToken.None);
        _service.Lock();

        var result = await _service.UnlockAsync("wrong", CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Security));
        Assert.That(_service.IsUnlocked, Is.False);
    }

    [Test]
    public async Task UnlockFailsWhenNoPasswordIsSet()
    {
        var result = await _service.UnlockAsync(MasterPassword, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [Test]
    public async Task LockClearsKeySoEncryptFails()
    {
        await _service.SetUpAsync(MasterPassword, CancellationToken.None);

        _service.Lock();
        var result = _service.Encrypt(PlainSecret);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Security));
    }

    [Test]
    public async Task LockRaisesStateChangedOnlyWhenUnlocked()
    {
        var raised = 0;
        _service.StateChanged += (_, _) => raised++;
        await _service.SetUpAsync(MasterPassword, CancellationToken.None);

        _service.Lock();
        _service.Lock();

        Assert.That(raised, Is.EqualTo(2));
    }

    [Test]
    public async Task EncryptThenDecryptRoundTrips()
    {
        await _service.SetUpAsync(MasterPassword, CancellationToken.None);

        var encrypted = _service.Encrypt(PlainSecret);
        var decrypted = _service.Decrypt(encrypted.Value!);

        Assert.That(decrypted.Value, Is.EqualTo(PlainSecret));
    }

    [Test]
    public async Task DecryptFailsForCiphertextMadeWithAnotherKey()
    {
        await _service.SetUpAsync(MasterPassword, CancellationToken.None);

        var decrypted = _service.Decrypt("AAAA|" + PlainSecret);

        Assert.That(decrypted.Error?.Category, Is.EqualTo(ErrorCategory.Security));
    }

    [TestCase("secrets.salt", "not base64!")]
    [TestCase("secrets.salt", "AAAA")]
    [TestCase("secrets.iterations", "many")]
    [TestCase("secrets.iterations", "0")]
    public async Task UnlockReportsStorageErrorWhenSetupSettingIsCorrupt(string key, string value)
    {
        await _service.SetUpAsync(MasterPassword, CancellationToken.None);
        _service.Lock();
        _settings.Values[key] = value;

        var result = await _service.UnlockAsync(MasterPassword, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
        Assert.That(_service.IsUnlocked, Is.False);
    }

    [Test]
    public async Task DecryptFailsWhileLocked()
    {
        var result = _service.Decrypt("anything");

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Security));
    }
}
