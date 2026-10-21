using System.Globalization;
using Serilog;

namespace RestClient.Infrastructure.Logging;

public static class LoggingSetup
{
    private const string LogFileName = "restclient-.log";

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
