using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LocalChat.Models;

namespace LocalChat.Services;

public class TcpCommunicationService
{
    private readonly DatabaseService _dbService;
    private readonly EncryptionService _encryption;
    private int _configuredPort;
    private int _actualListenPort;
    private TcpListener? _listener;
    private CancellationTokenSource? _listenerCts;
    private bool _initialized = false;

    public event Func<Message, Task>? MessageReceived;

    public TcpCommunicationService(DatabaseService dbService, EncryptionService encryption)
    {
        _dbService = dbService;
        _encryption = encryption;
        _configuredPort = 9000;
        _actualListenPort = 0;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _configuredPort = int.Parse(await _dbService.GetSetting(SettingsKeys.TcpListenPort) ?? "9000");
        _initialized = true;
    }

    /// <summary>
    /// Поиск свободного порта, начиная с заданного
    /// </summary>
    private async Task<int> FindFreePortAsync(int startPort, int maxAttempts = 100)
    {
        startPort = 9000;
        for (int port = startPort; port < startPort + maxAttempts; port++)
        {
            try
            {
                var tempListener = new TcpListener(IPAddress.Any, port);
                tempListener.Start();
                tempListener.Stop();
                System.Diagnostics.Debug.WriteLine($"Free TCP port {port}");
                return port; // порт свободен
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                continue; // порт занят, пробуем следующий
            }
        }
        throw new Exception($"No free ports available in range {startPort}-{startPort + maxAttempts - 1}");
    }

    public async Task StartServerAsync()
    {
        if (!_initialized)
            await InitializeAsync();

        try
        {
            _actualListenPort = _configuredPort;
            _listener = new TcpListener(IPAddress.Any, _actualListenPort);
            _listener.Start();
            System.Diagnostics.Debug.WriteLine($"TCP server started on port {_actualListenPort} {_listener.LocalEndpoint}");
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            System.Diagnostics.Debug.WriteLine($"Port {_configuredPort} is busy, finding free port...");
            // Ищем свободный порт, начиная с _configuredPort + 1
            _actualListenPort = await FindFreePortAsync(_configuredPort + 1);

            // Сохраняем новый порт в настройки, чтобы при следующем запуске использовать его
            await _dbService.SetSetting(SettingsKeys.TcpListenPort, _actualListenPort.ToString());
            _configuredPort = _actualListenPort;

            _listener = new TcpListener(IPAddress.Any, _actualListenPort);
            _listener.Start();
            System.Diagnostics.Debug.WriteLine($"TCP server started on alternate port {_actualListenPort}");
        }

        _listenerCts = new CancellationTokenSource();
        _ = Task.Run(() => AcceptClientsAsync(_listenerCts.Token));
    }

    public async Task StopServerAsync()
    {
        _listenerCts?.Cancel();
        _listener?.Stop();
        //_listener?.Dispose();
        await Task.CompletedTask;
    }

    private async Task AcceptClientsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(token);
                _ = HandleClientAsync(client, token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Accept error: {ex}"); }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using var stream = client.GetStream();
        var buffer = new byte[4096];
        var ms = new MemoryStream();
        int bytes;
        while ((bytes = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
        {
            ms.Write(buffer, 0, bytes);
            if (bytes < buffer.Length) break;
        }
        var json = Encoding.UTF8.GetString(ms.ToArray());
        try
        {
            var msgDto = JsonSerializer.Deserialize<MessageDto>(json);
            if (msgDto != null)
            {
                var password = await _dbService.GetSetting(SettingsKeys.EncryptionPassword) ?? "default2026!";
                var decryptedContent = _encryption.Decrypt(msgDto.EncryptedContent, password);
                var message = new Message
                {
                    SenderPeerId = msgDto.SenderPeerId,
                    RecipientPeerId = App.PeerId,
                    Content = decryptedContent,
                    Timestamp = msgDto.Timestamp,
                    IsSentByMe = false,
                    IsFileMessage = msgDto.IsFileMessage,
                    OriginalFileName = msgDto.OriginalFileName
                };
                await _dbService.SaveMessageAsync(message);
                MessageReceived?.Invoke(message);
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"HandleClient error: {ex}"); }
    }

    public async Task SendMessageAsync(Peer recipient, string content, bool isFileMessage = false, string? originalFileName = null)
    {
        var password = await _dbService.GetSetting(SettingsKeys.EncryptionPassword) ?? "default2026!";
        var encrypted = _encryption.Encrypt(content, password);
        var dto = new MessageDto
        {
            SenderPeerId = App.PeerId,
            Timestamp = DateTime.UtcNow,
            EncryptedContent = encrypted,
            IsFileMessage = isFileMessage,
            OriginalFileName = originalFileName
        };
        var json = JsonSerializer.Serialize(dto);
        var data = Encoding.UTF8.GetBytes(json);

        using var client = new TcpClient();
        await client.ConnectAsync(recipient.IpAddress, recipient.TcpPort);
        await using var stream = client.GetStream();
        await stream.WriteAsync(data);
    }

    private class MessageDto
    {
        public string SenderPeerId { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string EncryptedContent { get; set; } = "";
        public bool IsFileMessage { get; set; }
        public string? OriginalFileName { get; set; }
    }
}