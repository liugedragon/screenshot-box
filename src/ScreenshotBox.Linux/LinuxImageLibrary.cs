using ScreenshotBox.Core;
using SkiaSharp;

namespace ScreenshotBox.Linux;

/// <summary>Immutable PNG originals and proportional thumbnails, saved before metadata.</summary>
public sealed class LinuxImageLibrary(LibraryStore store)
{
    public Task<ScreenshotItem> AddFileAsync(string path, string title = "") =>
        AddDecodedAsync(() =>
        {
            using var input = File.OpenRead(path);
            return Decode(input);
        }, title);

    public Task<ScreenshotItem> AddPngAsync(byte[] png, string title = "")
    {
        ArgumentNullException.ThrowIfNull(png);
        // Own the input while asynchronous file writes run; the caller may reuse its buffer.
        var owned = (byte[])png.Clone();
        return AddDecodedAsync(() =>
        {
            using var input = new MemoryStream(owned, writable: false);
            return Decode(input);
        }, title);
    }

    private static SKBitmap? Decode(Stream input)
    {
        using var codec = SKCodec.Create(input);
        return codec is null ? null : SKBitmap.Decode(codec);
    }

    private async Task<ScreenshotItem> AddDecodedAsync(Func<SKBitmap?> decode, string title)
    {
        ScreenshotItem? item = null;
        string? original = null, thumbnail = null;
        try
        {
            await Task.Run(() =>
            {
                using var bitmap = decode() ?? throw new InvalidDataException(
                    L.T("无法读取图片，请选择有效的 PNG 或 JPEG 文件。", "The image could not be read. Select a valid PNG or JPEG file."));
                if (bitmap.Width <= 0 || bitmap.Height <= 0)
                    throw new InvalidDataException(L.T("图片尺寸无效。", "The image dimensions are invalid."));
                item = new ScreenshotItem { Title = title, Width = bitmap.Width, Height = bitmap.Height };
                item.ImagePath = $"images/{item.Id}.png";
                item.ThumbnailPath = $"thumbnails/{item.Id}.png";
                original = store.ResolvePath(item.ImagePath);
                thumbnail = store.ResolvePath(item.ThumbnailPath);
                WritePngAtomic(bitmap, original);
                double scale = Math.Min(1, 320.0 / Math.Max(bitmap.Width, bitmap.Height));
                var info = new SKImageInfo(Math.Max(1, (int)Math.Round(bitmap.Width * scale)),
                    Math.Max(1, (int)Math.Round(bitmap.Height * scale)), SKColorType.Bgra8888, SKAlphaType.Premul);
                using var thumb = bitmap.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                    ?? throw new IOException(L.T("无法生成缩略图。", "The thumbnail could not be generated."));
                WritePngAtomic(thumb, thumbnail);
            });
            await store.AddAsync(item!);
            return item!;
        }
        catch
        {
            // Files are not referenced until AddAsync commits. Roll back both on a failed import.
            DeleteIfPresent(original);
            DeleteIfPresent(thumbnail);
            throw;
        }
    }

    public static void WritePngAtomic(SKBitmap image, string path) => ReplaceAtomic(path, output =>
    {
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new IOException("PNG encoding failed.");
        encoded.SaveTo(output);
    });

    public static void WritePngAtomic(byte[] png, string path)
    {
        ArgumentNullException.ThrowIfNull(png);
        using var input = new MemoryStream(png, writable: false);
        using var image = Decode(input) ?? throw new InvalidDataException("Invalid PNG image.");
        WritePngAtomic(image, path);
    }

    public static void ExportAtomic(string source, string destination) => ReplaceAtomic(destination, output =>
    {
        using var input = File.OpenRead(source);
        input.CopyTo(output);
    });

    private static void ReplaceAtomic(string path, Action<FileStream> write)
    {
        path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { DeleteIfPresent(temporary); }
    }

    private static void DeleteIfPresent(string? path)
    {
        if (path is not null && File.Exists(path)) File.Delete(path);
    }
}
