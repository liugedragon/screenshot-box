using System.Text.Json;
using System.Threading.Channels;
using RapidOcrNet;

namespace ScreenshotBox.App;

public sealed class Settings
{
    public string Shortcut { get; set; } = "Ctrl+Alt+S";
    public string Theme { get; set; } = "System";
    public double ThumbnailSize { get; set; } = 220;
    public string DataDirectory { get; set; } = "";
    public string AnnotationColor { get; set; } = "#FF3B30";
    public double PenWidth { get; set; } = 4;
    public double EraserWidth { get; set; } = 20;
    public int MosaicSize { get; set; } = 12;
    private static string PathName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenshotBox", "settings.json");
    public static Settings Load() { try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathName)) ?? new(); } catch { return new(); } }
    public void Save() { if(Application.Current is App { IsTesting:true })return; Directory.CreateDirectory(Path.GetDirectoryName(PathName)!); var temporary = PathName + ".tmp"; File.WriteAllText(temporary, JsonSerializer.Serialize(this)); File.Move(temporary, PathName, true); }
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

public sealed class OcrQueue : IAsyncDisposable
{
    private readonly LibraryStore _store;
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _stop = new();
    private readonly HashSet<string> _queued = [];
    private readonly Task _worker;
    private RapidOcr? _engine;
    private int _disposed;
    public event Action<string>? Changed;
    public const string ModelVersion = "RapidOcrNet-4.2.0/PP-OCRv5-Chinese-Mobile";
    public OcrQueue(LibraryStore store) { _store = store; _worker = Task.Run(RunAsync); }
    public void Enqueue(string id)
    {
        if(Volatile.Read(ref _disposed)!=0)return;
        lock(_queued){if(_queued.Add(id)&&!_channel.Writer.TryWrite(id))_queued.Remove(id);}
    }
    private RapidOcr Engine()
    {
        if (_engine != null) return _engine;
        var root = AppContext.BaseDirectory;
        var engine = new RapidOcr();
        using var options = RapidOcr.GetDefaultSessionOptions(2);
        try {
            engine.InitModels(Path.Combine(root, "models/v5/ch_PP-OCRv5_mobile_det.onnx"),
                Path.Combine(root, "models/v5/ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx"),
                Path.Combine(root, "models/chinese/ch_PP-OCRv5_rec_mobile.onnx"),
                Path.Combine(root, "models/chinese/ppocrv5_dict.txt"), options);
            return _engine = engine;
        } catch { engine.Dispose(); throw; }
    }
    public async Task<(string Text, List<OcrBlock> Blocks)> RecognizeAsync(string path, CancellationToken token)
    {
        var result = await Engine().DetectAsync(path, RapidOcrOptions.Default, null, token);
        var blocks = result.TextBlocks.Select(b => {
            var xs = b.BoxPoints.Select(p => (double)p.X).ToArray(); var ys = b.BoxPoints.Select(p => (double)p.Y).ToArray();
            return new OcrBlock(b.Text, xs.Min(), ys.Min(), xs.Max()-xs.Min(), ys.Max()-ys.Min(), b.CharScores?.Average() ?? 0);
        }).ToList();
        return (result.StrRes ?? "", blocks);
    }
    private async Task RunAsync()
    {
        try {
            await foreach (var id in ReadJobsAsync()) {
                int generation = -1;
                try {
                    generation = await _store.BeginOcrAsync(id); if (generation < 0) continue;
                    Changed?.Invoke(id);
                    var item = await _store.GetAsync(id); if (item == null || item.IsDeleted) continue;
                    var (text, blocks) = await RecognizeAsync(_store.ResolvePath(item.ImagePath), _stop.Token);
                    await _store.CompleteOcrAsync(id, generation, text, blocks, ModelVersion);
                } catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
                catch (Exception ex) { if (generation >= 0) await _store.CompleteOcrAsync(id, generation, "", [], ModelVersion, ex.Message); }
                finally {
                    lock(_queued)_queued.Remove(id);
                    // Restore can invalidate this generation while the old job still owns the queue slot.
                    // Its Enqueue is then deduplicated; hand Pending back to the worker when that slot clears.
                    if(!_stop.IsCancellationRequested) {
                        var current=await _store.GetAsync(id);
                        if(current is {IsDeleted:false,OcrStatus:"Pending"})Enqueue(id);
                    }
                    Changed?.Invoke(id);
                }
            }
        } catch (OperationCanceledException) { }
        finally { _engine?.Dispose(); }
    }
    private async IAsyncEnumerable<string> ReadJobsAsync()
    {
        while(!_stop.IsCancellationRequested){
            using var idle=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);idle.CancelAfter(TimeSpan.FromSeconds(90));
            bool available;
            try {available=await _channel.Reader.WaitToReadAsync(idle.Token);}
            catch(OperationCanceledException) when(!_stop.IsCancellationRequested){_engine?.Dispose();_engine=null;continue;}
            if(!available)yield break;
            while(_channel.Reader.TryRead(out var id))yield return id;
        }
    }
    public async ValueTask DisposeAsync() { if(Interlocked.Exchange(ref _disposed,1)!=0)return;_stop.Cancel(); _channel.Writer.TryComplete(); await _worker; _stop.Dispose(); }
}

public static class ClipboardHelper
{
    public static async Task CopyImageAsync(BitmapSource image)
    {
        Exception? last = null;
        for (int attempt=0;attempt<5;attempt++) {
            try { Clipboard.SetImage(image); return; } catch (System.Runtime.InteropServices.ExternalException ex) { last=ex; await Task.Delay(80*(attempt+1)); }
        }
        throw new IOException("剪贴板暂时被其他应用占用。", last);
    }
}
