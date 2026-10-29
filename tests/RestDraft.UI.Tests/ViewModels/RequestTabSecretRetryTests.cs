using FakeItEasy;
using NUnit.Framework;
using RestDraft.Application.Abstractions;
using RestDraft.Application.Collections;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Responses;
using RestDraft.Domain.Results;
using RestDraft.UI.Services;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Tests.ViewModels;

[TestFixture]
public class RequestTabSecretRetryTests
{
    private static readonly ResponseSnapshot Snapshot = new(200, "OK", [], "body", false, 4, null, new ResponseTiming(null, 1, 2));

    private IRequestSender _sender = null!;
    private ISecretPrompt _prompt = null!;

    [SetUp]
    public void SetUp()
    {
        _sender = A.Fake<IRequestSender>();
        _prompt = A.Fake<ISecretPrompt>();
    }

    [Test]
    public async Task SendAsksForUnlockAndSendsAgainWhenSecretIsLocked()
    {
        ConfigureSender(
            Result<ResponseSnapshot>.Failure(new Error(ErrorCategory.Security, "Unlock the secrets.")),
            Result<ResponseSnapshot>.Success(Snapshot));
        A.CallTo(() => _prompt.EnsureUnlockedAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<bool>.Success(true)));
        var viewModel = CreateViewModel();

        await viewModel.SendAsync();

        A.CallTo(() => _prompt.EnsureUnlockedAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _sender.SendAsync(A<RequestDefinition>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Test]
    public async Task SendDoesNotSendAgainWhenUserDeclinesUnlock()
    {
        ConfigureSender(Result<ResponseSnapshot>.Failure(new Error(ErrorCategory.Security, "Unlock the secrets.")));
        A.CallTo(() => _prompt.EnsureUnlockedAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<bool>.Success(false)));
        var viewModel = CreateViewModel();

        await viewModel.SendAsync();

        A.CallTo(() => _sender.SendAsync(A<RequestDefinition>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [TestCase(ErrorCategory.Network)]
    [TestCase(ErrorCategory.Validation)]
    [TestCase(ErrorCategory.Timeout)]
    public async Task SendDoesNotPromptForOtherFailures(ErrorCategory category)
    {
        ConfigureSender(Result<ResponseSnapshot>.Failure(new Error(category, "Failed.")));
        var viewModel = CreateViewModel();

        await viewModel.SendAsync();

        A.CallTo(() => _prompt.EnsureUnlockedAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    private void ConfigureSender(params Result<ResponseSnapshot>[] results)
    {
        var tasks = results.Select(result => Task.FromResult(result)).ToArray();
        A.CallTo(() => _sender.SendAsync(A<RequestDefinition>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(tasks);
    }

    private RequestTabViewModel CreateViewModel()
    {
        var services = RequestTabServicesFactory.Create(
            _sender,
            A.Fake<IRequestService>(),
            A.Fake<IDestinationPicker>(),
            _prompt);
        return new RequestTabViewModel(services, new RequestDefinition { Id = Guid.NewGuid(), Url = "https://example.com" }, true);
    }
}
