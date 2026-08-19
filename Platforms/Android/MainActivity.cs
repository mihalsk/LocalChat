using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using AndroidX.Activity;
using LanChat.Platforms.Android.Services;
using LanChat.ViewModels;
using LocalChat.Services;

namespace LanChat.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private bool _backPressedOnce = false;
    private readonly Handler _handler = new(Looper.MainLooper);
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            // Запрос уведомлений (Android 13+)
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
                RequestPermissions(new[] { Manifest.Permission.PostNotifications }, 0);
            }
            NotificationHelper.CreateNotificationChannel(this);//////////////////
            
        }
        StartForegroundService();
        // Регистрируем современный перехватчик кнопки "Назад"
        OnBackPressedDispatcher.AddCallback(this, new MainBackPressedCallback(this));
        // Проверяем «холодный» старт из уведомления
        ProcessIntentData(Intent);
    }
    // Этот метод вызывается, если приложение уже было открыто, и по уведомлению кликнули повторно
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        ProcessIntentData(intent);
    }
    private void ProcessIntentData(Intent? intent)
    {
        if (intent != null && intent.HasExtra("target_peer_id"))
        {
            string? targetPeerId = intent.GetStringExtra("target_peer_id");

            if (!string.IsNullOrEmpty(targetPeerId))
            {
                // Перенаправляем выполнение в поток MAUI
                Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(async () =>
                {
                    // Небольшая пауза для инициализации базы и UI, если это был холодный старт
                    await Task.Delay(1000);

                    var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
                    if (window?.Page?.BindingContext is MainViewModel mainViewModel)
                    {
                        var targetPeer = mainViewModel.Peers.FirstOrDefault(p => p.PeerId == targetPeerId);
                        if (targetPeer != null)
                        {
                            mainViewModel.SelectedPeer = targetPeer;
                        }
                    }
                });
            }
        }
    }
    private void StartForegroundService()
    {
        var intent = new Intent(this, typeof(AndroidForegroundService));
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            StartForegroundService(intent);
        else
            StartService(intent);
    }

    private class MainBackPressedCallback : OnBackPressedCallback
    {
        private readonly MainActivity _activity;

        // Передаем true, чтобы коллбэк был активен по умолчанию
        public MainBackPressedCallback(MainActivity activity) : base(true)
        {
            _activity = activity;
        }

        public override void HandleOnBackPressed()
        {
            // Получаем доступ к навигации MAUI, чтобы проверить, находимся ли мы на главном экране
            var currentWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();

            // Если в MAUI открыто модальное окно (например, SettingsPage) или стек навигации глубже 1 страницы
            if (currentWindow?.Page?.Navigation != null &&
                (currentWindow.Page.Navigation.ModalStack.Count > 0 || currentWindow.Page.Navigation.NavigationStack.Count > 1))
            {
                // Отключаем на секунду этот перехватчик, даем MAUI штатно закрыть страницу назад, затем включаем обратно
                Enabled = false;
                _activity.OnBackPressedDispatcher.OnBackPressed();
                Enabled = true;
                return;
            }

            // --- Логика двойного тапа на Главном экране ---
            if (_activity._backPressedOnce)
            {
                // Получаем сервис жизненного цикла приложения из контейнера DI
                var lifecycleService = MauiProgram.Services?.GetService<IAppLifecycleService>();

                if (lifecycleService != null)
                {
                    lifecycleService.CloseApplication(); // Полное завершение с остановкой Foreground-сервиса
                }
                else
                {
                    // Резервный вариант, если DI недоступен
                    _activity.FinishAndRemoveTask();
                    global::Android.OS.Process.KillProcess(global::Android.OS.Process.MyPid());
                }
                return;
            }

            // Первое нажатие: взводим флаг и показываем предупреждение
            _activity._backPressedOnce = true;
            Toast.MakeText(_activity, "Нажмите назад еще раз для полного выхода", ToastLength.Short)?.Show(); //

            // Сбрасываем флаг через 2 секунды, если пользователь не нажал кнопку повторно
            _activity._handler.PostDelayed(() =>
            {
                _activity._backPressedOnce = false;
            }, 2000); // Интервал ожидания — 2000 мс
        }
    }
}