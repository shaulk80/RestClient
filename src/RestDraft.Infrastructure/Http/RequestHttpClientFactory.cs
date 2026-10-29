using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace RestDraft.Infrastructure.Http;

public static class RequestHttpClientFactory
{
    // Certificate validation is off only when the user turns SSL verification off in the settings, for example for a local test server.
    [SuppressMessage(
        "Security",
        "CA5359:Do Not Disable Certificate Validation",
        Justification = "A user setting turns certificate validation off on purpose.")]
    public static HttpClient Create(HttpTransportOptions options, ConnectTimingRecorder recorder)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = options.FollowRedirects,
            MaxAutomaticRedirections = options.MaxRedirects,
            ConnectCallback = recorder.ConnectAsync,
        };

        if (!options.VerifySsl)
        {
            handler.SslOptions.RemoteCertificateValidationCallback = AcceptAnyCertificate;
        }

        if (Uri.TryCreate(options.ProxyUrl, UriKind.Absolute, out var proxyUri))
        {
            handler.Proxy = new WebProxy(proxyUri);
            handler.UseProxy = true;
        }

        return new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private static bool AcceptAnyCertificate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors errors)
    {
        return true;
    }
}
