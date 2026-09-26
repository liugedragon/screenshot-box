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
        _app=app;Title="设置 · 截图资料盒";Width=640;Height=730;MinWidth=500;MinHeight=540;
        SetResourceReference(BackgroundProperty,"SbBackground");SetResourceReference(ForegroundProperty,"SbText");
        var root=new DockPanel{Margin=new(24)};var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        footer.Children.Add(_status);var close=new Button{Content="完成",HorizontalAlignment=HorizontalAlignment.Right};
        close.Click+=(_,_)=>{if(!_busy)Close();};footer.Children.Add(close);
        root.Children.Add(new ScrollViewer{Content=_body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});Content=root;
        Closing+=(_,e)=>{if(_busy&&!_restarting){e.Cancel=true;_status.Text="资料正在处理，请完成后再关闭设置。";}};
        _body.Children.Add(new TextBlock{Text="设置",FontSize=20,FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,16)});
        Section("快捷键与外观");Label("截图快捷键");
        var shortcut=new TextBox{Text=app.Preferences.Shortcut,ToolTip="点击后按组合键，或直接输入，例如 Alt+A",Margin=new(0,4,0,4)};_body.Children.Add(shortcut);
        Hint("按 Ctrl、Alt 或 Shift 加一个键；冲突时保留原快捷键。");
        shortcut.PreviewKeyDown+=(_,e)=>{
            var key=e.Key==Key.System?e.SystemKey:e.Key;
            if(key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.Tab or Key.Escape)return;
            var modifiers=Keyboard.Modifiers;if(modifiers==ModifierKeys.None)return;
            var parts=new List<string>();if(modifiers.HasFlag(ModifierKeys.Control))parts.Add("Ctrl");if(modifiers.HasFlag(ModifierKeys.Alt))parts.Add("Alt");if(modifiers.HasFlag(ModifierKeys.Shift))parts.Add("Shift");if(modifiers.HasFlag(ModifierKeys.Windows))parts.Add("Win");
            parts.Add(key.ToString());shortcut.Text=string.Join("+",parts);e.Handled=true;
        };
        AddButton("应用快捷键",()=>{
            if(hotkey.TrySet(shortcut.Text,out var error)){app.Preferences.Shortcut=hotkey.CurrentShortcut;app.Preferences.Save();shortcut.Text=hotkey.CurrentShortcut;_status.Text="截图快捷键已生效";}else _status.Text=error;
            return Task.CompletedTask;
        });
        Label("主题");var theme=new ComboBox{ItemsSource=new[]{"跟随系统","浅色","深色"},SelectedIndex=app.Preferences.Theme switch{"Light"=>1,"Dark"=>2,_=>0},Margin=new(0,4,0,8)};
        theme.SelectionChanged+=(_,_)=>{try{app.Preferences.Theme=theme.SelectedIndex switch{1=>"Light",2=>"Dark",_=>"System"};AppearanceService.Apply(app.Preferences.Theme);app.Preferences.Save();_status.Text="主题已更新";}catch(Exception ex){_status.Text="主题设置未能保存："+ex.Message;}};_body.Children.Add(theme);
        Section("本地资料");Label("保存位置");_body.Children.Add(new TextBlock{Text=app.Store.RootPath,TextWrapping=TextWrapping.Wrap,Margin=new(0,6,0,6)});
        Hint("图片、识别文字和分类保存在此文件夹，与软件安装目录分开；不会上传到服务器。");
        AddButton("打开资料文件夹",()=>{
            if(!Directory.Exists(app.Store.RootPath))throw new DirectoryNotFoundException("资料文件夹不存在，请检查保存位置。");
            Process.Start(new ProcessStartInfo(app.Store.RootPath){UseShellExecute=true});_status.Text="已打开当前资料文件夹";return Task.CompletedTask;
        });
        AddButton("更改保存位置并迁移资料",async()=>{
            var folder=new OpenFolderDialog{Title="选择资料保存位置（建议选择新建空目录）"};if(folder.ShowDialog(this)!=true)return;
            string destination=folder.FolderName;
            if(string.Equals(Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar),Path.GetFullPath(app.Store.RootPath).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)){_status.Text="这已经是当前保存位置";return;}
            if(Directory.EnumerateFileSystemEntries(destination).Any())destination=NewLibraryDirectory(destination,"ScreenshotBox-Library");
            await BusyAsync(async()=>{
                if(!await PrepareAsync()){_status.Text="已取消更改保存位置";return;}
                await GuardDataAsync(async()=>{
                    string temporary=Path.Combine(Path.GetTempPath(),"ScreenshotBox-Migrate-"+Guid.NewGuid().ToString("N")+".zip");
                    try{await app.Store.BackupAsync(temporary);await LibraryStore.RestoreAsync(temporary,destination);app.Preferences.DataDirectory=destination;app.Preferences.Save();_status.Text="资料已迁移，正在重新启动…";await RestartAsync();}
                    finally{if(File.Exists(temporary))File.Delete(temporary);}
                });
            });
        });
        Hint("迁移成功后自动重启；原保存目录保留，不会自动删除。");
        Section("备份与恢复");
        AddButton("备份资料库",async()=>{
            var dialog=new SaveFileDialog{Filter="资料库备份|*.zip",FileName=$"截图资料盒-{DateTime.Now:yyyyMMdd-HHmmss}.zip"};if(dialog.ShowDialog(this)!=true)return;
            await BusyAsync(async()=>{
                if(!await PrepareAsync()){_status.Text="已取消备份";return;}
                await GuardDataAsync(async()=>{await app.Store.BackupAsync(dialog.FileName);_status.Text="备份完成，包含图片、文字、分类和回收站资料";});
            });
        });
        AddButton("恢复到新目录",async()=>{
            var file=new OpenFileDialog{Filter="资料库备份|*.zip"};if(file.ShowDialog(this)!=true)return;
            var folder=new OpenFolderDialog{Title="选择恢复位置（将创建新的资料库子目录）"};if(folder.ShowDialog(this)!=true)return;
            string destination=NewLibraryDirectory(folder.FolderName,"ScreenshotBox-Restored");
            await BusyAsync(async()=>{
                if(!await PrepareAsync()){_status.Text="已取消恢复";return;}
                await GuardDataAsync(async()=>{await LibraryStore.RestoreAsync(file.FileName,destination);app.Preferences.DataDirectory=destination;app.Preferences.Save();_status.Text="资料已恢复，正在重新启动…";await RestartAsync();});
            });
        });
        Hint("备份选择新的 ZIP 文件名。恢复会建立新资料库并自动重启，不覆盖当前资料。");
        AddButton("检查原图和识别错误",async()=>await BusyAsync(async()=>{
            var missing=await app.Store.MissingFilesAsync();var failed=await app.Store.QueryAsync("","failed",1_000_000);
            string report=$"原图缺失：{missing.Count}\n"+string.Join("\n",missing.Select(i=>i.ImagePath))+"\n\n识别失败："+failed.Count+"\n"+string.Join("\n",failed.Select(i=>(string.IsNullOrWhiteSpace(i.Title)?"未命名截图":i.Title)+"："+i.OcrError))+"\n\n识别失败可以在资料详情中重试。\n模型："+OcrQueue.ModelVersion;
            new ReportWindow("资料检查",report){Owner=this}.ShowDialog();_status.Text="资料检查完成";
        }));
        Section("关于");Hint("截图资料盒 "+typeof(App).Assembly.GetName().Version?.ToString(3)+"\n图片和文字识别均在本地处理，无需账号或联网。\n关闭主窗口后保留在托盘；彻底退出请右键托盘图标。");
    }
    private Task<bool> PrepareAsync()=>_app.MainWindow is MainWindow library?library.PrepareForDataChangeAsync():Task.FromResult(true);
    private async Task GuardDataAsync(Func<Task> action){if(_app.DataTransition||_app.MainWindow is MainWindow library&&library.IsBusyForDataTransition)throw new IOException("当前正在保存或导入资料，请完成后再试。");_app.DataTransition=true;try{await action();}finally{_app.DataTransition=false;}}
    private async Task RestartAsync(){_restarting=true;try{await _app.RestartAsync();}catch{_restarting=false;throw;}}
    private async Task BusyAsync(Func<Task> action){if(_busy)return;_busy=true;_body.IsEnabled=false;_status.Text="正在处理资料…";try{await action();}finally{_body.IsEnabled=true;_busy=false;}}
    private void AddButton(string label,Func<Task> action){var button=new Button{Content=label,HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,4,0,4)};button.Click+=async(_,_)=>{if(_busy)return;try{await action();}catch(Exception ex){_status.Text="操作失败："+ex.Message;}};_body.Children.Add(button);}
    private void Section(string text){if(_body.Children.Count>1)_body.Children.Add(new Separator{Margin=new(0,18,0,14)});_body.Children.Add(new TextBlock{Text=text,FontSize=15,FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,8)});}
    private void Label(string text)=>_body.Children.Add(new TextBlock{Text=text,FontWeight=FontWeights.SemiBold,FontSize=12,Margin=new(0,8,0,0)});
    private void Hint(string text){var hint=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new(0,4,0,8)};hint.SetResourceReference(TextBlock.ForegroundProperty,"SbMuted");_body.Children.Add(hint);}
    private static string NewLibraryDirectory(string parent,string prefix)=>Path.Combine(parent,prefix+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]);
}
