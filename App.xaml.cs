namespace LocalChat;

public partial class App : Application
{
    public static string PeerId { get; private set; } = string.Empty;

    public App()
    {
        InitializeComponent();
        PeerId = Preferences.Get("PeerId", Guid.NewGuid().ToString());
        Preferences.Set("PeerId", PeerId);
        MainPage = new NavigationPage(new Views.MainPage(
            MauiProgram.CreateMauiApp().Services.GetRequiredService<ViewModels.MainViewModel>()));
    }
}