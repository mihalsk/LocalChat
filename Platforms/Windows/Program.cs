#if WINDOWS

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace LocalChat.WinUI;

public static class Program
{
    private static Mutex? _mutex;

    // Импортируем нативные функции Windows API для управления окнами
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    [STAThread]
    static void Main(string[] args)
    {
        // 1. Инициализируем COM-компоненты
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // 2. Проверяем запущенную копию через Mutex
        bool isNewInstance;
        _mutex = new Mutex(true, "Local\\LanChat_SingleInstance_Mutex_Key", out isNewInstance);

        if (!isNewInstance)
        {
            // --- СЮДА МЫ ПОПАДАЕМ ПРИ КЛИКЕ НА УВЕДОМЛЕНИЕ ---

            // Ищем процесс основного (первого) окна по имени
            Process current = Process.GetCurrentProcess();
            foreach (Process process in Process.GetProcessesByName(current.ProcessName))
            {
                // Находим процесс, ID которого не совпадает с текущим (дублирующим)
                if (process.Id != current.Id)
                {
                    IntPtr handle = process.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        // Если окно свернуто в панель задач — восстанавливаем его
                        if (IsIconic(handle))
                        {
                            ShowWindow(handle, SW_RESTORE);
                        }
                        else
                        {
                            ShowWindow(handle, SW_SHOW);
                        }

                        // Принудительно выводим окно на передний план перед пользователем
                        SetForegroundWindow(handle);
                    }
                    break;
                }
            }

            // Тихо закрываем дубликат без вызова критических ошибок и плашек
            _mutex.Dispose();
            Environment.Exit(0);
            return;
        }

        // Если это первый запуск — просто стартуем стандартную графическую оболочку MAUI
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