using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LanChat.Services;

public interface IFileStorageService
{
    /// <summary>
    /// Сохраняет поток в папку Downloads.
    /// Возвращает путь (абсолютный или content:// URI) к сохранённому файлу.
    /// </summary>
    Task<string> SaveFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Открывает файл через системное приложение.
    /// </summary>
    Task OpenFileAsync(string filePath);
}