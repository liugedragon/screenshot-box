using System.Windows.Input;
using System.Windows.Shapes;
namespace ScreenshotBox.App;

public sealed class PreviewWindow : Window
{
    private readonly Grid _content=new();
    private readonly ScrollViewer _scroll=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private readonly ScaleTransform _scale=new(1,1);
    private readonly TextBlock _label=new(){Margin=new(8),VerticalAlignment=VerticalAlignment.Center};
    private readonly BitmapSource _image;
    private Point? _pan;
    private Point _scrollStart;
    public PreviewWindow(ScreenshotItem item,LibraryStore store,string query)
    {
        query=query.Trim();
        Title=string.IsNullOrWhiteSpace(item.Title)?"图片预览":item.Title;Width=1000;Height=750;MinWidth=600;MinHeight=420;
        SetResourceReference(BackgroundProperty,"SbBackground");SetResourceReference(ForegroundProperty,"SbText");
        _image=ImageLibrary.Load(store.ResolvePath(item.ImagePath));
        var root=new DockPanel();root.SetResourceReference(Panel.BackgroundProperty,"SbBackground");var tools=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(tools,Dock.Top);root.Children.Add(tools);
        AddButton(tools,"适应窗口",()=>Fit());AddButton(tools,"实际大小",()=>Zoom(1/VisualTreeHelper.GetDpi(this).DpiScaleX));AddButton(tools,"−",()=>Zoom(_scale.ScaleX/1.25));AddButton(tools,"＋",()=>Zoom(_scale.ScaleX*1.25));tools.Children.Add(_label);
        _content.Width=_image.PixelWidth;_content.Height=_image.PixelHeight;_content.LayoutTransform=_scale;
        _content.Children.Add(new Image{Source=_image,Stretch=Stretch.Fill});
        var canvas=new Canvas{Width=_image.PixelWidth,Height=_image.PixelHeight,IsHitTestVisible=false};
        foreach(var block in item.OcrBlocks.Where(b=>query.Length>0&&b.Text.Contains(query,StringComparison.OrdinalIgnoreCase))) {
            var rect=new Rectangle{Width=block.Width,Height=block.Height,Stroke=Brushes.Orange,StrokeThickness=2,Fill=new SolidColorBrush(Color.FromArgb(56,255,192,0)),ToolTip=block.Text};
            Canvas.SetLeft(rect,block.X);Canvas.SetTop(rect,block.Y);canvas.Children.Add(rect);
        }
        _content.Children.Add(canvas);_scroll.Content=_content;root.Children.Add(_scroll);Content=root;
        Loaded+=(_,_)=>Fit();PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape)Close();};
        _scroll.PreviewMouseWheel+=(_,e)=>{if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){Zoom(_scale.ScaleX*(e.Delta>0?1.15:1/1.15));e.Handled=true;}};
        _scroll.PreviewMouseDown+=(_,e)=>{if(e.ChangedButton==MouseButton.Middle||e.ChangedButton==MouseButton.Left){_pan=e.GetPosition(_scroll);_scrollStart=new(_scroll.HorizontalOffset,_scroll.VerticalOffset);_scroll.CaptureMouse();e.Handled=true;}};
        _scroll.PreviewMouseMove+=(_,e)=>{if(_pan is Point start){var p=e.GetPosition(_scroll);_scroll.ScrollToHorizontalOffset(_scrollStart.X+start.X-p.X);_scroll.ScrollToVerticalOffset(_scrollStart.Y+start.Y-p.Y);}};
        _scroll.PreviewMouseUp+=(_,_)=>{_pan=null;_scroll.ReleaseMouseCapture();};
    }
    private void Zoom(double factor){factor=Math.Clamp(factor,.05,8);_scale.ScaleX=_scale.ScaleY=factor;UpdateZoomLabel();}
    private void UpdateZoomLabel()=>_label.Text=$"{_scale.ScaleX*VisualTreeHelper.GetDpi(this).DpiScaleX:P0} · Ctrl+滚轮缩放，拖动平移";
    protected override void OnDpiChanged(DpiScale oldDpi,DpiScale newDpi){base.OnDpiChanged(oldDpi,newDpi);UpdateZoomLabel();}
    private void Fit()=>Zoom(Math.Min(1,Math.Min(Math.Max(1,_scroll.ViewportWidth-24)/_image.PixelWidth,Math.Max(1,_scroll.ViewportHeight-24)/_image.PixelHeight)));
    private static void AddButton(Panel panel,string label,Action action){var b=new Button{Content=label};b.Click+=(_,_)=>action();panel.Children.Add(b);}
}
public sealed class ReportWindow : Window
{
    public ReportWindow(string title,string text){Title=title;Width=620;Height=420;SetResourceReference(BackgroundProperty,"SbBackground");SetResourceReference(ForegroundProperty,"SbText");Content=new TextBox{Text=text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new(16)};}
}
