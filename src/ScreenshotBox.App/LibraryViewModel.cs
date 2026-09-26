using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace ScreenshotBox.App;
public sealed class LibraryViewModel : INotifyPropertyChanged
{
    public ObservableCollection<ScreenshotCard> Items { get; } = [];
    private string _status=L.T("准备就绪","Ready");
    public string Status { get=>_status;set{_status=value;OnPropertyChanged();} }
    private double _thumbnailSize=220;
    public double ThumbnailSize { get=>_thumbnailSize;set{_thumbnailSize=value;OnPropertyChanged();} }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
public sealed class ScreenshotCard
{
    public ScreenshotItem Item { get; }
    public BitmapSource? Thumbnail { get; }
    public string Title => string.IsNullOrWhiteSpace(Item.Title)?Item.CreatedUtc.ToLocalTime().ToString(L.T("MM月dd日 HH:mm:ss","MMM dd HH:mm:ss")):Item.Title;
    public string Time => Item.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string Status => Item.OcrStatus switch { "Ready"=>L.T("可搜索","Searchable"), "Failed"=>L.T("识别失败，可重试","OCR failed · Retry available"), _=>L.T("正在识别 · 文字暂不可搜索","Recognizing · Image text is not searchable yet") };
    public string Summary { get; }
    public ScreenshotCard(ScreenshotItem item, LibraryStore store, string query)
    {
        Item=item;
        try { Thumbnail=ImageLibrary.Load(store.ResolvePath(item.ThumbnailPath),320); } catch { }
        var source=string.Join(" · ",new[]{item.Title,item.Notes,item.Tags,item.OcrText}.Where(s=>s.Length>0));
        query=query.Trim();var i=source.IndexOf(query,StringComparison.OrdinalIgnoreCase);
        Summary=query.Length>0 && i>=0?source.Substring(Math.Max(0,i-12),Math.Min(100,source.Length-Math.Max(0,i-12))).Replace('\r',' ').Replace('\n',' '):"";
    }
}
