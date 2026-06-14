using SQLite;
using LocalChat.Models;
using LocalChat.Helpers;

namespace LocalChat.Services;

public class DatabaseService
{
    private readonly SQLiteAsyncConnection _database;
    private readonly string _dbPath;
    private bool _initialized = false;

    public DatabaseService()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "localchat.db3");
        _database = new SQLiteAsyncConnection(_dbPath);
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            await _database.CreateTableAsync<Peer>();
            await _database.CreateTableAsync<Message>();
            await _database.CreateTableAsync<AppSettings>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Table creation error: {ex}");
            throw;
        }
        await InitDefaultSettings();

        _initialized = true;
    }

    private async Task InitDefaultSettings()
    {
        if (!await SettingExists(SettingsKeys.UserName))
            await SetSetting(SettingsKeys.UserName, Environment.MachineName);
        if (!await SettingExists(SettingsKeys.EncryptionPassword))
            await SetSetting(SettingsKeys.EncryptionPassword, "default2026!");
        if (!await SettingExists(SettingsKeys.TcpListenPort))
            await SetSetting(SettingsKeys.TcpListenPort, Constants.TCP_PORT.ToString());
        if (!await SettingExists(SettingsKeys.MulticastAddress))
            await SetSetting(SettingsKeys.MulticastAddress, Constants.MULTICAST_GROUP);
        if (!await SettingExists(SettingsKeys.MulticastPort))
            await SetSetting(SettingsKeys.MulticastPort, Constants.MULTICAST_PORT.ToString());
    }

    public async Task<bool> SettingExists(string key)
    {
        var setting = await _database.FindAsync<AppSettings>(key);
        return setting != null;
    }

    public async Task<string?> GetSetting(string key)
    {
        var setting = await _database.FindAsync<AppSettings>(key);
        return setting?.Value;
    }

    public async Task SetSetting(string key, string value)
    {
        var existing = await _database.FindAsync<AppSettings>(key);
        if (existing != null)
        {
            existing.Value = value;
            await _database.UpdateAsync(existing);
        }
        else
        {
            await _database.InsertAsync(new AppSettings { Key = key, Value = value });
        }
    }

    // --- Peers ---
    public Task<List<Peer>> GetAllPeersAsync() => _database.Table<Peer>().ToListAsync();
    public Task<int> SavePeerAsync(Peer peer) => _database.InsertOrReplaceAsync(peer);
    public Task<int> DeletePeerAsync(Peer peer) => _database.DeleteAsync(peer);
    public Task<Peer?> GetPeerByPeerIdAsync(string peerId) => _database.Table<Peer>().FirstOrDefaultAsync(p => p.PeerId == peerId);
    public Task<int> UpdatePeerLastSeen(string peerId, DateTime lastSeen) =>
        _database.ExecuteAsync("UPDATE Peers SET LastSeen = ? WHERE PeerId = ?", lastSeen, peerId); //lastSeen

    // --- Messages ---
    public Task<List<Message>> GetMessagesWithPeerAsync(string peerId) =>
        _database.Table<Message>().Where(m => m.SenderPeerId == peerId || m.RecipientPeerId == peerId).OrderBy(m => m.Timestamp).ToListAsync();
    public Task<int> SaveMessageAsync(Message message) => _database.InsertAsync(message);
    public Task<int> DeleteAllMessagesAsync() => _database.DeleteAllAsync<Message>();
}