using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using LocalChat.Services;

namespace LocalChat;

[Service(ForegroundServiceType = ForegroundService.TypeDataSync)]
public class AndroidForegroundService : Service
{
    private const int NotificationId = 1001;
    //private NetworkDiscoveryService? _discovery;
    //private TcpCommunicationService? _tcpComm;
    private NetworkServiceManager? _networkServiceManager;
    public override void OnCreate()
    {
        base.OnCreate();
        var services = MauiProgram.Services;
        if (services != null)
        {
            _networkServiceManager = services.GetService<NetworkServiceManager>();
        }
        //var dbService = new DatabaseService();
        //var encryption = new EncryptionService();
        //_discovery = new NetworkDiscoveryService(dbService);
        //_tcpComm = new TcpCommunicationService(dbService, encryption);
        //System.Diagnostics.Debug.WriteLine("Network init(A)...");
        //Task.Run(async () =>
        //{
        //    await _discovery.InitializeAsync();
        //    await _tcpComm.InitializeAsync();
        //    await _discovery.StartAsync();
        //    await _tcpComm.StartServerAsync();
        //});
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // Создаём канал уведомлений для Android O+
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel("localchat_channel", "Local Chat Service", NotificationImportance.Low);
            var manager = (NotificationManager)GetSystemService(NotificationService);
            manager.CreateNotificationChannel(channel);
        }

        var notification = new NotificationCompat.Builder(this, "localchat_channel")
            .SetContentTitle("Local Chat")
            .SetContentText("Online in background")
            .SetSmallIcon(Resource.Drawable.notification_bg_low_normal)
            .SetOngoing(true)
            .Build();

        // Для Android 14+ (API 34+) нужно передать тип сервиса
        if (Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake) // Android 14
        {
            StartForeground(NotificationId, notification, ForegroundService.TypeDataSync);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }
        if (_networkServiceManager != null)
        {
            Task.Run(async () => await _networkServiceManager.StartAsync());
        }
        return StartCommandResult.Sticky;
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        //_discovery?.StopAsync().Wait();
        //_tcpComm?.StopServerAsync().Wait();
        base.OnDestroy();
    }
}