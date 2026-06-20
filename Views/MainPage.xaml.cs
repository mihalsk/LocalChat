using LocalChat.ViewModels;

namespace LocalChat.Views;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
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
    private async void OnLabelTapped(object sender, TappedEventArgs e)
    {
        if (sender is Label label && !string.IsNullOrEmpty(label.Text))
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Clipboard.Default.SetTextAsync(label.Text);
            });
        }
    }
}