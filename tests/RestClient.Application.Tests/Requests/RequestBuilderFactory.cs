using FakeItEasy;
using RestClient.Application.Abstractions;
using RestClient.Application.Requests;
using RestClient.Application.Settings;
using RestClient.Domain.Results;

namespace RestClient.Application.Tests.Requests;

internal static class RequestBuilderFactory
{
    public static RequestBuilder Create(TimeSpan timeout, IFileReader? files = null, IIdGenerator? ids = null)
    {
        var settings = A.Fake<ISettingsService>();
        var current = new AppSettings { TimeoutSeconds = (int)timeout.TotalSeconds };
        A.CallTo(() => settings.GetAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<AppSettings>.Success(current)));
        return Create(settings, files, ids);
    }

    public static RequestBuilder Create(ISettingsService settings, IFileReader? files = null, IIdGenerator? ids = null)
    {
        var encoder = new MultipartBodyEncoder(files ?? A.Fake<IFileReader>());
        var resolver = new RequestBodyResolver(encoder, ids ?? A.Fake<IIdGenerator>());
        return new RequestBuilder(settings, resolver);
    }
}
