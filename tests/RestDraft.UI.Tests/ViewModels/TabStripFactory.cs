using FakeItEasy;
using RestDraft.Application.Tabs;
using RestDraft.Domain.Results;
using RestDraft.UI.Services;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Tests.ViewModels;

internal static class TabStripFactory
{
    // An hour is long enough that a scheduled store never runs by itself. Tests flush it when they need it.
    public static readonly TimeSpan NeverDelay = TimeSpan.FromHours(1);

    public static ITabService CreateTabService()
    {
        var service = A.Fake<ITabService>();
        A.CallTo(() => service.SaveAsync(A<IReadOnlyList<TabSnapshot>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        A.CallTo(() => service.RestoreAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<RestoredTab>>.Success([])));
        return service;
    }

    public static ISaveChangesPrompt CreatePrompt(SaveChangesChoice choice)
    {
        var prompt = A.Fake<ISaveChangesPrompt>();
        A.CallTo(() => prompt.AskAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(choice));
        return prompt;
    }

    public static TabStripViewModel Create(
        RequestTabServices services,
        ITabService? tabService = null,
        ISaveChangesPrompt? prompt = null,
        AutosaveScheduler? autosave = null)
    {
        return new TabStripViewModel(
            services,
            tabService ?? CreateTabService(),
            prompt ?? CreatePrompt(SaveChangesChoice.Cancel),
            autosave ?? new AutosaveScheduler(NeverDelay));
    }
}
