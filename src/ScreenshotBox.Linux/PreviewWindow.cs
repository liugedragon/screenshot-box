using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ScreenshotBox.Core;

namespace ScreenshotBox.Linux;

public sealed class PreviewWindow : Window
{
    private readonly Bitmap? _bitmap;
    private readonly Canvas _canvas = new();
    private readonly ScrollViewer _scroll;
    private readonly ScreenshotItem _item;
    private readonly string _query;
    private readonly TextBlock _scaleLabel = new();
    private double _scale = 1;
    public PreviewWindow(string path, ScreenshotItem item, string query)
    {
        _item = item; _query = query;
        Title = string.IsNullOrEmpty(item.Title) ? L.T("原图预览", "Image preview") : item.Title;
        Width = 1000; Height = 720; MinWidth = 540; MinHeight = 360; App.StyleWindow(this);
        var panel = new DockPanel();
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12) };
        tools.Children.Add(MainWindow.Button(L.T("适应窗口", "Fit"), () => { Fit(); return Task.CompletedTask; }));
        tools.Children.Add(MainWindow.Button("100%", () => { SetScale(1); return Task.CompletedTask; }));
        tools.Children.Add(MainWindow.Button("−", () => { SetScale(_scale / 1.25); return Task.CompletedTask; }));
        tools.Children.Add(MainWindow.Button("+", () => { SetScale(_scale * 1.25); return Task.CompletedTask; }));
        tools.Children.Add(_scaleLabel); DockPanel.SetDock(tools, Dock.Top); panel.Children.Add(tools);
        _scroll = new ScrollViewer { Content = _canvas, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        panel.Children.Add(_scroll); Content = panel;
        if (File.Exists(path)) _bitmap = new Bitmap(path);
        else _canvas.Children.Add(new TextBlock { Text = L.T("原图缺失，请从备份恢复。", "The original image is missing. Restore it from a backup."), Margin = new Thickness(20) });
        Opened += (_, _) => Fit();
        Closed += (_, _) => _bitmap?.Dispose();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        _scroll.PointerWheelChanged += (_, e) => { if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) { SetScale(_scale * (e.Delta.Y > 0 ? 1.15 : 1 / 1.15)); e.Handled = true; } };
        bool dragging = false; Point origin = default; Vector offset = default;
        _canvas.PointerPressed += (_, e) => { if (e.GetCurrentPoint(_canvas).Properties.IsLeftButtonPressed) { dragging = true; origin = e.GetPosition(_scroll); offset = _scroll.Offset; e.Pointer.Capture(_canvas); } };
        _canvas.PointerMoved += (_, e) => { if (dragging) { var delta = e.GetPosition(_scroll) - origin; _scroll.Offset = offset - delta; } };
        _canvas.PointerReleased += (_, e) => { dragging = false; e.Pointer.Capture(null); };
    }
    private void Fit() => SetScale(Math.Min(1, Math.Min(Math.Max(100, _scroll.Bounds.Width - 24) / Math.Max(1, _item.Width), Math.Max(100, _scroll.Bounds.Height - 24) / Math.Max(1, _item.Height))));
    private void SetScale(double scale)
    {
        _scale = Math.Clamp(scale, .05, 8); _scaleLabel.Text = $"{_scale:P0}";
        if (_bitmap is null) return;
        _canvas.Children.Clear(); _canvas.Width = _item.Width * _scale; _canvas.Height = _item.Height * _scale;
        _canvas.Children.Add(new Image { Source = _bitmap, Width = _canvas.Width, Height = _canvas.Height, Stretch = Stretch.Fill });
        if (string.IsNullOrWhiteSpace(_query)) return;
        foreach (var block in _item.OcrBlocks.Where(b => b.Text.Contains(_query.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            var box = new Border { Width = block.Width * _scale, Height = block.Height * _scale, Background = new SolidColorBrush(Color.Parse("#50FFC247")), BorderBrush = new SolidColorBrush(Color.Parse("#E8A600")), BorderThickness = new Thickness(1), IsHitTestVisible = false };
            Canvas.SetLeft(box, block.X * _scale); Canvas.SetTop(box, block.Y * _scale); _canvas.Children.Add(box);
        }
    }
}
