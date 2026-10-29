using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RestDraft.Application.Abstractions;
using RestDraft.Infrastructure.Http;
using RestDraft.Infrastructure.Logging;
using RestDraft.Infrastructure.Persistence;
using RestDraft.Infrastructure.Security;
using RestDraft.Infrastructure.Services;
using Serilog;

namespace RestDraft.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    private const string DatabaseFileName = "app.db";
    private const string MigrationScriptsFolderName = "MigrationScripts";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, InfrastructureOptions options)
    {
        var logger = LoggingSetup.CreateLogger(options.DataFolder);
        services.AddLogging(builder => builder.ClearProviders().AddSerilog(logger, dispose: true));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, IdGenerator>();

        services.AddSingleton(CreateConnectionFactory(options.DataFolder));
        services.AddSingleton<IMigrationSource>(provider => new SqlScriptMigrationSource(
            Path.Combine(AppContext.BaseDirectory, MigrationScriptsFolderName),
            provider.GetRequiredService<ILogger<SqlScriptMigrationSource>>()));
        services.AddSingleton<IMigrationSource, CodeMigrationSource>();
        services.AddSingleton<MigrationSetBuilder>();
        services.AddSingleton<SqliteMigrator>();
        services.AddSingleton<ICollectionRepository, SqliteCollectionRepository>();
        services.AddSingleton<IHistoryRepository, SqliteHistoryRepository>();
        services.AddSingleton<IEnvironmentRepository, SqliteEnvironmentRepository>();
        services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
        services.AddSingleton<IOpenTabsStore, SqliteOpenTabsStore>();
        services.AddSingleton<ISecretCipher, AesGcmSecretCipher>();
        services.AddSingleton<IKeyDeriver, Pbkdf2KeyDeriver>();
        services.AddSingleton<IRandomBytes, SecureRandomBytes>();

        services.AddSingleton(options);
        services.AddSingleton<ResponseBodyReader>();
        services.AddSingleton<IHttpExecutor, HttpRequestExecutor>();
        services.AddSingleton<IBodyFileStore, BodyFileStore>();
        services.AddSingleton<IFileReader, LocalFileReader>();

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
