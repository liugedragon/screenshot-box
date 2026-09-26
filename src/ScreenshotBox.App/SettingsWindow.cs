using Microsoft.Win32;
using ScreenshotBox.App.Native;
using System.Diagnostics;
using System.Windows.Input;
namespace ScreenshotBox.App;

public sealed class SettingsWindow : Window
{
    private readonly App _app;
    private readonly StackPanel _body=new(){Margin=new(0,0,12,0)};
    private readonly TextBlock _status=new(){TextWrapping=TextWrapping.Wrap,Margin=new(0,12,0,8)};
    private bool _busy, _restarting;
    public SettingsWindow(App app,HotkeyService hotkey)
    {
        _app=app;Title=L.T("设置 · 截图资料盒", "Settings · ScreenshotBox");Width=640;Height=730;MinWidth=500;MinHeight=540;
        SetResourceReference(BackgroundProperty,"SbBackground");SetResourceReference(ForegroundProperty,"SbText");
        var root=new DockPanel{Margin=new(24)};var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        footer.Children.Add(_status);var close=new Button{Content=L.T("完成", "Done"),HorizontalAlignment=HorizontalAlignment.Right};
        close.Click+=(_,_)=>{if(!_busy)Close();};footer.Children.Add(close);
        root.Children.Add(new ScrollViewer{Content=_body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});Content=root;
        Closing+=(_,e)=>{if(_busy&&!_restarting){e.Cancel=true;_status.Text=L.T("资料正在处理，请完成后再关闭设置。", "A library operation is in progress. Wait for it to finish before closing settings.");}};
        _body.Children.Add(new TextBlock{Text=L.T("设置", "Settings"),FontSize=20,FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,16)});
        Section(L.T("快捷键与外观", "Shortcut and appearance"));Label(L.T("截图快捷键", "Capture shortcut"));
        var shortcut=new TextBox{Text=app.Preferences.Shortcut,ToolTip=L.T("点击后按组合键，或直接输入，例如 Alt+A", "Press a key combination, or type one such as Alt+A"),Margin=new(0,4,0,4)};_body.Children.Add(shortcut);
        Hint(L.T("按 Ctrl、Alt 或 Shift 加一个键；冲突时保留原快捷键。", "Use Ctrl, Alt or Shift with one key. If registration fails, the current shortcut stays active."));
        shortcut.PreviewKeyDown+=(_,e)=>{
            var key=e.Key==Key.System?e.SystemKey:e.Key;
            if(key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.Tab or Key.Escape)return;
            var modifiers=Keyboard.Modifiers;if(modifiers==ModifierKeys.None)return;
            var parts=new List<string>();if(modifiers.HasFlag(ModifierKeys.Control))parts.Add("Ctrl");if(modifiers.HasFlag(ModifierKeys.Alt))parts.Add("Alt");if(modifiers.HasFlag(ModifierKeys.Shift))parts.Add("Shift");if(modifiers.HasFlag(ModifierKeys.Windows))parts.Add("Win");
            parts.Add(key.ToString());shortcut.Text=string.Join("+",parts);e.Handled=true;
        };
        AddButton(L.T("应用快捷键", "Apply shortcut"),()=>{
            if(hotkey.TrySet(shortcut.Text,out var error)){app.Preferences.Shortcut=hotkey.CurrentShortcut;app.Preferences.Save();shortcut.Text=hotkey.CurrentShortcut;_status.Text=L.T("截图快捷键已生效", "Capture shortcut updated");}else _status.Text=error;
            return Task.CompletedTask;
        });
        Label(L.T("主题", "Theme"));var theme=new ComboBox{Name="ThemeChoice",ItemsSource=new[]{L.T("跟随系统", "System"),L.T("浅色", "Light"),L.T("深色", "Dark")},SelectedIndex=app.Preferences.Theme switch{"Light"=>1,"Dark"=>2,_=>0},Margin=new(0,4,0,8)};
        theme.SelectionChanged+=(_,_)=>{try{app.Preferences.Theme=theme.SelectedIndex switch{1=>"Light",2=>"Dark",_=>"System"};AppearanceService.Apply(app.Preferences.Theme);app.Preferences.Save();_status.Text=L.T("主题已更新", "Theme updated");}catch(Exception ex){_status.Text=L.T("主题设置未能保存：", "Could not save the theme: ")+L.Error(ex);}};_body.Children.Add(theme);
        Label(L.T("界面语言", "Interface language"));
        var language=new ComboBox{Name="LanguageChoice",ItemsSource=new[]{L.T("跟随系统", "System"),"简体中文","English"},SelectedIndex=app.Preferences.Language switch{"zh-CN"=>1,"en-US"=>2,_=>0},Margin=new(0,4,0,8)};
        _body.Children.Add(language);
        Hint(L.T("跟随系统时，中文系统使用简体中文，其他系统使用英语；也可手动选择语言。应用后重新启动，图片、备注和识别文字不会翻译。", "System uses Simplified Chinese for Chinese Windows display languages and English for all others. Choose a language to override this. Applying restarts the app; images, notes and recognized text are not translated."));
        AddButton(L.T("应用语言并重启", "Apply language and restart"),async()=>{
            string nextLanguage=language.SelectedIndex switch{1=>"zh-CN",2=>"en-US",_=>"System"};
            if(nextLanguage==app.Preferences.Language){_status.Text=L.T("当前已使用此语言设置", "This language setting is already active");return;}
            await BusyAsync(async()=>{
                if(!await PrepareAsync()){_status.Text=L.T("已取消语言切换", "Language change canceled");return;}
                await GuardDataAsync(async()=>{
                    string previous=app.Preferences.Language;
                    app.Preferences.Language=nextLanguage;
                    try{app.Preferences.Save();_status.Text=L.T("语言已保存，正在重新启动…", "Language saved. Restarting…");await RestartAsync();}
                    catch{app.Preferences.Language=previous;app.Preferences.Save();throw;}
                });
            });
        },name:"ApplyLanguageButton");
        Section(L.T("本地资料", "Local library"));Label(L.T("保存位置", "Library location"));_body.Children.Add(new TextBlock{Text=app.Store.RootPath,TextWrapping=TextWrapping.Wrap,Margin=new(0,6,0,6)});
        Hint(L.T("图片、识别文字和分类保存在此文件夹，与软件安装目录分开；不会上传到服务器。", "Images, recognized text and metadata are stored here, separately from the application. They are not uploaded."));
        AddButton(L.T("打开资料文件夹", "Open library folder"),()=>{
            if(!Directory.Exists(app.Store.RootPath))throw new DirectoryNotFoundException(L.T("资料文件夹不存在，请检查保存位置。", "The library folder does not exist. Check the library location."));
            Process.Start(new ProcessStartInfo(app.Store.RootPath){UseShellExecute=true});_status.Text=L.T("已打开当前资料文件夹", "Library folder opened");return Task.CompletedTask;
        });
        AddButton(L.T("更改保存位置并迁移资料", "Move library to another folder"),async()=>{
            var folder=new OpenFolderDialog{Title=L.T("选择资料保存位置（建议选择新建空目录）", "Choose a library folder (an empty folder is recommended)")};if(folder.ShowDialog(this)!=true)return;
            string destination=folder.FolderName;
            if(string.Equals(Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar),Path.GetFullPath(app.Store.RootPath).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)){_status.Text=L.T("这已经是当前保存位置", "This is already the current library folder");return;}
            if(Directory.EnumerateFileSystemEntries(destination).Any())destination=NewLibraryDirectory(destination,"ScreenshotBox-Library");
            await BusyAsync(async()=>{
                if(!await PrepareAsync()){_status.Text=L.T("已取消更改保存位置", "Library move canceled");return;}
                await GuardDataAsync(async()=>{
                    string temporary=Path.Combine(Path.GetTempPath(),"ScreenshotBox-Migrate-"+Guid.NewGuid().ToString("N")+".zip");
                    try{await app.Store.BackupAsync(temporary);await LibraryStore.RestoreAsync(temporary,destination);app.Preferences.DataDirectory=destination;app.Preferences.Save();_status.Text=L.T("资料已迁移，正在重新启动…", "Library moved. Restarting…");await RestartAsync();}
                    finally{if(File.Exists(temporary))File.Delete(temporary);}
                });
            });
        });
        Hint(L.T("迁移成功后自动重启；原保存目录保留，不会自动删除。", "The app restarts after moving the library. The original folder is kept."));
        Section(L.T("备份与恢复", "Backup and restore"));
        AddButton(L.T("备份资料库", "Back up library"),async()=>{
            var dialog=new SaveFileDialog{Filter=L.T("资料库备份|*.zip", "Library backup|*.zip"),FileName=L.F("截图资料盒-{0:yyyyMMdd-HHmmss}.zip", "ScreenshotBox-{0:yyyyMMdd-HHmmss}.zip",DateTime.Now)};if(dialog.ShowDialog(this)!=true)return;
            await BusyAsync(async()=>{
                if(!await PrepareAsync()){_status.Text=L.T("已取消备份", "Backup canceled");return;}
                await GuardDataAsync(async()=>{await app.Store.BackupAsync(dialog.FileName);_status.Text=L.T("备份完成，包含图片、文字、分类和回收站资料", "Backup complete: images, text, metadata and items in the recycle bin are included");});
            });
        });
        AddButton(L.T("恢复到新目录", "Restore to a new folder"),async()=>{
            var file=new OpenFileDialog{Filter=L.T("资料库备份|*.zip", "Library backup|*.zip")};if(file.ShowDialog(this)!=true)return;
            var folder=new OpenFolderDialog{Title=L.T("选择恢复位置（将创建新的资料库子目录）", "Choose a restore location (a new library subfolder will be created)")};if(folder.ShowDialog(this)!=true)return;
            string destination=NewLibraryDirectory(folder.FolderName,"ScreenshotBox-Restored");
            await BusyAsync(async()=>{
                if(!await PrepareAsync()){_status.Text=L.T("已取消恢复", "Restore canceled");return;}
                await GuardDataAsync(async()=>{await LibraryStore.RestoreAsync(file.FileName,destination);app.Preferences.DataDirectory=destination;app.Preferences.Save();_status.Text=L.T("资料已恢复，正在重新启动…", "Library restored. Restarting…");await RestartAsync();});
            });
        });
        Hint(L.T("备份选择新的 ZIP 文件名。恢复会建立新资料库并自动重启，不覆盖当前资料。", "Choose a new ZIP filename for a backup. Restoring creates a new library and restarts the app; the current library is kept."));
        AddButton(L.T("检查原图和识别错误", "Check missing images and OCR errors"),async()=>await BusyAsync(async()=>{
            var missing=await app.Store.MissingFilesAsync();var failed=await app.Store.QueryAsync("","failed",1_000_000);
            string report=L.F("原图缺失：{0}\n{1}\n\n识别失败：{2}\n{3}\n\n识别失败可以在资料详情中重试。\n模型：{4}", "Missing originals: {0}\n{1}\n\nOCR failures: {2}\n{3}\n\nRetry OCR from the item's details.\nModel: {4}",
                missing.Count,string.Join("\n",missing.Select(i=>i.ImagePath)),failed.Count,
                string.Join("\n",failed.Select(i=>(string.IsNullOrWhiteSpace(i.Title)?L.T("未命名截图", "Untitled screenshot"):i.Title)+L.T("：", ": ")+L.Error(i.OcrError))),OcrQueue.ModelVersion);
            new ReportWindow(L.T("资料检查", "Library check"),report){Owner=this}.ShowDialog();_status.Text=L.T("资料检查完成", "Library check complete");
        }));
        Section(L.T("关于", "About"));Hint(L.F("截图资料盒 {0}\n图片和文字识别均在本地处理，无需账号或联网。\n关闭主窗口后保留在托盘；彻底退出请右键托盘图标。", "ScreenshotBox {0}\nImages and OCR are processed locally. No account or network connection is needed.\nClosing the main window keeps the app in the tray. Right-click the tray icon to quit.",typeof(App).Assembly.GetName().Version?.ToString(3)));
    }
    private Task<bool> PrepareAsync()=>_app.MainWindow is MainWindow library?library.PrepareForDataChangeAsync():Task.FromResult(true);
    private async Task GuardDataAsync(Func<Task> action){if(_app.DataTransition||_app.MainWindow is MainWindow library&&library.IsBusyForDataTransition)throw new IOException(L.T("当前正在保存或导入资料，请完成后再试。", "Saving or importing is in progress. Wait for it to finish and try again."));_app.DataTransition=true;try{await action();}finally{_app.DataTransition=false;}}
    private async Task RestartAsync(){_restarting=true;try{await _app.RestartAsync();}catch{_restarting=false;throw;}}
    private async Task BusyAsync(Func<Task> action){if(_busy)return;_busy=true;_body.IsEnabled=false;_status.Text=L.T("正在处理资料…", "Working on the library…");try{await action();}finally{_body.IsEnabled=true;_busy=false;}}
    private void AddButton(string label,Func<Task> action,string name=""){var button=new Button{Name=name,Content=label,HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,4,0,4)};button.Click+=async(_,_)=>{if(_busy)return;try{await action();}catch(Exception ex){_status.Text=L.T("操作失败：", "Operation failed: ")+L.Error(ex);}};_body.Children.Add(button);}
    private void Section(string text){if(_body.Children.Count>1)_body.Children.Add(new Separator{Margin=new(0,18,0,14)});_body.Children.Add(new TextBlock{Text=text,FontSize=15,FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,8)});}
    private void Label(string text)=>_body.Children.Add(new TextBlock{Text=text,FontWeight=FontWeights.SemiBold,FontSize=12,Margin=new(0,8,0,0)});
    private void Hint(string text){var hint=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new(0,4,0,8)};hint.SetResourceReference(TextBlock.ForegroundProperty,"SbMuted");_body.Children.Add(hint);}
    private static string NewLibraryDirectory(string parent,string prefix)=>Path.Combine(parent,prefix+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]);
}
