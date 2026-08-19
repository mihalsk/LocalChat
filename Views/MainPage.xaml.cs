using LanChat.Models;
using LanChat.Services;
using LanChat.ViewModels;

namespace LanChat.Views;

public partial class MainPage : ContentPage
{
    private readonly IFileStorageService _fileStorage;
    private readonly DatabaseService _databaseService;
    public MainPage(MainViewModel viewModel, IFileStorageService fileStorage) //, DatabaseService databaseService)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _fileStorage = fileStorage;
        //_databaseService = databaseService;
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
    }
    
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

    private void CollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }

    private async void OnLoadMoreMessages(object sender, EventArgs e)
    {
        //var collectionView = (CollectionView)sender;

        // Выключаем триггер на время загрузки, чтобы избежать повторных вызовов
        //collectionView.RemainingItemsThreshold = -1;

        //try
        //{
        //    // 1. Получаем старые сообщения из базы данных или API
        //    var oldMessages = await _databaseService.GetOlderMessagesAsync(page: 2);

        //    if (oldMessages != null && oldMessages.Any())
        //    {
        //        // 2. Добавляем старые сообщения в КОНЕЦ вашей ObservableCollection
        //        foreach (var msg in oldMessages)
        //        {
        //            ViewModel.Messages.Add(msg);
        //        }

        //        // 3. Возвращаем порог срабатывания для следующей пагинации
        //        collectionView.RemainingItemsThreshold = 5;
        //    }
        //}
        //catch (Exception ex)
        //{
        //    // Обработка ошибок
        //    collectionView.RemainingItemsThreshold = 5;
        //}

        // Безопасно приводим BindingContext к вашей ViewModel
        if (BindingContext is MainViewModel viewModel)
        {
            var collectionView = (CollectionView)sender;

            // Отключаем триггер, чтобы избежать дублирующих запросов во время загрузки
            collectionView.RemainingItemsThreshold = -1;

            // Вызываем метод загрузки во ViewModel
            bool hasMoreData = await viewModel.LoadOlderMessagesAsync();

            // Если данные еще есть, возвращаем порог срабатывания обратно
            if (hasMoreData)
            {
                collectionView.RemainingItemsThreshold = 5;
            }
        }
    }

    //private async void OnToSettingsTapped(object sender, TappedEventArgs e)
    //{
    //    var settingsViewModel = MauiProgram.CreateMauiApp().Services.GetRequiredService<SettingsViewModel>();
    //    await Navigation.PushModalAsync(new SettingsPage(settingsViewModel));
    //}
}