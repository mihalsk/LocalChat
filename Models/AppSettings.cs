using SQLite;

namespace LocalChat.Models;

[Table("Settings")]
public class AppSettings
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

// Вспомогательный класс для доступа
public static class SettingsKeys
{
    public const string UserName = "UserName";
    public const string EncryptionPassword = "EncryptionPassword";
    public const string TcpListenPort = "TcpListenPort";
    public const string MulticastAddress = "MulticastAddress";
    public const string MulticastPort = "MulticastPort";
}