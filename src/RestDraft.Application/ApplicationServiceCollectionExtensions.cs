using Microsoft.Extensions.DependencyInjection;
using RestDraft.Application.Abstractions;
using RestDraft.Application.Bodies;
using RestDraft.Application.Collections;
using RestDraft.Application.Environments;
using RestDraft.Application.History;
using RestDraft.Application.Requests;
using RestDraft.Application.Secrets;
using RestDraft.Application.Settings;
using RestDraft.Application.Tabs;
using RestDraft.Application.Workspaces;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Environments;

namespace RestDraft.Application;

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
        services.AddSingleton(provider =>
        {
            var repository = provider.GetRequiredService<ICollectionRepository>();
            return new Workspace<Collection>(repository.LoadAsync, repository.SaveAsync);
        });
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
