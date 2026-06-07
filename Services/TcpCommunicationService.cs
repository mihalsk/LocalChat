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
    private readonly int _listenPort;
    private TcpListener? _listener;
    private CancellationTokenSource? _listenerCts;
    public event Func<Message, Task>? MessageReceived;

    public TcpCommunicationService(DatabaseService dbService, EncryptionService encryption)
    {
        _dbService = dbService;
        _encryption = encryption;
        _listenPort = int.Parse(_dbService.GetSetting(SettingsKeys.TcpListenPort).Result ?? "9000");
    }

    public async Task StartServerAsync()
    {
        _listenerCts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _listenPort);
        _listener.Start();
        _ = Task.Run(() => AcceptClientsAsync(_listenerCts.Token));
    }

    public async Task StopServerAsync()
    {
        _listenerCts?.Cancel();
        _listener?.Stop();
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
            catch { }
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
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Decrypt error: {ex}"); }
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