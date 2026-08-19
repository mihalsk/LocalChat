using LanChat.Services;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System.Diagnostics;

namespace LanChat.Platforms.Windows;

public class WindowsNotificationService : INotificationService
{
    public void ShowNotification(string title, string message, string? senderId = null)
    {
        try
        {
            // Используем встроенный в Windows App SDK конструктор уведомлений
            var builder = new AppNotificationBuilder()
                .AddText(title)
                .AddText(message);
            // Если есть ID отправителя, зашиваем его в аргумент запуска (аргумент launch)
            if (!string.IsNullOrEmpty(senderId))
            {
                builder.AddArgument("action", "openChat");
                builder.AddArgument("peerId", senderId);
            }
            var notificationXml = builder.BuildNotification();
            // Отправляем системное уведомление
            AppNotificationManager.Default.Show(notificationXml);
        }
        catch (Exception ex)
        {
            // Уведомление не показалось, но приложение продолжит работать стабильно
            Debug.WriteLine($"[WindowsNotificationService] Ошибка отправки уведомления: {ex.Message}");
        }
    }
}
