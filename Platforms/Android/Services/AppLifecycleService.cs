using Android.Content;
using Android.OS;
using LanChat.Platforms.Android.Services;
using LocalChat.Services;

namespace LocalChat.Platforms.Android;

public class AppLifecycleService : IAppLifecycleService
{
    public void CloseApplication()
    {
        var context = Platform.AppContext;
        var currentActivity = Platform.CurrentActivity;

        // 1. Принудительно останавливаем ваш фоновый сервис
        var serviceIntent = new Intent(context, typeof(AndroidForegroundService));
        context.StopService(serviceIntent);

        // 2. Закрываем Activity (интерфейс)
        if (currentActivity != null)
        {
            currentActivity.FinishAndRemoveTask(); // Удаляет приложение из меню "Недавние"
        }

        // 3. Даем Android небольшую паузу на очистку ресурсов и жестко убиваем процесс
        new Handler(Looper.MainLooper).PostDelayed(() =>
        {
            Java.Lang.JavaSystem.Exit(0);
            // Standard Android activity finish (Bypasses hard JVM kills)
            // Альтернатива:
            //Process.KillProcess(Process.MyPid());
        }, 200); // 200 миллисекунд достаточно
    }
}
