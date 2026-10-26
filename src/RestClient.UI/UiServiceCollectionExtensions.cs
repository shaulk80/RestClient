using Microsoft.Extensions.DependencyInjection;
using RestClient.UI.Services;
using RestClient.UI.ViewModels;

namespace RestClient.UI;

public static class UiServiceCollectionExtensions
{
    public static IServiceCollection AddUi(this IServiceCollection services)
    {
        services.AddSingleton<IDestinationPicker, SaveDestinationPicker>();
        services.AddSingleton<IFilePickerService, StorageFilePickerService>();
        services.AddSingleton<ISecretDialogs, SecretDialogs>();
        services.AddSingleton<ISecretPrompt, SecretPrompt>();
        services.AddSingleton<IEnvironmentDialogs, EnvironmentDialogs>();
        services.AddSingleton<IConfirmationDialogs, ConfirmationDialogs>();
        services.AddSingleton<BodyFormatting>();
        services.AddSingleton<RequestTabServices>();
        services.AddSingleton<CollectionsTreeViewModel>();
        services.AddSingleton<HistoryListViewModel>();
        services.AddSingleton<EnvironmentEditorViewModel>();
        services.AddSingleton<EnvironmentSelectorViewModel>();
        services.AddSingleton<SecretStatusViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        return services;
    }
}
