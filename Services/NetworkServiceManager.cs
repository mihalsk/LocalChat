using LanChat.Models;

namespace LanChat.Services;

public class NetworkServiceManager
{
    private readonly NetworkDiscoveryService _discovery;
    private readonly TcpCommunicationService _tcpComm;
    private bool _isStarted = false;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

    // Публичные события для MainViewModel (и других подписчиков)
    public event Action<Peer>? PeerDiscovered;
    public event Func<Message, Task>? MessageReceived;
    public event Func<Message, Task>? FileReceived;
    public NetworkServiceManager(NetworkDiscoveryService discovery, TcpCommunicationService tcpComm)
    {
        _discovery = discovery;
        _tcpComm = tcpComm;
        // 1. Увязка синхронного Action (проброс напрямую)
        _discovery.PeerDiscovered += peer => PeerDiscovered?.Invoke(peer);

        // 2. Увязка асинхронных Func<Message, Task> через обертку
        _tcpComm.MessageReceived += OnTcpMessageReceived;
        _tcpComm.FileReceived += OnTcpFileReceived;
        var hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        System.Diagnostics.Debug.WriteLine($"[DI-DEBUG] Создан экземпляр {nameof(NetworkServiceManager)} с хэшем: {hash}");
    }

    // Обработчик асинхронного события сообщения
    private async Task OnTcpMessageReceived(Message message)
    {
        var handler = MessageReceived;
        if (handler != null)
        {
            // Безопасно вызываем всех подписчиков (например, MainViewModel) 
            // и дожидаемся завершения их асинхронной логики
            await InvokeAsync(handler, message);
        }
    }

    // Обработчик асинхронного события файла
    private async Task OnTcpFileReceived(Message message)
    {
        var handler = FileReceived;
        if (handler != null)
        {
            await InvokeAsync(handler, message);
        }
    }
    // Вспомогательный метод для последовательного выполнения всех асинхронных подписчиков
    private static async Task InvokeAsync<T>(Func<T, Task> handlers, T arg)
    {
        foreach (Func<T, Task> handler in handlers.GetInvocationList())
        {
            await handler(arg);
        }
    }

    // Проброс метода отправки сообщения
    public async Task SendMessageAsync(Peer targetPeer, string text)
    {
        // Здесь при необходимости можно добавить общую валидацию перед отправкой
        if (targetPeer == null) throw new ArgumentNullException(nameof(targetPeer));
        if (string.IsNullOrWhiteSpace(text)) return;

        // Делегируем задачу TCP-сервису
        await _tcpComm.SendMessageAsync(targetPeer, text);
    }
    public async Task StartAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await StartInternalAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await StopInternalAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RestartAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await StopInternalAsync();
            await StartInternalAsync();
            System.Diagnostics.Debug.WriteLine("Network services restarted.");
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task StartInternalAsync()
    {
        if (_isStarted) return;

        // Инициализация сервисов (они читают настройки из БД)
        await _discovery.InitializeAsync();
        await _tcpComm.InitializeAsync();
        // Запуск
        await _discovery.StartAsync();
        await _tcpComm.StartServerAsync();

        _isStarted = true;
        System.Diagnostics.Debug.WriteLine("Network services started.");
    }

    private async Task StopInternalAsync()
    {
        if (!_isStarted) return;

        await _discovery.StopAsync();
        await _tcpComm.StopServerAsync();

        _isStarted = false;
        System.Diagnostics.Debug.WriteLine("Network services stopped.");
    }
}