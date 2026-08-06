using LocalChat.Models;
using LocalChat.Services;
using LocalChat.ViewModels;

namespace LocalChat.Views;

public partial class MainPage : ContentPage
{
    private readonly IFileStorageService _fileStorage;
    public MainPage(MainViewModel viewModel, IFileStorageService fileStorage)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _fileStorage = fileStorage;
        // AppInfo.VersionString вернет значение из $(ApplicationDisplayVersion)
        string currentVersion = AppInfo.Current.VersionString;

        // Устанавливаем заголовок
        Title = $"MAUI App v{currentVersion}";
    }
    protected override async void OnAppearing()
    {
        
        base.OnAppearing();
        
        if (DeviceInfo.Current.Platform == DevicePlatform.Android &&
            OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
                await DisplayAlertAsync("Notification", "Enable notifications to receive messages in background.", "OK");
        }
        
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        //(BindingContext as MainViewModel)?.Dispose();
    }
    //private async void OnLabelTapped(object sender, TappedEventArgs e)
    //{
    //    if (sender is Label label && !string.IsNullOrEmpty(label.Text))
    //    {
    //        await MainThread.InvokeOnMainThreadAsync(async () =>
    //        {
    //            await Clipboard.Default.SetTextAsync(label.Text);
    //        });
    //    }
    //}
    
    private async void OnMessageTapped(object sender, TappedEventArgs e)
    {
        if ((sender is VerticalStackLayout tappedMessage) && tappedMessage.BindingContext is Message message)
        {
            if (message.IsFileMessage && !string.IsNullOrEmpty(message.FilePath))
            {
                // Открываем файл
                try
                {
                    await _fileStorage.OpenFileAsync(message.FilePath);
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync("Ошибка", $"Не удалось открыть файл: {ex.Message}", "OK");
                }
            }
            else if (!string.IsNullOrEmpty(message.Content))
            {
                // Копируем текст
                await Clipboard.Default.SetTextAsync(message.Content);
            }
        }
    }

    //private async void OnToSettingsTapped(object sender, TappedEventArgs e)
    //{
    //    var settingsViewModel = MauiProgram.CreateMauiApp().Services.GetRequiredService<SettingsViewModel>();
    //    await Navigation.PushModalAsync(new SettingsPage(settingsViewModel));
    //}
}