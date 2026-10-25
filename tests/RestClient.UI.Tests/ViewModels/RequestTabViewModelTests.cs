using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Abstractions;
using RestClient.Application.Collections;
using RestClient.Domain.Bodies;
using RestClient.Domain.Collections;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;
using RestClient.UI.Services;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

[TestFixture]
public class RequestTabViewModelTests
{
    [Test]
    public async Task SendShowsResponseWhenRequestSucceeds()
    {
        var sender = A.Fake<IRequestSender>();
        A.CallTo(() => sender.SendAsync(A<RequestDefinition>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<ResponseSnapshot>.Success(CreateSnapshot())));
        var viewModel = CreateViewModel(sender);

        await viewModel.SendAsync();

        Assert.That(viewModel.Response.StatusText, Is.EqualTo("200 OK"));
        Assert.That(viewModel.Response.BodyText, Is.EqualTo("body"));
        Assert.That(viewModel.Response.ErrorMessage, Is.Empty);
    }

    [Test]
    public async Task SendPassesTrimmedFieldsToSender()
    {
        RequestDefinition? sent = null;
        var sender = A.Fake<IRequestSender>();
        A.CallTo(() => sender.SendAsync(A<RequestDefinition>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._))
            .Invokes((RequestDefinition definition, IReadOnlyDictionary<string, string> _, CancellationToken _) => sent = definition)
            .Returns(Task.FromResult(Result<ResponseSnapshot>.Failure(new Error(ErrorCategory.Network, "Not sent."))));
        var viewModel = CreateViewModel(sender);
        viewModel.Method = RequestMethod.Post;
        viewModel.Url = " https://example.com/a ";
        viewModel.BodyEditor.SelectedType = RawChoice(viewModel);
        viewModel.BodyEditor.ContentType = "application/json";
        viewModel.BodyEditor.Text = "{}";

        await viewModel.SendAsync();

        Assert.That(sent?.Method, Is.EqualTo(RequestMethod.Post));
        Assert.That(sent?.Url, Is.EqualTo("https://example.com/a"));
        Assert.That(sent?.Body, Is.EqualTo(new RawBody("application/json", "{}")));
    }

    [Test]
    public void EditingBodyMarksTabDirty()
    {
        var viewModel = CreateViewModel(A.Fake<IRequestSender>());

        viewModel.BodyEditor.Text = "{}";

        Assert.That(viewModel.IsDirty, Is.True);
    }

    [TestCase(ErrorCategory.Network)]
    [TestCase(ErrorCategory.Timeout)]
    [TestCase(ErrorCategory.Unexpected)]
    public async Task SendShowsFormattedErrorWhenRequestFails(ErrorCategory category)
    {
        var sender = A.Fake<IRequestSender>();
        A.CallTo(() => sender.SendAsync(A<RequestDefinition>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<ResponseSnapshot>.Failure(new Error(category, "Something failed.", "details"))));
        var viewModel = CreateViewModel(sender);

        await viewModel.SendAsync();

        Assert.That(viewModel.Response.ErrorMessage, Is.EqualTo("Something failed. (details)"));
        Assert.That(viewModel.Response.StatusText, Is.Empty);
    }

    [Test]
    public async Task SendIsInFlightUntilRequestCompletes()
    {
        var sender = A.Fake<IRequestSender>();
        var completion = new TaskCompletionSource<Result<ResponseSnapshot>>();
        A.CallTo(() => sender.SendAsync(A<RequestDefinition>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._))
            .Returns(completion.Task);
        var viewModel = CreateViewModel(sender);

        var sending = viewModel.SendAsync();

        Assert.That(viewModel.IsSending, Is.True);
        Assert.That(viewModel.SendCommand.CanExecute(null), Is.False);
        Assert.That(viewModel.CancelCommand.CanExecute(null), Is.True);

        completion.SetResult(Result<ResponseSnapshot>.Success(CreateSnapshot()));
        await sending;

        Assert.That(viewModel.IsSending, Is.False);
        Assert.That(viewModel.SendCommand.CanExecute(null), Is.True);
        Assert.That(viewModel.CancelCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task CancelCancelsInFlightRequest()
    {
        var sender = A.Fake<IRequestSender>();
        var completion = new TaskCompletionSource<Result<ResponseSnapshot>>();
        var requestToken = CancellationToken.None;
        A.CallTo(() => sender.SendAsync(A<RequestDefinition>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._))
            .Invokes((RequestDefinition _, IReadOnlyDictionary<string, string> _, CancellationToken token) => requestToken = token)
            .Returns(completion.Task);
        var viewModel = CreateViewModel(sender);

        var sending = viewModel.SendAsync();
        viewModel.Cancel();

        Assert.That(requestToken.IsCancellationRequested, Is.True);

        completion.SetResult(Result<ResponseSnapshot>.Failure(new Error(ErrorCategory.Cancelled, "The request was cancelled.")));
        await sending;

        Assert.That(viewModel.Response.ErrorMessage, Is.EqualTo("The request was cancelled."));
    }

    [Test]
    public void EditingAFieldMarksTabDirty()
    {
        var viewModel = CreateViewModel(A.Fake<IRequestSender>());

        viewModel.Url = "https://example.com/changed";

        Assert.That(viewModel.IsDirty, Is.True);
        Assert.That(viewModel.Header, Is.EqualTo("Login *"));
    }

    [Test]
    public void SettingTheSameValueDoesNotMarkTabDirty()
    {
        var viewModel = CreateViewModel(A.Fake<IRequestSender>());

        viewModel.Url = viewModel.Url;

        Assert.That(viewModel.IsDirty, Is.False);
    }

    [Test]
    public async Task SaveWritesEditsThroughRequestServiceAndClearsDirty()
    {
        var requests = A.Fake<IRequestService>();
        Func<RequestDefinition, RequestDefinition>? applied = null;
        A.CallTo(() => requests.UpdateAsync(A<Guid>._, A<Func<RequestDefinition, RequestDefinition>>._, A<CancellationToken>._))
            .Invokes((Guid _, Func<RequestDefinition, RequestDefinition> change, CancellationToken _) => applied = change)
            .Returns(Task.FromResult(Result.Success()));
        var viewModel = CreateViewModel(A.Fake<IRequestSender>(), requests);
        viewModel.Method = RequestMethod.Put;
        viewModel.Url = " https://example.com/saved ";

        await viewModel.SaveAsync();

        var stored = applied!(CreateDefinition());
        Assert.That(stored.Method, Is.EqualTo(RequestMethod.Put));
        Assert.That(stored.Url, Is.EqualTo("https://example.com/saved"));
        Assert.That(viewModel.IsDirty, Is.False);
        Assert.That(viewModel.SaveError, Is.Empty);
    }

    [Test]
    public async Task SaveKeepsStoredFormBodyWhenOnlyUrlIsEdited()
    {
        var requests = A.Fake<IRequestService>();
        Func<RequestDefinition, RequestDefinition>? applied = null;
        A.CallTo(() => requests.UpdateAsync(A<Guid>._, A<Func<RequestDefinition, RequestDefinition>>._, A<CancellationToken>._))
            .Invokes((Guid _, Func<RequestDefinition, RequestDefinition> change, CancellationToken _) => applied = change)
            .Returns(Task.FromResult(Result.Success()));
        var formBody = new FormUrlEncodedBody([new KeyValueEntry("user", "me", true)]);
        var viewModel = CreateViewModel(A.Fake<IRequestSender>(), requests, definition: CreateDefinition() with { Body = formBody });
        viewModel.Url = "https://example.com/other";

        await viewModel.SaveAsync();

        var stored = applied!(CreateDefinition() with { Body = formBody });
        Assert.That((stored.Body as FormUrlEncodedBody)?.Fields, Is.EqualTo(formBody.Fields));
    }

    [Test]
    public async Task SaveShowsErrorAndKeepsTabDirtyWhenServiceFails()
    {
        var requests = A.Fake<IRequestService>();
        A.CallTo(() => requests.UpdateAsync(A<Guid>._, A<Func<RequestDefinition, RequestDefinition>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(new Error(ErrorCategory.Storage, "The collections could not be saved."))));
        var viewModel = CreateViewModel(A.Fake<IRequestSender>(), requests);
        viewModel.Url = "https://example.com/other";

        await viewModel.SaveAsync();

        Assert.That(viewModel.SaveError, Is.EqualTo("The collections could not be saved."));
        Assert.That(viewModel.IsDirty, Is.True);
    }

    [Test]
    public void RenamedTabShowsNewNameInHeader()
    {
        var viewModel = CreateViewModel(A.Fake<IRequestSender>());

        viewModel.Name = "Renamed";

        Assert.That(viewModel.Header, Is.EqualTo("Renamed"));
    }

    [Test]
    public async Task SendRaisesRequestSentWhenRequestFails()
    {
        var sender = A.Fake<IRequestSender>();
        A.CallTo(() => sender.SendAsync(A<RequestDefinition>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<ResponseSnapshot>.Failure(new Error(ErrorCategory.Network, "Not sent."))));
        var viewModel = CreateViewModel(sender);
        var raised = 0;
        viewModel.RequestSent += (_, _) => raised++;

        await viewModel.SendAsync();

        Assert.That(raised, Is.EqualTo(1));
    }

    [Test]
    public void UnsavedTabStartsDirtyAndUnlinked()
    {
        var viewModel = CreateViewModel(A.Fake<IRequestSender>(), isLinked: false);

        Assert.That(viewModel.IsLinked, Is.False);
        Assert.That(viewModel.IsDirty, Is.True);
    }

    [Test]
    public async Task SaveOfUnsavedTabCreatesRequestInChosenFolderAndLinksIt()
    {
        var folderId = Guid.NewGuid();
        var expandId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var destinations = A.Fake<IDestinationPicker>();
        A.CallTo(() => destinations.PickAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<SaveDestination?>.Success(new SaveDestination(folderId, expandId, "Login"))));
        var requests = A.Fake<IRequestService>();
        A.CallTo(() => requests.AddCopyAsync(folderId, A<RequestDefinition>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<Guid>.Success(newId)));
        var viewModel = CreateViewModel(A.Fake<IRequestSender>(), requests, destinations, isLinked: false);
        RequestLinkedEventArgs? linked = null;
        viewModel.Linked += (_, e) => linked = e;

        await viewModel.SaveAsync();

        A.CallTo(() => requests.AddCopyAsync(folderId, A<RequestDefinition>.That.Matches(definition => definition.Name == "Login"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        Assert.That(viewModel.IsLinked, Is.True);
        Assert.That(viewModel.RequestId, Is.EqualTo(newId));
        Assert.That(viewModel.IsDirty, Is.False);
        Assert.That(linked?.RequestId, Is.EqualTo(newId));
        Assert.That(linked?.ExpandId, Is.EqualTo(expandId));
    }

    [Test]
    public async Task SaveOfUnsavedTabDoesNothingWhenDialogIsCancelled()
    {
        var destinations = A.Fake<IDestinationPicker>();
        A.CallTo(() => destinations.PickAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<SaveDestination?>.Success(null)));
        var requests = A.Fake<IRequestService>();
        var viewModel = CreateViewModel(A.Fake<IRequestSender>(), requests, destinations, isLinked: false);

        await viewModel.SaveAsync();

        A.CallTo(() => requests.AddCopyAsync(A<Guid>._, A<RequestDefinition>._, A<CancellationToken>._)).MustNotHaveHappened();
        Assert.That(viewModel.IsLinked, Is.False);
        Assert.That(viewModel.IsDirty, Is.True);
    }

    [Test]
    public async Task SaveOfUnsavedTabShowsDialogErrorWithoutCreatingRequest()
    {
        var destinations = A.Fake<IDestinationPicker>();
        A.CallTo(() => destinations.PickAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<SaveDestination?>.Failure(new Error(ErrorCategory.Storage, "The collections could not be loaded."))));
        var requests = A.Fake<IRequestService>();
        var viewModel = CreateViewModel(A.Fake<IRequestSender>(), requests, destinations, isLinked: false);

        await viewModel.SaveAsync();

        Assert.That(viewModel.SaveError, Is.EqualTo("The collections could not be loaded."));
        A.CallTo(() => requests.AddCopyAsync(A<Guid>._, A<RequestDefinition>._, A<CancellationToken>._)).MustNotHaveHappened();
        Assert.That(viewModel.IsLinked, Is.False);
    }

    [Test]
    public async Task SaveOfUnsavedTabStaysUnlinkedWhenCreateFails()
    {
        var destinations = A.Fake<IDestinationPicker>();
        A.CallTo(() => destinations.PickAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<SaveDestination?>.Success(new SaveDestination(Guid.NewGuid(), Guid.NewGuid(), "Login"))));
        var requests = A.Fake<IRequestService>();
        A.CallTo(() => requests.AddCopyAsync(A<Guid>._, A<RequestDefinition>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<Guid>.Failure(new Error(ErrorCategory.Storage, "The request could not be saved."))));
        var viewModel = CreateViewModel(A.Fake<IRequestSender>(), requests, destinations, isLinked: false);

        await viewModel.SaveAsync();

        Assert.That(viewModel.SaveError, Is.EqualTo("The request could not be saved."));
        Assert.That(viewModel.IsLinked, Is.False);
        Assert.That(viewModel.IsDirty, Is.True);
    }

    private static BodyTypeChoice RawChoice(RequestTabViewModel viewModel)
    {
        return viewModel.BodyEditor.Types.Single(choice => choice.Type == BodyType.Raw);
    }

    private static RequestTabViewModel CreateViewModel(
        IRequestSender sender,
        IRequestService? requests = null,
        IDestinationPicker? destinations = null,
        bool isLinked = true,
        RequestDefinition? definition = null)
    {
        return new RequestTabViewModel(
            RequestTabServicesFactory.Create(sender, requests ?? A.Fake<IRequestService>(), destinations ?? A.Fake<IDestinationPicker>()),
            definition ?? CreateDefinition(),
            isLinked);
    }

    private static RequestDefinition CreateDefinition()
    {
        return new RequestDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Login",
            Url = "https://example.com/login",
        };
    }

    private static ResponseSnapshot CreateSnapshot()
    {
        return new ResponseSnapshot(200, "OK", [], "body", false, 4, null, new ResponseTiming(1, 2, 3));
    }
}
