using LocalChat;
using LocalChat.Models;
using LocalChat.Services;

public class FileTransferService
{
    private readonly DatabaseService _dbService;
    private readonly TcpCommunicationService _tcpComm;

    public FileTransferService(DatabaseService dbService, TcpCommunicationService tcpComm)
    {
        _dbService = dbService;
        _tcpComm = tcpComm;
    }

    public async Task SendFileAsync(Peer recipient, string localFilePath)
    {
        await _tcpComm.SendFileAsync(recipient, localFilePath);
        // Сохраняем исходящее сообщение в БД (без содержимого файла)
        var message = new Message
        {
            SenderPeerId = App.PeerId,
            RecipientPeerId = recipient.PeerId,
            Content = "", // не используется
            Timestamp = DateTime.UtcNow,
            IsSentByMe = true,
            IsFileMessage = true,
            FilePath = localFilePath, // можно сохранить путь к исходному файлу (для отображения)
            OriginalFileName = Path.GetFileName(localFilePath)
        };
        await _dbService.SaveMessageAsync(message);
    }
}