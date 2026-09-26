using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ScreenshotBox.App.Native;
using ScreenshotBox.Linux.Native;
using SkiaSharp;
using APixelPoint = Avalonia.PixelPoint;
using SelectionPoint = ScreenshotBox.App.Native.PixelPoint;

namespace ScreenshotBox.Linux;

public enum CaptureAction { SaveAndCopy, CopyOnly, Export }
public sealed record CaptureResult(byte[] Png, CaptureAction Action);

public sealed class CaptureWindow : Window
{
    private static bool _capturing;
    private static CaptureToolPreferences _preferences = new("#EF4444", 4, 24, 12);
    private readonly DesktopFrame _frame;
    private readonly CaptureSurface _surface;
    private readonly Canvas _canvas = new();
    private readonly Border _toolbar;
    private readonly Border _hint;
    private readonly TextBlock _dimensions = new();
    private readonly TextBlock _toolValue = new();
    private readonly Slider _width = new() { Width = 112, Minimum = 1, Maximum = 48, Value = 4 };
    private readonly TextBox _color = new() { Width = 88, MaxLength = 7, FontSize = 12, MinHeight = 30 };
    private readonly Button _undo;
    private readonly Dictionary<AnnotationTool, Button> _toolButtons = [];
    private readonly TaskCompletionSource<CaptureResult?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CaptureSession Session { get; }
    private bool _updatingWidth;
    private bool _finished;
    private double _scale = 1;
    public Task<CaptureResult?> Completion => _result.Task;

    public static void ConfigureTools(string colorHex, int penWidth, int eraserWidth, int mosaicSize)
    {
        if (string.IsNullOrEmpty(colorHex) || colorHex.Length != 7 || !SKColor.TryParse(colorHex, out _)) colorHex = "#EF4444";
        _preferences = new(colorHex, Math.Clamp(penWidth, 1, 48), Math.Clamp(eraserWidth, 4, 96), Math.Clamp(mosaicSize, 4, 48));
    }
    public static CaptureToolPreferences GetToolPreferences() => _preferences;

    public static async Task<CaptureResult?> CaptureAsync(Window owner)
    {
        if (_capturing) return null;
        _capturing = true;
        var restoreOnCancel = owner.IsVisible;
        CaptureWindow? overlay = null;
        try
        {
            owner.Hide();
            // Let X11 remove the owner before acquiring the frozen root image.
            await Task.Delay(180);
            var frame = await Task.Run(X11Desktop.Capture);
            try { overlay = new CaptureWindow(frame); }
            catch { frame.Dispose(); throw; }
            overlay.Show();
            var result = await overlay.Completion;
            if (result is null && restoreOnCancel) { owner.Show(); owner.Activate(); }
            return result;
        }
        catch
        {
            if (restoreOnCancel) { owner.Show(); owner.Activate(); }
            throw;
        }
        finally { overlay?.DisposeFrame(); _capturing = false; }
    }

    public CaptureWindow(DesktopFrame frame)
    {
        _frame = frame;
        Session = new CaptureSession(frame.Bounds) { Preferences = _preferences };
        Title = L.T("截图资料盒 · 区域截图", "ScreenshotBox · Region capture");
        WindowDecorations = WindowDecorations.None; CanResize = false; Topmost = true;
        ShowInTaskbar = false; Background = Brushes.Black; WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new APixelPoint(0, 0);
        _scale = Screens.Primary?.Scaling ?? 1;
        Width = frame.Bitmap.Width / _scale; Height = frame.Bitmap.Height / _scale;
        _surface = new CaptureSurface(this) { Focusable = true };
        _canvas.Children.Add(_surface);
        _hint = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F022252B")), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8),
            Child = new TextBlock { Text = L.T("拖动框选 · 八点调整或移动 · Enter 保存并复制 · Esc 取消", "Drag to select · Resize or move · Enter saves and copies · Esc cancels"), Foreground = Brushes.White, FontSize = 13, TextWrapping = TextWrapping.Wrap }
        };
        _canvas.Children.Add(_hint);
        var toolRow = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (tool, zh, en) in new[]
        {
            (AnnotationTool.Select, "选区", "Select"), (AnnotationTool.Pen, "画笔", "Pen"), (AnnotationTool.Arrow, "箭头", "Arrow"),
            (AnnotationTool.Rectangle, "矩形", "Rectangle"), (AnnotationTool.Mosaic, "马赛克", "Mosaic"), (AnnotationTool.Eraser, "橡皮擦", "Eraser")
        })
        {
            var captured = tool;
            var button = MakeButton(L.T(zh, en), () => SetTool(captured));
            if (tool == AnnotationTool.Eraser) ToolTip.SetTip(button, L.T("移除光标经过的整条标注，可撤销。", "Remove whole annotations under the cursor; Undo restores them."));
            _toolButtons.Add(tool, button); toolRow.Children.Add(button);
        }
        _undo = MakeButton(L.T("撤销", "Undo"), () => { Session.Annotations.Undo(); Refresh(); });
        ToolTip.SetTip(_undo, "Ctrl+Z"); toolRow.Children.Add(_undo);
        var options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 8, 0) };
        options.Children.Add(_toolValue); options.Children.Add(_width);
        _toolValue.Foreground = Brushes.White; _toolValue.FontSize = 12; _toolValue.VerticalAlignment = VerticalAlignment.Center;
        toolRow.Children.Add(options);
        var colorRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(4, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        foreach (var hex in new[] { "#EF4444", "#F59E0B", "#22C55E", "#3B82F6", "#FFFFFF", "#111827" })
        {
            var value = hex;
            var swatch = new Button { Width = 24, Height = 24, MinWidth = 0, MinHeight = 0, Padding = new Thickness(0), Margin = new Thickness(2), Background = new SolidColorBrush(Color.Parse(hex)), BorderBrush = new SolidColorBrush(Color.Parse("#94A3B8")), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
            ToolTip.SetTip(swatch, hex); swatch.Click += (_, _) => SetColor(value); colorRow.Children.Add(swatch);
        }
        _color.Text = _preferences.ColorHex; ToolTip.SetTip(_color, L.T("输入 #RRGGBB 后按 Enter", "Enter #RRGGBB and press Enter"));
        _color.LostFocus += (_, _) => ApplyColor(); colorRow.Children.Add(_color); toolRow.Children.Add(colorRow);
        var actionRow = new WrapPanel { Orientation = Orientation.Horizontal };
        var save = MakeButton(L.T("保存并复制", "Save and copy"), () => Complete(CaptureAction.SaveAndCopy));
        save.Background = new SolidColorBrush(Color.Parse("#2563EB")); save.Foreground = Brushes.White;
        actionRow.Children.Add(save);
        actionRow.Children.Add(MakeButton(L.T("仅复制", "Copy only"), () => Complete(CaptureAction.CopyOnly)));
        actionRow.Children.Add(MakeButton(L.T("另存 PNG", "Save PNG"), () => Complete(CaptureAction.Export)));
        actionRow.Children.Add(MakeButton(L.T("重新选择", "Reselect"), () => { Session.Redraw(); SetTool(AnnotationTool.Select); Refresh(); }));
        actionRow.Children.Add(MakeButton(L.T("取消", "Cancel"), Cancel));
        _dimensions.Foreground = new SolidColorBrush(Color.Parse("#CBD5E1")); _dimensions.FontSize = 12;
        _dimensions.Margin = new Thickness(12, 0, 4, 0); _dimensions.VerticalAlignment = VerticalAlignment.Center;
        actionRow.Children.Add(_dimensions);
        var toolbarContent = new StackPanel { Spacing = 4 };
        toolbarContent.Children.Add(toolRow); toolbarContent.Children.Add(actionRow);
        _toolbar = new Border { Background = new SolidColorBrush(Color.Parse("#F522252B")), BorderBrush = new SolidColorBrush(Color.Parse("#526171")), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(6), Child = toolbarContent, IsVisible = false };
        _canvas.Children.Add(_toolbar);
        Content = _canvas;
        _width.ValueChanged += (_, _) =>
        {
            if (_updatingWidth) return;
            var value = (int)Math.Round(_width.Value);
            var p = Session.Preferences;
            Session.Preferences = Session.Tool switch { AnnotationTool.Eraser => p with { EraserWidth = value }, AnnotationTool.Mosaic => p with { MosaicSize = value }, _ => p with { PenWidth = value } };
            _preferences = Session.Preferences; UpdateToolLabel();
        };
        Opened += (_, _) => { _scale = RenderScaling; Width = frame.Bitmap.Width / _scale; Height = frame.Bitmap.Height / _scale; Position = new APixelPoint(0, 0); LayoutOverlay(); _surface.Focus(); Activate(); };
        SizeChanged += (_, _) => LayoutOverlay();
        KeyDown += CaptureKeyDown;
        Closed += (_, _) => { if (!_finished) { _finished = true; _result.TrySetResult(null); } };
        SetTool(AnnotationTool.Select);
    }

    private static Button MakeButton(string label, Action action)
    {
        var button = new Button { Content = label, FontSize = 12, MinHeight = 32, Padding = new Thickness(9, 5), Margin = new Thickness(2), CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.Parse("#363C46")), Foreground = Brushes.White, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
        button.Click += (_, _) => action(); return button;
    }
    private void SetColor(string hex)
    {
        if (!SKColor.TryParse(hex, out _) || hex.Length != 7) return;
        Session.Preferences = Session.Preferences with { ColorHex = hex.ToUpperInvariant() };
        _preferences = Session.Preferences; _color.Text = Session.Preferences.ColorHex;
        _color.BorderBrush = new SolidColorBrush(Color.Parse("#64748B"));
    }
    private void ApplyColor()
    {
        var text = _color.Text?.Trim() ?? "";
        if (text.Length == 7 && SKColor.TryParse(text, out _)) SetColor(text);
        else _color.BorderBrush = new SolidColorBrush(Color.Parse("#EF4444"));
    }
    private void SetTool(AnnotationTool tool)
    {
        Session.Tool = tool;
        foreach (var (key, button) in _toolButtons)
            button.Background = new SolidColorBrush(Color.Parse(key == tool ? "#2563EB" : "#363C46"));
        _updatingWidth = true;
        _width.Minimum = tool == AnnotationTool.Mosaic ? 4 : tool == AnnotationTool.Eraser ? 4 : 1;
        _width.Maximum = tool == AnnotationTool.Eraser ? 96 : 48;
        _width.Value = tool switch { AnnotationTool.Eraser => Session.Preferences.EraserWidth, AnnotationTool.Mosaic => Session.Preferences.MosaicSize, _ => Session.Preferences.PenWidth };
        _width.IsEnabled = tool != AnnotationTool.Select;
        _updatingWidth = false; UpdateToolLabel(); _surface.InvalidateVisual();
    }
    private void UpdateToolLabel()
    {
        var p = Session.Preferences;
        _toolValue.Text = Session.Tool switch
        {
            AnnotationTool.Eraser => L.F("橡皮 {0}px", "Eraser {0}px", p.EraserWidth),
            AnnotationTool.Mosaic => L.F("色块 {0}px", "Blocks {0}px", p.MosaicSize),
            _ => L.F("笔宽 {0}px", "Width {0}px", p.PenWidth)
        };
    }
    private void CaptureKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Cancel(); e.Handled = true; }
        else if (e.Key == Key.Enter)
        {
            if (e.Source is TextBox) ApplyColor(); else Complete(CaptureAction.SaveAndCopy);
            e.Handled = true;
        }
        else if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { Session.Annotations.Undo(); Refresh(); e.Handled = true; }
    }
    public void Complete(CaptureAction action)
    {
        if (_finished || Session.Selection.IsEmpty) return;
        ApplyColor(); Session.End();
        var png = Session.Annotations.RenderPng(_frame.Bitmap, Session.Selection);
        _preferences = Session.Preferences;
        _finished = true; _result.TrySetResult(new CaptureResult(png, action)); Close();
    }
    public void Cancel()
    {
        if (_finished) return;
        _finished = true; _result.TrySetResult(null); Close();
    }
    public void Refresh()
    {
        _surface.UpdateEditedImage();
        _dimensions.Text = $"{Session.Selection.Width} × {Session.Selection.Height} px";
        _toolbar.IsVisible = !Session.Selection.IsEmpty && !Session.IsDragging;
        _hint.IsVisible = Session.Selection.IsEmpty;
        _undo.IsEnabled = Session.Annotations.CanUndo;
        LayoutOverlay(); _surface.InvalidateVisual();
    }
    private void LayoutOverlay()
    {
        var width = Math.Max(1, Bounds.Width); var height = Math.Max(1, Bounds.Height);
        _surface.Width = width; _surface.Height = height;
        Canvas.SetLeft(_surface, 0); Canvas.SetTop(_surface, 0);
        Canvas.SetLeft(_hint, 16); Canvas.SetTop(_hint, 16);
        _hint.MaxWidth = Math.Max(1, width - 32);
        _toolbar.Width = Math.Min(990, Math.Max(100, width - 24));
        _toolbar.Measure(new Size(_toolbar.Width, double.PositiveInfinity));
        var r = Session.Selection;
        var toolbarHeight = _toolbar.DesiredSize.Height;
        var x = Math.Clamp(r.Left / PixelScaleX, 12, Math.Max(12, width - _toolbar.Width - 12));
        var below = height - r.Bottom / PixelScaleY - 24;
        var above = r.Top / PixelScaleY - 24;
        var y = below >= toolbarHeight ? r.Bottom / PixelScaleY + 12
            : above >= toolbarHeight ? r.Top / PixelScaleY - toolbarHeight - 12
            : below >= above ? height - toolbarHeight - 12 : 12;
        y = Math.Clamp(y, 12, Math.Max(12, height - toolbarHeight - 12));
        Canvas.SetLeft(_toolbar, x); Canvas.SetTop(_toolbar, y);
    }
    internal double PixelScaleX => _frame.Bitmap.Width / Math.Max(1, _surface.Bounds.Width);
    internal double PixelScaleY => _frame.Bitmap.Height / Math.Max(1, _surface.Bounds.Height);
    private bool _frameDisposed;
    public void DisposeFrame()
    {
        if (_frameDisposed) return;
        _frameDisposed = true; _surface.DisposeImages(); _frame.Dispose();
    }

    private sealed class CaptureSurface : Control
    {
        private readonly CaptureWindow _owner;
        private Bitmap? _frozen, _edited;
        private SelectionPoint? _cursor;
        public CaptureSurface(CaptureWindow owner)
        {
            _owner = owner;
            using var image = SKImage.FromBitmap(owner._frame.Bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            _frozen = new Bitmap(new MemoryStream(data.ToArray()));
            PointerPressed += Pressed; PointerMoved += Moved; PointerReleased += Released;
            PointerCaptureLost += (_, _) => { owner.Session.End(); owner.Refresh(); };
        }
        private SelectionPoint Pixel(Point p) => SelectionGeometry.Clamp(new((int)Math.Round(p.X * _owner.PixelScaleX), (int)Math.Round(p.Y * _owner.PixelScaleY)), _owner.Session.Bounds);
        private void Pressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            Focus(); _owner.Session.Begin(Pixel(e.GetPosition(this)), Math.Max(6, (int)Math.Round(7 * _owner.PixelScaleX)));
            e.Pointer.Capture(this); _owner.Refresh(); e.Handled = true;
        }
        private void Moved(object? sender, PointerEventArgs e)
        {
            var p = Pixel(e.GetPosition(this)); _cursor = p;
            if (_owner.Session.IsDragging) { _owner.Session.Update(p); _owner.Refresh(); }
            else
            {
                var hit = SelectionGeometry.HitTest(_owner.Session.Selection, p);
                Cursor = new Cursor(_owner.Session.Tool == AnnotationTool.Select && hit == SelectionHandle.Move ? StandardCursorType.SizeAll : StandardCursorType.Cross);
                InvalidateVisual();
            }
        }
        private void Released(object? sender, PointerReleasedEventArgs e)
        {
            if (!_owner.Session.IsDragging) return;
            _owner.Session.Update(Pixel(e.GetPosition(this))); _owner.Session.End(); e.Pointer.Capture(null); _owner.Refresh(); e.Handled = true;
        }
        public void UpdateEditedImage()
        {
            _edited?.Dispose(); _edited = null;
            var r = _owner.Session.Selection;
            if (!r.IsEmpty && _owner.Session.Annotations.Count > 0)
                _edited = new Bitmap(new MemoryStream(_owner.Session.Annotations.RenderPng(_owner._frame.Bitmap, r)));
        }
        public override void Render(DrawingContext context)
        {
            if (_frozen is null) return;
            var all = new Rect(0, 0, Bounds.Width, Bounds.Height);
            context.DrawImage(_frozen, all);
            var selection = _owner.Session.Selection;
            var sx = _owner.PixelScaleX; var sy = _owner.PixelScaleY;
            var dim = new SolidColorBrush(Color.FromArgb(135, 0, 0, 0));
            if (selection.IsEmpty) { context.DrawRectangle(dim, null, all); return; }
            var r = new Rect(selection.Left / sx, selection.Top / sy, selection.Width / sx, selection.Height / sy);
            if (_edited is not null) context.DrawImage(_edited, r);
            context.DrawRectangle(dim, null, new Rect(0, 0, all.Width, r.Top));
            context.DrawRectangle(dim, null, new Rect(0, r.Bottom, all.Width, Math.Max(0, all.Height - r.Bottom)));
            context.DrawRectangle(dim, null, new Rect(0, r.Top, r.Left, r.Height));
            context.DrawRectangle(dim, null, new Rect(r.Right, r.Top, Math.Max(0, all.Width - r.Right), r.Height));
            var edge = new Pen(new SolidColorBrush(Color.Parse("#60A5FA")), 1.5);
            context.DrawRectangle(null, edge, r);
            foreach (var (_, p) in SelectionGeometry.Handles(selection))
                context.DrawRectangle(Brushes.White, edge, new Rect(p.X / sx - 3.5, p.Y / sy - 3.5, 7, 7), 1, 1);
            if (_owner.Session.Tool == AnnotationTool.Eraser && _cursor is { } cursor && selection.Contains(cursor))
            {
                var size = _owner.Session.Preferences.EraserWidth;
                context.DrawEllipse(null, new Pen(Brushes.White, 1), new Rect((cursor.X - size / 2d) / sx, (cursor.Y - size / 2d) / sy, size / sx, size / sy));
            }
        }
        public void DisposeImages() { _frozen?.Dispose(); _edited?.Dispose(); _frozen = null; _edited = null; }
    }
}
