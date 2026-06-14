using LocalChat.Services;
using LocalChat.ViewModels;
using LocalChat.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Compatibility.Hosting;

namespace LocalChat;

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
        builder.Services.AddSingleton<Platforms.Android.Services.MulticastLockService>();
#endif
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<EncryptionService>();
        builder.Services.AddSingleton<NetworkDiscoveryService>();
        builder.Services.AddSingleton<TcpCommunicationService>();

        

        builder.Services.AddSingleton<FileTransferService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<SettingsPage>();
        // Android-специфичный сервис


        // NetworkDiscoveryService регистрируем с фабрикой, чтобы передать опциональный MulticastLockService
        builder.Services.AddSingleton(provider =>
        {
            var dbService = provider.GetRequiredService<DatabaseService>();
#if ANDROID
            var multicastLockService = provider.GetService<Platforms.Android.Services.MulticastLockService>();
            return new NetworkDiscoveryService(dbService, multicastLockService);
#else
            return new NetworkDiscoveryService(dbService);
#endif
        });


#if DEBUG
        builder.Logging.AddDebug();
#endif
        var app = builder.Build();
        Services = app.Services;
        return app;
        //return builder.Build();
    }
}