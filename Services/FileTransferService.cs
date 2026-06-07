using System.Net.Sockets;
using LocalChat.Models;

namespace LocalChat.Services;

public class FileTransferService
{
    private readonly DatabaseService _dbService;
    private readonly EncryptionService _encryption;
    private readonly TcpCommunicationService _tcpComm;

    public FileTransferService(DatabaseService dbService, EncryptionService encryption, TcpCommunicationService tcpComm)
    {
        _dbService = dbService;
        _encryption = encryption;
        _tcpComm = tcpComm;
    }

    public async Task SendFileAsync(Peer recipient, string localFilePath)
    {
        if (!File.Exists(localFilePath)) throw new FileNotFoundException();
        var fileName = Path.GetFileName(localFilePath);
        var fileBytes = await File.ReadAllBytesAsync(localFilePath);
        var contentBase64 = Convert.ToBase64String(fileBytes);
        
        // Отправляем через TCP как специальное сообщение
        await _tcpComm.SendMessageAsync(recipient, contentBase64, true, fileName);
        
        // Сохраняем запись в БД (исходящий файл)
        var message = new Message
        {
            SenderPeerId = App.PeerId,
            RecipientPeerId = recipient.PeerId,
            Content = "[Файл]",
            Timestamp = DateTime.UtcNow,
            IsSentByMe = true,
            IsFileMessage = true,
            OriginalFileName = fileName,
            FilePath = localFilePath
        };
        await _dbService.SaveMessageAsync(message);
    }

    public async Task SaveReceivedFileAsync(Message fileMessage, string targetFolder)
    {
        if (!fileMessage.IsFileMessage) return;
        var password = await _dbService.GetSetting(SettingsKeys.EncryptionPassword) ?? "default2026!";
        var base64Content = _encryption.Decrypt(fileMessage.Content, password);
        var fileBytes = Convert.FromBase64String(base64Content);
        var savePath = Path.Combine(targetFolder, fileMessage.OriginalFileName ?? "received_file");
        Directory.CreateDirectory(targetFolder);
        await File.WriteAllBytesAsync(savePath, fileBytes);
        
        // Обновляем запись в БД
        fileMessage.FilePath = savePath;
        await _dbService.SaveMessageAsync(fileMessage);
    }
}