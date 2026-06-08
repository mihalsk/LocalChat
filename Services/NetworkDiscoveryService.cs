using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LocalChat.Models;

namespace LocalChat.Services;

public class NetworkDiscoveryService : IDisposable
{
    private readonly DatabaseService _dbService;
    private string _multicastAddress;
    private int _multicastPort;
    private int _tcpPort;
    private string _userName;
    private UdpClient? _udpClient;
    private Timer? _heartbeatTimer;
    private CancellationTokenSource? _listenerCts;
    private bool _disposed;
    private bool _initialized = false;

    public event Action<Peer>? PeerDiscovered;

    public NetworkDiscoveryService(DatabaseService dbService)
    {
        _dbService = dbService;
        // Временные значения по умолчанию, будут заменены при инициализации
        _multicastAddress = "239.0.0.1";
        _multicastPort = 8888;
        _tcpPort = 9000;
        _userName = Environment.MachineName;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _multicastAddress = await _dbService.GetSetting(SettingsKeys.MulticastAddress) ?? "239.0.0.1";
        _multicastPort = int.Parse(await _dbService.GetSetting(SettingsKeys.MulticastPort) ?? "8888");
        _tcpPort = int.Parse(await _dbService.GetSetting(SettingsKeys.TcpListenPort) ?? "9000");
        _userName = await _dbService.GetSetting(SettingsKeys.UserName) ?? Environment.MachineName;

        _initialized = true;
    }

    public async Task StartAsync()
    {
        if (!_initialized)
            await InitializeAsync();

        _listenerCts = new CancellationTokenSource();
        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _multicastPort));
        _udpClient.JoinMulticastGroup(IPAddress.Parse(_multicastAddress));

        _ = Task.Run(() => ListenForHeartbeatsAsync(_listenerCts.Token));
        _heartbeatTimer = new Timer(SendHeartbeat, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    public async Task StopAsync()
    {
        _heartbeatTimer?.Dispose();
        _listenerCts?.Cancel();
        _udpClient?.Close();
        await Task.CompletedTask;
    }

    private async void SendHeartbeat(object? state)
    {
        try
        {
            var localIp = GetLocalIpAddress();
            if (string.IsNullOrEmpty(localIp)) return;

            var heartbeat = new
            {
                Type = "heartbeat",
                PeerId = App.PeerId,
                Name = _userName,
                IpAddress = localIp,
                TcpPort = _tcpPort,
                Timestamp = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(heartbeat);
            var data = Encoding.UTF8.GetBytes(json);
            var endpoint = new IPEndPoint(IPAddress.Parse(_multicastAddress), _multicastPort);
            await _udpClient!.SendAsync(data, data.Length, endpoint);
        }
        catch { /* ignore network errors */ }
    }

    private async Task ListenForHeartbeatsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient!.ReceiveAsync(token);
                var json = Encoding.UTF8.GetString(result.Buffer);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.GetProperty("Type").GetString() != "heartbeat") continue;

                var peerId = root.GetProperty("PeerId").GetString();
                if (peerId == App.PeerId) continue;

                var peer = new Peer
                {
                    PeerId = peerId!,
                    Name = root.GetProperty("Name").GetString()!,
                    IpAddress = root.GetProperty("IpAddress").GetString()!,
                    TcpPort = root.GetProperty("TcpPort").GetInt32(),
                    LastSeen = root.GetProperty("Timestamp").GetDateTime()
                };

                var existing = await _dbService.GetPeerByPeerIdAsync(peer.PeerId);
                if (existing == null)
                    await _dbService.SavePeerAsync(peer);
                else
                    await _dbService.UpdatePeerLastSeen(peer.PeerId, peer.LastSeen);

                PeerDiscovered?.Invoke(peer);
            }
            catch (OperationCanceledException) { break; }
            catch { /* ignore */ }
        }
    }

    private string GetLocalIpAddress()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        socket.Connect("8.8.8.8", 65530);
        var endPoint = socket.LocalEndPoint as IPEndPoint;
        return endPoint?.Address.ToString() ?? "";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _heartbeatTimer?.Dispose();
        _listenerCts?.Cancel();
        _udpClient?.Close();
        _disposed = true;
    }
}