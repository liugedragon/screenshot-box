using System.Windows.Threading;
namespace ScreenshotBox.App;

public partial class App : Application
{
    private Mutex? _instance; private EventWaitHandle? _show; private RegisteredWaitHandle? _wait;
    private bool _ownsInstance;
    public bool IsTesting {get;private set;}
    public bool DataTransition {get;set;}
    public Settings Preferences { get; private set; } = null!;
    public LibraryStore Store { get; private set; } = null!;
    public OcrQueue Ocr { get; private set; } = null!;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool testing=e.Args.Contains("--self-test")||e.Args.Contains("--resume-ocr-test");
        IsTesting=testing;
        DispatcherUnhandledException += (_, args) => { if(testing){File.WriteAllText(Path.Combine(Store?.RootPath??AppContext.BaseDirectory,"self-test-error.txt"),args.Exception.ToString());Shutdown(1);}else MessageBox.Show(L.Error(args.Exception), L.T("截图资料盒", "ScreenshotBox")); args.Handled=true; };
        var suffix = Environment.UserName+(testing?".Tests":"");
        _instance = new Mutex(false, "Local\\ScreenshotBox."+suffix);
        bool first;try{first=_instance.WaitOne(0);}catch(AbandonedMutexException){first=true;}_ownsInstance=first;
        _show = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\ScreenshotBox.Show."+suffix);
        if (!first) { _show.Set(); Shutdown(); return; }
        try {
            Preferences = Settings.Load();
            var languageArg=Array.IndexOf(e.Args,"--language");
            if(testing&&languageArg>=0&&languageArg+1<e.Args.Length)Preferences.Language=e.Args[languageArg+1];
            Preferences.Language=L.Normalize(Preferences.Language);L.Apply(Preferences.Language);
            string root = Preferences.DataDirectory.Length > 0 ? Preferences.DataDirectory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenshotBox", "library");
            var dataArg = Array.IndexOf(e.Args,"--data-dir"); if (dataArg>=0 && dataArg+1<e.Args.Length) root=e.Args[dataArg+1];
            Store = new LibraryStore(root); await Store.InitializeAsync(); Ocr = new OcrQueue(Store);
            if(e.Args.Contains("--resume-ocr-test")){await SelfTest.ResumeAsync(this);await Ocr.DisposeAsync();Store.Dispose();Shutdown();return;}
            if (e.Args.Contains("--self-test")) { await SelfTest.RunAsync(this); await Ocr.DisposeAsync(); Store.Dispose(); Shutdown(); return; }
            var window = new MainWindow(this); MainWindow = window; window.Show();
            Microsoft.Win32.SystemEvents.UserPreferenceChanged+=OnSystemPreferenceChanged;
            _wait = ThreadPool.RegisterWaitForSingleObject(_show, (_,_) => Dispatcher.BeginInvoke(() => window.ShowLibrary()), null, Timeout.Infinite, false);
            foreach (var item in await Store.PendingAsync()) Ocr.Enqueue(item.Id);
        } catch (Exception ex) {
            if(testing)File.WriteAllText(Path.Combine(Store?.RootPath ?? AppContext.BaseDirectory,"self-test-error.txt"),ex.ToString());
            else {
                var diagnostic=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenshotBox","diagnostics","startup-error.txt");
                try{Directory.CreateDirectory(Path.GetDirectoryName(diagnostic)!);File.WriteAllText(diagnostic,ex.ToString());}catch{}
                MessageBox.Show(L.T("无法打开资料库或加载运行组件。\n详细原因已保存到：\n", "Unable to open the library or load a required component.\nError details were saved to:\n")+diagnostic,L.T("启动失败", "Startup failed"));
            }
            Shutdown(1);
        }
    }
    public async Task QuitAsync()
    {
        if(MainWindow is MainWindow library)library.DisposeRuntimeResources();
        _wait?.Unregister(null); if (Ocr != null) await Ocr.DisposeAsync(); Store?.Dispose(); Shutdown();
    }
    public async Task RestartAsync()
    {
        if(MainWindow is MainWindow library)library.DisposeRuntimeResources();
        DataTransition=true;_wait?.Unregister(null);await Ocr.DisposeAsync();Store.Dispose();
        _show?.Dispose();_show=null;if(_ownsInstance){_instance?.ReleaseMutex();_ownsInstance=false;}_instance?.Dispose();_instance=null;
        var path=Environment.ProcessPath??throw new IOException(L.T("找不到应用程序路径，请退出后手动重启。", "The application path is unavailable. Exit and restart ScreenshotBox manually."));
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=true});Shutdown();
    }
    private void OnSystemPreferenceChanged(object sender,Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if(Preferences?.Theme=="System"&&!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(()=>AppearanceService.Apply("System"));
    }
    protected override void OnExit(ExitEventArgs e) { if(MainWindow is MainWindow library)library.DisposeRuntimeResources();Microsoft.Win32.SystemEvents.UserPreferenceChanged-=OnSystemPreferenceChanged;_show?.Dispose();if(_ownsInstance){_instance?.ReleaseMutex();_ownsInstance=false;}_instance?.Dispose(); base.OnExit(e); }
}
