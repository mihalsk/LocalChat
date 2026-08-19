#if WINDOWS
using LanChat.Services;
using LanChat.Services;
using Microsoft.Maui.Storage;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LanChat.Platforms.Windows;

public class FileStorageService : IFileStorageService
{
    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var basePath = Path.Combine(downloads, fileName);
        var filePath = basePath;
        int counter = 1;
        while (File.Exists(filePath))
        {
            var name = Path.GetFileNameWithoutExtension(basePath);
            var ext = Path.GetExtension(basePath);
            filePath = Path.Combine(downloads, $"{name} ({counter}){ext}");
            counter++;
        }

        using var fileStreamOut = File.Create(filePath);
        await fileStream.CopyToAsync(fileStreamOut, cancellationToken);
        return filePath;
    }

    public Task OpenFileAsync(string filePath)
    {
        return Launcher.Default.OpenAsync(filePath);
    }
}
#endif