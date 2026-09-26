using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Interop;
using ScreenshotBox.App.Native;

namespace ScreenshotBox.App;

// Exercises the same startup path with an isolated mutex and library; never saves user settings.
internal static class StartupSelfTest
{
    internal static async Task RunAsync(App app,MainWindow window,string[] args)
    {
        var results=new Dictionary<string,object>();
        var handle=new WindowInteropHelper(window).Handle;
        await Task.Delay(250);
        results["backgroundWindowNeverLoaded"]=!window.IsLoaded;
        results["backgroundWindowHidden"]=!window.IsVisible&&handle!=IntPtr.Zero&&!IsWindowVisible(handle);
        results["backgroundTrayAvailable"]=window.HasTrayIcon;
        results["backgroundHotkeyRegistered"]=window.RegisteredShortcut==app.Preferences.Shortcut;
        var competitor=new Window();
        using(var hotkey=new HotkeyService(competitor,()=>{}))
            results["registrationOwnedByHiddenWindow"]=!hotkey.TrySet(app.Preferences.Shortcut,out _);
        competitor.Close();
        var common=args.Where(a=>a!="--background").ToArray();
        results["backgroundDuplicateExited"]=await DuplicateAsync(common.Append("--background"));
        await Task.Delay(250);
        results["backgroundDuplicateDidNotOpenLibrary"]=!window.IsVisible&&!window.IsLoaded&&!IsWindowVisible(handle);
        results["manualDuplicateExited"]=await DuplicateAsync(common);
        for(int i=0;i<30&&!window.IsVisible;i++)await Task.Delay(100);
        await Task.Delay(300);
        results["manualDuplicateOpenedExistingLibrary"]=window.IsVisible&&window.IsLoaded&&IsWindowVisible(handle);
        results["runtimeSurvivesLibraryOpen"]=window.HasTrayIcon&&window.RegisteredShortcut==app.Preferences.Shortcut;
        window.Close();await Task.Delay(100);
        results["closeReturnsToTray"]=!window.IsVisible&&window.HasTrayIcon&&window.RegisteredShortcut==app.Preferences.Shortcut;
        results["backgroundDuplicateAfterCloseExited"]=await DuplicateAsync(common.Append("--background"));
        await Task.Delay(250);
        results["backgroundDuplicateAfterCloseStaysHidden"]=!window.IsVisible&&!IsWindowVisible(handle);
        results["language"]=L.Language;
        results["runtimeLocation"]=typeof(object).Assembly.Location;
        results["testScope"]="Windows process, native visibility, live tray and RegisterHotKey; no sign-out or Task Manager interaction";
        File.WriteAllText(Path.Combine(app.Store.RootPath,"startup-test.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
        if(results.Values.OfType<bool>().Any(v=>!v))throw new InvalidOperationException("Startup integration checks failed. See startup-test.json.");
    }

    private static async Task<bool> DuplicateAsync(IEnumerable<string> args)
    {
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false};
        foreach(var arg in args)start.ArgumentList.Add(arg);
        using var process=Process.Start(start)??throw new IOException("Unable to launch startup probe.");
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await process.WaitForExitAsync(timeout.Token);
        return process.ExitCode==0;
    }

    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
}
