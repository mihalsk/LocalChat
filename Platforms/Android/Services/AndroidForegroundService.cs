using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using LocalChat.Services;

namespace LocalChat;

[Service]
public class AndroidForegroundService : Service
{
    private const int NotificationId = 1001;
    private NetworkDiscoveryService? _discovery;
    private TcpCommunicationService? _tcpComm;

    public override void OnCreate()
    {
        base.OnCreate();
        var dbService = new DatabaseService();
        var encryption = new EncryptionService();
        _discovery = new NetworkDiscoveryService(dbService);
        _tcpComm = new TcpCommunicationService(dbService, encryption);
        
        Task.Run(async () =>
        {
            await _discovery.StartAsync();
            await _tcpComm.StartServerAsync();
        });
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var notification = new NotificationCompat.Builder(this, "localchat_channel")
            .SetContentTitle("Local Chat")
            .SetContentText("Online in background")
            .SetSmallIcon(Resource.Drawable.notification_bg)
            .SetOngoing(true)
            .Build();
        
        StartForeground(NotificationId, notification);
        return StartCommandResult.Sticky;
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        _discovery?.StopAsync().Wait();
        _tcpComm?.StopServerAsync().Wait();
        base.OnDestroy();
    }
}