using LocalChat.Services;
using LocalChat.ViewModels;

namespace LocalChat;

public partial class App : Application
{
    public static string PeerId { get; private set; } = string.Empty;

    public App()
    {
        InitializeComponent();
        PeerId = Preferences.Get("PeerId", Guid.NewGuid().ToString());
        Preferences.Set("PeerId", PeerId);

        // Получаем MainViewModel через DI и создаём MainPage
        var mainViewModel = MauiProgram.CreateMauiApp().Services.GetRequiredService<MainViewModel>();
        var fileStorage = MauiProgram.CreateMauiApp().Services.GetRequiredService<IFileStorageService>();
        ContentPage mainPage = new Views.MainPage(mainViewModel, fileStorage);
        NavigationPage mainNavigationPage = new NavigationPage(mainPage);
        NavigationPage.SetHasNavigationBar(mainPage, false);
        MainPage = mainNavigationPage;
        
    }
}