using System.Globalization;
using Serilog;

namespace RestDraft.Infrastructure.Logging;

public static class LoggingSetup
{
    private const string LogFileName = "restdraft-.log";

    public static Serilog.ILogger CreateLogger(string dataFolder)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(dataFolder, "logs", LogFileName),
                rollingInterval: RollingInterval.Day,
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();
    }
}
