using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ScreenshotBox.App.Native;

public sealed record CaptureResult(BitmapSource Image, string Action);
public sealed record CaptureToolPreferences(string ColorHex, double PenWidth, double EraserWidth, int MosaicSize);

public static class CaptureService
{
    private static bool _active;
    private static CaptureToolPreferences _preferences = new("#E63737", 2, 24, 12);
    public static CaptureToolPreferences GetToolPreferences() => _preferences;
    public static void ConfigureTools(string colorHex, double penWidth, double eraserWidth, int mosaicSize)
    {
        if (!TryColor(colorHex, out var color)) color = Color.FromRgb(230, 55, 55);
        _preferences = new(Hex(color), double.IsFinite(penWidth) ? Math.Clamp(penWidth, 1, 32) : 2,
            double.IsFinite(eraserWidth) ? Math.Clamp(eraserWidth, 4, 80) : 24, Math.Clamp(mosaicSize, 6, 32));
    }
    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    private static bool TryColor(string? text, out Color color)
    {
        string value = (text ?? "").Trim().TrimStart('#');color = default;
        if (value.Length != 6 || !uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint rgb)) return false;
        color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);return true;
    }
    private enum EditMode { Select, Pen, Arrow, Rectangle, Eraser, Mosaic }
    private abstract record Annotation;
    private sealed record Stroke(List<PixelPoint> Points, Color Color, double Width) : Annotation;
    private sealed record Mosaic(PixelRect Bounds, BitmapSource Image) : Annotation;
    private sealed record Erase(List<PixelPoint> Points, double Width) : Annotation;
    private sealed record Shape(EditMode Mode, PixelPoint Start, PixelPoint End, Color Color, double Width) : Annotation;
    private sealed record Clear : Annotation;

    /// <summary>
    /// Caller hides its own windows first. The frozen real desktop is captured before any overlay exists.
    /// A null result means cancellation; the returned image contains only the selected physical pixels.
    /// </summary>
    public static async Task<CaptureResult?> CaptureAsync()
    {
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException(L.T("请从桌面应用调用截图。", "Capture must be started from the desktop app."));
        dispatcher.VerifyAccess();
        if (_active) return null;
        _active = true;
        try
        {
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            NativeMethods.DwmFlush();
            var monitors = System.Windows.Forms.Screen.AllScreens.Select(screen =>
                new PixelRect(screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height)).ToArray();
            if (monitors.Length == 0) throw new InvalidOperationException(L.T("没有发现可截图的显示器。", "No display is available for capture."));
            int left = monitors.Min(m => m.Left), top = monitors.Min(m => m.Top);
            var desktop = new PixelRect(left, top, monitors.Max(m => m.Right) - left, monitors.Max(m => m.Bottom) - top);
            BitmapSource bitmap = NativeMethods.CaptureDesktop(desktop);
            var session = new CaptureSession(bitmap, desktop, monitors);
            return await session.RunAsync();
        }
        finally { _active = false; }
    }

    private sealed class CaptureSession
    {
        public readonly BitmapSource Screenshot;
        public readonly PixelRect Desktop;
        public readonly PixelRect[] Monitors;
        public PixelRect Selection { get; private set; }
        public bool Dragging { get; private set; }
        public EditMode Mode { get; private set; }
        public Color PenColor { get; private set; } = Color.FromRgb(230, 55, 55);
        public double PenWidth { get; private set; } = 2;
        public double EraserWidth { get; private set; } = 24;
        public int MosaicBlock { get; private set; } = 12;
        public bool CanUndo => !Dragging && _annotations.Count > 0;
        public bool CanRedo => !Dragging && _redo.Count > 0;
        private readonly List<Annotation> _annotations = [];
        private readonly Stack<Annotation> _redo = [];
        private Stroke? _pendingStroke;
        private Erase? _pendingErase;
        private Shape? _pendingShape;
        private PixelRect _pendingMosaic;
        private readonly List<CaptureOverlay> _windows = [];
        private readonly TaskCompletionSource<CaptureResult?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private PixelPoint _start;
        private PixelRect _original;
        private SelectionHandle _mode;
        private bool _finished;
        private CaptureOverlay? _toolbarOwner;
        private bool _displaySubscribed;

        public CaptureSession(BitmapSource screenshot, PixelRect desktop, PixelRect[] monitors)
        {
            Screenshot = screenshot; Desktop = desktop; Monitors = monitors;
            TryColor(_preferences.ColorHex, out var color);PenColor = color;PenWidth = _preferences.PenWidth;EraserWidth = _preferences.EraserWidth;MosaicBlock = _preferences.MosaicSize;
        }

        public Task<CaptureResult?> RunAsync()
        {
            try
            {
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
                _displaySubscribed = true;
                foreach (PixelRect monitor in Monitors)
                {
                    var overlay = new CaptureOverlay(this, monitor);
                    _windows.Add(overlay);
                    overlay.Show();
                }
                PixelPoint mouse = NativeMethods.CursorPosition();
                (_windows.FirstOrDefault(w => w.Monitor.Contains(mouse)) ?? _windows[0]).Activate();
                Refresh();
            }
            catch (Exception exception)
            {
                Finish(null, exception);
            }
            return _completion.Task;
        }
        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.HasShutdownStarted) dispatcher.BeginInvoke(Cancel);
        }

        public void Begin(PixelPoint point)
        {
            if (_finished) return;
            if (Mode != EditMode.Select && !Selection.Contains(point)) return;
            _start = SelectionGeometry.Clamp(point, Desktop);
            _original = Selection;
            if (Mode == EditMode.Pen) _pendingStroke = new Stroke([point], PenColor, PenWidth);
            if (Mode == EditMode.Eraser) _pendingErase = new Erase([point], EraserWidth);
            if (Mode is EditMode.Arrow or EditMode.Rectangle) _pendingShape = new Shape(Mode, point, point, PenColor, PenWidth);
            if (Mode == EditMode.Mosaic) _pendingMosaic = default;
            _mode = SelectionGeometry.HitTest(Selection, point);
            if (Mode == EditMode.Select && _mode == SelectionHandle.None)
            {
                Selection = default;
                _annotations.Clear();
                _redo.Clear();
            }
            Dragging = true;
            Refresh();
        }

        public void Update(PixelPoint point)
        {
            if (!Dragging || _finished) return;
            if (Mode is EditMode.Pen or EditMode.Eraser)
            {
                point = SelectionGeometry.Clamp(point, Selection);
                if (_pendingStroke is not null && _pendingStroke.Points[^1] != point)
                    _pendingStroke.Points.Add(point);
                if (_pendingErase is not null && _pendingErase.Points[^1] != point)
                    _pendingErase.Points.Add(point);
                Refresh();
                return;
            }
            if (Mode is EditMode.Arrow or EditMode.Rectangle)
            {
                if (_pendingShape is not null) _pendingShape = _pendingShape with { End = SelectionGeometry.Clamp(point, Selection) };
                Refresh();return;
            }
            if (Mode == EditMode.Mosaic)
            {
                _pendingMosaic = PixelRect.FromPoints(_start, SelectionGeometry.Clamp(point, Selection)).Intersect(Selection);
                Refresh();
                return;
            }
            Selection = _mode switch
            {
                SelectionHandle.None => PixelRect.FromPoints(_start, SelectionGeometry.Clamp(point, Desktop)),
                SelectionHandle.Move => SelectionGeometry.Move(_original, _start, point, Desktop),
                _ => SelectionGeometry.Resize(_original, _mode, point, Desktop)
            };
            Refresh();
        }

        public void End(PixelPoint point)
        {
            if (!Dragging) return;
            Update(point);
            Dragging = false;
            if (_pendingStroke is not null)
            {
                Commit(_pendingStroke);
                _pendingStroke = null;
            }
            if (_pendingErase is not null) { Commit(_pendingErase); _pendingErase = null; }
            if (_pendingShape is not null) { if (_pendingShape.Start != _pendingShape.End) Commit(_pendingShape); _pendingShape = null; }
            if (!_pendingMosaic.IsEmpty)
            {
                Commit(new Mosaic(_pendingMosaic, Pixelate(_pendingMosaic)));
                _pendingMosaic = default;
            }
            if (Selection.Width < 2 || Selection.Height < 2) Selection = default;
            _toolbarOwner = _windows.FirstOrDefault(w => w.Monitor.Contains(point))
                ?? _windows.FirstOrDefault(w => !Selection.Intersect(w.Monitor).IsEmpty) ?? _windows.FirstOrDefault();
            Refresh();
        }

        public void Reset()
        {
            Dragging = false;
            Selection = default;
            _annotations.Clear();
            _redo.Clear();
            _pendingStroke = null;
            _pendingErase = null;
            _pendingShape = null;
            _pendingMosaic = default;
            Mode = EditMode.Select;
            Refresh();
        }

        public void SetMode(EditMode mode) { if (!Dragging) { Mode = mode; Refresh(); } }
        private void SaveToolPreferences() => _preferences = new(Hex(PenColor), PenWidth, EraserWidth, MosaicBlock);
        public void SetPenColor(Color color) { if (!Dragging) { PenColor = Color.FromRgb(color.R, color.G, color.B);SaveToolPreferences();Refresh(); } }
        public void SetPenWidth(double width) { if (!Dragging && double.IsFinite(width)) { PenWidth = Math.Clamp(width, 1, 32);SaveToolPreferences();Refresh(); } }
        public void SetEraserWidth(double width) { if (!Dragging && double.IsFinite(width)) { EraserWidth = Math.Clamp(width, 4, 80);SaveToolPreferences();Refresh(); } }
        public void SetMosaicBlock(double width) { if (!Dragging && double.IsFinite(width)) { MosaicBlock = (int)Math.Clamp(Math.Round(width), 6, 32);SaveToolPreferences();Refresh(); } }
        private void Commit(Annotation annotation) { _annotations.Add(annotation); _redo.Clear(); }
        public void Undo()
        {
            if (CanUndo) { _redo.Push(_annotations[^1]); _annotations.RemoveAt(_annotations.Count - 1); }
            Refresh();
        }
        public void Redo() { if (CanRedo) _annotations.Add(_redo.Pop()); Refresh(); }
        public void ClearAnnotations() { if (CanUndo) Commit(new Clear()); Refresh(); }

        public void DrawAnnotations(DrawingContext dc)
        {
            foreach (Annotation annotation in _annotations) Draw(annotation);
            if (_pendingStroke is not null) Draw(_pendingStroke);
            if (_pendingErase is not null) Draw(_pendingErase);
            if (_pendingShape is not null) Draw(_pendingShape);
            if (!_pendingMosaic.IsEmpty)
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(80, 240, 240, 240)),
                    new Pen(Brushes.White, 1), ToRect(_pendingMosaic));
            void Draw(Annotation annotation)
            {
                if (annotation is Mosaic mosaic) dc.DrawImage(mosaic.Image, ToRect(mosaic.Bounds));
                else if (annotation is Clear) dc.DrawImage(Screenshot, ToRect(Desktop));
                else if (annotation is Erase erase && erase.Points.Count > 0)
                {
                    Geometry clip = erase.Points.Count == 1
                        ? new EllipseGeometry(new Point(erase.Points[0].X, erase.Points[0].Y), erase.Width / 2, erase.Width / 2)
                        : Path(erase.Points).GetWidenedPathGeometry(RoundPen(Brushes.Black, erase.Width));
                    dc.PushClip(clip);dc.DrawImage(Screenshot, ToRect(Desktop));dc.Pop();
                }
                else if (annotation is Shape shape)
                {
                    var brush = new SolidColorBrush(shape.Color);var pen = RoundPen(brush, shape.Width);
                    Point start = new(shape.Start.X, shape.Start.Y), end = new(shape.End.X, shape.End.Y);
                    if (shape.Mode == EditMode.Rectangle) dc.DrawRectangle(null, pen, new Rect(start, end));
                    else
                    {
                        Vector direction = end - start;
                        if (direction.Length < 0.01) return;
                        double length = Math.Min(direction.Length, Math.Max(10, shape.Width * 3));direction.Normalize();
                        Vector normal = new(-direction.Y, direction.X);
                        dc.DrawLine(pen, start, end);
                        var head = new StreamGeometry();using(var ctx = head.Open())
                        { ctx.BeginFigure(end, true, true);ctx.LineTo(end - direction * length + normal * length * .45, true, false);ctx.LineTo(end - direction * length - normal * length * .45, true, false); }
                        head.Freeze();dc.DrawGeometry(brush, null, head);
                    }
                }
                else if (annotation is Stroke stroke && stroke.Points.Count > 0)
                {
                    var brush = new SolidColorBrush(stroke.Color);
                    var pen = RoundPen(brush, stroke.Width);
                    if (stroke.Points.Count == 1)
                        dc.DrawEllipse(brush, null, new Point(stroke.Points[0].X, stroke.Points[0].Y), stroke.Width / 2, stroke.Width / 2);
                    else
                    {
                        dc.DrawGeometry(null, pen, Path(stroke.Points));
                    }
                }
            }
        }
        private static Pen RoundPen(Brush brush, double width) => new(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        private static StreamGeometry Path(List<PixelPoint> points)
        {
            var geometry = new StreamGeometry();using(var context = geometry.Open())
            { context.BeginFigure(new Point(points[0].X, points[0].Y), false, false);context.PolyLineTo(points.Skip(1).Select(p => new Point(p.X, p.Y)).ToArray(), true, false); }
            geometry.Freeze();return geometry;
        }

        private BitmapSource Pixelate(PixelRect region)
        {
            var crop = new CroppedBitmap(Screenshot, new Int32Rect(region.Left - Desktop.Left,
                region.Top - Desktop.Top, region.Width, region.Height));
            int stride = checked(region.Width * 4);
            var pixels = new byte[checked(stride * region.Height)];
            crop.CopyPixels(pixels, stride, 0);
            int block = MosaicBlock;
            for (int y = 0; y < region.Height; y += block)
                for (int x = 0; x < region.Width; x += block)
                {
                    int right = Math.Min(region.Width, x + block), bottom = Math.Min(region.Height, y + block);
                    long blue = 0, green = 0, red = 0;
                    int count = (right - x) * (bottom - y);
                    for (int by = y; by < bottom; by++)
                        for (int bx = x; bx < right; bx++)
                        {
                            int i = by * stride + bx * 4;
                            blue += pixels[i]; green += pixels[i + 1]; red += pixels[i + 2];
                        }
                    for (int by = y; by < bottom; by++)
                        for (int bx = x; bx < right; bx++)
                        {
                            int i = by * stride + bx * 4;
                            pixels[i] = (byte)(blue / count); pixels[i + 1] = (byte)(green / count);
                            pixels[i + 2] = (byte)(red / count);
                        }
                }
            var image = BitmapSource.Create(region.Width, region.Height, 96, 96, PixelFormats.Bgr32, null, pixels, stride);
            image.Freeze();
            return image;
        }

        private static Rect ToRect(PixelRect r) => new(r.Left, r.Top, r.Width, r.Height);

        private void Refresh()
        {
            foreach (CaptureOverlay window in _windows)
                window.Refresh(window == _toolbarOwner && !Dragging && !Selection.IsEmpty);
        }

        public void Accept(string action)
        {
            if (_finished || Dragging || Selection.IsEmpty) return;
            var crop = new CroppedBitmap(Screenshot, new Int32Rect(Selection.Left - Desktop.Left,
                Selection.Top - Desktop.Top, Selection.Width, Selection.Height));
            crop.Freeze();
            if (_annotations.Count == 0) { Finish(new CaptureResult(crop, action)); return; }
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(crop, new Rect(0, 0, Selection.Width, Selection.Height));
                dc.PushTransform(new TranslateTransform(-Selection.Left, -Selection.Top));
                DrawAnnotations(dc);
                dc.Pop();
            }
            var result = new RenderTargetBitmap(Selection.Width, Selection.Height, 96, 96, PixelFormats.Pbgra32);
            result.Render(visual);
            result.Freeze();
            Finish(new CaptureResult(result, action));
        }

        public void Cancel() => Finish(null);

        private void Finish(CaptureResult? result, Exception? exception = null)
        {
            if (_finished) return;
            _finished = true;
            if (_displaySubscribed) { Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged; _displaySubscribed = false; }
            Mouse.Capture(null);
            foreach (CaptureOverlay window in _windows) if (window.IsVisible) window.Close();
            _windows.Clear();
            if (exception is null) _completion.TrySetResult(result);
            else _completion.TrySetException(exception);
        }
    }

    private sealed class CaptureOverlay : Window
    {
        private readonly CaptureSession _session;
        private readonly CaptureSurface _surface;
        private readonly Canvas _floating;
        private readonly Border _toolbar;
        private readonly TextBlock _help;
        private readonly Dictionary<EditMode, Button> _modeButtons = [];
        private readonly Dictionary<EditMode, WrapPanel> _toolOptions = [];
        private readonly Dictionary<Color, Button> _colorButtons = [];
        private Slider? _penSlider, _eraserSlider, _mosaicSlider;
        private TextBlock? _penValue, _eraserValue, _mosaicValue;
        private Button? _undoButton, _redoButton, _clearButton;
        private Button? _toolMenuButton;
        private bool _updatingOptions;
        private readonly Popup _palettePopup = new() { AllowsTransparency = true, StaysOpen = false, Placement = PlacementMode.Bottom };
        private Border? _customColorPreview;
        private readonly List<Button> _toolbarButtons = [];
        public PixelRect Monitor { get; }

        public CaptureOverlay(CaptureSession session, PixelRect monitor)
        {
            _session = session;
            Monitor = monitor;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Background = Brushes.Black;
            Title = L.T("截图选区", "Capture region");
            // Win32 sets the actual physical bounds after the WPF handle has been created.
            Left = 0; Top = 0; Width = 100; Height = 100;
            var root = new Grid { ClipToBounds = true };
            _surface = new CaptureSurface(session, monitor) { Focusable = true };
            root.Children.Add(_surface);
            _floating = new Canvas { IsHitTestVisible = true };
            root.Children.Add(_floating);
            _toolbar = BuildToolbar();
            _floating.Children.Add(_toolbar);
            _help = new TextBlock
            {
                Text = L.T("拖动选择截图范围  ·  Enter 保存并复制  ·  Esc 取消", "Drag to select a region · Enter to save and copy · Esc to cancel"),
                Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromArgb(210, 24, 28, 35)),
                Padding = new Thickness(12, 8, 12, 8), FontSize = 13, IsHitTestVisible = false,
                TextWrapping = TextWrapping.Wrap
            };
            _floating.Children.Add(_help);
            Content = root;
            SourceInitialized += (_, _) => PositionNative();
            Loaded += (_, _) => { PositionNative(); _surface.Focus(); Refresh(false); };
            SizeChanged += (_, _) => Refresh(_toolbar.Visibility == Visibility.Visible);
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) { e.Handled = true; if (_palettePopup.IsOpen) _palettePopup.IsOpen = false; else session.Cancel(); }
                else if (e.Key == Key.Enter) { e.Handled = true; session.Accept("Collect"); }
                else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; session.Undo(); }
                else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; session.Redo(); }
                else if (e.OriginalSource is not TextBox && Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.B or Key.E or Key.M or Key.V)
                {
                    session.SetMode(e.Key switch { Key.B => EditMode.Pen, Key.E => EditMode.Eraser, Key.M => EditMode.Mosaic, _ => EditMode.Select });e.Handled = true;
                }
            };
            Closed += (_, _) => { _palettePopup.IsOpen = false;if (_toolMenuButton?.ContextMenu is ContextMenu menu) menu.IsOpen = false;session.Cancel(); };
            _surface.MouseLeftButtonDown += (_, e) =>
            {
                Activate();
                _surface.Focus();
                session.Begin(NativeMethods.CursorPosition());
                _surface.CaptureMouse();
                e.Handled = true;
            };
            _surface.MouseMove += (_, _) =>
            {
                PixelPoint point = NativeMethods.CursorPosition();
                _surface.PointerPosition = point;
                _surface.InvalidateVisual();
                if (session.Dragging) session.Update(point);
                else _surface.Cursor = session.Mode == EditMode.Select
                    ? CursorFor(SelectionGeometry.HitTest(session.Selection, point)) : session.Mode is EditMode.Pen or EditMode.Eraser ? Cursors.None : Cursors.Cross;
            };
            _surface.MouseLeave += (_, _) => { _surface.PointerPosition = null; _surface.InvalidateVisual(); };
            _surface.MouseLeftButtonUp += (_, e) =>
            {
                session.End(NativeMethods.CursorPosition());
                _surface.ReleaseMouseCapture();
                e.Handled = true;
            };
            _surface.LostMouseCapture += (_, _) =>
            {
                if (session.Dragging) session.End(NativeMethods.CursorPosition());
            };
            _surface.MouseRightButtonDown += (_, e) => { session.Cancel(); e.Handled = true; };
        }

        private void PositionNative()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowPos(handle, new IntPtr(-1), Monitor.Left, Monitor.Top,
                Monitor.Width, Monitor.Height, 0x0010 | 0x0040);
        }

        private Border BuildToolbar()
        {
            var buttonStyle = RoundedButtonStyle();
            var body = new StackPanel();
            var row = new WrapPanel(); body.Children.Add(row);
            AddMode(L.T("选区", "Select"), EditMode.Select);
            AddMode(L.T("画笔", "Pen"), EditMode.Pen);
            AddMode(L.T("箭头", "Arrow"), EditMode.Arrow);
            AddMode(L.T("矩形", "Rectangle"), EditMode.Rectangle);
            AddMode(L.T("橡皮擦", "Eraser"), EditMode.Eraser);
            AddMode(L.T("马赛克", "Mosaic"), EditMode.Mosaic);
            _toolMenuButton = AddButton(row, L.T("工具⌄", "Tools⌄"), () =>
            {
                var menu = new ContextMenu { Background = new SolidColorBrush(Color.FromRgb(248, 249, 251)), Foreground = Brushes.Black, BorderBrush = Brushes.LightGray, PlacementTarget = _toolMenuButton, Placement = PlacementMode.Bottom };
                foreach (var pair in _modeButtons)
                {
                    var item = new MenuItem { Header = pair.Value.ToolTip, IsCheckable = true, IsChecked = pair.Key == _session.Mode, Style = new Style(typeof(MenuItem)), Foreground = Brushes.Black, FontSize = 13 };
                    item.Click += (_, _) => _session.SetMode(pair.Key);menu.Items.Add(item);
                }
                _toolMenuButton!.ContextMenu = menu;menu.IsOpen = true;
            });
            _toolMenuButton.Visibility = Visibility.Collapsed;_toolMenuButton.ToolTip = L.T("选择标注工具", "Choose an annotation tool");
            _undoButton = AddButton(row, L.T("撤销", "Undo"), _session.Undo);
            _redoButton = AddButton(row, L.T("重做", "Redo"), _session.Redo);
            _clearButton = AddButton(row, L.T("清空标注", "Clear"), _session.ClearAnnotations);
            var penOptions = Options(EditMode.Pen);
            foreach (var (name, color) in new[] { (L.T("红", "Red"), Color.FromRgb(230, 55, 55)), (L.T("蓝", "Blue"), Color.FromRgb(23, 105, 194)),
                (L.T("绿", "Green"), Color.FromRgb(24, 167, 91)), (L.T("黄", "Yellow"), Color.FromRgb(244, 196, 48)), (L.T("白", "White"), Colors.White), (L.T("黑", "Black"), Colors.Black) })
            {
                var button = new Button { Width = 20, Height = 24, Padding = new Thickness(0), Margin = new Thickness(1, 2, 1, 2),
                    Style = buttonStyle, Background = new SolidColorBrush(color), BorderBrush = Brushes.Gray,
                    Foreground = color == Colors.White || color == Color.FromRgb(244, 196, 48) ? Brushes.Black : Brushes.White, ToolTip = L.F("{0}色", "{0}",name) };
                button.Click += (_, _) => _session.SetPenColor(color);_colorButtons.Add(color, button);penOptions.Children.Add(button);
            }
            var more = MakeButton(L.T("更多色", "More"));more.ToolTip = L.T("自定义颜色 · RGB / Hex", "Custom color · RGB / Hex");
            _customColorPreview = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 4, 0), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
            var moreContent = new StackPanel { Orientation = Orientation.Horizontal };moreContent.Children.Add(_customColorPreview);moreContent.Children.Add(new TextBlock { Text = L.T("更多色", "More"), FontSize = 11, Foreground = Brushes.Black });more.Content = moreContent;
            more.Padding = new Thickness(4);more.MinHeight = 24;
            more.Click += (_, _) => { _palettePopup.PlacementTarget = more;_palettePopup.Child = CreatePalette();_palettePopup.IsOpen = true; };
            penOptions.Children.Add(more);
            (_penSlider, _penValue) = SizeOption(penOptions, L.T("笔宽", "Width"), 1, 32, _session.SetPenWidth);
            var eraserOptions = Options(EditMode.Eraser);
            (_eraserSlider, _eraserValue) = SizeOption(eraserOptions, L.T("大小", "Size"), 4, 80, _session.SetEraserWidth);
            eraserOptions.Children.Add(new TextBlock { Text = L.T("移除标注 · 可撤销", "Remove annotations · Undo available"), FontSize = 11, Foreground = Brushes.Black, Margin = new Thickness(6, 6, 2, 4) });
            var mosaicOptions = Options(EditMode.Mosaic);
            (_mosaicSlider, _mosaicValue) = SizeOption(mosaicOptions, L.T("块大小", "Block"), 6, 32, _session.SetMosaicBlock);
            var actions = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };body.Children.Add(actions);
            var save = AddButton(actions, L.T("保存并复制", "Save and copy"), () => _session.Accept("Collect"));
            save.Background = new SolidColorBrush(Color.FromRgb(23, 105, 194));save.Foreground = Brushes.White;
            AddButton(actions, L.T("仅复制", "Copy only"), () => _session.Accept("Copy"));
            AddButton(actions, L.T("另存 PNG", "Save PNG"), () => _session.Accept("Save"));
            AddButton(actions, L.T("重新选择", "Reselect"), _session.Reset);
            AddButton(actions, L.T("取消", "Cancel"), _session.Cancel);
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 251)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(170, 177, 189)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5),
                Padding = new Thickness(4), Child = body, Visibility = Visibility.Collapsed
            };
            Button AddButton(Panel panel, string title, Action action)
            {
                var button = MakeButton(title);
                button.Click += (_, _) => action();
                panel.Children.Add(button);return button;
            }
            void AddMode(string title, EditMode mode)
            {
                var button = MakeButton(title);
                button.ToolTip = title;
                button.Click += (_, _) => _session.SetMode(mode);
                _modeButtons.Add(mode, button);
                row.Children.Add(button);
            }
            WrapPanel Options(EditMode mode)
            {
                var options = new WrapPanel { Margin = new Thickness(0, 2, 0, 0), Visibility = Visibility.Collapsed };
                body.Children.Add(options);_toolOptions.Add(mode, options);return options;
            }
            (Slider, TextBlock) SizeOption(Panel panel, string title, double minimum, double maximum, Action<double> changed)
            {
                var group = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(3, 0, 0, 0) };
                group.Children.Add(new TextBlock { Text = title, FontSize = 11, Foreground = Brushes.Black, MinWidth = 34, Margin = new Thickness(0,0,4,0), VerticalAlignment = VerticalAlignment.Center });
                var slider = new Slider { Minimum = minimum, Maximum = maximum, Width = 76, Margin = new Thickness(2, 4, 2, 4), VerticalAlignment = VerticalAlignment.Center,
                    SmallChange = 1, LargeChange = 4, IsSnapToTickEnabled = false, IsMoveToPointEnabled = true, Style = RoundedSliderStyle(), Focusable = false };
                var value = new TextBlock { FontSize = 11, Foreground = Brushes.Black, Width = 40, VerticalAlignment = VerticalAlignment.Center };
                slider.ValueChanged += (_, _) => { if (!_updatingOptions) changed(slider.Value); };
                group.Children.Add(slider);group.Children.Add(value);panel.Children.Add(group);return (slider, value);
            }
            Button MakeButton(string title)
            {
                var button = new Button {
                Style = buttonStyle, FontSize = 13,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
                Background = new SolidColorBrush(Color.FromRgb(245, 247, 250)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(174, 182, 194)),
                Margin = new Thickness(2), Padding = new Thickness(8, 7, 8, 7), MinHeight = 32
                };
                var text = new TextBlock { Text = title, Style = new Style(typeof(TextBlock)) };
                text.SetBinding(TextBlock.FontSizeProperty, new System.Windows.Data.Binding("FontSize") { Source = button });
                text.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { Source = button });
                button.Content = text;
                _toolbarButtons.Add(button);return button;
            }
        }

        private FrameworkElement CreatePalette()
        {
            var body = new StackPanel { Width = 238 };
            body.Children.Add(new TextBlock { Text = L.T("自定义颜色", "Custom color"), FontSize = 13, Foreground = Brushes.Black, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
            var preview = new Border { Height = 36, CornerRadius = new CornerRadius(4), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 8) };body.Children.Add(preview);
            var hex = new TextBox { FontSize = 13, Foreground = Brushes.Black, Background = Brushes.White, BorderBrush = Brushes.LightGray, Padding = new Thickness(6), MaxLength = 7, Margin = new Thickness(0, 8, 0, 4) };
            var channels = new List<Slider>();bool syncing = true;
            Color current = _session.PenColor;
            foreach (var (title, initial) in new[] { ("R", current.R), ("G", current.G), ("B", current.B) })
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                line.Children.Add(new TextBlock { Text = title, Foreground = Brushes.Black, FontSize = 12, Width = 18, VerticalAlignment = VerticalAlignment.Center });
                var slider = new Slider { Minimum = 0, Maximum = 255, Value = initial, Width = 174, SmallChange = 1, LargeChange = 16, IsMoveToPointEnabled = true, Style = RoundedSliderStyle(), VerticalAlignment = VerticalAlignment.Center };
                var value = new TextBlock { Text = initial.ToString(), Width = 40, TextAlignment = TextAlignment.Right, Foreground = Brushes.Black, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
                slider.ValueChanged += (_, _) => { value.Text = Math.Round(slider.Value).ToString("0");if (!syncing) { current = Color.FromRgb((byte)Math.Round(channels[0].Value), (byte)Math.Round(channels[1].Value), (byte)Math.Round(channels[2].Value));hex.Text = Hex(current);preview.Background = new SolidColorBrush(current); } };
                channels.Add(slider);line.Children.Add(slider);line.Children.Add(value);body.Children.Add(line);
            }
            preview.Background = new SolidColorBrush(current);hex.Text = Hex(current);syncing = false;
            body.Children.Add(hex);
            var error = new TextBlock { FontSize = 11, Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap };body.Children.Add(error);
            hex.TextChanged += (_, _) =>
            {
                if (syncing || !TryColor(hex.Text, out var color)) return;
                current = color;preview.Background = new SolidColorBrush(color);syncing = true;
                try { channels[0].Value = color.R;channels[1].Value = color.G;channels[2].Value = color.B; }finally { syncing = false; }
                error.Text = "";
            };
            var apply = new Button { Content = L.T("应用颜色", "Apply color"), Style = RoundedButtonStyle(), Background = new SolidColorBrush(Color.FromRgb(23, 105, 194)), Foreground = Brushes.White, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 8, 0, 0) };
            void Apply()
            {
                if (!TryColor(hex.Text, out var color)) { error.Text = L.T("请输入 6 位色值，如 #2D7FE5", "Enter a 6-digit color, such as #2D7FE5");return; }
                _session.SetPenColor(color);_palettePopup.IsOpen = false;
            }
            apply.Click += (_, _) => Apply();body.Children.Add(apply);
            body.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { Apply();e.Handled = true; }else if (e.Key == Key.Escape) { _palettePopup.IsOpen = false;e.Handled = true; } };
            return new Border { Child = body, Padding = new Thickness(12), Background = new SolidColorBrush(Color.FromRgb(248, 249, 251)), BorderBrush = new SolidColorBrush(Color.FromRgb(170, 177, 189)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6) };
        }

        private static Style RoundedButtonStyle()
        {
            var chrome = new FrameworkElementFactory(typeof(Border), "Chrome");
            chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            chrome.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            chrome.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            content.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ContentControl.ContentTemplateProperty));
            content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            content.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            chrome.AppendChild(content);var template = new ControlTemplate(typeof(Button)) { VisualTree = chrome };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };hover.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(83, 142, 200)), "Chrome"));template.Triggers.Add(hover);
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };focus.Setters.Add(new Setter(Border.BorderBrushProperty, Brushes.DodgerBlue, "Chrome"));focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "Chrome"));template.Triggers.Add(focus);
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .42));template.Triggers.Add(disabled);
            var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };pressed.Setters.Add(new Setter(UIElement.OpacityProperty, .7));template.Triggers.Add(pressed);
            var style = new Style(typeof(Button));style.Setters.Add(new Setter(Control.TemplateProperty, template));style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));return style;
        }
        private static Style RoundedSliderStyle() => (Style)System.Windows.Markup.XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Slider">
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Slider">
                <Grid MinHeight="18">
                  <Border Height="4" CornerRadius="2" Background="#D6DEE8" VerticalAlignment="Center"/>
                  <Border Height="4" CornerRadius="2" Background="#1769C2" VerticalAlignment="Center" HorizontalAlignment="Left" Width="{Binding DecreaseRepeatButton.ActualWidth, ElementName=PART_Track}"/>
                  <Track x:Name="PART_Track" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}" IsDirectionReversed="{TemplateBinding IsDirectionReversed}">
                    <Track.DecreaseRepeatButton><RepeatButton Command="{x:Static Slider.DecreaseLarge}" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Height="4" CornerRadius="2" Background="#1769C2"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
                    <Track.Thumb><Thumb Width="14" Height="14" Focusable="False"><Thumb.Template><ControlTemplate TargetType="Thumb"><Ellipse Fill="#1769C2" Stroke="White" StrokeThickness="1.5"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                    <Track.IncreaseRepeatButton><RepeatButton Command="{x:Static Slider.IncreaseLarge}" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Height="4" CornerRadius="2" Background="#D6DEE8"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
                  </Track>
                </Grid>
              </ControlTemplate></Setter.Value></Setter>
            </Style>
            """);

        public void Refresh(bool showToolbar)
        {
            _surface.InvalidateVisual();
            _toolbar.Visibility = showToolbar ? Visibility.Visible : Visibility.Collapsed;
            foreach (var button in _toolbarButtons)
            {
                bool compact = ActualWidth < 420;
                button.Padding = new Thickness(compact ? 3 : 8, compact ? 3 : 7, compact ? 3 : 8, compact ? 3 : 7);
                button.MinHeight = compact ? 24 : 32;button.FontSize = compact ? 12 : 13;
            }
            if (_undoButton is not null) _undoButton.IsEnabled = _session.CanUndo;
            if (_redoButton is not null) _redoButton.IsEnabled = _session.CanRedo;
            if (_clearButton is not null) _clearButton.IsEnabled = _session.CanUndo;
            foreach (var pair in _toolOptions)
                pair.Value.Visibility = pair.Key == _session.Mode || (pair.Key == EditMode.Pen && _session.Mode is EditMode.Arrow or EditMode.Rectangle) ? Visibility.Visible : Visibility.Collapsed;
            _updatingOptions = true;
            try
            {
                if (_penSlider is not null) _penSlider.Value = _session.PenWidth;
                if (_eraserSlider is not null) _eraserSlider.Value = _session.EraserWidth;
                if (_mosaicSlider is not null) _mosaicSlider.Value = _session.MosaicBlock;
                if (_penValue is not null) _penValue.Text = $"{_session.PenWidth:0.#} px";
                if (_eraserValue is not null) _eraserValue.Text = $"{_session.EraserWidth:0.#} px";
                if (_mosaicValue is not null) _mosaicValue.Text = $"{_session.MosaicBlock} px";
            }
            finally { _updatingOptions = false; }
            foreach (var pair in _colorButtons) { bool selected = pair.Key.Equals(_session.PenColor);pair.Value.Content = selected ? "✓" : "";pair.Value.BorderThickness = new Thickness(selected ? 2 : 1);pair.Value.BorderBrush = selected ? Brushes.DodgerBlue : Brushes.Gray; }
            if (_customColorPreview is not null) _customColorPreview.Background = new SolidColorBrush(_session.PenColor);
            foreach (var pair in _modeButtons)
            {
                pair.Value.Visibility = ActualWidth < 420 && pair.Key != _session.Mode ? Visibility.Collapsed : Visibility.Visible;
                pair.Value.Background = pair.Key == _session.Mode
                    ? new SolidColorBrush(Color.FromRgb(204, 228, 255)) : new SolidColorBrush(Color.FromRgb(245, 247, 250));
                pair.Value.Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32));
            }
            if (_toolMenuButton is not null) _toolMenuButton.Visibility = ActualWidth < 420 ? Visibility.Visible : Visibility.Collapsed;
            _help.Text = _session.Mode switch
            {
                EditMode.Pen => L.T("画笔 B · 拖动画线 · Ctrl+Z / Ctrl+Y 撤销重做 · Enter 保存并复制 · Esc 取消", "Pen B · Drag to draw · Ctrl+Z / Ctrl+Y to undo / redo · Enter to save and copy · Esc to cancel"),
                EditMode.Arrow => L.T("箭头 · 拖动起点到终点 · Enter 保存并复制 · Esc 取消", "Arrow · Drag from start to end · Enter to save and copy · Esc to cancel"),
                EditMode.Rectangle => L.T("矩形 · 拖动框选 · Enter 保存并复制 · Esc 取消", "Rectangle · Drag to draw · Enter to save and copy · Esc to cancel"),
                EditMode.Eraser => L.T("橡皮擦 E · 移除标注，恢复原图 · Ctrl+Z 可撤销 · Enter 保存并复制 · Esc 取消", "Eraser E · Remove annotations and restore the image · Ctrl+Z to undo · Enter to save and copy · Esc to cancel"),
                EditMode.Mosaic => L.T("马赛克 M · 拖动矩形 · Ctrl+Z / Ctrl+Y 撤销重做 · Enter 保存并复制 · Esc 取消", "Mosaic M · Drag a rectangle · Ctrl+Z / Ctrl+Y to undo / redo · Enter to save and copy · Esc to cancel"),
                _ => L.T("选区 V · 拖动选择，可移动或调整大小 · Enter 保存并复制 · Esc 取消", "Select V · Drag to select, move or resize · Enter to save and copy · Esc to cancel")
            };
            Canvas.SetLeft(_help, 20); Canvas.SetTop(_help, 20);
            _help.MaxWidth = Math.Max(40, ActualWidth - 40);
            _help.Measure(new Size(_help.MaxWidth, double.PositiveInfinity));
            _surface.MinimumLabelTop = 28 + _help.DesiredSize.Height;
            if (!showToolbar || ActualWidth < 1 || ActualHeight < 1) return;
            _toolbar.MaxWidth = Math.Max(80, ActualWidth - 16);
            if (_toolbar.Child is FrameworkElement toolbarBody) toolbarBody.InvalidateMeasure();
            _toolbar.InvalidateMeasure();
            _toolbar.Measure(new Size(_toolbar.MaxWidth, double.PositiveInfinity));
            double sx = ActualWidth / Monitor.Width, sy = ActualHeight / Monitor.Height;
            double wantedLeft = (_session.Selection.Right - Monitor.Left) * sx - _toolbar.DesiredSize.Width;
            double wantedTop = (_session.Selection.Bottom - Monitor.Top) * sy + 12;
            if (wantedTop + _toolbar.DesiredSize.Height > ActualHeight - 8)
            {
                wantedTop = (_session.Selection.Top - Monitor.Top) * sy - _toolbar.DesiredSize.Height - 12;
                if (wantedTop < _surface.MinimumLabelTop)
                    wantedTop = (_session.Selection.Top - Monitor.Top) * sy + 12;
            }
            Canvas.SetLeft(_toolbar, Math.Clamp(wantedLeft, 8, Math.Max(8, ActualWidth - _toolbar.DesiredSize.Width - 8)));
            Canvas.SetTop(_toolbar, Math.Clamp(wantedTop, 8, Math.Max(8, ActualHeight - _toolbar.DesiredSize.Height - 8)));
        }

        private static Cursor CursorFor(SelectionHandle handle) => handle switch
        {
            SelectionHandle.Move => Cursors.SizeAll,
            SelectionHandle.North or SelectionHandle.South => Cursors.SizeNS,
            SelectionHandle.East or SelectionHandle.West => Cursors.SizeWE,
            SelectionHandle.NorthWest or SelectionHandle.SouthEast => Cursors.SizeNWSE,
            SelectionHandle.NorthEast or SelectionHandle.SouthWest => Cursors.SizeNESW,
            _ => Cursors.Cross
        };
    }

    private sealed class CaptureSurface : FrameworkElement
    {
        private readonly CaptureSession _session;
        private readonly PixelRect _monitor;
        private readonly BitmapSource _image;
        public double MinimumLabelTop { get; set; } = 65;
        public PixelPoint? PointerPosition { get; set; }

        public CaptureSurface(CaptureSession session, PixelRect monitor)
        {
            _session = session; _monitor = monitor;
            var image = new CroppedBitmap(session.Screenshot, new Int32Rect(monitor.Left - session.Desktop.Left,
                monitor.Top - session.Desktop.Top, monitor.Width, monitor.Height));
            image.Freeze(); _image = image;
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            double sx = ActualWidth / _monitor.Width, sy = ActualHeight / _monitor.Height;
            Rect whole = new(0, 0, ActualWidth, ActualHeight);
            dc.DrawImage(_image, whole);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(125, 0, 0, 0)), null, whole);
            PixelRect selected = _session.Selection;
            if (selected.IsEmpty) return;
            Rect r = new((selected.Left - _monitor.Left) * sx, (selected.Top - _monitor.Top) * sy,
                selected.Width * sx, selected.Height * sy);
            dc.PushClip(new RectangleGeometry(r));
            dc.DrawImage(_image, whole);
            dc.PushTransform(new MatrixTransform(sx, 0, 0, sy, -_monitor.Left * sx, -_monitor.Top * sy));
            _session.DrawAnnotations(dc);
            dc.Pop();
            dc.Pop();
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(91, 179, 255)), 2), r);
            if (!_session.Dragging)
                foreach (var (_, p) in SelectionGeometry.Handles(selected))
                    dc.DrawRectangle(Brushes.White, new Pen(Brushes.DodgerBlue, 1),
                        new Rect((p.X - _monitor.Left) * sx - 4, (p.Y - _monitor.Top) * sy - 4, 8, 8));
            var label = new FormattedText($"{selected.Width} × {selected.Height} px",
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 13, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double x = Math.Clamp(r.Left + 6, 8, Math.Max(8, ActualWidth - label.Width - 16));
            double maximumY = Math.Max(8, ActualHeight - label.Height - 12);
            double y = Math.Clamp(r.Top - label.Height - 14, Math.Min(MinimumLabelTop, maximumY), maximumY);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 24, 28, 35)), null,
                new Rect(x - 6, y - 4, label.Width + 12, label.Height + 8));
            dc.DrawText(label, new Point(x, y));
            if (PointerPosition is PixelPoint pointer && selected.Contains(pointer) && _session.Mode is EditMode.Pen or EditMode.Eraser)
            {
                double radius = (_session.Mode == EditMode.Pen ? _session.PenWidth : _session.EraserWidth) / 2;
                var center = new Point((pointer.X - _monitor.Left) * sx, (pointer.Y - _monitor.Top) * sy);
                dc.DrawEllipse(null, new Pen(Brushes.Black, 2), center, radius * sx, radius * sy);
                dc.DrawEllipse(null, new Pen(Brushes.White, 1), center, radius * sx, radius * sy);
            }
        }
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)] private struct PointNative { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)] private struct BitmapInfoHeader
        {
            public uint Size; public int Width; public int Height; public ushort Planes; public ushort BitCount;
            public uint Compression; public uint SizeImage; public int XPelsPerMeter; public int YPelsPerMeter;
            public uint ClrUsed; public uint ClrImportant;
        }
        [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public BitmapInfoHeader Header; public uint Color; }

        public static PixelPoint CursorPosition()
        {
            if (!GetCursorPos(out var point)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return new(point.X, point.Y);
        }

        public static BitmapSource CaptureDesktop(PixelRect r)
        {
            int stride = checked(r.Width * 4);
            int length = checked(stride * r.Height);
            IntPtr desktop = GetDC(IntPtr.Zero), memory = IntPtr.Zero, bitmap = IntPtr.Zero, old = IntPtr.Zero;
            if (desktop == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                memory = CreateCompatibleDC(desktop);
                if (memory == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                var info = new BitmapInfo { Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(), Width = r.Width, Height = -r.Height,
                    Planes = 1, BitCount = 32, Compression = 0, SizeImage = (uint)length
                } };
                bitmap = CreateDIBSection(desktop, ref info, 0, out IntPtr bits, IntPtr.Zero, 0);
                if (bitmap == IntPtr.Zero || bits == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                old = SelectObject(memory, bitmap);
                if (old == IntPtr.Zero || old == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!BitBlt(memory, 0, 0, r.Width, r.Height, desktop, r.Left, r.Top, 0x00CC0020 | 0x40000000))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var pixels = new byte[length];
                Marshal.Copy(bits, pixels, 0, length);
                // Bgr32 ignores the unused fourth byte written by GDI, unlike BGRA's alpha channel.
                var result = BitmapSource.Create(r.Width, r.Height, 96, 96, PixelFormats.Bgr32, null, pixels, stride);
                result.Freeze();
                return result;
            }
            finally
            {
                if (old != IntPtr.Zero && old != new IntPtr(-1)) SelectObject(memory, old);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (memory != IntPtr.Zero) DeleteDC(memory);
                ReleaseDC(IntPtr.Zero, desktop);
            }
        }

        [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out PointNative point);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(IntPtr dest, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("dwmapi.dll")] public static extern int DwmFlush();
    }
}
