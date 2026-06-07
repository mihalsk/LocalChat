using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalChat.Models;
using LocalChat.Services;
using System.Windows.Input;

namespace LocalChat.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly DatabaseService _dbService;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _encryptionPassword = string.Empty;

    [ObservableProperty]
    private string _tcpPort = "9000";

    [ObservableProperty]
    private string _multicastAddress = "239.0.0.1";

    [ObservableProperty]
    private string _multicastPort = "8888";

    public ICommand SaveCommand { get; }

    public SettingsViewModel(DatabaseService dbService)
    {
        _dbService = dbService;
        SaveCommand = new AsyncRelayCommand(SaveSettingsAsync);
        LoadSettings();
    }

    private async void LoadSettings()
    {
        UserName = (await _dbService.GetSetting(SettingsKeys.UserName)) ?? Environment.MachineName;
        EncryptionPassword = (await _dbService.GetSetting(SettingsKeys.EncryptionPassword)) ?? "default2026!";
        TcpPort = (await _dbService.GetSetting(SettingsKeys.TcpListenPort)) ?? "9000";
        MulticastAddress = (await _dbService.GetSetting(SettingsKeys.MulticastAddress)) ?? "239.0.0.1";
        MulticastPort = (await _dbService.GetSetting(SettingsKeys.MulticastPort)) ?? "8888";
    }

    private async Task SaveSettingsAsync()
    {
        await _dbService.SetSetting(SettingsKeys.UserName, UserName);
        await _dbService.SetSetting(SettingsKeys.EncryptionPassword, EncryptionPassword);
        await _dbService.SetSetting(SettingsKeys.TcpListenPort, TcpPort);
        await _dbService.SetSetting(SettingsKeys.MulticastAddress, MulticastAddress);
        await _dbService.SetSetting(SettingsKeys.MulticastPort, MulticastPort);
        await Application.Current!.MainPage!.DisplayAlert("Settings", "Saved. Restart app for changes.", "OK");
    }
}