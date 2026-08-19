using LanChat.Helpers;
using SQLite;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LanChat.Models;

[Table("Peers")]
public class Peer : INotifyPropertyChanged
{
    private string _name = "Unknown";
    private string _ipAddress = string.Empty;
    private int _tcpPort = Constants.TCP_PORT;
    private DateTime _lastSeen;

    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique]
    public string PeerId { get; set; } = Guid.NewGuid().ToString();

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    public string IpAddress
    {
        get => _ipAddress;
        set { if (_ipAddress != value) { _ipAddress = value; OnPropertyChanged(); } }
    }

    public int TcpPort
    {
        get => _tcpPort;
        set { if (_tcpPort != value) { _tcpPort = value; OnPropertyChanged(); } }
    }


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

    public bool Equals(Peer? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return PeerId == other.PeerId &&
               Name == other.Name &&
               IpAddress == other.IpAddress &&
               TcpPort == other.TcpPort;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Peer);
    }

    public override int GetHashCode()
    {
        // HashCode.Combine поддерживает до 8 параметров из коробки
        return HashCode.Combine(PeerId, Name, IpAddress, TcpPort);
    }

    public static bool operator ==(Peer? left, Peer? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(Peer? left, Peer? right)
    {
        return !(left == right);
    }
}