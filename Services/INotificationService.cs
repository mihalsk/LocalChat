using System;
using System.Collections.Generic;
using System.Text;

namespace LanChat.Services
{
    public interface INotificationService
    {
        void ShowNotification(string title, string message, string? senderId = null);
    }
}
