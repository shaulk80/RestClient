using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RestClient.Application.Abstractions;
using RestClient.Infrastructure.Logging;
using RestClient.Infrastructure.Persistence;
using RestClient.Infrastructure.Persistence.Migrations;
using RestClient.Infrastructure.Services;
using Serilog;

namespace RestClient.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    private const string DatabaseFileName = "app.db";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, InfrastructureOptions options)
    {
        var logger = LoggingSetup.CreateLogger(options.DataFolder);
        services.AddLogging(builder => builder.ClearProviders().AddSerilog(logger, dispose: true));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, IdGenerator>();

        services.AddSingleton(CreateConnectionFactory(options.DataFolder));
        services.AddSingleton<IMigration, Migration0001Settings>();
        services.AddSingleton<SqliteMigrator>();

        return services;
    }

    private static SqliteConnectionFactory CreateConnectionFactory(string dataFolder)
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataFolder, DatabaseFileName),
        };
        return new SqliteConnectionFactory(connectionStringBuilder.ConnectionString);
    }
}
