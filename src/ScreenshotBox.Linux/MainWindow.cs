using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ScreenshotBox.Core;
using ScreenshotBox.Ocr;
using ScreenshotBox.Linux.Native;

namespace ScreenshotBox.Linux;

public sealed class MainWindow : Window
{
    internal LibraryStore Store { get; private set; } = null!;
    internal OcrQueue Ocr { get; private set; } = null!;
    private LinuxImageLibrary _images = null!;
    private GlobalHotkeyService? _hotkey;
    private readonly Grid _layout = new() { ColumnDefinitions = new("190,*,300"), RowDefinitions = new("Auto,*,Auto") };
    private readonly WrapPanel _cards = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _navigation = new() { Spacing = 4 };
    private readonly TextBox _search = new() { MinWidth = 180, PlaceholderText = L.T("搜索标题、标签和图片文字", "Search titles, tags and image text") };
    private readonly TextBlock _status = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _details = new() { Spacing = 12 };
    private readonly Border _detailsHost;
    private readonly List<Bitmap> _thumbnails = [];
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(220) };
    private ScreenshotItem? _selected;
    private string _filter = "all";
    private string? _tag;
    private bool _oldest;
    private bool _ready;
    private string _hotkeyError = "";
    private string? _detailsItemId;
    private TextBox? _titleEditor, _notesEditor, _tagsEditor;
    private CheckBox? _starEditor;
    private readonly Dictionary<string, (string Title, string Notes, string Tags, bool Star)> _drafts = [];
    private Bitmap? _clipboardBitmap;
    private ScreenshotItem? _detailItem;
    private TextBlock? _ocrLabel;
    private TextBox? _ocrText;
    private Button? _ocrRetry;
    private Button? _captureButton;
    private bool _closing;
    private bool _shutdownComplete;
    private int _queryGeneration;
    private Socket? _instanceSocket;
    private string? _instanceSocketPath;
    
    private readonly string? _demoPath;

    public MainWindow()
    {
        Title = L.T("截图资料盒", "ScreenshotBox"); Width = 1200; Height = 800; MinWidth = 720; MinHeight = 500;
        FontSize = 14; App.StyleWindow(this);
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ScreenshotBox.Linux/Assets/app.ico")));
        var args = App.Arguments;
        int demo = Array.IndexOf(args, "--demo-output");
        _demoPath = demo >= 0 && demo + 1 < args.Length ? Path.GetFullPath(args[demo + 1]) : null;
        var side = new DockPanel { Margin = new Thickness(12) };
        var settings = Button(L.T("设置", "Settings"), async () => await OpenSettingsAsync());
        DockPanel.SetDock(settings, Dock.Bottom); side.Children.Add(settings);
        side.Children.Add(_navigation); Grid.SetRowSpan(side, 2); _layout.Children.Add(side);
        var toolbar = new Grid { ColumnDefinitions = new("*,Auto,Auto,Auto"), Margin = new Thickness(16, 12), ColumnSpacing = 8 };
        toolbar.Children.Add(_search);
        _captureButton = Button(L.T("新截图", "Capture"), CaptureAsync);
        _captureButton.IsEnabled = false; AddAt(toolbar, _captureButton, 1);
        AddAt(toolbar, Button(L.T("导入", "Import"), ImportAsync), 2);
        var sort = new ComboBox { ItemsSource = new[] { L.T("最新在前", "Newest first"), L.T("最早在前", "Oldest first") }, SelectedIndex = 0, Width = 138 };
        sort.SelectionChanged += async (_, _) => { _oldest = sort.SelectedIndex == 1; if (_ready) await RefreshAsync(); };
        AddAt(toolbar, sort, 3); Grid.SetColumn(toolbar, 1); Grid.SetColumnSpan(toolbar, 2); _layout.Children.Add(toolbar);
        var center = new DockPanel { Margin = new Thickness(16, 0, 16, 12) };
        var sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        sizeRow.Children.Add(new TextBlock { Text = L.T("缩略图", "Thumbnails"), VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
        var size = new Slider { Minimum = 150, Maximum = 340, Value = App.Settings.ThumbnailSize, Width = 120 };
        size.ValueChanged += async (_, _) => { App.Settings.ThumbnailSize = size.Value; if (_ready) await RefreshAsync(); };
        size.PointerCaptureLost += (_, _) => App.Settings.Save(App.SettingsFile);
        sizeRow.Children.Add(size);
        sizeRow.Children.Add(Button(L.T("详情", "Details"), () => { if (_detailsHost is null) return Task.CompletedTask; _detailsHost.IsVisible = !_detailsHost.IsVisible; _layout.ColumnDefinitions[2].Width = _detailsHost.IsVisible ? new GridLength(300) : new GridLength(0); return Task.CompletedTask; }));
        DockPanel.SetDock(sizeRow, Dock.Top); center.Children.Add(sizeRow);
        center.Children.Add(new ScrollViewer { Content = _cards, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Grid.SetColumn(center, 1); Grid.SetRow(center, 1); _layout.Children.Add(center);
        _detailsHost = new Border { Padding = new Thickness(16), Child = new ScrollViewer { Content = _details }, BorderThickness = new Thickness(1, 0, 0, 0), BorderBrush = new SolidColorBrush(Color.Parse("#40808080")) };
        Grid.SetColumn(_detailsHost, 2); Grid.SetRow(_detailsHost, 1); _layout.Children.Add(_detailsHost);
        var footer = new Border { Child = _status, Padding = new Thickness(16, 8), BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = new SolidColorBrush(Color.Parse("#40808080")) };
        Grid.SetRow(footer, 2); Grid.SetColumnSpan(footer, 3); _layout.Children.Add(footer);
        Content = _layout;
        _search.TextChanged += (_, _) => { _searchTimer.Stop(); _searchTimer.Start(); };
        _searchTimer.Tick += async (_, _) => { _searchTimer.Stop(); if (_ready) await RefreshAsync(); };
        KeyDown += async (_, e) => { if (e.Key == Key.Space && e.Source is not TextBox && _selected is not null) { e.Handled = true; await PreviewAsync(_selected); } };
        SizeChanged += (_, _) => { bool show = Bounds.Width >= 1000; _detailsHost.IsVisible = show; _layout.ColumnDefinitions[2].Width = show ? new GridLength(300) : new GridLength(0); };
        Opened += async (_, _) => await InitializeAsync();
        Closing += async (_, e) =>
        {
            if (_shutdownComplete) return;
            e.Cancel = true; if (_closing) return;
            _closing = true; _hotkey?.Dispose(); _searchTimer.Stop();
            if (Ocr is not null) await Ocr.DisposeAsync();
            _instanceSocket?.Dispose(); if (_instanceSocketPath is not null && File.Exists(_instanceSocketPath)) File.Delete(_instanceSocketPath); _clipboardBitmap?.Dispose(); foreach (var image in _thumbnails) image.Dispose();
            _shutdownComplete = true; Close();
        };
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, async (_, e) =>
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files is not null) await ImportPathsAsync(files.Select(x => x.TryGetLocalPath()).OfType<string>());
        });
    }

    private async Task InitializeAsync()
    {
        await Task.Yield();
        try
        {
            if (!AcquireInstance()) { Close(); return; }
            Store = new LibraryStore(App.Settings.DataDirectory); await Store.InitializeAsync();
            _images = new(Store); Ocr = new(Store);
            CaptureWindow.ConfigureTools(App.Settings.PenColor, App.Settings.PenWidth, App.Settings.EraserWidth, App.Settings.MosaicSize);
            Ocr.Changed += _ => Dispatcher.UIThread.Post(async () => { if (!_closing && _ready) await RefreshAsync(); });
            var x11 = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) && Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") != "wayland";
            _captureButton!.IsEnabled = x11;
            _hotkey = new GlobalHotkeyService(() => _ = CaptureAsync());
            if (x11 && !_hotkey.TrySet(App.Settings.Shortcut, out _hotkeyError)) { }
            if (!x11) _hotkeyError = L.T("截图和全局快捷键需要 X11；Wayland 暂不支持。", "Capture and global shortcuts require X11; Wayland is not supported.");
            foreach (var item in await Store.PendingAsync()) Ocr.Enqueue(item.Id);
            _ready = true; await RefreshAsync();
            if (Array.IndexOf(App.Arguments, "--self-test") >= 0) await LinuxSelfTest.RunAsync(this);
            if (Array.IndexOf(App.Arguments, "--clipboard-read") >= 0) await LinuxSelfTest.ReadClipboardAsync(this);
            if (_demoPath is not null) await CreateDemoAsync(_demoPath);
        }
        catch (Exception ex) { await ErrorAsync(ex); }
    }

    private bool AcquireInstance()
    {
        var identity = Path.GetFullPath(App.SettingsFile ?? AppSettings.SettingsPath);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName + "|" + identity)))[..24];
        var path = Path.Combine(Path.GetTempPath(), "screenshotbox-" + hash + ".sock");
        var endpoint = new UnixDomainSocketEndPoint(path);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                socket.Bind(endpoint); socket.Listen(4);
                if (OperatingSystem.IsLinux()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                _instanceSocket = socket; _instanceSocketPath = path;
                _ = ListenForActivationAsync(socket); return true;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                socket.Dispose();
                using var existing = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try { existing.Connect(endpoint); existing.Send(new byte[] { 1 }); return false; }
                catch (SocketException connect) when (connect.SocketErrorCode is SocketError.ConnectionRefused or SocketError.AddressNotAvailable)
                { if (attempt == 0 && File.Exists(path)) File.Delete(path); else throw; }
            }
        }
        throw new IOException("Cannot establish the application instance socket.");
    }
    private async Task ListenForActivationAsync(Socket listener)
    {
        try
        {
            while (!_closing)
            {
                using var client = await listener.AcceptAsync();
                var signal = new byte[1];
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { if (await client.ReceiveAsync(signal, SocketFlags.None, deadline.Token) > 0)
                    Dispatcher.UIThread.Post(() => { if (!_closing) { Show(); Activate(); } }); }
                catch (OperationCanceledException) when (!_closing) { }
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or SocketException or OperationCanceledException) { }
    }

    internal async Task RefreshAsync()
    {
        int generation = ++_queryGeneration;
        var items = await Store.QueryAsync(_search.Text ?? "", _filter, limit: 1000000, oldestFirst: _oldest, requiredTag: _tag);
        var tags = await Store.GetTagsAsync(); if (generation != _queryGeneration || _closing) return;
        _cards.Children.Clear(); foreach (var bitmap in _thumbnails) bitmap.Dispose(); _thumbnails.Clear();
        _navigation.Children.Clear();
        foreach (var (filter, zh, en) in new[] { ("all", "全部截图", "All screenshots"), ("recent", "最近保存", "Recent"), ("favorites", "星标", "Starred"), ("pending", "正在识别", "Recognizing"), ("failed", "识别失败", "Recognition failed"), ("trash", "回收站", "Trash") })
        {
            var key = filter; var nav = Button(L.T(zh, en), async () => { _filter = key; _tag = null; await RefreshAsync(); });
            nav.HorizontalContentAlignment = HorizontalAlignment.Left; nav.Classes.Add("navigation");
            nav.Classes.Set("selected", _filter == key && _tag is null);
            _navigation.Children.Add(nav);
        }
        _navigation.Children.Add(new TextBlock { Text = L.T("标签", "Tags"), FontSize = 12, Margin = new Thickness(8, 16, 0, 4) });
        foreach (var tag in tags)
        {
            var nav = Button(tag, async () => { _filter = "all"; _tag = tag; await RefreshAsync(); }); nav.HorizontalContentAlignment = HorizontalAlignment.Left; nav.Classes.Add("navigation"); nav.Classes.Set("selected", _tag == tag);
            _navigation.Children.Add(nav);
        }
        if (items.Count == 0) _cards.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(_search.Text) ? L.T("按快捷键框选截图，或拖入图片。", "Capture with your shortcut, or drop images here.") : L.T("没有找到匹配的截图。", "No matching screenshots."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 80), MaxWidth = 400 });
        foreach (var item in items.Take(400))
        {
            var card = new StackPanel { Spacing = 6 };
            var path = Store.ResolvePath(item.ThumbnailPath);
            if (File.Exists(path)) { var bitmap = new Bitmap(path); _thumbnails.Add(bitmap); card.Children.Add(new Image { Source = bitmap, Height = App.Settings.ThumbnailSize * .64, Stretch = Stretch.Uniform }); }
            else card.Children.Add(new TextBlock { Text = L.T("原图或缩略图缺失", "Image or thumbnail missing"), Height = App.Settings.ThumbnailSize * .64, TextWrapping = TextWrapping.Wrap });
            card.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(item.Title) ? item.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : item.Title, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 });
            var excerpt = MatchSummary(item, _search.Text ?? "");
            card.Children.Add(new TextBlock { Text = excerpt, FontSize = 12, MaxLines = 2, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = .7 });
            var button = new Button { Content = card, Width = App.Settings.ThumbnailSize, Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(12), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            button.Classes.Add("flat"); button.Classes.Set("selected", _selected?.Id == item.Id);
            button.Click += async (_, _) => { _selected = item; foreach (var other in _cards.Children.OfType<Button>()) other.Classes.Set("selected", other == button); ShowDetails(item); await Task.CompletedTask; };
            button.DoubleTapped += async (_, _) => await PreviewAsync(item);
            _cards.Children.Add(button);
        }
        if (_selected is not null) { _selected = items.FirstOrDefault(x => x.Id == _selected.Id); }
        ShowDetails(_selected);
        _status.Text = L.F("{0} 张截图 · {1} · {2}", "{0} screenshots · {1} · {2}", items.Count, string.IsNullOrEmpty(_hotkey?.CurrentShortcut) ? L.T("快捷键未注册", "Shortcut not registered") : _hotkey.CurrentShortcut, Store.RootPath);
        if (!string.IsNullOrEmpty(_hotkeyError) && string.IsNullOrEmpty(_hotkey?.CurrentShortcut)) _status.Text += "\n" + _hotkeyError;
        if (items.Count > 400) _status.Text += L.T(" · 仅显示前 400 张，请搜索缩小范围", " · First 400 shown; search to narrow results");
    }

    internal void ResizeClient(Size size) { Width = size.Width; Height = size.Height; ClientSize = size; }
    internal void SelectItem(ScreenshotItem item) { _selected = item; ShowDetails(item); }
    internal void ShowDetails(ScreenshotItem? item)
    {
        _detailItem = item;
        if (item is not null && item.Id == _detailsItemId && _ocrLabel is not null && _ocrText is not null && _ocrRetry is not null)
        {
            _ocrLabel.Text = OcrLabel(item); _ocrText.Text = item.OcrText;
            _ocrRetry.IsVisible = !item.IsDeleted && item.OcrStatus == "Failed";
            return;
        }
        if (_detailsItemId is not null && _titleEditor is not null && _notesEditor is not null && _tagsEditor is not null && _starEditor is not null)
            _drafts[_detailsItemId] = (_titleEditor.Text ?? "", _notesEditor.Text ?? "", _tagsEditor.Text ?? "", _starEditor.IsChecked == true);
        _detailsItemId = item?.Id;
        _titleEditor = _notesEditor = _tagsEditor = null; _starEditor = null;
        _details.Children.Clear();
        if (item is null) { _details.Children.Add(new TextBlock { Text = L.T("选择截图查看详情", "Select a screenshot to view details"), TextWrapping = TextWrapping.Wrap, Opacity = .7 }); return; }
        var draft = _drafts.GetValueOrDefault(item.Id, (item.Title, item.Notes, item.Tags, item.IsFavorite));
        var title = _titleEditor = new TextBox { Text = draft.Item1, PlaceholderText = L.T("标题（可选）", "Title (optional)") };
        var notes = _notesEditor = new TextBox { Text = draft.Item2, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 88, PlaceholderText = L.T("备注", "Notes") };
        var tags = _tagsEditor = new TextBox { Text = draft.Item3, PlaceholderText = L.T("标签，以逗号分隔", "Tags, separated by commas") };
        var starred = _starEditor = new CheckBox { Content = L.T("星标", "Starred"), IsChecked = draft.Item4 };
        _details.Children.Add(title); _details.Children.Add(notes); _details.Children.Add(tags); _details.Children.Add(starred);
        _details.Children.Add(Button(L.T("保存修改", "Save changes"), async () => { await Store.UpdateMetadataAsync(item.Id, title.Text ?? "", notes.Text ?? "", tags.Text ?? "", starred.IsChecked == true); await RefreshAsync(); }));
        _details.Children.Add(new TextBlock { Text = $"{item.Width} × {item.Height} px · {item.CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm}", FontSize = 12, TextWrapping = TextWrapping.Wrap });
        _details.Children.Add(Button(L.T("查看原图", "Open image"), () => PreviewAsync(_detailItem ?? item)));
        _details.Children.Add(Button(L.T("导出 PNG", "Export PNG"), () => ExportItemAsync(item)));
        _details.Children.Add(Button(item.IsDeleted ? L.T("恢复", "Restore") : L.T("移到回收站", "Move to trash"), async () => { await Store.SetDeletedAsync(item.Id, !item.IsDeleted); if (item.IsDeleted) Ocr.Enqueue(item.Id); await RefreshAsync(); }));
        _ocrLabel = new TextBlock { Text = OcrLabel(item), FontSize = 12, TextWrapping = TextWrapping.Wrap };
        _details.Children.Add(_ocrLabel);
        _ocrText = new TextBox { Text = item.OcrText, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 130, MaxHeight = 280 };
        _details.Children.Add(_ocrText);
        _details.Children.Add(Button(L.T("复制识别文字", "Copy recognized text"), async () => { if (Clipboard is not null) await Clipboard.SetTextAsync(_detailItem?.OcrText ?? item.OcrText); }));
        _ocrRetry = Button(L.T("重试识别", "Retry recognition"), async () => { Ocr.Enqueue(item.Id); await Task.CompletedTask; });
        _ocrRetry.IsVisible = !item.IsDeleted && item.OcrStatus == "Failed"; _details.Children.Add(_ocrRetry);
    }

    internal static string OcrLabel(ScreenshotItem item) => item.OcrStatus switch { "Ready" => L.T("可搜索", "Searchable"), "Failed" => L.T("识别失败，可重试", "Recognition failed; retry available") + "\n" + L.ErrorText(item.OcrError), _ => L.T("正在识别；标题、备注和标签仍可搜索", "Recognizing; titles, notes and tags remain searchable") };
    internal static string MatchSummary(ScreenshotItem item, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return OcrLabel(item);
        var text = string.Join(" · ", new[] { item.Title, item.Notes, item.Tags, item.OcrText });
        int start = text.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase);
        return start < 0 ? OcrLabel(item) : text.Substring(Math.Max(0, start - 18), Math.Min(90, text.Length - Math.Max(0, start - 18))).Replace('\n', ' ');
    }
    internal async Task CaptureAsync()
    {
        if (!_ready || _captureButton?.IsEnabled != true) return;
        try
        {
            var result = await CaptureWindow.CaptureAsync(this);
            var prefs = CaptureWindow.GetToolPreferences();
            App.Settings.PenColor = prefs.ColorHex; App.Settings.PenWidth = prefs.PenWidth;
            App.Settings.EraserWidth = prefs.EraserWidth; App.Settings.MosaicSize = prefs.MosaicSize; App.Settings.Save(App.SettingsFile);
            if (result is null) return;
            if (result.Action == CaptureAction.Export) { Show(); await ExportPngAsync(result.Png); return; }
            bool saved = false;
            if (result.Action == CaptureAction.SaveAndCopy) { var item = await _images.AddPngAsync(result.Png); Ocr.Enqueue(item.Id); saved = true; }
            try { if (Clipboard is null) throw new IOException(L.T("剪贴板不可用", "Clipboard unavailable")); using var stream = new MemoryStream(result.Png);
                var bitmap = new Bitmap(stream);
                try { await Clipboard.SetBitmapAsync(bitmap); var previous = _clipboardBitmap; _clipboardBitmap = bitmap; previous?.Dispose(); }
                catch { bitmap.Dispose(); throw; } }
            catch (Exception ex) { Show(); await ErrorAsync(new IOException(saved ? L.T("图片已保存，但复制失败：", "Image saved, but copy failed: ") + ex.Message : ex.Message)); }
            // XLaunch has no desktop tray host. Keep the library reachable through its window.
            ShowActivated = false; Show(); ShowActivated = true; await RefreshAsync();
        }
        catch (Exception ex) { Show(); await ErrorAsync(ex); }
    }
    private async Task ImportAsync()
    {
        if (!_ready) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = L.T("导入图片", "Import images"), AllowMultiple = true, FileTypeFilter = [FilePickerFileTypes.ImageAll] });
        await ImportPathsAsync(files.Select(x => x.TryGetLocalPath()).OfType<string>());
    }
    internal async Task ImportPathsAsync(IEnumerable<string> paths)
    {
        if (!_ready) return;
        var failures = new List<string>();
        foreach (var path in paths) { try { var item = await _images.AddFileAsync(path, Path.GetFileNameWithoutExtension(path)); Ocr.Enqueue(item.Id); } catch (Exception ex) { failures.Add(Path.GetFileName(path) + ": " + ex.Message); } }
        await RefreshAsync(); if (failures.Count > 0) await MessageAsync(L.T("导入失败", "Import failures"), string.Join("\n", failures));
    }
    private Task PreviewAsync(ScreenshotItem item) => new PreviewWindow(Store.ResolvePath(item.ImagePath), item, _search.Text ?? "").ShowDialog(this);
    private async Task ExportItemAsync(ScreenshotItem item) { await ExportPngAsync(await File.ReadAllBytesAsync(Store.ResolvePath(item.ImagePath))); }
    private async Task ExportPngAsync(byte[] png)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = L.T("导出 PNG", "Export PNG"), SuggestedFileName = "screenshot.png", DefaultExtension = "png", FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }] });
        if (file is null) return;
        var path = file.TryGetLocalPath(); if (path is null) throw new IOException("A local destination is required.");
        LinuxImageLibrary.WritePngAtomic(png, path);
    }
    private async Task OpenSettingsAsync()
    {
        if (!_ready) return;
        var settings = new SettingsWindow(this, _hotkey!); await settings.ShowDialog(this);
        if (!settings.RestartRequested) await RefreshAsync();
        if (settings.RestartRequested)
        {
            var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(typeof(App).Assembly.Location);
            start.ArgumentList.Add("--settings"); start.ArgumentList.Add(App.SettingsFile ?? AppSettings.SettingsPath);
            Closed += (_, _) => System.Diagnostics.Process.Start(start);
            Close();
        }
    }
    internal Task ErrorAsync(Exception ex) => MessageAsync(L.T("操作失败", "Operation failed"), L.ErrorText(ex.Message));
    internal async Task MessageAsync(string title, string message)
    {
        var dialog = new Window { Title = title, Width = 520, Height = 280, MinWidth = 400, MinHeight = 220, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var stack = new DockPanel { Margin = new Thickness(20) }; var close = Button(L.T("关闭", "Close"), () => { dialog.Close(); return Task.CompletedTask; });
        DockPanel.SetDock(close, Dock.Bottom); stack.Children.Add(close); stack.Children.Add(new ScrollViewer { Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap } });
        dialog.Content = stack; await dialog.ShowDialog(this);
    }
    internal static Button Button(string text, Func<Task> action)
    {
        var button = new Button { Content = text, MinHeight = 34 }; button.Classes.Add("flat");
        button.Click += async (_, _) => { try { await action(); } catch (Exception ex) { if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime { MainWindow: MainWindow main }) await main.ErrorAsync(ex); } };
        return button;
    }
    private static void AddAt(Grid grid, Control control, int column) { Grid.SetColumn(control, column); grid.Children.Add(control); }
    private async Task CreateDemoAsync(string output)
    {
        var fixtures = App.Arguments.SkipWhile(x => x != "--demo-input").Skip(1).FirstOrDefault();
        if (fixtures is not null) await ImportPathsAsync(Directory.EnumerateFiles(fixtures, "*.png"));
        for (int i = 0; i < 90 && (await Store.QueryAsync("", "pending")).Count > 0; i++) await Task.Delay(1000);
        if (fixtures is not null)
        {
            foreach (var item in await Store.QueryAsync(""))
                await Store.UpdateMetadataAsync(item.Id, System.Globalization.CultureInfo.GetCultureInfo("en-US").TextInfo.ToTitleCase(item.Title.Replace('-', ' ')), "", "Examples", false);
        }
        await RefreshAsync(); await Task.Delay(800);
        var demoItems = await Store.QueryAsync("");
        if (demoItems.Count > 0) { _selected = demoItems[0]; ShowDetails(_selected); }
        await Task.Delay(250);

        using var render = new RenderTargetBitmap(new PixelSize((int)ClientSize.Width, (int)ClientSize.Height)); render.Render(this);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!); render.Save(output, PngBitmapEncoderOptions.Default);
        var settingsWindow = new SettingsWindow(this, _hotkey!); settingsWindow.Show(this); await Task.Delay(300);
        using (var settingsRender = new RenderTargetBitmap(new PixelSize((int)settingsWindow.ClientSize.Width, (int)settingsWindow.ClientSize.Height)))
        { settingsRender.Render(settingsWindow); settingsRender.Save(Path.ChangeExtension(output, ".settings.png"), PngBitmapEncoderOptions.Default); }
        settingsWindow.Close();
        if (demoItems.Count > 0)
        {
            var previewItem = demoItems.FirstOrDefault(x => x.OcrText.Contains("warranty", StringComparison.OrdinalIgnoreCase)) ?? demoItems[0];
            var preview = new PreviewWindow(Store.ResolvePath(previewItem.ImagePath), previewItem, "warranty"); preview.Show(this); await Task.Delay(350);
            using var previewRender = new RenderTargetBitmap(new PixelSize((int)preview.ClientSize.Width, (int)preview.ClientSize.Height));
            previewRender.Render(preview); previewRender.Save(Path.ChangeExtension(output, ".preview.png"), PngBitmapEncoderOptions.Default); preview.Close();
        }
        File.WriteAllText(Path.ChangeExtension(output, ".json"), System.Text.Json.JsonSerializer.Serialize(new { language = L.Language, screenshots = demoItems.Count, ready = demoItems.Count(i => i.OcrStatus == "Ready"), textBoxes = demoItems.Sum(i => i.OcrBlocks.Count), syntheticContent = true, nativeWindow = true }));
        Close();
    }
}
