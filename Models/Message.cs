using SQLite;

namespace LanChat.Models;

[Table("Messages")]
public class Message
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    public string SenderPeerId { get; set; } = string.Empty;
    public string RecipientPeerId { get; set; } = string.Empty; // null для общего чата
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsSentByMe { get; set; }
    public bool IsFileMessage { get; set; }
    public string? FilePath { get; set; } // локальный путь к файлу (если получен)
    public string? OriginalFileName { get; set; }
}