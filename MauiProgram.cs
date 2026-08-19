#if ANDROID
using LanChat.Platforms.Android.Services;
#endif
using LanChat.Services;
using LanChat.ViewModels;
using LanChat.Views;
using LocalChat.Services;
using Microsoft.Extensions.Logging;

namespace LanChat;

public static class MauiProgram
{
    public static IServiceProvider? Services { get; private set; }
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            //.UseMauiCompatibility()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Регистрация сервисов
        builder.Services.AddSingleton<NetworkServiceManager>();
#if ANDROID
        builder.Services.AddSingleton<MulticastLockService>();
#endif
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<EncryptionService>();
        builder.Services.AddSingleton<NetworkDiscoveryService>();
        builder.Services.AddSingleton<TcpCommunicationService>();

        

        builder.Services.AddSingleton<FileTransferService>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<SettingsPage>();
        // Android-специфичный сервис


        // NetworkDiscoveryService регистрируем с фабрикой, чтобы передать опциональный MulticastLockService
        builder.Services.AddSingleton(provider =>
        {
            var dbService = provider.GetRequiredService<DatabaseService>();
#if ANDROID
            var multicastLockService = provider.GetService<MulticastLockService>();
            return new NetworkDiscoveryService(dbService, multicastLockService);
#else
            return new NetworkDiscoveryService(dbService);
#endif
        });
#if ANDROID
        builder.Services.AddSingleton<IFileStorageService, Platforms.Android.Services.FileStorageService>();
        builder.Services.AddSingleton<IAppLifecycleService, LocalChat.Platforms.Android.AppLifecycleService>();
        builder.Services.AddSingleton<INotificationService, Platforms.Android.Services.AndroidNotificationService>();
#elif WINDOWS
        builder.Services.AddSingleton<IFileStorageService, Platforms.Windows.FileStorageService>();
        builder.Services.AddSingleton<IAppLifecycleService, LocalChat.Platforms.Windows.AppLifecycleService>();
        builder.Services.AddSingleton<INotificationService, Platforms.Windows.WindowsNotificationService>();
#endif
#if DEBUG
        builder.Logging.AddDebug();
#endif
        var app = builder.Build();
        Services = app.Services;
        return app;
        //return builder.Build();
    }
}