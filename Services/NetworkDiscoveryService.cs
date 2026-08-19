#if ANDROID
using LanChat.Platforms.Android.Services;
using Android.Provider;
using JavaLang = Java.Lang;
#endif
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LanChat.Models;
using LanChat.Helpers;
using LanChat;
namespace LanChat.Services;

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

    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;

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
        _multicastAddress = Constants.MULTICAST_GROUP;
        _multicastPort = Constants.MULTICAST_PORT;
        _tcpPort = Constants.TCP_PORT;
        _userName = GetHostName(); // Environment.MachineName;
#if ANDROID
        _multicastLockService = multicastLockService;
#endif
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _multicastAddress = await _dbService.GetSetting(SettingsKeys.MulticastAddress) ?? Constants.MULTICAST_GROUP;
        _multicastPort = int.Parse(await _dbService.GetSetting(SettingsKeys.MulticastPort) ?? Constants.MULTICAST_PORT.ToString());
        _tcpPort = int.Parse(await _dbService.GetSetting(SettingsKeys.TcpListenPort) ?? Constants.TCP_PORT.ToString());
        _userName = await _dbService.GetSetting(SettingsKeys.UserName) ?? GetHostName();
//#if ANDROID
//        Java.Net.InetAddress.LocalHost.HostName;
//#else
//        DeviceInfo.Current.Name; //Dns.GetHostName(); //Environment.MachineName;
//#endif
        _initialized = true;
    }

    public async Task StartAsync()
    {
        if (!_initialized)
            await InitializeAsync();

        // Гарантируем, что нет висящих клиентов
        if (_udpClients.Any())
        {
            foreach (var c in _udpClients) c?.Close();
            _udpClients.Clear();
        }
        if (_heartbeatCts != null)
        {
            _heartbeatCts.Cancel();
            if (_heartbeatTask != null)
            {
                try { await _heartbeatTask; }
                catch (OperationCanceledException) { /* ожидаемо */ }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Heartbeat stop error: {ex}"); }
                _heartbeatTask = null;
            }
            _heartbeatCts.Dispose();
            _heartbeatCts = null;
        }
        _heartbeatCts = new CancellationTokenSource();
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
        foreach (var localIp in localIps) //.Where(x => x.Address.ToString().StartsWith("192.168.")))
        {
            try
            {
                IPEndPoint ipAny = new IPEndPoint(
#if ANDROID
                    IPAddress.Any,
#elif WINDOWS
                    localIp, 
#endif
                    _multicastPort); //IPAddress.Any, _multicastPort);
                var udpClient = new UdpClient();
                
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                
                udpClient.Client.Bind(ipAny);

                var mcastOption = new MulticastOption(multicastGroup, localIp);
                udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, mcastOption);
                //udpClient.JoinMulticastGroup(multicastGroup, IPAddress.Any);
                //var isMultiCast = udpClient?.Client?.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.MulticastInterface);
                // if (isMultiCast is not null && !(bool)isMultiCast)
                //udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.MulticastInterface, true);
                //udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.MulticastLoopback, true);
                //udpClient.Client.Bind(new IPEndPoint(localIp, _multicastPort));


                _udpClients.Add(udpClient);
                System.Diagnostics.Debug.WriteLine($"Multicast listener on {localIp}:{_multicastPort}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to bind multicast on {localIp}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Failed to bind multicast on {localIp}: {ex.StackTrace}");
                // Продолжаем с другими интерфейсами
            }
        }

        //if (_udpClients.Count == 0)
        //{
        //    // Последняя попытка: привязываемся к любому интерфейсу
        //    var fallbackClient = new UdpClient();
        //    fallbackClient.ExclusiveAddressUse = false;
        //    fallbackClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        //    fallbackClient.Client.Bind(new IPEndPoint(IPAddress.Any, _multicastPort));
        //    fallbackClient.JoinMulticastGroup(IPAddress.Parse(_multicastAddress));
        //    _udpClients.Add(fallbackClient);
        //    if (_udpClients.Count == 0) // strange decision 
        //        throw new Exception("Could not bind multicast on any network interface");
        //    System.Diagnostics.Debug.WriteLine($"Multicast listener on 'any' {IPAddress.Any.ToString()}:{fallbackClient.Client.LocalEndPoint}");
        //}
        

        foreach (var client in _udpClients)
        {
            //_ = Task.Factory.StartNew(() => ListenForHeartbeatsAsync(client, _listenerCts.Token), TaskCreationOptions.LongRunning);
            _ = Task.Run(() => ListenForHeartbeatsAsync(client, _listenerCts.Token));
        }

        //_heartbeatTimer = new Timer(SendHeartbeat, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        //_heartbeatTimer = new Timer(_ => SendHeartbeat(_heartbeatCts.Token), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_heartbeatCts.Token));
    }

    public async Task StopAsync()
    {
        // Остановить heartbeat
        if (_heartbeatCts != null)
        {
            _heartbeatCts.Cancel();
            if (_heartbeatTask != null)
            {
                try { await _heartbeatTask; }
                catch (OperationCanceledException) { }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Heartbeat stop error: {ex}"); }
                _heartbeatTask = null;
            }
            _heartbeatCts.Dispose();
            _heartbeatCts = null;
        }
        _listenerCts?.Cancel();
        foreach (var client in _udpClients)
            client?.Close();
        _udpClients.Clear();
        _initialized = false;
#if ANDROID
        if (_multicastLockService != null)
        {
            _multicastLockService.ReleaseLock();
            System.Diagnostics.Debug.WriteLine("Multicast lock released.");
        }
#endif
        await Task.CompletedTask;
    }

    private async Task HeartbeatLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await SendHeartbeatAsync(token);
            if (token.IsCancellationRequested)
                break;
            await Task.Delay(5000, token);
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken token)
    {
        if (token.IsCancellationRequested) return;
        try
        {
            var clients = _udpClients.ToList();
            if (!clients.Any()) return;

            var localIps = GetLocalIpAddresses();
            if (!localIps.Any()) return;

            var localIp = localIps.First().ToString();
            var heartbeat = new
            {
                Type = "heartbeat",
                PeerId = App.PeerId,
                Name = _userName, // актуальное имя из настроек
                IpAddress = localIp,
                TcpPort = _tcpPort,
                Timestamp = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(heartbeat);
            var data = Encoding.UTF8.GetBytes(json);
            var endpoint = new IPEndPoint(IPAddress.Parse(_multicastAddress), _multicastPort);

            foreach (var client in clients)
            {
                token.ThrowIfCancellationRequested();
                await client.SendAsync(data, data.Length, endpoint);
                System.Diagnostics.Debug.WriteLine($"SendHeartbeat for {client.Client.LocalEndPoint} {json}");
            }
        }
        catch (OperationCanceledException) { /* ожидаемо */ }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SendHeartbeat error: {ex.Message}"); }
    }

    private async Task ListenForHeartbeatsAsync(UdpClient client, CancellationToken token)
    {
        System.Diagnostics.Debug.WriteLine($"Start listening {client.Client.LocalEndPoint}");
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
                System.Diagnostics.Debug.WriteLine($"Listening {client.Client.LocalEndPoint} - recieve from {peerId}");
                if (peerId == App.PeerId) continue;

                var peer = new Peer
                {
                    PeerId = peerId!,
                    Name = root.GetProperty("Name").GetString()!,
                    IpAddress = root.GetProperty("IpAddress").GetString()!,
                    TcpPort = root.GetProperty("TcpPort").GetInt32(),
                    LastSeen = root.GetProperty("Timestamp").GetDateTime()
                };
                System.Diagnostics.Debug.WriteLine($"{peer.IpAddress}:{peer.TcpPort}");
                var existingPeer = await _dbService.GetPeerByIpAddressAsync(peer.IpAddress); //_dbService.GetPeerByPeerIdAsync(peer.PeerId)
                if (existingPeer != null)
                {
                    // Если изменились ключевые данные (используется наш Equals)
                    if (existingPeer != peer)
                    {
                        existingPeer.Name = peer.Name;
                        existingPeer.IpAddress = peer.IpAddress;
                        existingPeer.TcpPort = peer.TcpPort;
                    }

                    // Время обновляем всегда, это пересчитает IsOnline -> true
                    existingPeer.LastSeen = DateTime.UtcNow;
                }
                else
                {
                    // Новый собеседник появился в сети
                    peer.LastSeen = DateTime.UtcNow;
                    await _dbService.SavePeerAsync(peer);
                }                
                PeerDiscovered?.Invoke(peer);
            }
            catch (OperationCanceledException ex) { System.Diagnostics.Debug.WriteLine($"Listen error: {ex.Message}"); break; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Listen error: {ex.Message}"); }
        }
        System.Diagnostics.Debug.WriteLine($"Stop listening {client?.Client?.LocalEndPoint}");
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
                //.Where(ip => !ip.ToString().StartsWith("169.254."))
                .Where(ip => ip.ToString().StartsWith("192.168."))
                .Where(ip => !ip.ToString().EndsWith(".1")));
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
                        addr.Address.ToString().StartsWith("192.168."))
                        //!addr.Address.ToString().StartsWith("169.254."))
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

    public string GetHostName()
    {
        string hostName = "";

#if ANDROID
        System.Diagnostics.Debug.WriteLine($"host: {JavaLang.Runtime.GetRuntime()
                         .Exec("getprop net.hostname")
                         .InputStream
                         .ToString()}");
        System.Diagnostics.Debug.WriteLine($"host: {Settings.Global.GetString(Android.App.Application.Context.ContentResolver,
                                            "device_name")}");
        //hostName = JavaLang.Runtime.GetRuntime()
        //                 .Exec("getprop net.hostname")
        //                 .InputStream
        //                 .ToString();
        hostName = Settings.Global.GetString(Android.App.Application.Context.ContentResolver,
        "device_name");
#else
        hostName = DeviceInfo.Current.Name; // Environment.MachineName; 
#endif
        return hostName;
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