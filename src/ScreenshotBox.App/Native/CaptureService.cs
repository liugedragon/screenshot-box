using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ScreenshotBox.App.Native;

public sealed record CaptureResult(BitmapSource Image, string Action);

public static class CaptureService
{
    private static bool _active;
    private enum EditMode { Select, Pen, Mosaic }
    private abstract record Annotation;
    private sealed record Stroke(List<PixelPoint> Points, Color Color, double Width) : Annotation;
    private sealed record Mosaic(PixelRect Bounds, BitmapSource Image) : Annotation;

    /// <summary>
    /// Caller hides its own windows first. The frozen real desktop is captured before any overlay exists.
    /// A null result means cancellation; the returned image contains only the selected physical pixels.
    /// </summary>
    public static async Task<CaptureResult?> CaptureAsync()
    {
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("请从桌面应用调用截图。");
        dispatcher.VerifyAccess();
        if (_active) return null;
        _active = true;
        try
        {
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            NativeMethods.DwmFlush();
            var monitors = System.Windows.Forms.Screen.AllScreens.Select(screen =>
                new PixelRect(screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height)).ToArray();
            if (monitors.Length == 0) throw new InvalidOperationException("没有发现可截图的显示器。");
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
        public bool CanUndo => !Dragging && _annotations.Count > 0;
        private readonly List<Annotation> _annotations = [];
        private Stroke? _pendingStroke;
        private PixelRect _pendingMosaic;
        private readonly List<CaptureOverlay> _windows = [];
        private readonly TaskCompletionSource<CaptureResult?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private PixelPoint _start;
        private PixelRect _original;
        private SelectionHandle _mode;
        private bool _finished;
        private CaptureOverlay? _toolbarOwner;

        public CaptureSession(BitmapSource screenshot, PixelRect desktop, PixelRect[] monitors)
        { Screenshot = screenshot; Desktop = desktop; Monitors = monitors; }

        public Task<CaptureResult?> RunAsync()
        {
            try
            {
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

        public void Begin(PixelPoint point)
        {
            if (_finished) return;
            if (Mode != EditMode.Select && !Selection.Contains(point)) return;
            _start = SelectionGeometry.Clamp(point, Desktop);
            _original = Selection;
            if (Mode == EditMode.Pen) _pendingStroke = new Stroke([point], PenColor, PenWidth);
            if (Mode == EditMode.Mosaic) _pendingMosaic = default;
            _mode = SelectionGeometry.HitTest(Selection, point);
            if (Mode == EditMode.Select && _mode == SelectionHandle.None)
            {
                Selection = default;
                _annotations.Clear();
            }
            Dragging = true;
            Refresh();
        }

        public void Update(PixelPoint point)
        {
            if (!Dragging || _finished) return;
            if (Mode == EditMode.Pen)
            {
                point = SelectionGeometry.Clamp(point, Selection);
                if (_pendingStroke is not null && _pendingStroke.Points[^1] != point)
                    _pendingStroke.Points.Add(point);
                Refresh();
                return;
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
                _annotations.Add(_pendingStroke);
                _pendingStroke = null;
            }
            if (!_pendingMosaic.IsEmpty)
            {
                _annotations.Add(new Mosaic(_pendingMosaic, Pixelate(_pendingMosaic)));
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
            _pendingStroke = null;
            _pendingMosaic = default;
            Mode = EditMode.Select;
            Refresh();
        }

        public void SetMode(EditMode mode) { if (!Dragging) { Mode = mode; Refresh(); } }
        public void SetPenColor(Color color) { if (!Dragging) { PenColor = color; Refresh(); } }
        public void SetPenWidth(double width) { if (!Dragging) { PenWidth = Math.Clamp(width, 2, 8); Refresh(); } }
        public void Undo()
        {
            if (!Dragging && _annotations.Count > 0) _annotations.RemoveAt(_annotations.Count - 1);
            Refresh();
        }

        public void DrawAnnotations(DrawingContext dc)
        {
            foreach (Annotation annotation in _annotations) Draw(annotation);
            if (_pendingStroke is not null) Draw(_pendingStroke);
            if (!_pendingMosaic.IsEmpty)
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(80, 240, 240, 240)),
                    new Pen(Brushes.White, 1), ToRect(_pendingMosaic));
            void Draw(Annotation annotation)
            {
                if (annotation is Mosaic mosaic) dc.DrawImage(mosaic.Image, ToRect(mosaic.Bounds));
                else if (annotation is Stroke stroke && stroke.Points.Count > 0)
                {
                    var brush = new SolidColorBrush(stroke.Color);
                    var pen = new Pen(brush, stroke.Width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                    if (stroke.Points.Count == 1)
                        dc.DrawEllipse(brush, null, new Point(stroke.Points[0].X, stroke.Points[0].Y), stroke.Width / 2, stroke.Width / 2);
                    else
                    {
                        var geometry = new StreamGeometry();
                        using (var context = geometry.Open())
                        {
                            context.BeginFigure(new Point(stroke.Points[0].X, stroke.Points[0].Y), false, false);
                            context.PolyLineTo(stroke.Points.Skip(1).Select(p => new Point(p.X, p.Y)).ToArray(), true, false);
                        }
                        geometry.Freeze();
                        dc.DrawGeometry(null, pen, geometry);
                    }
                }
            }
        }

        private BitmapSource Pixelate(PixelRect region)
        {
            var crop = new CroppedBitmap(Screenshot, new Int32Rect(region.Left - Desktop.Left,
                region.Top - Desktop.Top, region.Width, region.Height));
            int stride = checked(region.Width * 4);
            var pixels = new byte[checked(stride * region.Height)];
            crop.CopyPixels(pixels, stride, 0);
            const int block = 12;
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
        private WrapPanel? _penOptions;
        private readonly Dictionary<string, Button> _penButtons = [];
        private Button? _undoButton;
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
            Title = "截图选区";
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
                Text = "拖动选择截图范围  ·  选区可移动和调整大小  ·  Enter 收藏并复制  ·  Esc 取消",
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
                if (e.Key == Key.Escape) { e.Handled = true; session.Cancel(); }
                else if (e.Key == Key.Enter) { e.Handled = true; session.Accept("Collect"); }
                else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; session.Undo(); }
            };
            Closed += (_, _) => session.Cancel();
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
                if (session.Dragging) session.Update(point);
                else _surface.Cursor = session.Mode == EditMode.Select
                    ? CursorFor(SelectionGeometry.HitTest(session.Selection, point)) : Cursors.Cross;
            };
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
            var body = new StackPanel();
            var row = new WrapPanel(); body.Children.Add(row);
            AddMode("选区", EditMode.Select);
            AddMode("画笔", EditMode.Pen);
            AddMode("马赛克", EditMode.Mosaic);
            AddButton("撤销", _session.Undo);
            AddButton("收藏并复制", () => _session.Accept("Collect"));
            AddButton("仅复制", () => _session.Accept("Copy"));
            AddButton("另存 PNG", () => _session.Accept("Save"));
            AddButton("重新选择", _session.Reset);
            AddButton("取消", _session.Cancel);
            _penOptions = new WrapPanel { Margin = new Thickness(0, 2, 0, 0), Visibility = Visibility.Collapsed };
            body.Children.Add(_penOptions);
            AddPen("红色", () => _session.SetPenColor(Color.FromRgb(230, 55, 55)));
            AddPen("蓝色", () => _session.SetPenColor(Color.FromRgb(23, 105, 194)));
            AddPen("细", () => _session.SetPenWidth(2));
            AddPen("粗", () => _session.SetPenWidth(6));
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 251)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(170, 177, 189)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5),
                Padding = new Thickness(4), Child = body, Visibility = Visibility.Collapsed
            };
            void AddButton(string title, Action action)
            {
                var button = MakeButton(title);
                if (title == "撤销") _undoButton = button;
                if (title == "收藏并复制") { button.Background = new SolidColorBrush(Color.FromRgb(23, 105, 194)); button.Foreground = Brushes.White; }
                button.Click += (_, _) => action();
                row.Children.Add(button);
            }
            void AddMode(string title, EditMode mode)
            {
                var button = MakeButton(title);
                button.Click += (_, _) => _session.SetMode(mode);
                _modeButtons.Add(mode, button);
                row.Children.Add(button);
            }
            void AddPen(string title, Action action)
            {
                var button = MakeButton(title); button.Click += (_, _) => action();
                _penButtons.Add(title, button); _penOptions.Children.Add(button);
            }
            Button MakeButton(string title)
            {
                var button = new Button {
                Content = title, Style = new Style(typeof(Button)), FontSize = 13,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
                Background = new SolidColorBrush(Color.FromRgb(245, 247, 250)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(174, 182, 194)),
                Margin = new Thickness(2), Padding = new Thickness(8, 7, 8, 7), MinHeight = 32
                };
                _toolbarButtons.Add(button);return button;
            }
        }

        public void Refresh(bool showToolbar)
        {
            _surface.InvalidateVisual();
            _toolbar.Visibility = showToolbar ? Visibility.Visible : Visibility.Collapsed;
            foreach (var button in _toolbarButtons)
            {
                button.Padding = new Thickness(8, ActualWidth < 420 ? 4 : 7, 8, ActualWidth < 420 ? 4 : 7);
                button.MinHeight = ActualWidth < 420 ? 28 : 32;
            }
            if (_undoButton is not null) _undoButton.IsEnabled = _session.CanUndo;
            if (_penOptions is not null) _penOptions.Visibility = _session.Mode == EditMode.Pen ? Visibility.Visible : Visibility.Collapsed;
            foreach (var pair in _penButtons)
            {
                bool selected = pair.Key switch { "红色" => _session.PenColor.R > 200, "蓝色" => _session.PenColor.B > 150,
                    "细" => _session.PenWidth <= 2, "粗" => _session.PenWidth >= 6, _ => false };
                pair.Value.Background = new SolidColorBrush(selected ? Color.FromRgb(204, 228, 255) : Color.FromRgb(245, 247, 250));
            }
            foreach (var pair in _modeButtons)
            {
                pair.Value.Background = pair.Key == _session.Mode
                    ? new SolidColorBrush(Color.FromRgb(204, 228, 255)) : new SolidColorBrush(Color.FromRgb(245, 247, 250));
                pair.Value.Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32));
            }
            _help.Text = _session.Mode switch
            {
                EditMode.Pen => "画笔：在选区内拖动画线  ·  Ctrl+Z 撤销  ·  Enter 收藏并复制  ·  Esc 取消",
                EditMode.Mosaic => "马赛克：在选区内拖动矩形  ·  Ctrl+Z 撤销  ·  Enter 收藏并复制  ·  Esc 取消",
                _ => "拖动选择截图范围  ·  选区可移动和调整大小  ·  Enter 收藏并复制  ·  Esc 取消"
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
