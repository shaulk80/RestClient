using System.Diagnostics;
using System.Net.Sockets;

namespace RestClient.Infrastructure.Http;

public sealed class ConnectTimingRecorder
{
    public TimeSpan? ConnectDuration { get; private set; }

    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        ConnectDuration = Stopwatch.GetElapsedTime(start);
        return new NetworkStream(socket, ownsSocket: true);
    }
}
