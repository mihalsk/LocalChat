using LocalChat.Services;

namespace LocalChat.Platforms.Windows;

public class AppLifecycleService : IAppLifecycleService
{
    public void CloseApplication()
    {
        Application.Current?.Quit();
    }
}
