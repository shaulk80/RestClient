using FakeItEasy;
using NUnit.Framework;
using RestDraft.Application.Abstractions;
using RestDraft.Application.Settings;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Tests.Settings;

[TestFixture]
public class SettingsServiceTests
{
    private ISettingsStore _store = null!;
    private SettingsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _store = A.Fake<ISettingsStore>();
        A.CallTo(() => _store.GetAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<string?>.Success(null)));
        A.CallTo(() => _store.SetAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        _service = new SettingsService(new AppSettingsStorage(_store));
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    [Test]
    public async Task GetReturnsDefaultsWhenNothingIsStored()
    {
        var result = await _service.GetAsync(CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo(AppSettings.Default));
    }

    [Test]
    public async Task GetReadsTheStoreOnlyOnce()
    {
        await _service.GetAsync(CancellationToken.None);
        await _service.GetAsync(CancellationToken.None);

        A.CallTo(() => _store.GetAsync("http.timeoutSeconds", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task SaveMakesNewSettingsCurrent()
    {
        var settings = new AppSettings { TimeoutSeconds = 5 };

        var saved = await _service.SaveAsync(settings, CancellationToken.None);
        var current = await _service.GetAsync(CancellationToken.None);

        Assert.That(saved.IsSuccess, Is.True);
        Assert.That(current.Value, Is.EqualTo(settings));
    }

    [Test]
    public async Task SaveRejectsInvalidSettingsWithoutWriting()
    {
        var result = await _service.SaveAsync(new AppSettings { TimeoutSeconds = 0 }, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
        A.CallTo(() => _store.SetAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task FailedSaveKeepsPreviousSettings()
    {
        A.CallTo(() => _store.SetAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(new Error(ErrorCategory.Storage, "Disk full."))));

        var saved = await _service.SaveAsync(new AppSettings { TimeoutSeconds = 5 }, CancellationToken.None);
        var current = await _service.GetAsync(CancellationToken.None);

        Assert.That(saved.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
        Assert.That(current.Value, Is.EqualTo(AppSettings.Default));
    }

    [Test]
    public async Task GetFailsWhenTheStoreCannotBeRead()
    {
        A.CallTo(() => _store.GetAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<string?>.Failure(new Error(ErrorCategory.Storage, "Unreadable."))));

        var result = await _service.GetAsync(CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
    }
}
