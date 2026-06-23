using LocalChat.Services;

namespace LocalChat.Services;

public class NetworkServiceManager
{
    private readonly NetworkDiscoveryService _discovery;
    private readonly TcpCommunicationService _tcpComm;
    private bool _isStarted = false;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

    public NetworkServiceManager(NetworkDiscoveryService discovery, TcpCommunicationService tcpComm)
    {
        _discovery = discovery;
        _tcpComm = tcpComm;
    }

    public async Task StartAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_isStarted) return;

            await _discovery.InitializeAsync();
            await _tcpComm.InitializeAsync();
            await _discovery.StartAsync();
            await _tcpComm.StartServerAsync();
            _isStarted = true;
            System.Diagnostics.Debug.WriteLine("Network services started once.");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!_isStarted) return;
            await _discovery.StopAsync();
            await _tcpComm.StopServerAsync();
            _isStarted = false;
            System.Diagnostics.Debug.WriteLine("Network services stopped.");
        }
        finally
        {
            _lock.Release();
        }
    }
}