using Microsoft.Win32;
using System.Windows.Input;
using System.Windows.Threading;

namespace ScreenshotBox.App;

internal sealed record MetadataDraft(string Title, string Notes, string Tags, bool Favorite)
{
    public static MetadataDraft From(ScreenshotItem item) => new(item.Title, item.Notes, item.Tags, item.IsFavorite);
}

/// <summary>A real metadata editor, available even when the library's side pane is collapsed.</summary>
internal sealed class ItemEditorWindow : Window
{
    private readonly App _app;
    private ScreenshotItem _item;
    private readonly Action<MetadataDraft> _onChanged;
    private readonly Func<MetadataDraft, Task> _save;
    private readonly TextBox _title = new() { MaxLength = 1000, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _notes = new() { MaxLength = 20000, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 100 };
    private readonly TextBox _tags = new() { MaxLength = 2000 };
    private readonly CheckBox _favorite = new() { Content = "星标", Margin = new Thickness(0, 8, 0, 12) };
    private readonly TextBox _ocr = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 150, MaxHeight = 340, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
    private readonly TextBlock _ocrStatus = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 8) };
    private readonly StackPanel _editable = new();
    private readonly DispatcherTimer _ocrDelay = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private bool _closed;
    public string ItemId => _item.Id;
    private MetadataDraft Draft => new(_title.Text, _notes.Text, _tags.Text, _favorite.IsChecked == true);

    public ItemEditorWindow(App app, ScreenshotItem item, MetadataDraft draft,
        Action<MetadataDraft> onChanged, Func<MetadataDraft, Task> save)
    {
        _app = app; _item = item; _onChanged = onChanged; _save = save;
        Title = "资料详情 · 截图资料盒"; Width = 570; Height = 740; MinWidth = 400; MinHeight = 480;
        SetResourceReference(BackgroundProperty, "SbBackground");
        SetResourceReference(ForegroundProperty, "SbText");
        var root = new DockPanel { Margin = new Thickness(20) };
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        footer.Children.Add(_status);
        var shortcut = new TextBlock { Text = "Ctrl+S 保存", FontSize = 12, Margin = new Thickness(4, 0, 0, 4) };
        shortcut.SetResourceReference(TextBlock.ForegroundProperty, "SbMuted");footer.Children.Add(shortcut);
        var buttons = new WrapPanel(); footer.Children.Add(buttons);
        AddButton(buttons, "保存资料", SaveAsync);
        AddButton(buttons, "关闭", () => { Close(); return Task.CompletedTask; });
        var body = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        body.Children.Add(new TextBlock { Text = "资料详情", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
        try {
            var thumbnail = new Image { Source = ImageLibrary.Load(app.Store.ResolvePath(item.ThumbnailPath), 400), MaxHeight = 140, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 12) };
            SizeChanged += (_, _) => thumbnail.MaxHeight = ActualHeight < 600 ? 90 : 140;
            body.Children.Add(thumbnail);
        } catch { }
        body.Children.Add(_editable);
        AddField(_editable, "标题", _title); AddField(_editable, "备注", _notes); AddField(_editable, "标签（逗号分隔）", _tags);
        _editable.Children.Add(_favorite);
        _title.Text = draft.Title; _notes.Text = draft.Notes; _tags.Text = draft.Tags; _favorite.IsChecked = draft.Favorite;
        _title.TextChanged += (_, _) => _onChanged(Draft); _notes.TextChanged += (_, _) => _onChanged(Draft);
        _tags.TextChanged += (_, _) => _onChanged(Draft); _favorite.Checked += (_, _) => _onChanged(Draft); _favorite.Unchecked += (_, _) => _onChanged(Draft);
        var imageActions = new WrapPanel(); body.Children.Add(imageActions);
        AddButton(imageActions, "预览原图", () => { new PreviewWindow(_item, app.Store, "") { Owner = this }.Show(); return Task.CompletedTask; });
        AddButton(imageActions, "导出图片", () =>
        {
            var dialog = new SaveFileDialog { Filter = "PNG 图片|*.png", FileName = "截图.png" };
            if (dialog.ShowDialog(this) == true) { File.Copy(app.Store.ResolvePath(_item.ImagePath), dialog.FileName, true); _status.Text = "图片已导出"; }
            return Task.CompletedTask;
        });
        body.Children.Add(_ocrStatus);
        var ocrActions = new WrapPanel(); body.Children.Add(ocrActions);
        AddButton(ocrActions, "复制文字", () => { if (_ocr.Text.Length > 0) { Clipboard.SetText(_ocr.Text); _status.Text = "识别文字已复制"; } else _status.Text = "这张图片暂时没有可复制的识别文字"; return Task.CompletedTask; });
        AddButton(ocrActions, "重新识别", () => { if (app.DataTransition) _status.Text = "正在迁移资料库，请稍后再识别"; else if (_item.IsDeleted) _status.Text = "请先从回收站恢复资料"; else { app.Ocr.Enqueue(_item.Id); _status.Text = "已加入识别队列"; } return Task.CompletedTask; });
        body.Children.Add(_ocr); Content = root; RefreshOcr();
        app.Ocr.Changed += OnOcrChanged;
        _ocrDelay.Tick += async (_, _) =>
        {
            _ocrDelay.Stop();
            try { var latest = await app.Store.GetAsync(ItemId); if (!_closed && latest is not null) { _item = latest; RefreshOcr(); } }
            catch (Exception ex) { if (!_closed) _status.Text = "读取识别结果失败：" + ex.Message; }
        };
        Closed += (_, _) => { _closed = true; _ocrDelay.Stop(); app.Ocr.Changed -= OnOcrChanged; };
        PreviewKeyDown += async (_, e) => { if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; await SaveAsync(); } };
    }

    private async Task SaveAsync()
    {
        if (!_editable.IsEnabled) return;
        _editable.IsEnabled = false;
        try { await _save(Draft); _status.Text = "资料已保存"; }
        catch (Exception ex) { _status.Text = "保存失败：" + ex.Message; }
        finally { _editable.IsEnabled = true; }
    }

    private void OnOcrChanged(string id)
    {
        if (id != ItemId || _closed || Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(() => { if (!_closed) { _ocrDelay.Stop(); _ocrDelay.Start(); } });
    }

    private void RefreshOcr()
    {
        _ocr.Text = _item.OcrText;
        _ocrStatus.Text = _item.OcrStatus switch { "Ready" => "识别完成", "Failed" => "识别失败：" + _item.OcrError, _ => "正在识别 · 完成后可搜索图片文字" };
    }

    private static void AddField(Panel panel, string label, TextBox input)
    {
        var caption = new TextBlock { Text = label, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "SbMuted");
        panel.Children.Add(caption); panel.Children.Add(input);
    }

    private void AddButton(Panel panel, string text, Func<Task> action)
    {
        var button = new Button { Content = text };
        button.Click += async (_, _) => { try { await action(); } catch (Exception ex) { _status.Text = "操作失败：" + ex.Message; } };
        panel.Children.Add(button);
    }
}
