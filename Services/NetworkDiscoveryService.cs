using LocalChat.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace LocalChat.Services;

public class NetworkDiscoveryService : IDisposable
{
    private readonly DatabaseService _dbService;
    private string _multicastAddress;
    private int _multicastPort;
    private int _tcpPort;
    private string _userName;
    private List<UdpClient> _udpClients = new();
    private Timer? _heartbeatTimer;
    private CancellationTokenSource? _listenerCts;
    private bool _disposed;
    private bool _initialized = false;

    public event Action<Peer>? PeerDiscovered;

    public NetworkDiscoveryService(DatabaseService dbService)
    {
        _dbService = dbService;
        _multicastAddress = "224.0.0.252";
        _multicastPort = 8888;
        _tcpPort = 9000;
        _userName = Environment.MachineName;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _multicastAddress = await _dbService.GetSetting(SettingsKeys.MulticastAddress) ?? "224.0.0.252";
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

        // Получаем все локальные IPv4-адреса (не loopback)
        var localIps = GetLocalIpAddresses();
        System.Diagnostics.Debug.WriteLine($"localIps {string.Join(", ", localIps)}");
        if (!localIps.Any())
        {
            throw new Exception("No suitable network interface found for multicast.");
        }

        foreach (var localIp in localIps)
        {
            try
            {
                var udpClient = new UdpClient();
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpClient.Client.Bind(new IPEndPoint(localIp, _multicastPort));
                udpClient.JoinMulticastGroup(IPAddress.Parse(_multicastAddress), localIp);
                _udpClients.Add(udpClient);
                System.Diagnostics.Debug.WriteLine($"Multicast listener on {localIp}:{_multicastPort}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to bind multicast on {localIp}: {ex.Message} {ex.StackTrace.ToString()}");
                // Продолжаем с другими интерфейсами
            }
        }

        if (_udpClients.Count == 0)
            throw new Exception("Could not bind multicast on any network interface");

        // Запуск прослушивания на всех клиентах
        foreach (var client in _udpClients)
        {
            _ = Task.Run(() => ListenForHeartbeatsAsync(client, _listenerCts.Token));
        }

        _heartbeatTimer = new Timer(SendHeartbeat, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    public async Task StopAsync()
    {
        _heartbeatTimer?.Dispose();
        _listenerCts?.Cancel();
        foreach (var client in _udpClients)
        {
            client?.Close();
        }
        _udpClients.Clear();
        await Task.CompletedTask;
    }

    private async void SendHeartbeat(object? state)
    {
        try
        {
            var localIps = GetLocalIpAddresses();
            if (!localIps.Any()) return;

            // Отправляем heartbeat с первым доступным IP
            var localIp = localIps.First().ToString();

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

            // Отправляем через первый активный UdpClient
            if (_udpClients.Count > 0)
            {
                await _udpClients[0].SendAsync(data, data.Length, endpoint);
            }
            else
            {
                // Fallback: временный клиент для отправки
                using var tempClient = new UdpClient();
                tempClient.JoinMulticastGroup(IPAddress.Parse(_multicastAddress));
                await tempClient.SendAsync(data, data.Length, endpoint);
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SendHeartbeat error: {ex.Message}"); }
    }

    private async Task ListenForHeartbeatsAsync(UdpClient client, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync(token);
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Listen error: {ex.Message}"); }
        }
    }

    private List<IPAddress> GetLocalIpAddresses()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        return host.AddressList
            .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
            .ToList();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _heartbeatTimer?.Dispose();
        _listenerCts?.Cancel();
        foreach (var client in _udpClients)
            client?.Close();
        _disposed = true;
    }
}