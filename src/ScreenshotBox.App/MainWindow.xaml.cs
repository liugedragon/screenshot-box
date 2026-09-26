using Microsoft.Win32;
using ScreenshotBox.App.Native;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace ScreenshotBox.App;

public partial class MainWindow : Window
{
    private readonly App _app;
    private readonly LibraryViewModel _vm = new();
    private readonly ImageLibrary _images;
    private readonly DispatcherTimer _searchDelay = new() { Interval=TimeSpan.FromMilliseconds(220) };
    private readonly DispatcherTimer _ocrDelay = new() { Interval=TimeSpan.FromMilliseconds(450) };
    private readonly Dictionary<string, MetadataDraft> _drafts = [];
    private ScreenshotItem? _editorItem;
    private bool _refreshing, _loadingEditor, _savingMetadata, _resolvingDrafts;
    private int _importsActive, _metadataWrites;
    public bool IsBusyForDataTransition => _captureBusy || _importsActive > 0 || _metadataWrites > 0 || _resolvingDrafts;
    private HotkeyService? _hotkey;
    private System.Windows.Forms.NotifyIcon? _tray;
    private System.Drawing.Icon? _trayOwnedIcon;
    private bool _runtimeDisposed;
    private string _filter="all";
    private string? _selectedTag;
    private readonly DispatcherTimer _preferenceDelay=new(){Interval=TimeSpan.FromMilliseconds(600)};
    private bool _captureBusy, _detailsVisible=true, _quitting;
    private int _loadLimit=200, _queryVersion;
    private ScreenshotCard? Selected => Gallery.SelectedItem as ScreenshotCard;
    public MainWindow(App app)
    {
        _app=app; _images=new(app.Store); InitializeComponent(); DataContext=_vm;
        Title="截图资料盒 · ScreenshotBox "+typeof(App).Assembly.GetName().Version?.ToString(3);
        _vm.ThumbnailSize=Math.Clamp(app.Preferences.ThumbnailSize,160,320);
        CaptureService.ConfigureTools(app.Preferences.AnnotationColor,app.Preferences.PenWidth,app.Preferences.EraserWidth,app.Preferences.MosaicSize);
        _preferenceDelay.Tick+=(_,_)=>{_preferenceDelay.Stop();PersistPreferences();};
        _vm.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(LibraryViewModel.ThumbnailSize)){_preferenceDelay.Stop();_preferenceDelay.Start();}};
        _searchDelay.Tick+=async (_,_)=>{_searchDelay.Stop();await RefreshAsync();};
        _ocrDelay.Tick+=async (_,_)=>{_ocrDelay.Stop();if(!_captureBusy)await RefreshAsync();};
        TitleBox.TextChanged+=MetadataChanged;NotesBox.TextChanged+=MetadataChanged;TagBox.TextChanged+=MetadataChanged;
        FavoriteBox.Checked+=(_,_)=>RememberDraft();FavoriteBox.Unchecked+=(_,_)=>RememberDraft();
        Loaded+=async (_,_)=>{
            ApplyTheme();
            ShortcutLabel.Text=$"截图快捷键\n{_app.Preferences.Shortcut}";
            if(!_app.IsTesting) {
            _hotkey=new(this,()=>_ = CaptureAsync());
            if (!_hotkey.TrySet(_app.Preferences.Shortcut,out var error)) _vm.Status=error;
            System.Drawing.Icon? icon=null;
            try{if(Environment.ProcessPath is string executable)icon=System.Drawing.Icon.ExtractAssociatedIcon(executable);}catch{}
            _trayOwnedIcon=icon;
            _tray=new(){Icon=icon??System.Drawing.SystemIcons.Application,Text="截图资料盒",Visible=true};
            var menu=new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("打开资料库",null,(_,_)=>Dispatcher.Invoke(ShowLibrary));
            menu.Items.Add("新截图",null,(_,_)=>Dispatcher.Invoke(()=>_ = CaptureAsync()));
            menu.Items.Add("退出",null,(_,_)=>Dispatcher.Invoke(()=>_ = QuitAsync()));
            _tray.ContextMenuStrip=menu; _tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(ShowLibrary);
            }
            await RefreshAsync();
        };
        Closing+=OnClosing;
        app.Ocr.Changed+=OnOcrChanged;
    }
    public void ShowLibrary(){if(_captureBusy)return;Show();WindowState=WindowState.Normal;Activate();}
    private void OnClosing(object? sender,CancelEventArgs e){if(!_quitting){e.Cancel=true;Hide();}}
    internal void DisposeRuntimeResources()
    {
        if(_runtimeDisposed)return;
        _runtimeDisposed=true;_quitting=true;_queryVersion++;
        _searchDelay.Stop();_ocrDelay.Stop();_preferenceDelay.Stop();PersistPreferences();_app.Ocr.Changed-=OnOcrChanged;
        _hotkey?.Dispose();_hotkey=null;
        if(_tray!=null){_tray.Visible=false;_tray.ContextMenuStrip?.Dispose();_tray.Dispose();_tray=null;}
        _trayOwnedIcon?.Dispose();_trayOwnedIcon=null;
    }
    private async Task QuitAsync(){
        if(IsBusyForDataTransition||_app.DataTransition){_vm.Status="正在保存或导入图片，请完成后再退出。";_tray?.ShowBalloonTip(3000,"截图资料盒",_vm.Status,System.Windows.Forms.ToolTipIcon.Info);return;}
        if(await PrepareForDataChangeAsync())await _app.QuitAsync();
    }
    private void PersistPreferences()
    {
        if(_app.IsTesting)return;
        try {
            _app.Preferences.ThumbnailSize=_vm.ThumbnailSize;
            var tools=CaptureService.GetToolPreferences();_app.Preferences.AnnotationColor=tools.ColorHex;
            _app.Preferences.PenWidth=tools.PenWidth;_app.Preferences.EraserWidth=tools.EraserWidth;_app.Preferences.MosaicSize=tools.MosaicSize;
            _app.Preferences.Save();
        }catch(Exception ex){_vm.Status="设置暂未保存："+ex.Message;}
    }
    internal async Task<bool> PrepareForDataChangeAsync()
    {
        if(IsBusyForDataTransition||_app.DataTransition){MessageBox.Show(this,"正在保存或导入图片，请完成后再处理资料库。","资料处理中",MessageBoxButton.OK,MessageBoxImage.Information);return false;}
        RememberDraft();
        if(_drafts.Count==0)return true;
        var answer=MessageBox.Show(this,$"有 {_drafts.Count} 张截图的资料修改尚未保存。\n是否保存这些修改？\n选择‘否’将放弃修改，选择‘取消’继续编辑。","未保存的修改",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);
        if(answer==MessageBoxResult.Cancel)return false;
        if(answer==MessageBoxResult.No){_drafts.Clear();foreach(var editor in Application.Current.Windows.OfType<ItemEditorWindow>().ToArray())editor.Close();LoadDetails(Selected?.Item);return true;}
        var editors=Application.Current.Windows.OfType<ItemEditorWindow>().Select(w=>(Window:w,Enabled:w.IsEnabled)).ToArray();
        _resolvingDrafts=true;UpdateMetadataState();foreach(var editor in editors)editor.Window.IsEnabled=false;
        try{
            foreach(var pair in _drafts.ToArray())await SaveDraftAsync(pair.Key,pair.Value);
            if(_drafts.Count>0)throw new IOException("仍有未保存的修改，请继续编辑后再试。");
            foreach(var editor in editors)editor.Window.Close();return true;
        }
        catch(Exception ex){_vm.Status="保存失败："+ex.Message;MessageBox.Show(this,_vm.Status,"保存修改",MessageBoxButton.OK,MessageBoxImage.Error);return false;}
        finally{_resolvingDrafts=false;foreach(var editor in editors)editor.Window.IsEnabled=editor.Enabled;UpdateMetadataState();}
    }
    private void OnOcrChanged(string id)
    {
        if(_quitting||Dispatcher.HasShutdownStarted)return;
        Dispatcher.BeginInvoke(()=>{if(!_quitting){_ocrDelay.Stop();_ocrDelay.Start();}});
    }
    private void ApplyTheme()
    {
        AppearanceService.Apply(_app.Preferences.Theme);
    }
    private async Task RefreshAsync()
    {
        if(!IsLoaded)return;
        var version=++_queryVersion; var id=Selected?.Item.Id; var query=SearchBox.Text;
        try {
            var items=await _app.Store.QueryAsync(query,_filter,Math.Min(1000000,_loadLimit+1),oldestFirst:SortBox.SelectedIndex==1,requiredTag:_selectedTag);
            var tags=_filter=="all"?await _app.Store.GetTagsAsync():null;
            if(version!=_queryVersion)return;
            MoreButton.Visibility=items.Count>_loadLimit&&_loadLimit<999999?Visibility.Visible:Visibility.Collapsed;
            items=items.Take(_loadLimit).ToList();
            RememberDraft();
            id=Selected?.Item.Id;
            _refreshing=true;
            try {
                _vm.Items.Clear();foreach(var item in items)_vm.Items.Add(new(item,_app.Store,query));
                Gallery.SelectedItem=_vm.Items.FirstOrDefault(c=>c.Item.Id==id);
            }finally{_refreshing=false;}
            EmptyState.Visibility=items.Count==0?Visibility.Visible:Visibility.Collapsed;
            EmptyText.Text=query.Length>0||_selectedTag!=null?"没有匹配的截图。":"按快捷键框选截图，或拖入图片。";
            LoadDetails(Selected?.Item);
            if(_filter=="all") {
                TagsPanel.Children.Clear();
                foreach(var tag in tags!) {
                    var button=new Button{Content=tag,HorizontalContentAlignment=HorizontalAlignment.Left,MaxWidth=144,ClipToBounds=true};
                    button.SetResourceReference(Button.BackgroundProperty,_selectedTag==tag?"SbSelection":"SbBackground");
                    button.SetResourceReference(Button.ForegroundProperty,_selectedTag==tag?"SbAccentText":"SbText");
                    button.ToolTip=tag;
                    button.Click+=async (_,_)=>{_selectedTag=tag;_filter="all";SectionTitle.Text="标签 · "+tag;_loadLimit=200;await RefreshAsync();};TagsPanel.Children.Add(button);
                }
            }
        }catch(Exception ex){_vm.Status="读取资料失败："+ex.Message;}
    }
    private async void ChangeFilter(object sender,RoutedEventArgs e)
    {
        var tag=(string)((Button)sender).Tag;_selectedTag=null;
        foreach(var button in NavigationPanel.Children.OfType<Wpf.Ui.Controls.Button>()) {
            bool selected=ReferenceEquals(button,sender);
            button.SetResourceReference(Button.BackgroundProperty,selected?"SbSelection":"SbBackground");
            button.SetResourceReference(Button.ForegroundProperty,selected?"SbAccentText":"SbText");
        }
        if(tag=="tags"){_filter="all";SectionTitle.Text="标签 · 点击左侧标签查找";}else{_filter=tag;SectionTitle.Text=(string)((Button)sender).Content;}
        _loadLimit=200;await RefreshAsync();
    }
    private void SearchChanged(object sender,TextChangedEventArgs e){if(!IsLoaded)return;_loadLimit=200;_searchDelay.Stop();_searchDelay.Start();}
    private async void SortChanged(object sender,SelectionChangedEventArgs e){if(IsLoaded)await RefreshAsync();}
    private async void LoadMore(object sender,RoutedEventArgs e){_loadLimit=Math.Min(999999,_loadLimit+200);await RefreshAsync();}
    private void WindowSized(object sender,SizeChangedEventArgs e){if(Details!=null)UpdateDetails();}
    private void ToggleDetails(object sender,RoutedEventArgs e)
    {
        if(ActualWidth<1040){OpenItemEditor();return;}
        _detailsVisible=!_detailsVisible;UpdateDetails();
    }
    private void UpdateDetails(){bool show=_detailsVisible&&ActualWidth>=1040;DetailColumn.Width=new GridLength(show?300:0);Details.Visibility=show?Visibility.Visible:Visibility.Collapsed;}
    private void SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(_refreshing)return;
        RememberDraft();LoadDetails(Selected?.Item);
    }
    private void MetadataChanged(object sender,TextChangedEventArgs e)=>RememberDraft();
    private MetadataDraft CurrentDraft => new(TitleBox.Text,NotesBox.Text,TagBox.Text,FavoriteBox.IsChecked==true);
    private void RememberDraft()
    {
        if(_loadingEditor||_editorItem==null)return;
        var draft=CurrentDraft;
        if(draft==MetadataDraft.From(_editorItem))_drafts.Remove(_editorItem.Id);
        else _drafts[_editorItem.Id]=draft;
        UpdateMetadataState();
    }
    private void LoadDetails(ScreenshotItem? item)
    {
        _loadingEditor=true;
        try {
        _editorItem=item;DetailContents.IsEnabled=item!=null;
        DetailsScroll.Visibility=item!=null?Visibility.Visible:Visibility.Collapsed;
        DetailEmptyText.Visibility=item==null?Visibility.Visible:Visibility.Collapsed;
        CopyTextButton.IsEnabled=!string.IsNullOrEmpty(item?.OcrText);
        if(item==null){TitleBox.Text=NotesBox.Text=TagBox.Text=OcrTextBox.Text="";FavoriteBox.IsChecked=false;OcrStatusLabel.Text="";return;}
        var draft=_drafts.GetValueOrDefault(item.Id)??MetadataDraft.From(item);
        TitleBox.Text=draft.Title;NotesBox.Text=draft.Notes;TagBox.Text=draft.Tags;FavoriteBox.IsChecked=draft.Favorite;
        OcrTextBox.Text=item.OcrText;OcrStatusLabel.Text=Selected!.Status;
        OcrStatusLabel.ToolTip=item.OcrStatus=="Failed"?item.OcrError:null;
        DeleteButton.Content=item.IsDeleted?"恢复资料":"移入回收站";
        }finally{_loadingEditor=false;UpdateMetadataState();}
    }
    private void UpdateMetadataState()
    {
        bool external=_editorItem!=null&&Application.Current.Windows.OfType<ItemEditorWindow>().Any(w=>w.ItemId==_editorItem.Id);
        MetadataFields.IsEnabled=_editorItem!=null&&!external&&!_resolvingDrafts;
        bool dirty=_editorItem!=null&&_drafts.ContainsKey(_editorItem.Id);
        SaveMetadataButton.IsEnabled=dirty&&!external&&!_resolvingDrafts;
        MetadataStatus.Text=external?"正在独立详情窗口编辑":dirty?"有未保存的修改":"";
    }
    private async void SaveMetadata(object sender,RoutedEventArgs e)
    {
        if(_editorItem==null||_savingMetadata||!MetadataFields.IsEnabled)return;
        string id=_editorItem.Id;var draft=CurrentDraft;_savingMetadata=true;
        try{await SaveDraftAsync(id,draft);_vm.Status="资料已保存";}
        catch(Exception ex){_vm.Status="保存失败："+ex.Message;}
        finally{_savingMetadata=false;}
    }
    private async Task SaveDraftAsync(string id,MetadataDraft draft)
    {
        if(_app.DataTransition)throw new InvalidOperationException("正在迁移资料库，请稍后再保存。" );
        _metadataWrites++;
        try {
        await _app.Store.UpdateMetadataAsync(id,draft.Title,draft.Notes,draft.Tags,draft.Favorite);
        if(_editorItem?.Id==id&&CurrentDraft==draft) {
            _editorItem.Title=draft.Title;_editorItem.Notes=draft.Notes;_editorItem.Tags=draft.Tags;_editorItem.IsFavorite=draft.Favorite;
        }
        if(_drafts.GetValueOrDefault(id)==draft)_drafts.Remove(id);
        await RefreshAsync();
        }finally{_metadataWrites--;}
    }
    private void OpenItemEditor()
    {
        if(Selected==null){_vm.Status="请先选择一张截图，再打开详情";return;}
        RememberDraft();var item=Selected.Item;
        var existing=Application.Current.Windows.OfType<ItemEditorWindow>().FirstOrDefault(w=>w.ItemId==item.Id);
        if(existing!=null){existing.Show();existing.Activate();return;}
        var editor=new ItemEditorWindow(_app,item,_drafts.GetValueOrDefault(item.Id)??MetadataDraft.From(item),draft=>{
            if(draft==MetadataDraft.From(item))_drafts.Remove(item.Id);else _drafts[item.Id]=draft;
            if(_editorItem?.Id==item.Id){_loadingEditor=true;try{TitleBox.Text=draft.Title;NotesBox.Text=draft.Notes;TagBox.Text=draft.Tags;FavoriteBox.IsChecked=draft.Favorite;}finally{_loadingEditor=false;}}
            UpdateMetadataState();
        },async draft=>{
            await SaveDraftAsync(item.Id,draft);item.Title=draft.Title;item.Notes=draft.Notes;item.Tags=draft.Tags;item.IsFavorite=draft.Favorite;
        }){Owner=this};
        editor.Closed+=(_,_)=>LoadDetails(Selected?.Item);editor.Show();UpdateMetadataState();
    }
    private async void DeleteClicked(object sender,RoutedEventArgs e)
    {
        if(Selected==null)return;
        if(_app.DataTransition){_vm.Status="正在迁移资料库，请稍后再修改资料。";return;}
        try{var item=Selected.Item;await _app.Store.SetDeletedAsync(item.Id,!item.IsDeleted);if(item.IsDeleted&&item.OcrStatus!="Ready")_app.Ocr.Enqueue(item.Id);await RefreshAsync();}
        catch(Exception ex){_vm.Status="修改资料失败："+ex.Message;}
    }
    private void CopyTextClicked(object sender,RoutedEventArgs e){if(Selected==null)return;try{Clipboard.SetText(Selected.Item.OcrText);_vm.Status="识别文字已复制";}catch(Exception ex){_vm.Status="复制失败："+ex.Message;}}
    private void RetryClicked(object sender,RoutedEventArgs e){if(Selected==null||Selected.Item.IsDeleted)return;if(_app.DataTransition){_vm.Status="正在迁移资料库，请稍后再识别。";return;}_app.Ocr.Enqueue(Selected.Item.Id);_vm.Status="已加入识别队列";}
    private void ExportClicked(object sender,RoutedEventArgs e)
    {
        if(Selected==null)return;var dialog=new SaveFileDialog{Filter="PNG图片|*.png",FileName="截图.png"};if(dialog.ShowDialog(this)!=true)return;
        try{ImageLibrary.ExportAtomic(_app.Store.ResolvePath(Selected.Item.ImagePath),dialog.FileName);_vm.Status="图片已导出";}catch(Exception ex){_vm.Status="导出失败："+ex.Message;}
    }
    private void WindowKey(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.F&&Keyboard.Modifiers==ModifierKeys.Control){SearchBox.Focus();SearchBox.SelectAll();e.Handled=true;}
        else if(e.Key==Key.Space&&Gallery.IsKeyboardFocusWithin&&e.OriginalSource is not Button){OpenPreview(sender,e);e.Handled=true;}
        else if(e.Key==Key.Enter&&Gallery.IsKeyboardFocusWithin&&e.OriginalSource is not Button){OpenItemEditor();e.Handled=true;}
        else if(e.Key==Key.S&&Keyboard.Modifiers==ModifierKeys.Control){SaveMetadata(sender,e);e.Handled=true;}
    }
    private void OpenPreview(object sender,RoutedEventArgs e){if(Selected==null)return;try{new PreviewWindow(Selected.Item,_app.Store,SearchBox.Text){Owner=this}.Show();}catch(Exception ex){_vm.Status="原图无法打开："+ex.Message;}}
    private async void CaptureClicked(object sender,RoutedEventArgs e)=>await CaptureAsync();
    private async Task CaptureAsync()
    {
        if(_captureBusy||_resolvingDrafts||_app.DataTransition)return;_captureBusy=true;
        var visibleWindows=Application.Current.Windows.Cast<Window>().Where(w=>w.IsVisible)
            .OrderBy(w=>w==this?0:1).Select(w=>new WindowVisibility(w,w.WindowState,new WindowInteropHelper(w).Handle)).ToArray();
        IntPtr foreground=GetForegroundWindow();GetWindowThreadProcessId(foreground,out uint foregroundProcess);
        bool accepted=false;
        try{
            foreach(var state in visibleWindows)state.Window.Hide();
            await Task.Delay(180);var result=await CaptureService.CaptureAsync();if(result==null)return;
            if(result.Action=="Save"){
                var dialog=new SaveFileDialog{Filter="PNG图片|*.png",FileName="截图.png"};
                if(dialog.ShowDialog()==true){ImageLibrary.WritePngAtomic(result.Image,dialog.FileName);accepted=true;_vm.Status="图片已保存";}return;
            }
            if(result.Action=="Collect"){
                var item=await _images.AddAsync(result.Image);_app.Ocr.Enqueue(item.Id);
                _vm.Status="已保存到资料库";accepted=true;
            }
            try{await ClipboardHelper.CopyImageAsync(result.Image);_vm.Status=result.Action=="Collect"?"已保存并复制":"图片已复制";}
            catch(Exception ex){throw new IOException(result.Action=="Collect"?"已保存到资料库，但复制失败。"+ex.Message:ex.Message,ex);}
            accepted=true;
        }catch(Exception ex){_vm.Status=ex.Message;_tray?.ShowBalloonTip(4000,"截图资料盒",ex.Message,System.Windows.Forms.ToolTipIcon.Info);}
        finally{
            PersistPreferences();ApplyTheme();
            foreach(var state in visibleWindows) {
                if(!IsWindow(state.Handle))continue;
                var previous=state.Window.ShowActivated;state.Window.ShowActivated=false;
                try{state.Window.WindowState=state.State;state.Window.Show();}
                finally{state.Window.ShowActivated=previous;}
            }
            // Successful capture returns to the source application. Cancellation never forces focus.
            if(accepted&&foregroundProcess!=(uint)Environment.ProcessId&&IsWindow(foreground))SetForegroundWindow(foreground);
            _captureBusy=false;await RefreshAsync();
        }
    }
    private sealed record WindowVisibility(Window Window,WindowState State,IntPtr Handle);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetForegroundWindow(IntPtr window);
    private async void ImportClicked(object sender,RoutedEventArgs e){var dialog=new OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg",Multiselect=true};if(dialog.ShowDialog(this)==true)await ImportAsync(dialog.FileNames);}
    private async void FilesDropped(object sender,DragEventArgs e){if(e.Data.GetData(DataFormats.FileDrop) is string[] files)await ImportAsync(files);}
    private async Task ImportAsync(IEnumerable<string> files)
    {
        if(_app.DataTransition||_resolvingDrafts)return;
        _importsActive++;
        try {
        var errors=new List<string>();int count=0;
        foreach(var file in files){if(_app.DataTransition)break;try{
            if(!new[]{".png",".jpg",".jpeg"}.Contains(Path.GetExtension(file).ToLowerInvariant()))throw new IOException("仅支持PNG或JPEG");
            var bmp=await Task.Run(()=>ImageLibrary.Load(file));var item=await _images.AddAsync(bmp,Path.GetFileNameWithoutExtension(file));_app.Ocr.Enqueue(item.Id);count++;
        }catch(Exception ex){errors.Add(Path.GetFileName(file)+"："+ex.Message);}}
        _vm.Status=$"已导入 {count} 张";await RefreshAsync();if(errors.Count>0)new ReportWindow("导入失败的项目",string.Join("\n",errors)){Owner=this}.ShowDialog();
        }finally{_importsActive--;}
    }
    private void SettingsClicked(object sender,RoutedEventArgs e){if(_hotkey==null)return;new SettingsWindow(_app,_hotkey){Owner=this}.ShowDialog();ApplyTheme();ShortcutLabel.Text=$"截图快捷键\n{_app.Preferences.Shortcut}";_app.Preferences.ThumbnailSize=_vm.ThumbnailSize;_app.Preferences.Save();}
}
