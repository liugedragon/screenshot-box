using Microsoft.Win32;
using System.Windows.Input;
using System.Windows.Shapes;
namespace ScreenshotBox.App;

public sealed class PreviewWindow : Window
{
    private readonly Grid _content=new();
    private readonly ScrollViewer _scroll=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private readonly ScaleTransform _scale=new(1,1);
    private readonly TextBlock _label=new(){Margin=new(8),VerticalAlignment=VerticalAlignment.Center,FontSize=12,TextWrapping=TextWrapping.Wrap};
    private readonly BitmapSource _image;
    private Point? _pan;
    private Point _scrollStart;
    private bool _fitMode=true;
    public PreviewWindow(ScreenshotItem item,LibraryStore store,string query)
    {
        query=query.Trim();
        Title=string.IsNullOrWhiteSpace(item.Title)?L.T("图片预览", "Image preview"):item.Title;Width=1000;Height=750;MinWidth=600;MinHeight=420;
        SetResourceReference(BackgroundProperty,"SbBackground");SetResourceReference(ForegroundProperty,"SbText");
        _image=ImageLibrary.Load(store.ResolvePath(item.ImagePath));
        var root=new DockPanel();root.SetResourceReference(Panel.BackgroundProperty,"SbBackground");
        var tools=new WrapPanel{Margin=new(8,4,8,4)};DockPanel.SetDock(tools,Dock.Top);root.Children.Add(tools);
        AddButton(tools,L.T("适应窗口", "Fit to window"),Fit,"Ctrl+0");AddButton(tools,L.T("实际大小", "Actual size"),()=>Zoom(1/VisualTreeHelper.GetDpi(this).DpiScaleX),"Ctrl+1");
        AddButton(tools,"−",()=>Zoom(_scale.ScaleX/1.25),L.T("缩小", "Zoom out"));AddButton(tools,"＋",()=>Zoom(_scale.ScaleX*1.25),L.T("放大", "Zoom in"));
        AddButton(tools,L.T("复制图片", "Copy image"),async()=>await CopyAsync(),"Ctrl+C");
        AddButton(tools,L.T("导出图片", "Export image"),()=>{
            var dialog=new SaveFileDialog{Filter=L.T("PNG 图片|*.png", "PNG image|*.png"),FileName=L.T("截图.png", "Screenshot.png")};
            if(dialog.ShowDialog(this)!=true)return;
            try{ImageLibrary.ExportAtomic(store.ResolvePath(item.ImagePath),dialog.FileName);_label.Text=L.T("图片已导出", "Image exported");}catch(Exception ex){_label.Text=L.T("导出失败：", "Export failed: ")+L.Error(ex);}
        });tools.Children.Add(_label);_label.SetResourceReference(TextBlock.ForegroundProperty,"SbMuted");
        _content.Width=_image.PixelWidth;_content.Height=_image.PixelHeight;_content.LayoutTransform=_scale;
        _content.Children.Add(new Image{Source=_image,Stretch=Stretch.Fill});
        var canvas=new Canvas{Width=_image.PixelWidth,Height=_image.PixelHeight,IsHitTestVisible=false};
        foreach(var block in item.OcrBlocks.Where(b=>query.Length>0&&b.Text.Contains(query,StringComparison.OrdinalIgnoreCase))) {
            var rect=new Rectangle{Width=block.Width,Height=block.Height,Stroke=Brushes.Orange,StrokeThickness=2,Fill=new SolidColorBrush(Color.FromArgb(56,255,192,0))};
            Canvas.SetLeft(rect,block.X);Canvas.SetTop(rect,block.Y);canvas.Children.Add(rect);
        }
        _content.Children.Add(canvas);_scroll.Content=_content;root.Children.Add(_scroll);Content=root;
        Loaded+=(_,_)=>Fit();_scroll.SizeChanged+=(_,_)=>{if(_fitMode)Dispatcher.BeginInvoke(()=>{if(_fitMode)Fit();});};
        PreviewKeyDown+=async (_,e)=>{
            if(e.Key==Key.Escape){Close();e.Handled=true;}
            else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.C){e.Handled=true;await CopyAsync();}
            else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.D0){Fit();e.Handled=true;}
            else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.D1){Zoom(1/VisualTreeHelper.GetDpi(this).DpiScaleX);e.Handled=true;}
            else if(e.Key is Key.Add or Key.OemPlus){Zoom(_scale.ScaleX*1.25);e.Handled=true;}
            else if(e.Key is Key.Subtract or Key.OemMinus){Zoom(_scale.ScaleX/1.25);e.Handled=true;}
        };
        _scroll.PreviewMouseWheel+=(_,e)=>{if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){Zoom(_scale.ScaleX*(e.Delta>0?1.15:1/1.15));e.Handled=true;}};
        // Capture only image drags. Scroll bars retain their own native mouse handling.
        _content.MouseDown+=(_,e)=>{if(e.ChangedButton is MouseButton.Middle or MouseButton.Left){_pan=e.GetPosition(_scroll);_scrollStart=new(_scroll.HorizontalOffset,_scroll.VerticalOffset);_content.CaptureMouse();_content.Cursor=Cursors.Hand;e.Handled=true;}};
        _content.MouseMove+=(_,e)=>{if(_pan is Point start){var p=e.GetPosition(_scroll);_scroll.ScrollToHorizontalOffset(_scrollStart.X+start.X-p.X);_scroll.ScrollToVerticalOffset(_scrollStart.Y+start.Y-p.Y);}};
        _content.MouseUp+=(_,_)=>EndPan();_content.LostMouseCapture+=(_,_)=>{_pan=null;_content.Cursor=null;};
    }
    private void EndPan(){_pan=null;_content.ReleaseMouseCapture();_content.Cursor=null;}
    private async Task CopyAsync(){try{await ClipboardHelper.CopyImageAsync(_image);_label.Text=L.T("图片已复制", "Image copied");}catch(Exception ex){_label.Text=L.T("复制失败：", "Copy failed: ")+L.Error(ex);}}
    private void Zoom(double factor,bool fit=false){_fitMode=fit;factor=Math.Clamp(factor,.0001,8);_scale.ScaleX=_scale.ScaleY=factor;UpdateZoomLabel();}
    private void UpdateZoomLabel()=>_label.Text=L.F("{0:P0} · Ctrl+滚轮缩放，拖动图片平移", "{0:P0} · Ctrl+wheel to zoom; drag the image to pan",_scale.ScaleX*VisualTreeHelper.GetDpi(this).DpiScaleX);
    protected override void OnDpiChanged(DpiScale oldDpi,DpiScale newDpi){base.OnDpiChanged(oldDpi,newDpi);if(_fitMode)Dispatcher.BeginInvoke(Fit);else UpdateZoomLabel();}
    private void Fit(){Zoom(Math.Min(1/VisualTreeHelper.GetDpi(this).DpiScaleX,Math.Min(Math.Max(1,_scroll.ViewportWidth-24)/_image.PixelWidth,Math.Max(1,_scroll.ViewportHeight-24)/_image.PixelHeight)),true);_scroll.ScrollToHorizontalOffset(0);_scroll.ScrollToVerticalOffset(0);}
    private static void AddButton(Panel panel,string label,Action action,string? tip=null){var b=new Button{Content=label,ToolTip=tip};b.Click+=(_,_)=>action();panel.Children.Add(b);}
}
public sealed class ReportWindow : Window
{
    public ReportWindow(string title,string text){Title=title;Width=620;Height=420;MinWidth=400;MinHeight=260;PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}};SetResourceReference(BackgroundProperty,"SbBackground");SetResourceReference(ForegroundProperty,"SbText");Content=new TextBox{Text=text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new(16)};}
}
