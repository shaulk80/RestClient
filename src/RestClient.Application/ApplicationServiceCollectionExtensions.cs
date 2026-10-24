using Microsoft.Extensions.DependencyInjection;
using RestClient.Application.Abstractions;
using RestClient.Application.Collections;
using RestClient.Application.Requests;

namespace RestClient.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(new RequestBuilder(RequestBuilder.DefaultTimeout));
        services.AddSingleton<IRequestSender, RequestSender>();
        services.AddSingleton<CollectionWorkspace>();
        services.AddSingleton<ICollectionService, CollectionService>();
        services.AddSingleton<IFolderService, FolderService>();
        services.AddSingleton<IRequestService, RequestService>();
        services.AddSingleton<ICollectionTreeService, CollectionTreeService>();

        return services;
    }
}
