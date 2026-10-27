using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RestClient.Integration.Tests;

public sealed class TestHttpServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    public TestHttpServer()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        _app = builder.Build();
        MapEndpoints(_app);
    }

    public Uri BaseAddress { get; private set; } = new("http://127.0.0.1");

    public async Task StartAsync()
    {
        await _app.StartAsync();

        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        BaseAddress = new Uri(addresses!.Addresses.First());
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/ok", () => Results.Text("hello"));
        app.MapGet("/redirect", () => Results.Redirect("/ok"));
        app.MapGet("/slow", async (CancellationToken cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Results.Text("late");
        });
        app.MapGet("/redirect/{hops:int}", (int hops) => Results.Redirect(hops <= 1 ? "/ok" : $"/redirect/{hops - 1}"));
        app.MapGet("/delay/{milliseconds:int}", async (int milliseconds, CancellationToken cancellationToken) =>
        {
            await Task.Delay(milliseconds, cancellationToken);
            return Results.Text("done");
        });
        app.MapGet("/bytes/{size:int}", (int size) => Results.Bytes(new byte[size]));
    }
}
