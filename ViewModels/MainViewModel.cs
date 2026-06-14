using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalChat.Models;
using LocalChat.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace LocalChat.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly DatabaseService _dbService;
    private readonly NetworkDiscoveryService _discovery;
    private readonly TcpCommunicationService _tcpComm;
    private readonly FileTransferService _fileTransfer;
    private readonly EncryptionService _encryption;
    private readonly NetworkServiceManager _networkServiceManager;
    private Timer? _onlineStatusTimer;

    [ObservableProperty]
    private ObservableCollection<Peer> _peers = new();

    [ObservableProperty]
    private ObservableCollection<Message> _messages = new();

    [ObservableProperty]
    private Peer? _selectedPeer;

    [ObservableProperty]
    private string _newMessageText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Initializing...";

    [ObservableProperty]
    private bool _isBusy;

    public ICommand SendMessageCommand { get; }
    public ICommand SendFileCommand { get; }
    public ICommand RefreshPeersCommand { get; }

    public MainViewModel(DatabaseService dbService, NetworkDiscoveryService discovery,
                         TcpCommunicationService tcpComm, FileTransferService fileTransfer,
                         EncryptionService encryption, NetworkServiceManager networkServiceManager                         
        )
    {
        _dbService = dbService;
        _discovery = discovery;
        _tcpComm = tcpComm;
        _fileTransfer = fileTransfer;
        _encryption = encryption;
        _networkServiceManager = networkServiceManager;

        SendMessageCommand = new AsyncRelayCommand(SendMessageAsync);
        SendFileCommand = new AsyncRelayCommand(SendFileAsync);
        RefreshPeersCommand = new AsyncRelayCommand(RefreshPeersAsync);

        _discovery.PeerDiscovered += OnPeerDiscovered;
        _tcpComm.MessageReceived += OnMessageReceived;

        _onlineStatusTimer = new Timer(UpdateOnlineStatus, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        Task.Run(InitializeAsync);
    }
    private async Task InitializeAsync()
    {
        try
        {
            await UpdateStatus("Initializing database...");
            await _dbService.InitializeAsync();

            await UpdateStatus("Loading peers...");
            await LoadPeersAsync();

            await UpdateStatus("Starting network services...");

            await _networkServiceManager.StartAsync(); // вместо отдельных вызовов

            await UpdateStatus("Online");
            System.Diagnostics.Debug.WriteLine("Network init(MVM)...");
            await UpdateStatus($"Online:{await _dbService.GetSetting(SettingsKeys.MulticastAddress)}:" +
                $"{await _dbService.GetSetting(SettingsKeys.MulticastPort)}," +
                //$"{await _tcpComm.}" +
                $"{await _dbService.GetSetting(SettingsKeys.TcpListenPort)}");
        }
        catch (Exception ex)
        {
            await UpdateStatus($"Init error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Init error: {ex}");
        }
    }
    private async Task InitializeAsync__()
    {
        try
        {
            await UpdateStatus("Initializing database...");
            await _dbService.InitializeAsync();

            await UpdateStatus("Loading network settings...");
            await _discovery.InitializeAsync();
            await _tcpComm.InitializeAsync();

            await UpdateStatus("Loading peers...");
            await LoadPeersAsync();

            await UpdateStatus("Starting network services...");
            await _discovery.StartAsync();
            await _tcpComm.StartServerAsync();
            System.Diagnostics.Debug.WriteLine("Network init(MVM)...");
            await UpdateStatus($"Online:{await _dbService.GetSetting(SettingsKeys.MulticastAddress)}:" +
                $"{await _dbService.GetSetting(SettingsKeys.MulticastPort)}," +
                //$"{await _tcpComm.}" +
                $"{await _dbService.GetSetting(SettingsKeys.TcpListenPort)}");
        }
        catch (Exception ex)
        {
            await UpdateStatus($"Init error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Init error: {ex}");
        }
    }

    private async Task UpdateStatus(string status)
    {
        await (Application.Current?.Dispatcher?.DispatchAsync(() => StatusText = status) ?? Task.CompletedTask);
    }

    private void UpdateOnlineStatus(object? state)
    {
        Application.Current?.Dispatcher?.DispatchAsync(() =>
        {
            foreach (var peer in Peers)
            {
                peer.RefreshOnlineStatus();
            }
        });
    }

    private async Task LoadPeersAsync()
    {
        var list = await _dbService.GetAllPeersAsync();
        await (Application.Current?.Dispatcher?.DispatchAsync(() =>
        {
            Peers.Clear();
            foreach (var peer in list.OrderByDescending(p => p.LastSeen))
                Peers.Add(peer);
        }) ?? Task.CompletedTask);
    }

    private void OnPeerDiscovered(Peer peer)
    {
        Application.Current?.Dispatcher?.DispatchAsync(() =>
        {
            var existing = Peers.FirstOrDefault(p => p.PeerId == peer.PeerId);
            if (existing == null)
            {
                Peers.Add(peer);
            }
            else
            {
                // Обновляем существующий объект – тогда UI тоже обновится
                existing.Name = peer.Name;
                existing.IpAddress = peer.IpAddress;
                existing.TcpPort = peer.TcpPort;
                existing.LastSeen = peer.LastSeen;
            }
            // При желании пересортировать список:
            // var sorted = Peers.OrderByDescending(p => p.LastSeen).ToList();
            // Peers.Clear();
            // foreach (var p in sorted) Peers.Add(p);
        });
    }

    private async Task OnMessageReceived(Message message)
    {
        await (Application.Current?.Dispatcher?.DispatchAsync(() =>
        {
            Messages.Add(message);
            StatusText = $"New message from {message.SenderPeerId}";
        }) ?? Task.CompletedTask);
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMessageText) || SelectedPeer == null || !SelectedPeer.IsOnline) return;
        IsBusy = true;
        try
        {
            await _tcpComm.SendMessageAsync(SelectedPeer, NewMessageText);
            var msg = new Message
            {
                SenderPeerId = App.PeerId,
                RecipientPeerId = SelectedPeer.PeerId,
                Content = NewMessageText,
                Timestamp = DateTime.UtcNow,
                IsSentByMe = true
            };
            await _dbService.SaveMessageAsync(msg);
            await (Application.Current?.Dispatcher?.DispatchAsync(() => Messages.Add(msg)) ?? Task.CompletedTask);
            NewMessageText = string.Empty;
        }
        finally { IsBusy = false; }
    }

    private async Task SendFileAsync()
    {
        if (SelectedPeer == null || !SelectedPeer.IsOnline) return;
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select file to send"
            });
            if (result == null) return;
            await _fileTransfer.SendFileAsync(SelectedPeer, result.FullPath);
            await UpdateStatus($"File sent to {SelectedPeer.Name}");
        }
        catch (Exception ex) { await UpdateStatus($"Error: {ex.Message}"); }
    }

    private async Task RefreshPeersAsync() => await LoadPeersAsync();

    public void Dispose()
    {
        _discovery.PeerDiscovered -= OnPeerDiscovered;
        _tcpComm.MessageReceived -= OnMessageReceived;
        _networkServiceManager.StopAsync().Wait(); // остановка через менеджер
    }
    public void Dispose__()
    {
        _discovery.PeerDiscovered -= OnPeerDiscovered;
        _tcpComm.MessageReceived -= OnMessageReceived;
        _discovery.StopAsync().Wait();
        _tcpComm.StopServerAsync().Wait();
        _onlineStatusTimer?.Dispose();
    }
}