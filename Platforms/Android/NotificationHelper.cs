using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
namespace LanChat.Platforms.Android;

public static class NotificationHelper
{
    public const string ChannelId = "lanchat_channel";
    public const string SilentChannelId = "lanchat_channel_silent";
    public static void CreateNotificationChannel(Context context)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var manager = (NotificationManager)context.GetSystemService(Context.NotificationService);

            // Канал для чата (со звуком/всплытием)
            var chatChannel = new NotificationChannel(ChannelId, "Чат - Сообщения", NotificationImportance.High);
            manager.CreateNotificationChannel(chatChannel);

            // Канал для Фонового сервиса (тихий)
            var serviceChannel = new NotificationChannel(SilentChannelId, "Работа в фоне", NotificationImportance.Low);
            manager.CreateNotificationChannel(serviceChannel);
        }
    }

    public static void ShowMessageNotification(Context context, string title, string message)
    {
        var builder = new NotificationCompat.Builder(context, ChannelId)
            .SetSmallIcon(Resource.Drawable.messagesquare)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetAutoCancel(true);

        var notificationManager = NotificationManagerCompat.From(context);
        notificationManager.Notify(DateTime.Now.Millisecond, builder.Build());
    }
}