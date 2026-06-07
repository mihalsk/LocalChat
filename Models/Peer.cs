using SQLite;

namespace LocalChat.Models;

[Table("Peers")]
public class Peer
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Unique]
    public string PeerId { get; set; } = Guid.NewGuid().ToString();
    
    public string Name { get; set; } = "Unknown";
    public string IpAddress { get; set; } = string.Empty;
    public int TcpPort { get; set; } = 9000;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    
    [Ignore]
    public bool IsOnline => (DateTime.UtcNow - LastSeen).TotalSeconds < 15;
}