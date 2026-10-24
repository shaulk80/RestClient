using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RestClient.Application;
using RestClient.Infrastructure;
using RestClient.Infrastructure.DataFolder;
using RestClient.Infrastructure.Persistence;

namespace RestClient.UI;

internal sealed class Program
{
    private static readonly Action<ILogger, string, Exception?> StartupStarted = LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(1, nameof(StartupStarted)),
        "Starting REST Client with data folder {DataFolder}");

    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        var environmentValue = Environment.GetEnvironmentVariable(DataFolderLocator.DataDirEnvironmentVariable);
        var folderResult = DataFolderLocator.Locate(args, environmentValue, AppContext.BaseDirectory);
        if (folderResult.Value is not { } dataFolder)
        {
            ReportStartupError(folderResult.Error?.Message, folderResult.Error?.Details);
            return 1;
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(new InfrastructureOptions(dataFolder));
        builder.Services.AddUi();

        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        StartupStarted(logger, dataFolder, null);

        var migrationResult = await host.Services.GetRequiredService<SqliteMigrator>().MigrateAsync(CancellationToken.None);
        if (!migrationResult.IsSuccess)
        {
            ReportStartupError(migrationResult.Error?.Message, migrationResult.Error?.Details);
            return 1;
        }

        return BuildAvaloniaApp(host.Services).StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider services)
    {
        return AppBuilder.Configure(() => new App(services)).UsePlatformDetect();
    }

    private static void ReportStartupError(string? message, string? details)
    {
        Console.Error.WriteLine($"{message} {details}");
    }
}
