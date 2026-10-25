using FakeItEasy;
using RestClient.Application.Abstractions;
using RestClient.Application.Requests;

namespace RestClient.Application.Tests.Requests;

internal static class RequestBuilderFactory
{
    public static RequestBuilder Create(TimeSpan timeout, IFileReader? files = null, IIdGenerator? ids = null)
    {
        var encoder = new MultipartBodyEncoder(files ?? A.Fake<IFileReader>());
        var resolver = new RequestBodyResolver(encoder, ids ?? A.Fake<IIdGenerator>());
        return new RequestBuilder(timeout, resolver);
    }
}
