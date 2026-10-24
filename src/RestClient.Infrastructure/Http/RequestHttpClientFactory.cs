namespace RestClient.Infrastructure.Http;

public static class RequestHttpClientFactory
{
    public static HttpClient Create(HttpTransportOptions options, ConnectTimingRecorder recorder)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = options.FollowRedirects,
            MaxAutomaticRedirections = options.MaxRedirects,
            ConnectCallback = recorder.ConnectAsync,
        };

        return new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
    }
}
