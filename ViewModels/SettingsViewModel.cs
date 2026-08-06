using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalChat.Helpers;
using LocalChat.Models;
using LocalChat.Services;
using LocalChat.Views;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace LocalChat.ViewModels;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly DatabaseService _dbService;
    private readonly NetworkServiceManager _networkServiceManager;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _encryptionPassword = string.Empty;

    [ObservableProperty]
    private string _tcpPort = Constants.TCP_PORT.ToString();

    [ObservableProperty]
    private string _multicastAddress = Constants.MULTICAST_GROUP;

    [ObservableProperty]
    private string _multicastPort = Constants.MULTICAST_PORT.ToString();

    public ICommand SaveCommand { get; }
    public ICommand BackCommand { get; }

    public SettingsViewModel(DatabaseService dbService, NetworkServiceManager networkServiceManager)
    {
        _dbService = dbService;
        _networkServiceManager = networkServiceManager;
        System.Diagnostics.Debug.WriteLine(RuntimeHelpers.GetHashCode(_networkServiceManager));
        SaveCommand = new AsyncRelayCommand(SaveSettingsAsync);
        BackCommand = new AsyncRelayCommand(BackAsync);
        LoadSettings();
    }

    private async Task BackAsync()
    {
        //await (Application.Current?.Dispatcher?.DispatchAsync(() => 
        //    MauiProgram.Services.GetRequiredService<NetworkServiceManager>().RestartAsync()) ?? Task.CompletedTask);
        await App.Current.MainPage.Navigation.PopModalAsync();
    }

    private async void LoadSettings()
    {
        UserName = (await _dbService.GetSetting(SettingsKeys.UserName)) ?? Environment.MachineName;
        EncryptionPassword = (await _dbService.GetSetting(SettingsKeys.EncryptionPassword)) ?? "default2026!";
        TcpPort = (await _dbService.GetSetting(SettingsKeys.TcpListenPort)) ?? Constants.TCP_PORT.ToString();
        MulticastAddress = (await _dbService.GetSetting(SettingsKeys.MulticastAddress)) ?? Constants.MULTICAST_GROUP;
        MulticastPort = (await _dbService.GetSetting(SettingsKeys.MulticastPort)) ?? Constants.MULTICAST_PORT.ToString();
    }

    private async Task SaveSettingsAsync()
    {
        await _dbService.SetSetting(SettingsKeys.UserName, UserName);
        await _dbService.SetSetting(SettingsKeys.EncryptionPassword, EncryptionPassword);
        await _dbService.SetSetting(SettingsKeys.TcpListenPort, TcpPort);
        await _dbService.SetSetting(SettingsKeys.MulticastAddress, MulticastAddress);
        await _dbService.SetSetting(SettingsKeys.MulticastPort, MulticastPort);
        // Перезапускаем сетевые службы с новыми настройками
        try
        {
            await _networkServiceManager.RestartAsync();
            await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Настройки", "Настройки сохранены, сеть перезапущена.", "OK");
        }
        catch (Exception ex)
        {
            await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Ошибка", $"Не удалось перезапустить сеть: {ex.Message}", "OK");
        }

        // Закрываем окно настроек
        await App.Current.MainPage.Navigation.PopModalAsync();
    }

    public void Dispose()
    {
    }
}