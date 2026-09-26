using Microsoft.Win32;
using System.Windows.Input;
using System.Windows.Threading;

namespace ScreenshotBox.App;

internal sealed record MetadataDraft(string Title, string Notes, string Tags, bool Favorite)
{
    public static MetadataDraft From(ScreenshotItem item) => new(item.Title, item.Notes, item.Tags, item.IsFavorite);
}

/// <summary>Metadata editor for screenshots, including when the library's details pane is collapsed.</summary>
internal sealed class ItemEditorWindow : Window
{
    private readonly App _app;
    private ScreenshotItem _item;
    private readonly Action<MetadataDraft> _onChanged;
    private readonly Func<MetadataDraft, Task> _save;
    private readonly TextBox _title = new() { MaxLength = 1000, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _notes = new() { MaxLength = 20000, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 100 };
    private readonly TextBox _tags = new() { MaxLength = 2000 };
    private readonly CheckBox _favorite = new() { Content = L.T("星标", "Starred"), Margin = new Thickness(0, 8, 0, 12) };
    private readonly TextBox _ocr = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 150, MaxHeight = 340, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
    private readonly TextBlock _ocrStatus = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 8) };
    private readonly StackPanel _editable = new();
    private readonly TextBlock _dirty = new() { FontSize = 12, Margin = new Thickness(0, 0, 0, 6) };
    private Button? _saveButton, _copyButton, _retryButton;
    private MetadataDraft _savedDraft;
    private readonly DispatcherTimer _ocrDelay = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private bool _closed, _saving;
    public string ItemId => _item.Id;
    public bool HasUnsavedChanges => Draft != _savedDraft;
    public bool IsSaving => _saving;
    private MetadataDraft Draft => new(_title.Text, _notes.Text, _tags.Text, _favorite.IsChecked == true);

    public ItemEditorWindow(App app, ScreenshotItem item, MetadataDraft draft,
        Action<MetadataDraft> onChanged, Func<MetadataDraft, Task> save)
    {
        _app = app; _item = item; _onChanged = onChanged; _save = save;
        _savedDraft = MetadataDraft.From(item);
        Title = L.T("资料详情 · 截图资料盒", "Details · ScreenshotBox"); Width = 570; Height = 740; MinWidth = 400; MinHeight = 480;
        SetResourceReference(BackgroundProperty, "SbBackground");
        SetResourceReference(ForegroundProperty, "SbText");
        var root = new DockPanel { Margin = new Thickness(20) };
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        footer.Children.Add(_dirty);
        footer.Children.Add(_status);
        var shortcut = new TextBlock { Text = L.T("Ctrl+S 保存", "Ctrl+S to save"), FontSize = 12, Margin = new Thickness(4, 0, 0, 4) };
        shortcut.SetResourceReference(TextBlock.ForegroundProperty, "SbMuted");footer.Children.Add(shortcut);
        var buttons = new WrapPanel(); footer.Children.Add(buttons);
        _saveButton = AddButton(buttons, L.T("保存修改", "Save changes"), SaveAsync, primary: true);
        AddButton(buttons, L.T("关闭", "Close"), () => { Close(); return Task.CompletedTask; });
        var body = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        body.Children.Add(new TextBlock { Text = L.T("资料详情", "Details"), FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
        try {
            var thumbnail = new Image { Source = ImageLibrary.Load(app.Store.ResolvePath(item.ThumbnailPath), 400), MaxHeight = 140, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 12) };
            SizeChanged += (_, _) => thumbnail.MaxHeight = ActualHeight < 600 ? 90 : 140;
            body.Children.Add(thumbnail);
        } catch { }
        body.Children.Add(_editable);
        AddField(_editable, L.T("标题", "Title"), _title); AddField(_editable, L.T("备注", "Notes"), _notes); AddField(_editable, L.T("标签（逗号分隔）", "Tags (comma-separated)"), _tags);
        _editable.Children.Add(_favorite);
        _title.Text = draft.Title; _notes.Text = draft.Notes; _tags.Text = draft.Tags; _favorite.IsChecked = draft.Favorite;
        _title.TextChanged += (_, _) => DraftChanged(); _notes.TextChanged += (_, _) => DraftChanged();
        _tags.TextChanged += (_, _) => DraftChanged(); _favorite.Checked += (_, _) => DraftChanged(); _favorite.Unchecked += (_, _) => DraftChanged();
        var imageActions = new WrapPanel(); body.Children.Add(imageActions);
        AddButton(imageActions, L.T("预览原图", "Preview image"), () => { new PreviewWindow(_item, app.Store, "") { Owner = this }.Show(); return Task.CompletedTask; });
        AddButton(imageActions, L.T("导出图片", "Export image"), () =>
        {
            var dialog = new SaveFileDialog { Filter = L.T("PNG 图片|*.png", "PNG image|*.png"), FileName = L.T("截图.png", "Screenshot.png") };
            if (dialog.ShowDialog(this) == true) { ImageLibrary.ExportAtomic(app.Store.ResolvePath(_item.ImagePath), dialog.FileName); _status.Text = L.T("图片已导出", "Image exported"); }
            return Task.CompletedTask;
        });
        body.Children.Add(_ocrStatus);
        var ocrActions = new WrapPanel(); body.Children.Add(ocrActions);
        _copyButton = AddButton(ocrActions, L.T("复制文字", "Copy text"), () => { if (_ocr.Text.Length > 0) { Clipboard.SetText(_ocr.Text); _status.Text = L.T("识别文字已复制", "Recognized text copied"); } else _status.Text = L.T("这张图片暂时没有可复制的识别文字", "There is no recognized text to copy yet"); return Task.CompletedTask; });
        _retryButton = AddButton(ocrActions, L.T("重新识别", "Retry OCR"), () => { if (app.DataTransition) _status.Text = L.T("正在处理资料库，请稍后再识别", "A library operation is in progress. Try OCR again when it finishes."); else if (_item.IsDeleted) _status.Text = L.T("请先从回收站恢复资料", "Restore this item from the recycle bin first"); else { app.Ocr.Enqueue(_item.Id); _status.Text = L.T("已加入识别队列", "Queued for OCR"); } return Task.CompletedTask; });
        body.Children.Add(_ocr); Content = root; RefreshOcr();
        UpdateSavingState();
        app.Ocr.Changed += OnOcrChanged;
        _ocrDelay.Tick += async (_, _) =>
        {
            _ocrDelay.Stop();
            try { var latest = await app.Store.GetAsync(ItemId); if (!_closed && latest is not null) { _item = latest; RefreshOcr(); } }
            catch (Exception ex) { if (!_closed) _status.Text = L.T("读取识别结果失败：", "Could not load OCR results: ") + L.Error(ex); }
        };
        Closed += (_, _) => { _closed = true; _ocrDelay.Stop(); app.Ocr.Changed -= OnOcrChanged; };
        PreviewKeyDown += async (_, e) => { if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; await SaveAsync(); } else if (e.Key == Key.Escape && !_saving) { e.Handled = true; Close(); } };
    }

    private async Task SaveAsync()
    {
        if (_saving || !HasUnsavedChanges) return;
        if (_app.DataTransition) { _status.Text = L.T("正在处理资料库，请稍后再保存", "A library operation is in progress. Try saving again when it finishes."); return; }
        var draft = Draft; _saving = true; _editable.IsEnabled = false; UpdateSavingState();
        try { await _save(draft); NotifySaved(draft); _status.Text = L.T("资料已保存", "Changes saved"); }
        catch (Exception ex) { _status.Text = L.T("保存失败：", "Save failed: ") + L.Error(ex); }
        finally { _saving = false; _editable.IsEnabled = true; UpdateSavingState(); }
    }

    // MainWindow also calls this after saving a draft during backup, migration,
    // or quit. It updates the baseline without replacing a newer unsaved draft.
    public void NotifySaved(MetadataDraft saved)
    {
        _savedDraft = saved;
        _item.Title = saved.Title; _item.Notes = saved.Notes; _item.Tags = saved.Tags; _item.IsFavorite = saved.Favorite;
        UpdateSavingState();
    }
    private void DraftChanged() { _onChanged(Draft); UpdateSavingState(); }
    private void UpdateSavingState()
    {
        _dirty.Text = _saving ? L.T("正在保存…", "Saving…") : HasUnsavedChanges ? L.T("有未保存的修改 · 点击保存修改写入资料库", "Unsaved changes · Select Save changes to keep them") : L.T("修改已保存", "Changes saved");
        _dirty.SetResourceReference(TextBlock.ForegroundProperty, HasUnsavedChanges ? "SbAccentText" : "SbMuted");
        if (_saveButton is not null) _saveButton.IsEnabled = HasUnsavedChanges && !_saving;
    }

    private void OnOcrChanged(string id)
    {
        if (id != ItemId || _closed || Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(() => { if (!_closed) { _ocrDelay.Stop(); _ocrDelay.Start(); } });
    }

    private void RefreshOcr()
    {
        _ocr.Text = _item.OcrText;
        _ocrStatus.Text = _item.OcrStatus switch { "Ready" => L.T("可搜索 · 本地识别完成", "Searchable · OCR complete"), "Failed" => L.T("识别失败，可重试\n", "OCR failed. You can retry.\n") + L.Error(_item.OcrError), _ => L.T("正在识别 · 完成后可搜索图片文字", "Recognizing text · Image text will be searchable when complete") };
        if (_copyButton is not null) _copyButton.IsEnabled = !string.IsNullOrEmpty(_item.OcrText);
        if (_retryButton is not null) _retryButton.IsEnabled = !_item.IsDeleted;
    }

    private static void AddField(Panel panel, string label, TextBox input)
    {
        var caption = new TextBlock { Text = label, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "SbMuted");
        panel.Children.Add(caption); panel.Children.Add(input);
    }

    private Button AddButton(Panel panel, string text, Func<Task> action, bool primary = false)
    {
        Button button = primary ? new Wpf.Ui.Controls.Button { Content = text, Appearance = Wpf.Ui.Controls.ControlAppearance.Primary } : new Button { Content = text };
        button.Click += async (_, _) => { try { await action(); } catch (Exception ex) { _status.Text = L.T("操作失败：", "Operation failed: ") + L.Error(ex); } };
        panel.Children.Add(button);
        return button;
    }
}
