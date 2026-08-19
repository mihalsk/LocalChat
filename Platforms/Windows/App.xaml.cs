using LanChat.ViewModels;
using Microsoft.Maui;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using System.Diagnostics;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LanChat.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            // Пытаемся зарегистрировать текущий процесс как единственный экземпляр мессенджера
            //var singleInstance = AppInstance.FindOrRegisterForKey("LanChat_SingleInstance_Key");
            //singleInstance.Activated += OnAppInstanceActivated;
            this.InitializeComponent();

            // Регистрируем менеджер уведомлений
            AppNotificationManager.Default.Register();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);
        }
        private void OnAppInstanceActivated(object? sender, AppActivationArguments e)
        {
            // Выводим существующее окно на передний план при клике
            Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
            {
                var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
                if (window != null)
                {
                    var nativeWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                    nativeWindow?.Activate();

                    // Если нужно сразу переключить чат (извлечение peerId из e):
                    if (e.Kind == ExtendedActivationKind.AppNotification &&
                        e.Data is AppNotificationActivatedEventArgs notificationArgs &&
                        notificationArgs.Arguments.TryGetValue("peerId", out var targetPeerId))
                    {
                        if (window.Page?.BindingContext is MainViewModel mainViewModel)
                        {
                            var targetPeer = mainViewModel.Peers.FirstOrDefault(p => p.PeerId == targetPeerId);
                            if (targetPeer != null) mainViewModel.SelectedPeer = targetPeer;
                        }
                    }
                }
            });
        }

        ~App() 
        {
            AppNotificationManager.Default.Unregister();
        }
    }

}
