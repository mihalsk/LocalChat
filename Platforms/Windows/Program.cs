#if WINDOWS

using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LocalChat.WinUI;

public static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        bool isNewInstance;
        _mutex = new Mutex(true, "Local\\LanChat_SingleInstance_Mutex_Key", out isNewInstance);

        if (!isNewInstance)
        {
            // Это дублирующий экземпляр от клика по уведомлению
            var currentInstance = AppInstance.GetCurrent();
            var activatedArgs = currentInstance.GetActivatedEventArgs();

            var singleInstance = AppInstance.FindOrRegisterForKey("LanChat_SingleInstance_Key");
            if (singleInstance != null)
            {
                // КРИТИЧЕСКИ ВАЖНО: Ждем завершения перенаправления аргументов клика в основное окно.
                // .GetAwaiter().GetResult() заставит поток дождаться окончания отправки COM-пакета
                singleInstance.RedirectActivationToAsync(activatedArgs).GetAwaiter().GetResult();
            }

            // Освобождаем ресурсы ядра
            _mutex.Dispose();

            // ИСПОЛЬЗУЕМ ЭТО: Мягкое завершение процесса без вызова окна ошибки Windows ( FailFast )
            Environment.Exit(0);
            return;
        }

        AppInstance.FindOrRegisterForKey("LanChat_SingleInstance_Key");

        Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);

            _ = new LanChat.WinUI.App();
        });

        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}

#endif
