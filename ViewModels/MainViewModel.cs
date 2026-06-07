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
                         EncryptionService encryption)
    {
        _dbService = dbService;
        _discovery = discovery;
        _tcpComm = tcpComm;
        _fileTransfer = fileTransfer;
        _encryption = encryption;

        SendMessageCommand = new AsyncRelayCommand(SendMessageAsync);
        SendFileCommand = new AsyncRelayCommand(SendFileAsync);
        RefreshPeersCommand = new AsyncRelayCommand(RefreshPeersAsync);

        _discovery.PeerDiscovered += OnPeerDiscovered;
        _tcpComm.MessageReceived += OnMessageReceived;
        
        Task.Run(InitializeAsync);
    }

    private async Task InitializeAsync()
    {
        await LoadPeersAsync();
        await _discovery.StartAsync();
        await _tcpComm.StartServerAsync();
        StatusText = "Online";
    }

    private async Task LoadPeersAsync()
    {
        var list = await _dbService.GetAllPeersAsync();
        Peers.Clear();
        foreach (var peer in list.OrderByDescending(p => p.LastSeen))
            Peers.Add(peer);
    }

    private void OnPeerDiscovered(Peer peer)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (!Peers.Any(p => p.PeerId == peer.PeerId))
                Peers.Add(peer);
            else
            {
                var existing = Peers.First(p => p.PeerId == peer.PeerId);
                existing.LastSeen = peer.LastSeen;
                existing.Name = peer.Name;
                existing.IpAddress = peer.IpAddress;
            }
            await LoadPeersAsync(); // обновить порядок
        });
    }

    private async Task OnMessageReceived(Message message)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Messages.Add(message);
            StatusText = $"New message from {message.SenderPeerId}";
        });
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMessageText) || SelectedPeer == null) return;
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
            Messages.Add(msg);
            NewMessageText = string.Empty;
        }
        finally { IsBusy = false; }
    }

    private async Task SendFileAsync()
    {
        if (SelectedPeer == null) return;
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select file to send"
            });
            if (result == null) return;
            await _fileTransfer.SendFileAsync(SelectedPeer, result.FullPath);
            StatusText = $"File sent to {SelectedPeer.Name}";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }

    private async Task RefreshPeersAsync() => await LoadPeersAsync();

    public void Dispose()
    {
        _discovery.PeerDiscovered -= OnPeerDiscovered;
        _tcpComm.MessageReceived -= OnMessageReceived;
        _discovery.StopAsync().Wait();
        _tcpComm.StopServerAsync().Wait();
    }
}