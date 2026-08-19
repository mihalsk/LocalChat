using Android.App;
using Android.Content;
using AndroidX.Core.App;
using LanChat.Services;
using LanChat.Services;

namespace LanChat.Platforms.Android.Services;

public class AndroidNotificationService : INotificationService
{
    //private const string ChannelId = "lanchat_messages_channel";

    public void ShowNotification(string title, string message, string? senderId = null)
    {
        var context = Platform.AppContext;

        // Создаем Intent для запуска главного экрана
        var intent = new Intent(context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop); // Не плодит копии окон

        // Флаг Immutable обязателен для Android 12+
        var pendingIntentFlags = OperatingSystem.IsAndroidVersionAtLeast(31)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;
        // Передаем peerId внутрь Intent
        if (!string.IsNullOrEmpty(senderId))
        {
            intent.PutExtra("target_peer_id", senderId);
        }
        var pendingIntent = PendingIntent.GetActivity(context, 0, intent, pendingIntentFlags);

        var builder = new NotificationCompat.Builder(context, NotificationHelper.ChannelId)
            .SetSmallIcon(Microsoft.Maui.Resource.Drawable.messagesquare) // Проверьте наличие иконки
            .SetContentTitle(title)
            .SetContentText(message)
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetContentIntent(pendingIntent) // Отвечает за открытие по тапу
            .SetAutoCancel(true);

        var notificationManager = NotificationManagerCompat.From(context);
        notificationManager.Notify(Guid.NewGuid().GetHashCode(), builder.Build());
    }
}

