using Microsoft.Win32;
using ScreenshotBox.App.Native;
using System.Windows.Input;
namespace ScreenshotBox.App;

public sealed class SettingsWindow : Window
{
    private readonly App _app;
    private readonly TextBlock _status=new(){TextWrapping=TextWrapping.Wrap,Margin=new(0,12,0,0)};
    public SettingsWindow(App app,HotkeyService hotkey)
    {
        _app=app;Title="设置 · 截图资料盒";Width=620;Height=660;MinWidth=500;MinHeight=540;SetResourceReference(BackgroundProperty,"SbBackground");SetResourceReference(ForegroundProperty,"SbText");
        var panel=new StackPanel{Margin=new(24,24,36,24)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        panel.Children.Add(new TextBlock{Text="设置",FontSize=20,Margin=new(0,0,0,20)});
        Label(panel,"截图快捷键");
        var shortcut=new TextBox{Text=app.Preferences.Shortcut,ToolTip="点击后按组合键，或输入Alt+A"};panel.Children.Add(shortcut);
        shortcut.PreviewKeyDown+=(_,e)=>{
            var key=e.Key==Key.System?e.SystemKey:e.Key;
            if(key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift)return;
            var modifiers=Keyboard.Modifiers;
            if(modifiers==ModifierKeys.None)return;
            var parts=new List<string>();if(modifiers.HasFlag(ModifierKeys.Control))parts.Add("Ctrl");if(modifiers.HasFlag(ModifierKeys.Alt))parts.Add("Alt");if(modifiers.HasFlag(ModifierKeys.Shift))parts.Add("Shift");if(modifiers.HasFlag(ModifierKeys.Windows))parts.Add("Win");parts.Add(key.ToString());shortcut.Text=string.Join("+",parts);e.Handled=true;
        };
        Button(panel,"应用快捷键",()=>{if(hotkey.TrySet(shortcut.Text,out var error)){app.Preferences.Shortcut=hotkey.CurrentShortcut;app.Preferences.Save();_status.Text="快捷键已生效";}else _status.Text=error;});
        Label(panel,"主题");var theme=new ComboBox{ItemsSource=new[]{"跟随系统","浅色","深色"},SelectedIndex=app.Preferences.Theme switch{"Light"=>1,"Dark"=>2,_=>0},Margin=new(0,8,0,16)};
        theme.SelectionChanged+=(_,_)=>{app.Preferences.Theme=theme.SelectedIndex switch{1=>"Light",2=>"Dark",_=>"System"};AppearanceService.Apply(app.Preferences.Theme);app.Preferences.Save();};panel.Children.Add(theme);
        Label(panel,"数据位置");panel.Children.Add(new TextBlock{Text=app.Store.RootPath,TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,12)});
        Button(panel,"更改保存位置并迁移资料",async()=>{
            if(app.MainWindow is MainWindow library && library.IsBusyForDataTransition){_status.Text="当前正在保存、导入或刷新资料，请完成后再更改位置。";return;}
            var folder=new OpenFolderDialog{Title="选择资料保存位置（建议选择新建空目录）"};if(folder.ShowDialog(this)!=true)return;
            string destination=folder.FolderName;
            if(string.Equals(Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar),Path.GetFullPath(app.Store.RootPath).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)){_status.Text="这已经是当前保存位置";return;}
            if(Directory.EnumerateFileSystemEntries(destination).Any())destination=Path.Combine(destination,"ScreenshotBox-Library-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            await BusyAsync(async()=>{
                if(app.MainWindow is MainWindow current && current.IsBusyForDataTransition)throw new IOException("当前资料处理尚未完成，请稍后更改位置。");
                app.DataTransition=true;string temporary=Path.Combine(Path.GetTempPath(),"ScreenshotBox-Migrate-"+Guid.NewGuid().ToString("N")+".zip");
                try{
                    await app.Store.BackupAsync(temporary);await LibraryStore.RestoreAsync(temporary,destination);
                    app.Preferences.DataDirectory=destination;app.Preferences.Save();_status.Text="资料迁移完成，正在重新启动…";
                    await app.RestartAsync();
                }finally{if(File.Exists(temporary))File.Delete(temporary);app.DataTransition=false;}
            });
        });
        Button(panel,"备份资料库",async()=>{var d=new SaveFileDialog{Filter="资料库备份|*.zip",FileName=$"截图资料盒-{DateTime.Now:yyyyMMdd-HHmmss}.zip"};if(d.ShowDialog(this)!=true)return;await BusyAsync(async()=>{await app.Store.BackupAsync(d.FileName);_status.Text="备份完成，包含图片、文字和分类";});});
        Button(panel,"恢复到新目录",async()=>{
            var file=new OpenFileDialog{Filter="资料库备份|*.zip"};if(file.ShowDialog(this)!=true)return;
            var folder=new OpenFolderDialog{Title="选择恢复位置（将创建新的资料库子目录）"};if(folder.ShowDialog(this)!=true)return;
            string destination=Path.Combine(folder.FolderName,"ScreenshotBox-Restored-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            await BusyAsync(async()=>{await LibraryStore.RestoreAsync(file.FileName,destination);app.Preferences.DataDirectory=destination;app.Preferences.Save();_status.Text="恢复完成。退出软件后重新启动，将使用新资料库。";});
        });
        Button(panel,"检查原图和识别错误",async()=>await BusyAsync(async()=>{
            var missing=await app.Store.MissingFilesAsync();var failed=await app.Store.QueryAsync("","failed",1000);
            new ReportWindow("诊断",$"原图缺失：{missing.Count}\n"+string.Join("\n",missing.Select(i=>i.ImagePath))+"\n\n识别失败："+failed.Count+"\n"+string.Join("\n",failed.Select(i=>i.Title+"："+i.OcrError))+"\n\n模型："+OcrQueue.ModelVersion){Owner=this}.ShowDialog();
        }));
        panel.Children.Add(new TextBlock{Text="关闭窗口后保留在托盘。右键托盘图标可打开资料库或退出。\n图片和识别均在本地处理，无需联网。",TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new(0,16,0,0)});
        panel.Children.Add(_status);
    }
    private async Task BusyAsync(Func<Task> action){IsEnabled=false;_status.Text="正在处理…";try{await action();}catch(Exception ex){_status.Text="操作失败："+ex.Message;}finally{IsEnabled=true;}}
    private static void Label(Panel p,string text)=>p.Children.Add(new TextBlock{Text=text,FontWeight=FontWeights.SemiBold,Margin=new(0,8,0,0)});
    private static void Button(Panel p,string label,Action action){var b=new Button{Content=label,HorizontalAlignment=HorizontalAlignment.Left};b.Click+=(_,_)=>action();p.Children.Add(b);}
}
