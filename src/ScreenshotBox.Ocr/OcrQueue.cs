using System.Threading.Channels;
using RapidOcrNet;
using ScreenshotBox.Core;

namespace ScreenshotBox.Ocr;

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

