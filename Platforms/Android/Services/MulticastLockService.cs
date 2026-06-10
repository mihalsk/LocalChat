using Android.Net.Wifi;
using Android.Content;

namespace LocalChat.Platforms.Android.Services;

public class MulticastLockService
{
    private readonly WifiManager? _wifiManager;
    private WifiManager.MulticastLock? _multicastLock;

    public MulticastLockService()
    {
        // Получаем контекст приложения Android
        //var context = Platform.AppContext.GetSystemService(Context.WifiService);
        _wifiManager = Platform.AppContext.GetSystemService(Context.WifiService) as WifiManager;

        if (_wifiManager != null)
        {
            _multicastLock = _wifiManager.CreateMulticastLock("LocalChat.MulticastLock");
            _multicastLock?.SetReferenceCounted(false);
        }
    }

    public void AcquireLock()
    {
        if (_multicastLock != null && !_multicastLock.IsHeld)
            _multicastLock.Acquire();
    }

    public void ReleaseLock()
    {
        if (_multicastLock != null && _multicastLock.IsHeld)
            _multicastLock.Release();
    }
}