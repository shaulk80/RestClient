using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Abstractions;
using RestClient.Application.History;
using RestClient.Domain.Collections;
using RestClient.Domain.History;
using RestClient.Domain.Results;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

[TestFixture]
public class HistoryListViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] NewestFirstUrls = ["https://example.com/new", "https://example.com/old"];

    private IHistoryService _history = null!;
    private HistoryListViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _history = A.Fake<IHistoryService>();
        A.CallTo(() => _history.SearchAsync(A<HistoryFilter>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<HistoryEntry>>.Success([])));
        _viewModel = new HistoryListViewModel(_history);
    }

    [Test]
    public async Task ReloadShowsEntriesReturnedBySearch()
    {
        var newest = CreateEntry(Now, "https://example.com/new");
        var oldest = CreateEntry(Now.AddMinutes(-5), "https://example.com/old");
        SetSearchResult([newest, oldest]);

        await _viewModel.ReloadAsync();

        Assert.That(_viewModel.Items.Select(item => item.Url), Is.EqualTo(NewestFirstUrls));
    }

    [Test]
    public async Task ReloadShowsSearchErrorInStatusMessage()
    {
        A.CallTo(() => _history.SearchAsync(A<HistoryFilter>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<HistoryEntry>>.Failure(new Error(ErrorCategory.Storage, "The history could not be read."))));

        await _viewModel.ReloadAsync();

        Assert.That(_viewModel.Items, Is.Empty);
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("The history could not be read."));
    }

    [Test]
    public async Task SearchUsesTrimmedUrlTextAndSelectedMethod()
    {
        _viewModel.UrlFilter = " api ";
        _viewModel.SelectedMethod = nameof(RequestMethod.Post);

        await _viewModel.ReloadAsync();

        A.CallTo(() => _history.SearchAsync(new HistoryFilter("api", RequestMethod.Post), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task AnyMethodChoiceSearchesWithoutMethodFilter()
    {
        _viewModel.SelectedMethod = HistoryListViewModel.AnyMethod;

        await _viewModel.ReloadAsync();

        A.CallTo(() => _history.SearchAsync(new HistoryFilter(string.Empty, null), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RestoreRaisesRestoreRequestedWithTheUnsavedDefinition()
    {
        var entry = CreateEntry(Now, "https://example.com/login");
        var restored = new RequestDefinition { Id = Guid.NewGuid(), Name = "Login", Url = entry.ResolvedUrl };
        SetSearchResult([entry]);
        await _viewModel.ReloadAsync();
        _viewModel.SelectedItem = _viewModel.Items[0];
        A.CallTo(() => _history.RestoreAsync(entry.Id, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestDefinition>.Success(restored)));
        var raised = new List<RequestDefinition>();
        _viewModel.RestoreRequested += raised.Add;

        await _viewModel.RestoreAsync();

        Assert.That(raised, Is.EqualTo(new[] { restored }));
        Assert.That(_viewModel.StatusMessage, Is.Empty);
    }

    [Test]
    public async Task RestoreWithoutSelectedItemDoesNothing()
    {
        var raised = new List<RequestDefinition>();
        _viewModel.RestoreRequested += raised.Add;

        await _viewModel.RestoreAsync();

        A.CallTo(() => _history.RestoreAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
        Assert.That(raised, Is.Empty);
    }

    [Test]
    public async Task RestoreFailureShowsErrorAndRaisesNothing()
    {
        var entry = CreateEntry(Now, "https://example.com/login");
        SetSearchResult([entry]);
        await _viewModel.ReloadAsync();
        _viewModel.SelectedItem = _viewModel.Items[0];
        A.CallTo(() => _history.RestoreAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestDefinition>.Failure(new Error(ErrorCategory.NotFound, "The history entry was not found."))));
        var raised = new List<RequestDefinition>();
        _viewModel.RestoreRequested += raised.Add;

        await _viewModel.RestoreAsync();

        Assert.That(_viewModel.StatusMessage, Is.EqualTo("The history entry was not found."));
        Assert.That(raised, Is.Empty);
    }

    private void SetSearchResult(IReadOnlyList<HistoryEntry> entries)
    {
        A.CallTo(() => _history.SearchAsync(A<HistoryFilter>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<HistoryEntry>>.Success(entries)));
    }

    private static HistoryEntry CreateEntry(DateTimeOffset sentAt, string url)
    {
        var request = new RequestDefinition { Id = Guid.NewGuid(), Name = "Login", Url = url };
        return new HistoryEntry(Guid.NewGuid(), sentAt, RequestMethod.Get, url, request, 200, 3, null, null);
    }
}
