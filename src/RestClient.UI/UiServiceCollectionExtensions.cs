using Microsoft.Extensions.DependencyInjection;
using RestClient.UI.ViewModels;

namespace RestClient.UI;

public static class UiServiceCollectionExtensions
{
    public static IServiceCollection AddUi(this IServiceCollection services)
    {
        services.AddSingleton<CollectionsTreeViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        return services;
    }
}
