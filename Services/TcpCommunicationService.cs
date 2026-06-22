using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LocalChat.Helpers;
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
    public event Func<Message, Task>? FileReceived;


    public TcpCommunicationService(DatabaseService dbService, EncryptionService encryption)
    {
        _dbService = dbService;
        _encryption = encryption;
        _configuredPort = Constants.TCP_PORT;
        _actualListenPort = 0;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _configuredPort = int.Parse(await _dbService.GetSetting(SettingsKeys.TcpListenPort) ?? Constants.TCP_PORT.ToString());
        _initialized = true;
    }

    /// <summary>
    /// Поиск свободного порта, начиная с заданного
    /// </summary>
    private async Task<int> FindFreePortAsync(int startPort, int maxAttempts = 100)
    {
        startPort = Constants.TCP_PORT;
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
        await _dbService.SetSetting(SettingsKeys.TcpListenPort, _actualListenPort.ToString());
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

    // Новый метод обработки клиента с поддержкой файлов
    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using var stream = client.GetStream();
        try
        {
            // Читаем первый байт
            var firstByteBuffer = new byte[1];
            int read = await stream.ReadAsync(firstByteBuffer, 0, 1, token);
            if (read == 0) return;
            int firstByte = firstByteBuffer[0];

            if (firstByte == 0) // Новый текстовый протокол
            {
                var ms = new MemoryStream();
                var buffer = new byte[4096];
                int bytes;
                while ((bytes = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    ms.Write(buffer, 0, bytes);
                    //if (bytes < buffer.Length) break;
                }
                var json = Encoding.UTF8.GetString(ms.ToArray());
                await ProcessTextMessage(json);
            }
            else if (firstByte == 1) // Файловый протокол
            {
                // Читаем длину метаданных
                var lengthBuffer = new byte[4];
                int readLen = await stream.ReadAsync(lengthBuffer, 0, 4, token);
                if (readLen != 4) return;
                int metadataLength = BitConverter.ToInt32(lengthBuffer, 0);

                // Читаем метаданные
                var metadataBuffer = new byte[metadataLength];
                int totalRead = 0;
                while (totalRead < metadataLength)
                {
                    int chunk = await stream.ReadAsync(metadataBuffer, totalRead, metadataLength - totalRead, token);
                    if (chunk == 0) break;
                    totalRead += chunk;
                }
                var metadataJson = Encoding.UTF8.GetString(metadataBuffer);
                var metadata = JsonSerializer.Deserialize<FileMetadata>(metadataJson);

                // Сохраняем файл через IFileStorageService
                var fileStorage = MauiProgram.Services!.GetRequiredService<IFileStorageService>();
                var savedPath = await fileStorage.SaveFileAsync(stream, metadata.FileName, token);

                // Создаём сообщение
                var message = new Message
                {
                    SenderPeerId = metadata.SenderPeerId,
                    RecipientPeerId = App.PeerId,
                    Content = "",
                    Timestamp = DateTime.UtcNow,
                    IsSentByMe = false,
                    IsFileMessage = true,
                    FilePath = savedPath,
                    OriginalFileName = metadata.FileName
                };

                await _dbService.SaveMessageAsync(message);
                if (FileReceived != null)
                    await FileReceived.Invoke(message);
            }
            else // Старый текстовый протокол (без префикса типа)
            {
                // Собираем весь поток вместе с первым байтом
                var ms = new MemoryStream();
                ms.WriteByte((byte)firstByte);
                var buffer = new byte[4096];
                int bytes;
                while ((bytes = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    ms.Write(buffer, 0, bytes);
                    //if (bytes < buffer.Length) break;
                }
                var json = Encoding.UTF8.GetString(ms.ToArray());
                await ProcessTextMessage(json);
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"HandleClient error: {ex}"); }
    }

    private async Task ProcessTextMessage(string json)
    {
        // Удаляем BOM и обрезаем пробелы
        //if (json.StartsWith("\uFEFF"))
        //    json = json.Substring(1);
        //json = json.Trim();

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
                if (MessageReceived != null)
                    await MessageReceived.Invoke(message);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ProcessTextMessage error: {ex}");
        }
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
        System.Diagnostics.Debug.WriteLine($"TcpClient message: {recipient.IpAddress}:{recipient.TcpPort}");
        await client.ConnectAsync(recipient.IpAddress, recipient.TcpPort);

        await using var stream = client.GetStream();
        // Отправляем тип 0 (текстовое сообщение)
        await stream.WriteAsync(new byte[] { 0 }, 0, 1);
        await stream.WriteAsync(data);
        System.Diagnostics.Debug.WriteLine($"TcpClient message: {recipient.IpAddress}:{recipient.TcpPort} complete?");
    }
    // Новый метод для отправки файла
    public async Task SendFileAsync(Peer recipient, string localFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(localFilePath))
            throw new FileNotFoundException("Файл не найден.", localFilePath);

        var fileName = Path.GetFileName(localFilePath);
        var fileInfo = new FileInfo(localFilePath);
        var metadata = new
        {
            SenderPeerId = App.PeerId,
            FileName = fileName,
            FileSize = fileInfo.Length
        };
        var metadataJson = JsonSerializer.Serialize(metadata);
        var metadataBytes = Encoding.UTF8.GetBytes(metadataJson);
        var metadataLength = BitConverter.GetBytes(metadataBytes.Length); // 4 байта

        using var client = new TcpClient();
        System.Diagnostics.Debug.WriteLine($"TcpClient file: {recipient.IpAddress}:{recipient.TcpPort}");
        await client.ConnectAsync(recipient.IpAddress, recipient.TcpPort);
        await using var stream = client.GetStream();

        // Отправляем тип 1 (файл)
        await stream.WriteAsync(new byte[] { 1 }, 0, 1, cancellationToken);
        await stream.WriteAsync(metadataLength, 0, 4, cancellationToken);
        await stream.WriteAsync(metadataBytes, 0, metadataBytes.Length, cancellationToken);

        using var fileStream = File.OpenRead(localFilePath);
        await fileStream.CopyToAsync(stream, cancellationToken);
        System.Diagnostics.Debug.WriteLine($"TcpClient file: {recipient.IpAddress}:{recipient.TcpPort} complete?");
    }

    // Вспомогательный класс для метаданных файла
    private class FileMetadata
    {
        public string SenderPeerId { get; set; } = "";
        public string FileName { get; set; } = "";
        public long FileSize { get; set; }
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

static class NetworkStreamExtension
{
    extension(NetworkStream stream)
    {
        public async Task<int> ReadByteAsync(CancellationToken token)
        {
            var buffer = new byte[1];
            int read = await stream.ReadAsync(buffer, 0, 1, token);
            return read == 1 ? buffer[0] : -1;
        }
        public async Task WriteByteAsync(byte value, CancellationToken token)
        {
            await stream.WriteAsync(new byte[] { value }, 0, 1, token);
        }
    }
}