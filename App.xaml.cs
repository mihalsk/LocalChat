using LanChat.Services;
using LanChat.ViewModels;
using LanChat.Views;

namespace LanChat;

public partial class App : Application
{
    public static event Action AppResumed;
    public static event Action AppStopped;
    public static event Action AppActivated;
    public static event Action AppDeactivated;
    public static string PeerId { get; private set; } = string.Empty;
    public static bool IsMainWindowActive { get; private set; }
    public App()
    {
        InitializeComponent();
        PeerId = Preferences.Get("PeerId", Guid.NewGuid().ToString());
        Preferences.Set("PeerId", PeerId);
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, view) =>
        {
#if ANDROID
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#elif WINDOWS
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
#endif
        });
        //Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        // Получаем MainViewModel через DI и создаём MainPage
        var mainViewModel = MauiProgram.CreateMauiApp().Services.GetRequiredService<MainViewModel>();
        var fileStorage = MauiProgram.CreateMauiApp().Services.GetRequiredService<IFileStorageService>();
        ContentPage mainPage = new Views.MainPage(mainViewModel, fileStorage);
        NavigationPage mainNavigationPage = new NavigationPage(mainPage);
        NavigationPage.SetHasNavigationBar(mainPage, false);
        MainPage = mainNavigationPage;
        
    }
    protected override Window CreateWindow(IActivationState activationState)
    {
        var window = base.CreateWindow(activationState);

        // Window lifecycle events
        window.Resumed += (s, e) => {
            AppResumed?.Invoke();
        };

        window.Stopped += (s, e) => {
            AppStopped?.Invoke();
        };

        window.Activated += (s, e) => {
            AppActivated?.Invoke();
            IsMainWindowActive = true;
        };

        window.Deactivated += (s, e) => {
            AppDeactivated?.Invoke();
            IsMainWindowActive = false;
        };

        return window;
    }

    // Backup methods for broader compatibility
    protected override void OnResume()
    {
        base.OnResume();
        AppResumed?.Invoke();
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        AppStopped?.Invoke();
    }
}