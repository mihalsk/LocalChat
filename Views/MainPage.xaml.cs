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
        (BindingContext as MainViewModel)?.Dispose();
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
    private async void OnLabelTapped(object sender, TappedEventArgs e)
    {
        if (sender is Label label && label.BindingContext is Message message)
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
                    await DisplayAlert("Ошибка", $"Не удалось открыть файл: {ex.Message}", "OK");
                }
            }
            else if (!string.IsNullOrEmpty(label.Text))
            {
                // Копируем текст
                await Clipboard.Default.SetTextAsync(label.Text);
            }
        }
    }
}