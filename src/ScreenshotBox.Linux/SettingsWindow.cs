using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ScreenshotBox.Core;
using ScreenshotBox.Linux.Native;

namespace ScreenshotBox.Linux;

public sealed class SettingsWindow : Window
{
    public bool RestartRequested { get; private set; }
    public SettingsWindow(MainWindow owner, GlobalHotkeyService hotkey)
    {
        Title = L.T("设置", "Settings"); Width = 660; Height = 680; MinWidth = 500; MinHeight = 430; App.StyleWindow(this);
        var stack = new StackPanel { Spacing = 12, Margin = new Thickness(24) };
        Content = new ScrollViewer { Content = stack };
        stack.Children.Add(new TextBlock { Text = L.T("设置", "Settings"), FontSize = 20 });
        var language = new ComboBox { ItemsSource = new[] { L.T("跟随系统", "Follow system"), L.T("简体中文", "Simplified Chinese"), "English" }, SelectedIndex = App.Settings.Language switch { "zh-CN" => 1, "en-US" => 2, _ => 0 } };
        var theme = new ComboBox { ItemsSource = new[] { L.T("跟随系统", "Follow system"), L.T("浅色", "Light"), L.T("深色", "Dark") }, SelectedIndex = App.Settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 } };
        AddField(L.T("语言", "Language"), language); AddField(L.T("主题", "Theme"), theme);
        var shortcut = new TextBox { Text = App.Settings.Shortcut, PlaceholderText = "Ctrl+Alt+S / Alt+A" };
        AddField(L.T("截图快捷键", "Capture shortcut"), shortcut);
        stack.Children.Add(new TextBlock { Text = L.T("检查常见桌面保留组合和 X11 全局占用；应用内部快捷键无法完整检测。", "Checks common desktop combinations and X11 global grabs. Shortcuts inside other apps cannot all be detected."), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var directory = new TextBox { Text = App.Settings.DataDirectory, IsReadOnly = true };
        AddField(L.T("资料目录", "Library directory"), directory);
        stack.Children.Add(MainWindow.Button(L.T("选择新目录并迁移", "Choose a directory and migrate"), async () =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = L.T("选择新的空资料目录", "Choose an empty library directory") });
            var path = folders.FirstOrDefault()?.TryGetLocalPath(); if (path is null || Path.GetFullPath(path) == owner.Store.RootPath) return;
            if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any()) throw new IOException(L.T("请选择空目录，防止覆盖已有资料。", "Choose an empty directory to avoid overwriting existing data."));
            var backup = Path.Combine(Path.GetTempPath(), "screenshotbox-migrate-" + Guid.NewGuid().ToString("N") + ".zip");
            try { await owner.Store.BackupAsync(backup); await LibraryStore.RestoreAsync(backup, path); App.Settings.DataDirectory = path; App.Settings.Save(App.SettingsFile); RestartRequested = true; Close(); }
            finally { if (File.Exists(backup)) File.Delete(backup); }
        }));
        stack.Children.Add(new TextBlock { Text = L.T("迁移后重新启动，原目录保留。", "Migration restarts the app and preserves the original directory."), FontSize = 12 });
        stack.Children.Add(MainWindow.Button(L.T("备份资料库", "Back up library"), async () =>
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "ScreenshotBox-backup.zip", DefaultExtension = "zip" });
            var path = file?.TryGetLocalPath(); if (path is not null) await owner.Store.BackupAsync(path);
        }));
        stack.Children.Add(MainWindow.Button(L.T("从备份恢复到新目录", "Restore a backup to a new directory"), async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = L.T("选择备份", "Choose a backup"), FileTypeFilter = [new FilePickerFileType("ZIP") { Patterns = ["*.zip"] }] });
            var zip = files.FirstOrDefault()?.TryGetLocalPath(); if (zip is null) return;
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = L.T("选择空目录", "Choose an empty directory") });
            var path = folders.FirstOrDefault()?.TryGetLocalPath(); if (path is null) return;
            await LibraryStore.RestoreAsync(zip, path); App.Settings.DataDirectory = path; App.Settings.Save(App.SettingsFile); RestartRequested = true; Close();
        }));
        stack.Children.Add(MainWindow.Button(L.T("保存设置", "Save settings"), () =>
        {
            if (shortcut.Text != App.Settings.Shortcut && !hotkey.TrySet(shortcut.Text ?? "", out var error)) throw new InvalidOperationException(error);
            var policy = language.SelectedIndex switch { 1 => "zh-CN", 2 => "en-US", _ => "System" };
            RestartRequested = policy != App.Settings.Language;
            App.Settings.Language = policy; App.Settings.Theme = theme.SelectedIndex switch { 1 => "Light", 2 => "Dark", _ => "System" }; App.Settings.Shortcut = string.IsNullOrEmpty(hotkey.CurrentShortcut) ? App.Settings.Shortcut : hotkey.CurrentShortcut;
            App.Settings.Save(App.SettingsFile); App.ApplyTheme(); Close(); return Task.CompletedTask;
        }));
        stack.Children.Add(new TextBlock { Text = L.T("ScreenshotBox 0.1.5-linux.1 · X11 试用版\n本地 CPU 识别，资料不上传。Wayland 截图与快捷键暂不支持。", "ScreenshotBox 0.1.5-linux.1 · X11 preview\nLocal CPU recognition; library data stays on your computer. Wayland capture and hotkeys are not supported."), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        void AddField(string label, Control control) { stack.Children.Add(new TextBlock { Text = label, FontSize = 12 }); stack.Children.Add(control); }
    }
}
