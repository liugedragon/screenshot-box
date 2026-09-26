using System.Globalization;
using System.Text.Json;
namespace ScreenshotBox.App;
internal static class SelfTest
{
    public static async Task RunAsync(App app)
    {
        app.Preferences.Theme="Light";var emptyWindow=new MainWindow(app);app.MainWindow=emptyWindow;emptyWindow.Show();await Task.Delay(450);AppearanceService.Apply("Light");SaveVisual(emptyWindow,Path.Combine(app.Store.RootPath,"ui-empty.png"));emptyWindow.DisposeRuntimeResources();emptyWindow.Close();
        var root=app.Store.RootPath;var drawing=new DrawingVisual();
        using(var dc=drawing.RenderOpen()){
            dc.DrawRectangle(Brushes.White,null,new Rect(0,0,1000,600));
            string[] lines=["课程资料 · ScreenshotBox","订单编号 AB-20260926-001","保修日期 2026-09-26","中文 English 100% A_B \"quote\""];
            for(int i=0;i<lines.Length;i++)dc.DrawText(new FormattedText(lines[i],CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),32,Brushes.Black,1),new Point(40,50+i*110));
        }
        var image=new RenderTargetBitmap(1000,600,96,96,PixelFormats.Pbgra32);image.Render(drawing);image.Freeze();
        var item=await new ImageLibrary(app.Store).AddAsync(image,"课程安排");
        var generation=await app.Store.BeginOcrAsync(item.Id);
        var stopwatch=System.Diagnostics.Stopwatch.StartNew();
        var (text,blocks)=await app.Ocr.RecognizeAsync(app.Store.ResolvePath(item.ImagePath),CancellationToken.None);
        stopwatch.Stop();
        await app.Store.CompleteOcrAsync(item.Id,generation,text,blocks,OcrQueue.ModelVersion);
        var results=new Dictionary<string,object>{["language"]=L.Language,["text"]=text,["blocks"]=blocks,["width"]=image.PixelWidth,["height"]=image.PixelHeight,["model"]=OcrQueue.ModelVersion};
        foreach(var query in new[]{"课程","订单","保修","AB-20260926","100%","A_B","English"})results["search:"+query]=(await app.Store.QueryAsync(query)).Any(i=>i.Id==item.Id);
        var clipboard=false;try{await ClipboardHelper.CopyImageAsync(image);await Task.Delay(80);var read=Clipboard.GetImage();results["clipboardReadWidth"]=read?.PixelWidth??0;results["clipboardReadHeight"]=read?.PixelHeight??0;results["clipboardFormats"]=Clipboard.GetDataObject()?.GetFormats()??[];clipboard=read?.PixelWidth==1000&&read.PixelHeight==600;}catch(Exception ex){results["clipboardError"]=ex.Message;}
        var atomic=Path.Combine(root,"atomic-overwrite.png");ImageLibrary.WritePngAtomic(image,atomic);
        var before=File.ReadAllBytes(atomic);bool rejected=false;
        using(var locked=new FileStream(atomic,FileMode.Open,FileAccess.Read,FileShare.Read)) {
            try{ImageLibrary.WritePngAtomic(image,atomic);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){rejected=true;}
        }
        results["atomicOverwritePreservesLockedDestination"]=rejected&&before.SequenceEqual(File.ReadAllBytes(atomic))&&!Directory.EnumerateFiles(root,".atomic-overwrite.png.*.tmp").Any();
        ImageLibrary.WritePngAtomic(image,atomic);results["atomicPngOverwrite"]=ImageLibrary.Load(atomic).PixelWidth==1000;
        ImageLibrary.ExportAtomic(app.Store.ResolvePath(item.ImagePath),atomic);results["atomicExportOverwrite"]=ImageLibrary.Load(atomic).PixelHeight==600;
        results["clipboardImage"]=clipboard;
        results["ocrMilliseconds"]=stopwatch.ElapsedMilliseconds;
        results["workingSetAfterOcrMiB"]=System.Diagnostics.Process.GetCurrentProcess().WorkingSet64/1048576.0;
        await app.Store.UpdateMetadataAsync(item.Id,"课程安排","修改后的备注：保修凭证","学习,凭证",true);
        results["editedNotesSearch"]=(await app.Store.QueryAsync("保修凭证")).Any(i=>i.Id==item.Id);
        results["editedTagSearch"]=(await app.Store.QueryAsync("凭证")).Any(i=>i.Id==item.Id);
        var missing=await new ImageLibrary(app.Store).AddAsync(image,"订单记录");
        var missingPath=app.Store.ResolvePath(missing.ImagePath);File.Delete(missingPath);app.Ocr.Enqueue(missing.Id);
        var failed=await WaitAsync(app,missing.Id,"Failed");results["missingImageFailure"]=failed.OcrStatus=="Failed";
        ImageLibrary.WritePng(image,missingPath);app.Ocr.Enqueue(missing.Id);var retry=await WaitAsync(app,missing.Id,"Ready");results["retryReady"]=retry.OcrText.Contains("课程");
        var race=await new ImageLibrary(app.Store).AddAsync(image,"保修凭证");bool restoredInFlight=false;
        void RestoreWhileSlotIsOwned(string id) {
            if(id!=race.Id||restoredInFlight)return;
            if(app.Store.GetAsync(id).GetAwaiter().GetResult()?.OcrStatus!="Processing")return;
            restoredInFlight=true;
            app.Store.SetDeletedAsync(id,true).GetAwaiter().GetResult();
            app.Store.SetDeletedAsync(id,false).GetAwaiter().GetResult();
            app.Ocr.Enqueue(id);
        }
        app.Ocr.Changed+=RestoreWhileSlotIsOwned;
        try{app.Ocr.Enqueue(race.Id);var recovered=await WaitAsync(app,race.Id,"Ready");results["deleteRestoreDuringOcr"]=restoredInFlight&&!recovered.IsDeleted&&recovered.OcrText.Contains("课程");}
        finally{app.Ocr.Changed-=RestoreWhileSlotIsOwned;}
        var zip=Path.Combine(root,"self-test-backup-"+Guid.NewGuid().ToString("N")+".zip");await app.Store.BackupAsync(zip);
        var restored=root+"-restored-"+Guid.NewGuid().ToString("N");await LibraryStore.RestoreAsync(zip,restored);
        using(var store=new LibraryStore(restored)){await store.InitializeAsync();results["restoredSearch"]=(await store.QueryAsync("课程")).Any(i=>i.Id==item.Id);results["restoredImage"]=File.Exists(store.ResolvePath(item.ImagePath));}
        results["runtimeLocation"]=typeof(object).Assembly.Location;
        results["baseDirectory"]=AppContext.BaseDirectory;
        results["network"]= "应用测试未调用网络；未隔离系统网络";
        var savedLanguage=L.Language;
        var settingsPath=Path.Combine(root,"language-settings.json");
        foreach(var language in new[]{"zh-CN","en-US"}){
            L.Apply(language);
            var preferences=new Settings{Language=language,Shortcut="Alt+A",DataDirectory=Path.Combine(root,"资料目录")};preferences.SaveTo(settingsPath);
            var loaded=Settings.LoadFrom(settingsPath);
            results["languagePersists:"+language]=loaded.Language==language&&loaded.Shortcut=="Alt+A"&&loaded.DataDirectory==preferences.DataDirectory;
            results["localizedResources:"+language]=Application.Current.Resources["Ui.All"] is string value&&value==L.T("全部截图","All screenshots")&&L.Get("Settings")==L.T("设置","Settings");
            results["localizedErrors:"+language]=L.Error(new IOException("备份文件已存在，请选择新的文件名。"))==L.T("备份文件已存在，请选择新的文件名。","The backup file already exists. Choose a new filename.");
        }
        File.WriteAllText(settingsPath,"{\"Shortcut\":\"Alt+A\"}");
        results["legacySettingsFollowSystem"]=Settings.LoadFrom(settingsPath).Language=="System";
        L.Apply("unsupported");results["unknownLanguageFollowsSystem"]=L.Language==L.Resolve("System");L.Apply(savedLanguage);
        results["systemChineseUsesChinese"]=new[]{"zh-CN","zh-TW","zh-HK","zh-Hant"}.All(c=>L.Resolve("System",c)=="zh-CN");
        results["otherSystemLanguagesUseEnglish"]=new[]{"en-US","fr-FR","ja-JP","de-DE","ko-KR"}.All(c=>L.Resolve("System",c)=="en-US");
        results["manualLanguageOverridesSystem"]=L.Resolve("en-US","zh-CN")=="en-US"&&L.Resolve("zh-CN","en-US")=="zh-CN";
        var follow=new Settings{Language="System"};follow.SaveTo(settingsPath);results["systemPolicyPersists"]=Settings.LoadFrom(settingsPath).Language=="System";

        app.Preferences.Theme="Light";var window=new MainWindow(app);app.MainWindow=window;window.Show();await Task.Delay(1200);AppearanceService.Apply("Light");
        var search=(TextBox)window.FindName("SearchBox");search.Text="课程";await Task.Delay(450);
        var gallery=(ListBox)window.FindName("Gallery");gallery.SelectedIndex=0;
        var title=(TextBox)window.FindName("TitleBox");title.Text="课程资料：人工智能课程安排、电脑订单编号与保修记录（2026年秋季学期）";
        var metadataButton=(Button)window.FindName("SaveMetadataButton");
        var source=PresentationSource.FromVisual(window)!;
        foreach(var key in new[]{System.Windows.Input.Key.Space,System.Windows.Input.Key.Enter}){
            var keyArgs=new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,source,Environment.TickCount,key){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent,Source=metadataButton};
            typeof(MainWindow).GetMethod("WindowKey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(window,new object[]{metadataButton,keyArgs});
            results["buttonKeyboardPreserved:"+key]=!keyArgs.Handled;
        }
        results["mainWindowLanguage"]=window.Title.StartsWith(L.T("截图资料盒","ScreenshotBox"),StringComparison.Ordinal)&&((Button)window.FindName("SaveMetadataButton")).Content?.ToString()==L.T("保存修改","Save changes");
        SaveVisual(window,Path.Combine(root,"ui-light.png"));
        app.Preferences.Theme="Dark";AppearanceService.Apply("Dark");await Task.Delay(150);
        SaveVisual(window,Path.Combine(root,"ui-dark.png"));
        window.Width=800;window.Height=580;await Task.Delay(150);SaveVisual(window,Path.Combine(root,"ui-narrow.png"));
        results["uiGalleryCount"]=gallery.Items.Count;
        results["narrowDetailsCollapsed"]=((FrameworkElement)window.FindName("Details")).Visibility==Visibility.Collapsed;
        var selected=((ScreenshotCard)gallery.SelectedItem).Item;
        typeof(MainWindow).GetMethod("OpenItemEditor",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(window,null);
        var editor=Application.Current.Windows.OfType<ItemEditorWindow>().Single(w=>w.ItemId==selected.Id);await Task.Delay(150);
        results["onlyOneMetadataEditor"]=!((FrameworkElement)window.FindName("MetadataFields")).IsEnabled;
        var externalTitle="课程资料：图书馆讨论记录、电脑订单与保修凭证（2026年秋季学期）";
        ((TextBox)typeof(ItemEditorWindow).GetField("_title",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(editor)!).Text=externalTitle;
        ((TextBox)typeof(ItemEditorWindow).GetField("_notes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(editor)!).Text="周三15:00，图书馆二楼；携带电脑和课程讲义。";
        SaveVisual(editor,Path.Combine(root,"ui-editor-dark.png"));editor.Width=410;editor.Height=500;await Task.Delay(100);SaveVisual(editor,Path.Combine(root,"ui-editor-small.png"));
        editor.Close();results["metadataDraftSurvivesEditorClose"]=title.Text==externalTitle&&((TextBox)window.FindName("NotesBox")).Text=="周三15:00，图书馆二楼；携带电脑和课程讲义。"&&((FrameworkElement)window.FindName("MetadataFields")).IsEnabled;
        ((Button)window.FindName("SaveMetadataButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(500);
        results["metadataSavedAndDirtyCleared"]=(await app.Store.GetAsync(selected.Id))?.Title.StartsWith("课程资料")==true&&!((Button)window.FindName("SaveMetadataButton")).IsEnabled;
        var tagPanel=(Panel)window.FindName("TagsPanel");tagPanel.Children.OfType<Button>().Single(b=>(string)b.Content=="凭证").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(350);
        results["uiExactTagFilter"]=gallery.Items.Cast<ScreenshotCard>().All(c=>c.Item.Id==item.Id)&&gallery.Items.Count==1;
        var hotkey=new ScreenshotBox.App.Native.HotkeyService(window,()=>{});var settings=new SettingsWindow(app,hotkey);settings.Show();await Task.Delay(150);SaveVisual(settings,Path.Combine(root,"ui-settings.png"));
        var settingsScroll=((DockPanel)settings.Content).Children.OfType<ScrollViewer>().Single();
        var languageChoice=((StackPanel)settingsScroll.Content).Children.OfType<ComboBox>().Single(c=>c.Name=="LanguageChoice");
        results["settingsLanguageChoice"]=languageChoice.Items.Count==3&&languageChoice.Items[0]?.ToString()==L.T("跟随系统","System")&&((StackPanel)settingsScroll.Content).Children.OfType<ComboBox>().Single(c=>c.Name=="ThemeChoice").Items[0]?.ToString()==L.T("跟随系统","System")&&languageChoice.SelectedIndex==(app.Preferences.Language switch{"zh-CN"=>1,"en-US"=>2,_=>0});settingsScroll.ScrollToBottom();await Task.Delay(100);SaveVisual(settings,Path.Combine(root,"ui-settings-bottom.png"));
        app.Preferences.Theme="Light";AppearanceService.Apply("Light");((StackPanel)settingsScroll.Content).Children.OfType<ComboBox>().Single(c=>c.Name!="LanguageChoice").SelectedIndex=1;settings.Width=510;settings.Height=550;settingsScroll.ScrollToHome();await Task.Delay(150);SaveVisual(settings,Path.Combine(root,"ui-settings-small-light.png"));settings.Close();app.Preferences.Theme="Dark";AppearanceService.Apply("Dark");hotkey.Dispose();
        var missingFiles=await app.Store.MissingFilesAsync();var failedItems=await app.Store.QueryAsync("","failed");
        var diagnostic=new ReportWindow(L.T("资料检查","Library check"),L.F("原图缺失：{0}\n识别失败：{1}\n\n识别失败可以在资料详情中重试。\n模型：{2}","Missing originals: {0}\nOCR failures: {1}\n\nRetry failed recognition from image details.\nModel: {2}",missingFiles.Count,failedItems.Count,OcrQueue.ModelVersion));diagnostic.Show();await Task.Delay(100);SaveVisual(diagnostic,Path.Combine(root,"ui-diagnostic.png"));diagnostic.Close();
        var recognized=await app.Store.GetAsync(item.Id)??item;var preview=new PreviewWindow(recognized,app.Store,"课程");preview.Show();await Task.Delay(250);SaveVisual(preview,Path.Combine(root,"ui-preview.png"));
        var previewRoot=(DockPanel)preview.Content;var previewTools=(Panel)previewRoot.Children[0];
        previewTools.Children.OfType<Button>().Single(b=>(string)b.Content==L.T("实际大小","Actual size")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));preview.UpdateLayout();
        var previewImage=(Grid)((ScrollViewer)previewRoot.Children[1]).Content;var previewScale=(ScaleTransform)previewImage.LayoutTransform;
        var actualWidth=previewImage.Width*previewScale.ScaleX*VisualTreeHelper.GetDpi(preview).DpiScaleX;
        results["previewActualSizePixelWidth"]=actualWidth;results["previewActualSizeIsOneToOne"]=Math.Abs(actualWidth-image.PixelWidth)<.1;
        preview.Close();
        var longImage=BitmapSource.Create(40,20000,96,96,PixelFormats.Bgra32,null,new byte[40*20000*4],40*4);longImage.Freeze();
        var longItem=await new ImageLibrary(app.Store).AddAsync(longImage,"长图适应测试（合成）");
        var longPreview=new PreviewWindow(longItem,app.Store,""){Width=620,Height=440};longPreview.Show();await Task.Delay(200);longPreview.UpdateLayout();
        var longRoot=(DockPanel)longPreview.Content;var longScroll=(ScrollViewer)longRoot.Children[1];var longContent=(Grid)longScroll.Content;var longScale=(ScaleTransform)longContent.LayoutTransform;
        results["longImageFitsWindow"]=longScale.ScaleY<.05&&longContent.Height*longScale.ScaleY<=longScroll.ViewportHeight;
        var initialFit=longScale.ScaleY;longPreview.Height=600;await Task.Delay(150);
        var expectedFit=Math.Min(1/VisualTreeHelper.GetDpi(longPreview).DpiScaleX,Math.Min((longScroll.ViewportWidth-24)/longImage.PixelWidth,(longScroll.ViewportHeight-24)/longImage.PixelHeight));
        results["fitTracksWindowResize"]=longScale.ScaleY>initialFit&&Math.Abs(longScale.ScaleY-expectedFit)<.00001;
        longPreview.Height=420;await Task.Delay(150);results["fitTracksSmallerWindow"]=longScale.ScaleY<initialFit&&longContent.Height*longScale.ScaleY<=longScroll.ViewportHeight;
        longPreview.Close();window.Hide();
        var interrupted=await new ImageLibrary(app.Store).AddAsync(image,"重启恢复用合成图");await app.Store.BeginOcrAsync(interrupted.Id);results["interruptedItemId"]=interrupted.Id;
        if(Environment.GetCommandLineArgs().Contains("--idle-memory-test")){await Task.Delay(92000);results["workingSetAfterIdleMiB"]=System.Diagnostics.Process.GetCurrentProcess().WorkingSet64/1048576.0;}
        File.WriteAllText(Path.Combine(root,"self-test.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
        var failedChecks=results.Where(p=>p.Value is bool success&&!success).Select(p=>p.Key).ToArray();
        if(failedChecks.Length>0)throw new InvalidOperationException("验收断言失败："+string.Join(", ",failedChecks));
    }
    private static void SaveVisual(Window window,string path)
    {
        window.UpdateLayout();var content=(FrameworkElement)window.Content;
        // Render keeps the root's layout offset; include its margins instead of clipping the right/bottom edges.
        int width=(int)Math.Ceiling(content.ActualWidth+content.Margin.Left+content.Margin.Right),height=(int)Math.Ceiling(content.ActualHeight+content.Margin.Top+content.Margin.Bottom);
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
        var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(window.Background,null,new Rect(0,0,width,height));bitmap.Render(background);bitmap.Render(content);bitmap.Freeze();ImageLibrary.WritePng(bitmap,path);
    }
    private static async Task<ScreenshotItem> WaitAsync(App app,string id,string status)
    {
        var timeout=DateTime.UtcNow.AddSeconds(45);while(DateTime.UtcNow<timeout){var current=await app.Store.GetAsync(id);if(current?.OcrStatus==status)return current;await Task.Delay(100);}throw new TimeoutException("识别状态未在45秒内变为"+status);
    }
    public static async Task ResumeAsync(App app)
    {
        var pending=await app.Store.PendingAsync();foreach(var item in pending)app.Ocr.Enqueue(item.Id);
        var recovered=new List<string>();foreach(var item in pending){var current=await WaitAsync(app,item.Id,"Ready");if(current.OcrText.Contains("课程"))recovered.Add(item.Id);}
        var report=new { recoveredCount=recovered.Count,recoveredIds=recovered,courseSearch=(await app.Store.QueryAsync("课程")).Count,notesSearch=(await app.Store.QueryAsync("保修凭证")).Count,missingImages=(await app.Store.MissingFilesAsync()).Count };
        File.WriteAllText(Path.Combine(app.Store.RootPath,"restart-test.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
    }
}
