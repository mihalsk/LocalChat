using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using LanChat;
using LanChat.Services;

namespace LanChat.Platforms.Android.Services;

[Service(ForegroundServiceType = ForegroundService.TypeDataSync)]
public class AndroidForegroundService : Service
{
    private const int NotificationId = 1001;
    private NetworkServiceManager? _networkServiceManager;
    public override void OnCreate()
    {
        base.OnCreate();
        var services = MauiProgram.Services;
        if (services != null)
        {
            _networkServiceManager = services.GetService<NetworkServiceManager>();
        }
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // Интент для открытия приложения по тапу на плашку сервиса
        var appIntent = new Intent(this, typeof(MainActivity));
        appIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);

        var pendingIntentFlags = Build.VERSION.SdkInt >= BuildVersionCodes.S
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetActivity(this, 0, appIntent, pendingIntentFlags);

        // Используем созданный нами в Helper "тихий" канал
        var notification = new NotificationCompat.Builder(this, NotificationHelper.SilentChannelId)
            .SetContentTitle("Чадище")
            .SetContentText("Запущено в фоновом режиме")
            .SetSmallIcon(Resource.Drawable.messagesquare) // проверьте имя ресурса
            .SetContentIntent(pendingIntent) // <--- ПРИВЯЗКА ТАПА
            .SetOngoing(true)
            .Build();

        if (Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake)
        {
            StartForeground(NotificationId, notification, ForegroundService.TypeDataSync);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }

        return StartCommandResult.Sticky;
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        base.OnDestroy();
    }
}