using System.Text.Json;

namespace ScreenshotBox.App;

public sealed class Settings
{
    public string Shortcut { get; set; } = "Ctrl+Alt+S";
    public string Theme { get; set; } = "System";
    public string Language { get; set; } = "System";
    public double ThumbnailSize { get; set; } = 220;
    public string DataDirectory { get; set; } = "";
    public string AnnotationColor { get; set; } = "#FF3B30";
    public double PenWidth { get; set; } = 4;
    public double EraserWidth { get; set; } = 20;
    public int MosaicSize { get; set; } = 12;
    private static string PathName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenshotBox", "settings.json");
    public static Settings Load() => LoadFrom(PathName);
    internal static Settings LoadFrom(string path) { try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new(); } catch { return new(); } }
    public void Save() { if(Application.Current is App { IsTesting:true })return; SaveTo(PathName); }
    internal void SaveTo(string path) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); var temporary=path+".tmp"; try { File.WriteAllText(temporary,JsonSerializer.Serialize(this)); File.Move(temporary,path,true); } finally { if(File.Exists(temporary))File.Delete(temporary); } }
}

public sealed class ImageLibrary(LibraryStore store)
{
    public static BitmapSource Load(string path, int decodeWidth = 0)
    {
        using var stream = File.OpenRead(path);
        var bmp = new BitmapImage(); bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad;
        if (decodeWidth > 0) bmp.DecodePixelWidth = decodeWidth;
        bmp.StreamSource = stream; bmp.EndInit(); bmp.Freeze(); return bmp;
    }
    public static void WritePng(BitmapSource image, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new FileStream(path, FileMode.CreateNew); encoder.Save(stream);
    }
    // Write completely next to the destination, then replace atomically. A failed write preserves the old image.
    public static void WritePngAtomic(BitmapSource image, string path) => ReplaceAtomic(path, stream => {
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(stream);
    });
    public static void ExportAtomic(string source, string destination) => ReplaceAtomic(destination, stream => {
        using var input=File.OpenRead(source);input.CopyTo(stream);
    });
    private static void ReplaceAtomic(string path, Action<FileStream> write)
    {
        path=Path.GetFullPath(path);var directory=Path.GetDirectoryName(path)!;Directory.CreateDirectory(directory);
        var temporary=Path.Combine(directory,"."+Path.GetFileName(path)+"."+Guid.NewGuid().ToString("N")+".tmp");
        try {
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){write(stream);stream.Flush(true);}
            File.Move(temporary,path,true);
        }finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public async Task<ScreenshotItem> AddAsync(BitmapSource image, string title = "")
    {
        var item = new ScreenshotItem { Title = title, Width = image.PixelWidth, Height = image.PixelHeight };
        item.ImagePath = $"images/{item.Id}.png"; item.ThumbnailPath = $"thumbnails/{item.Id}.png";
        var original = store.ResolvePath(item.ImagePath); var thumb = store.ResolvePath(item.ThumbnailPath);
        await Task.Run(() => {
            WritePng(image, original);
            try { WritePng(Load(original, Math.Min(320, image.PixelWidth)), thumb); }
            catch { File.Delete(original); throw; }
        });
        try { await store.AddAsync(item); }
        catch { File.Delete(original); File.Delete(thumb); throw; }
        return item;
    }
}

public static class ClipboardHelper
{
    public static async Task CopyImageAsync(BitmapSource image)
    {
        Exception? last = null;
        for (int attempt=0;attempt<5;attempt++) {
            try { Clipboard.SetImage(image); return; } catch (System.Runtime.InteropServices.ExternalException ex) { last=ex; await Task.Delay(80*(attempt+1)); }
        }
        throw new IOException(L.T("剪贴板暂时被其他应用占用。", "The clipboard is in use by another application."), last);
    }
}
