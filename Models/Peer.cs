using SQLite;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LocalChat.Models;

[Table("Peers")]
public class Peer : INotifyPropertyChanged
{
    private DateTime _lastSeen;

    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique]
    public string PeerId { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = "Unknown";
    public string IpAddress { get; set; } = string.Empty;
    public int TcpPort { get; set; } = 9000;

    public DateTime LastSeen
    {
        get => _lastSeen;
        set
        {
            if (_lastSeen != value)
            {
                _lastSeen = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOnline));
            }
        }
    }

    [Ignore]
    public bool IsOnline => (DateTime.UtcNow - LastSeen).TotalSeconds < 15;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Публичный метод для принудительного обновления IsOnline в UI
    public void RefreshOnlineStatus()
    {
        OnPropertyChanged(nameof(IsOnline));
    }
}