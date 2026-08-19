using Android.Net.Wifi;
using Android.Content;

namespace LanChat.Platforms.Android.Services;

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
            _multicastLock = _wifiManager.CreateMulticastLock("LanChat.MulticastLock");
            _multicastLock?.SetReferenceCounted(false);
            System.Diagnostics.Debug.WriteLine("(A)Multicast lock created.");
        }
    }

    public void AcquireLock()
    {
        if (_multicastLock != null && !_multicastLock.IsHeld)
        {
            _multicastLock.Acquire();
            System.Diagnostics.Debug.WriteLine("(A)Multicast lock acquired."); ;
        } 
    }

    public void ReleaseLock()
    {
        if (_multicastLock != null && _multicastLock.IsHeld)
        {  
            _multicastLock.Release();
            System.Diagnostics.Debug.WriteLine("(A)Multicast lock realeased.");
        }
    }
}