#if ANDROID
using Android.Content;
using AndroidOS = Android.OS;
using Android.Provider;
using AndroidX.Core.Content;
using AndroidNet = Android.Net;
using JavaIO = Java.IO;
using SystemIO = System.IO;
using LocalChat.Services;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using FileProvider = Microsoft.Maui.Storage.FileProvider;
using Android.App;

namespace LocalChat.Platforms.Android.Services;

public class FileStorageService : IFileStorageService
{
    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var context = Platform.AppContext;

        if (AndroidOS.Build.VERSION.SdkInt >= AndroidOS.BuildVersionCodes.Q) // Android 10+
        {
            var resolver = context.ContentResolver;
            var contentValues = new ContentValues();
            contentValues.Put(MediaStore.IMediaColumns.DisplayName, fileName);
            contentValues.Put(MediaStore.IMediaColumns.MimeType, "application/octet-stream");
            contentValues.Put(MediaStore.IMediaColumns.RelativePath, AndroidOS.Environment.DirectoryDownloads);

            var uri = resolver.Insert(MediaStore.Downloads.ExternalContentUri, contentValues);
            if (uri == null)
                throw new Exception("Не удалось создать файл в Downloads через MediaStore.");

            using var outputStream = resolver.OpenOutputStream(uri);
            await fileStream.CopyToAsync(outputStream, cancellationToken);
            return uri.ToString(); // content:// URI
        }
        else
        {
            // Для старых версий – прямой доступ к папке Downloads
            var downloadsDir = AndroidOS.Environment.GetExternalStoragePublicDirectory(AndroidOS.Environment.DirectoryDownloads);
            if (downloadsDir == null)
                throw new Exception("Не удалось получить папку Downloads.");

            var file = new Java.IO.File(downloadsDir, fileName);
            // Используем System.IO.FileStream для записи
            using var output = new FileStream(file.AbsolutePath, FileMode.Create, FileAccess.Write);
            await fileStream.CopyToAsync(output, cancellationToken);
            return file.AbsolutePath;
        }
    }

    //public Task OpenFileAsync(string filePath)
    //{
    //    var context = Platform.AppContext;
    //    AndroidNet.Uri? uri;

    //    if (filePath.StartsWith("content://"))
    //    {
    //        uri = AndroidNet.Uri.Parse(filePath);
    //    }
    //    else
    //    {
    //        var file = new Java.IO.File(filePath);
    //        // Используем FileProvider для предоставления доступа
    //        uri = FileProvider.GetUriForFile(context, context.PackageName + ".fileprovider", file);
    //    }

    //    var intent = new Intent(Intent.ActionView);
    //    intent.SetDataAndType(uri, "application/octet-stream");
    //    intent.AddFlags(ActivityFlags.GrantReadUriPermission);
    //    context.StartActivity(intent);
    //    return Task.CompletedTask;
    //}
    public Task OpenFileAsync(string filePath)
    {
        var context = Platform.CurrentActivity ?? Platform.AppContext;
        if (context == null)
            throw new Exception("No context available.");

        AndroidNet.Uri? uri;

        if (filePath.StartsWith("content://"))
        {
            uri = AndroidNet.Uri.Parse(filePath);
        }
        else
        {
            var file = new Java.IO.File(filePath);
            uri = FileProvider.GetUriForFile(context, context.PackageName + ".fileprovider", file);
        }

        var intent = new Intent(Intent.ActionView);
        intent.SetDataAndType(uri, "application/octet-stream");
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);

        // Если контекст не Activity, добавляем флаг NEW_TASK
        if (context is not Activity)
        {
            intent.AddFlags(ActivityFlags.NewTask);
        }

        context.StartActivity(intent);
        return Task.CompletedTask;
    }
}
#endif