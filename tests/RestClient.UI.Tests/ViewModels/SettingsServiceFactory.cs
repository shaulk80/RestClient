using FakeItEasy;
using RestClient.Application.Settings;
using RestClient.Domain.Results;

namespace RestClient.UI.Tests.ViewModels;

internal static class SettingsServiceFactory
{
    // Reads give the given settings, and every save is accepted and replaces them, as the real service does.
    public static ISettingsService Create(AppSettings? settings = null)
    {
        var current = settings ?? AppSettings.Default;
        var service = A.Fake<ISettingsService>();
        A.CallTo(() => service.GetAsync(A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult(Result<AppSettings>.Success(current)));
        A.CallTo(() => service.SaveAsync(A<AppSettings>._, A<CancellationToken>._))
            .Invokes((AppSettings saved, CancellationToken _) => current = saved)
            .Returns(Task.FromResult(Result.Success()));
        return service;
    }
}
