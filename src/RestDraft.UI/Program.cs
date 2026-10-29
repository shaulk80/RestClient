using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RestDraft.Application;
using RestDraft.Infrastructure;
using RestDraft.Infrastructure.DataFolder;
using RestDraft.Infrastructure.Persistence;
using RestDraft.UI.Services;

namespace RestDraft.UI;

internal sealed class Program
{
    private static readonly Action<ILogger, string, Exception?> StartupStarted = LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(1, nameof(StartupStarted)),
        "Starting RestDraft with data folder {DataFolder}");

    private static readonly Action<ILogger, Exception?> UiStopped = LoggerMessage.Define(
        LogLevel.Critical,
        new EventId(2, nameof(UiStopped)),
        "The UI stopped because of an unhandled exception");

    // The last-resort boundary: nothing here has a logger yet, so an unexpected failure goes to stderr and a dialog.
    public static async Task<int> Main(string[] args)
    {
        var reporter = StartupErrorReporter.CreateDefault();
        try
        {
            return await RunAsync(args, reporter);
        }
        catch (Exception exception)
        {
            reporter.Report("RestDraft stopped because of an unexpected error.", exception.ToString());
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args, StartupErrorReporter reporter)
    {
        var environmentValue = Environment.GetEnvironmentVariable(DataFolderLocator.DataDirEnvironmentVariable);
        var folderResult = DataFolderLocator.Locate(args, environmentValue, AppContext.BaseDirectory);
        if (folderResult.Value is not { } dataFolder)
        {
            reporter.Report(folderResult.Error?.Message, folderResult.Error?.Details);
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
            reporter.Report(migrationResult.Error?.Message, migrationResult.Error?.Details);
            return 1;
        }

        return RunUiOnStaThread(host.Services, args, logger, reporter);
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider services)
    {
        return AppBuilder.Configure(() => new App(services)).UsePlatformDetect();
    }

    // The clipboard needs an STA thread. An async Main resumes on an MTA thread after its first await,
    // so the UI runs on a dedicated STA thread instead.
    // An exception that escapes the UI loop is logged with its full text, then shown, instead of ending the process silently.
    private static int RunUiOnStaThread(IServiceProvider services, string[] args, ILogger logger, StartupErrorReporter reporter)
    {
        var exitCode = 0;
        var uiThread = new Thread(() =>
        {
            try
            {
                exitCode = BuildAvaloniaApp(services).StartWithClassicDesktopLifetime(args);
            }
            catch (Exception exception)
            {
                UiStopped(logger, exception);
                reporter.Report("RestDraft stopped because of an unexpected error.", exception.ToString());
                exitCode = 1;
            }
        });
        if (OperatingSystem.IsWindows())
        {
            uiThread.SetApartmentState(ApartmentState.STA);
        }

        uiThread.Start();
        uiThread.Join();
        return exitCode;
    }
}
