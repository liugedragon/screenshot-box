using Avalonia;

namespace ScreenshotBox.Linux;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(a => a is "--self-test" or "--demo-output" or "--clipboard-read") && !args.Contains("--settings"))
        { Console.Error.WriteLine("Test and demo modes require --settings with an isolated settings file."); return 2; }
        App.Arguments = args;
        try { var code = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); return App.TestExitCode == 0 ? code : App.TestExitCode; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
