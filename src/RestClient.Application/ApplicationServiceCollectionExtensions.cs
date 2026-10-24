using Microsoft.Extensions.DependencyInjection;
using RestClient.Application.Abstractions;
using RestClient.Application.Requests;

namespace RestClient.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(new RequestBuilder(RequestBuilder.DefaultTimeout));
        services.AddSingleton<IRequestSender, RequestSender>();

        return services;
    }
}
