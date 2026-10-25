using Microsoft.Extensions.DependencyInjection;
using RestClient.Application.Abstractions;
using RestClient.Application.Bodies;
using RestClient.Application.Collections;
using RestClient.Application.History;
using RestClient.Application.Requests;

namespace RestClient.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<MultipartBodyEncoder>();
        services.AddSingleton<RequestBodyResolver>();
        services.AddSingleton(provider => new RequestBuilder(
            RequestBuilder.DefaultTimeout,
            provider.GetRequiredService<RequestBodyResolver>()));
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
