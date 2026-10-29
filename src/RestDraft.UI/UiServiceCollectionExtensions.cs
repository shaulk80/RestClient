using Microsoft.Extensions.DependencyInjection;
using RestDraft.UI.Services;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI;

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
        services.AddSingleton<ISaveChangesPrompt, SaveChangesPrompt>();
        services.AddSingleton<ISettingsDialogs, SettingsDialogs>();
        services.AddSingleton(new AutosaveScheduler(AutosaveScheduler.DefaultDelay));
        services.AddSingleton<TabStripViewModel>();
        services.AddSingleton<BodyFormatting>();
        services.AddSingleton<RequestTabServices>();
        services.AddSingleton<CollectionsTreeViewModel>();
        services.AddSingleton<HistoryListViewModel>();
        services.AddSingleton<EnvironmentEditorViewModel>();
        services.AddSingleton<EnvironmentSelectorViewModel>();
        services.AddSingleton<SecretStatusViewModel>();
        services.AddSingleton<WorkspaceLayoutViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        return services;
    }
}
