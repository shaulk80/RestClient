using Microsoft.Extensions.DependencyInjection;
using RestClient.Application.Abstractions;
using RestClient.Application.Bodies;
using RestClient.Application.Collections;
using RestClient.Application.Environments;
using RestClient.Application.History;
using RestClient.Application.Requests;
using RestClient.Application.Secrets;
using RestClient.Application.Settings;
using RestClient.Application.Tabs;
using RestClient.Application.Workspaces;
using RestClient.Domain.Environments;

namespace RestClient.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IRequestVariables, EnvironmentRequestVariables>();
        services.AddSingleton<ISecretService, SecretService>();
        services.AddSingleton<IEnvironmentService, EnvironmentService>();
        services.AddSingleton(provider =>
        {
            var repository = provider.GetRequiredService<IEnvironmentRepository>();
            return new Workspace<RequestEnvironment>(repository.LoadAsync, repository.SaveAsync);
        });
        services.AddSingleton<MultipartBodyEncoder>();
        services.AddSingleton<RequestBodyResolver>();
        services.AddSingleton<AppSettingsStorage>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ITabService, TabService>();
        services.AddSingleton<RequestBuilder>();
        services.AddSingleton<IRequestSender, RequestSender>();
        services.AddSingleton<CollectionWorkspace>();
        services.AddSingleton<ICollectionService, CollectionService>();
        services.AddSingleton<IFolderService, FolderService>();
        services.AddSingleton<IRequestService, RequestService>();
        services.AddSingleton<ICollectionTreeService, CollectionTreeService>();
        services.AddSingleton(RetentionPolicy.Default);
        services.AddSingleton<HistoryRetention>();
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<JsonBodyFormatter>();
        services.AddSingleton<XmlBodyFormatter>();

        return services;
    }
}
