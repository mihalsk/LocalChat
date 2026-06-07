using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
namespace LocalChat;

public static class NotificationHelper
{
    private const string ChannelId = "localchat_channel";
    private const string ChannelName = "Local Chat Messages";

    public static void CreateNotificationChannel(Context context)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.High);
            var manager = (NotificationManager)context.GetSystemService(Context.NotificationService);
            manager.CreateNotificationChannel(channel);
        }
    }

    public static void ShowMessageNotification(Context context, string title, string message)
    {
        var builder = new NotificationCompat.Builder(context, ChannelId)
            .SetSmallIcon(Microsoft.Maui.Resource.Drawable.notification_icon_background)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetAutoCancel(true);

        var notificationManager = NotificationManagerCompat.From(context);
        notificationManager.Notify(DateTime.Now.Millisecond, builder.Build());
    }
}