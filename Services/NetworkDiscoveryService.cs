using LocalChat.Models;
#if ANDROID
using LocalChat.Platforms.Android.Services;
#endif
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace LocalChat.Services;

public class NetworkDiscoveryService : IDisposable
{
    private readonly DatabaseService _dbService;
#if ANDROID
    private readonly MulticastLockService? _multicastLockService;
#endif
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

    public NetworkDiscoveryService(DatabaseService dbService
#if ANDROID
        , MulticastLockService? multicastLockService = null
#endif
        )
    {
        _dbService = dbService;
        _multicastAddress = "239.0.0.1";
        _multicastPort = 8888;
        _tcpPort = 9000;
        _userName = Environment.MachineName;
#if ANDROID
        _multicastLockService = multicastLockService;
#endif
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

        var localIps = GetLocalIpAddresses();
        if (!localIps.Any())
        {
            throw new Exception("No suitable network interface found for multicast.");
        }

        var multicastGroup = IPAddress.Parse(_multicastAddress);
#if ANDROID
        if (_multicastLockService != null)
        {
            _multicastLockService.AcquireLock();
            System.Diagnostics.Debug.WriteLine("Multicast lock acquired.");
        }
#endif
        foreach (var localIp in localIps)
        {
            try
            {
                var udpClient = new UdpClient();
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpClient.Client.Bind(new IPEndPoint(localIp, _multicastPort));
                udpClient.JoinMulticastGroup(multicastGroup, localIp);
                _udpClients.Add(udpClient);
                System.Diagnostics.Debug.WriteLine($"Multicast listener on {localIp}:{_multicastPort}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to bind multicast on {localIp}: {ex.Message}");
                // Продолжаем с другими интерфейсами
            }
        }

        if (_udpClients.Count == 0)
        {
            // Последняя попытка: привязываемся к любому интерфейсу
            var fallbackClient = new UdpClient();
            fallbackClient.ExclusiveAddressUse = false;
            fallbackClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            fallbackClient.Client.Bind(new IPEndPoint(IPAddress.Any, _multicastPort));
            fallbackClient.JoinMulticastGroup(IPAddress.Parse(_multicastAddress));
            _udpClients.Add(fallbackClient);
            if (_udpClients.Count == 0) // strange decision 
                throw new Exception("Could not bind multicast on any network interface");
            System.Diagnostics.Debug.WriteLine($"Multicast listener on 'any' {IPAddress.Any.ToString()}:{_multicastPort}");
        }
        

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
#if ANDROID
        if (_multicastLockService != null)
        {
            _multicastLockService.ReleaseLock();
            System.Diagnostics.Debug.WriteLine("Multicast lock released.");
        }
#endif
        await Task.CompletedTask;
    }

    private async void SendHeartbeat(object? state)
    {
        try
        {
            var localIps = GetLocalIpAddresses();
            if (!localIps.Any()) return;

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

            if (_udpClients.Count > 0)
            {
                await _udpClients[0].SendAsync(data, data.Length, endpoint);
            }
            else
            {
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
                System.Diagnostics.Debug.WriteLine($"{peerId}-{peer.IpAddress}:{peer.TcpPort}");
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
        var addresses = new List<IPAddress>();

        // Способ 1: через Dns (работает на Windows, может не работать на Android)
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            addresses.AddRange(host.AddressList
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                .Where(ip => !ip.ToString().StartsWith("169.254.")));
        }
        catch { /* игнорируем */ }

        // Способ 2: перебор сетевых интерфейсов (обязателен для Android)
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in interfaces)
            {
                // Только активные интерфейсы
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                // Исключаем loopback
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var props = ni.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(addr.Address) &&
                        !addr.Address.ToString().StartsWith("169.254."))
                    {
                        addresses.Add(addr.Address);
                    }
                }
            }
        }
        catch { /* игнорируем */ }

        // Убираем дубликаты
        return addresses.Distinct().ToList();
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