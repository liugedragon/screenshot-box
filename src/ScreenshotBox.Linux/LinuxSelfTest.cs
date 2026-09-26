using System.Text.Json;
using System.Diagnostics;
using Avalonia.Input.Platform;
using SkiaSharp;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace ScreenshotBox.Linux;

internal static class LinuxSelfTest
{
    public static async Task ReadClipboardAsync(MainWindow window)
    {
        var path = App.Arguments.SkipWhile(x => x != "--clipboard-read").Skip(1).First();
        try
        {
            using var bitmap = window.Clipboard is null ? null : await window.Clipboard.TryGetBitmapAsync();
            if (bitmap is null) throw new IOException("Native clipboard did not contain a bitmap.");
            bitmap.Save(path, PngBitmapEncoderOptions.Default);
        }
        catch (Exception ex) { File.WriteAllText(path + ".error.txt", ex.ToString()); App.TestExitCode = 1; }
        finally { window.Close(); }
    }
    public static async Task RunAsync(MainWindow window)
    {
        var output = App.Arguments.SkipWhile(x => x != "--self-test").Skip(1).FirstOrDefault();
        if (output is null) throw new ArgumentException("--self-test requires a result path.");
        var checks = new List<string>();
        try
        {
            L.Apply("en-US"); Check(L.T("跟随系统", "Follow system") == "Follow system", "English settings label");
            L.Apply("zh-CN"); Check(L.T("保存", "Save") == "保存", "Chinese localization");
            var oldLocale = Environment.GetEnvironmentVariable("LC_ALL");
            try
            {
                Environment.SetEnvironmentVariable("LC_ALL", "zh_TW.UTF-8"); L.Apply("System"); Check(L.IsChinese, "Chinese system language defaults to Chinese");
                Environment.SetEnvironmentVariable("LC_ALL", "fr_FR.UTF-8"); L.Apply("System"); Check(!L.IsChinese, "Other system languages default to English");
            }
            finally { Environment.SetEnvironmentVariable("LC_ALL", oldLocale); L.Apply(App.Settings.Language); }
            var oldSession = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
            try
            {
                Environment.SetEnvironmentVariable("XDG_SESSION_TYPE", "wayland");
                using var disabled = new ScreenshotBox.Linux.Native.GlobalHotkeyService(() => { });
                Check(!disabled.TrySet("Ctrl+Alt+J", out _), "Wayland guard prevents XWayland global grabs");
            }
            finally { Environment.SetEnvironmentVariable("XDG_SESSION_TYPE", oldSession); }
            Check(window.ClientSize.Width >= 720, "Native window initialized");
            using var fixture = new SKBitmap(80, 48);
            fixture.Erase(SKColors.CornflowerBlue);
            fixture.SetPixel(3, 4, SKColors.OrangeRed);
            var png = Path.ChangeExtension(output, ".fixture.png");
            LinuxImageLibrary.WritePngAtomic(fixture, png);
            var imported = await new LinuxImageLibrary(window.Store).AddFileAsync(png, "Clipboard fixture");
            Check(imported.Width == 80 && imported.Height == 48, "UI library imports actual PNG dimensions");
            await window.Store.UpdateMetadataAsync(imported.Id, "课程 Example", "order 25% _ \"", "demo,资料", true);
            Check((await window.Store.QueryAsync("课程")).Count == 1, "Chinese metadata search through UI library");
            Check((await window.Store.QueryAsync("%")).Count == 1, "Literal symbol search through UI library");
            Check(window.Clipboard is not null, "Native clipboard available");
            await window.Clipboard!.SetBitmapAsync(new Bitmap(png));
            var readerRoot = Path.Combine(Path.GetDirectoryName(output)!, "clipboard-reader");
            Directory.CreateDirectory(readerRoot);
            var readerSettings = Path.Combine(readerRoot, "settings.json");
            new AppSettings { Language = "en-US", DataDirectory = Path.Combine(readerRoot, "library") }.Save(readerSettings);
            var clipboardResult = Path.Combine(readerRoot, "clipboard.png");
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(typeof(App).Assembly.Location);
            foreach (var argument in new[] { "--settings", readerSettings, "--clipboard-read", clipboardResult }) start.ArgumentList.Add(argument);
            using var reader = Process.Start(start)!;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await reader.WaitForExitAsync(deadline.Token);
            Check(reader.ExitCode == 0 && File.Exists(clipboardResult), "Independent process reads native image clipboard");
            using var roundtrip = SKBitmap.Decode(clipboardResult);
            Check(roundtrip.Width == 80 && roundtrip.Height == 48 && roundtrip.GetPixel(3, 4) == SKColors.OrangeRed, "Clipboard preserves image dimensions and pixels");
            var duplicateStart = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") duplicateStart.ArgumentList.Add(typeof(App).Assembly.Location);
            duplicateStart.ArgumentList.Add("--settings"); duplicateStart.ArgumentList.Add(App.SettingsFile!);
            using (var duplicate = Process.Start(duplicateStart)!)
            {
                using var duplicateDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await duplicate.WaitForExitAsync(duplicateDeadline.Token);
                Check(duplicate.ExitCode == 0, "Second launch activates existing instance and exits");
            }
            await window.Store.SetDeletedAsync(imported.Id, true);
            Check((await window.Store.QueryAsync("课程")).Count == 0, "Trash excluded from normal search");
            await window.Store.SetDeletedAsync(imported.Id, false);
            Check((await window.Store.QueryAsync("课程")).Count == 1, "Restored item searchable");
            var selected = await window.Store.GetAsync(imported.Id);
            window.SelectItem(selected!);
            var editor = window.GetVisualDescendants().OfType<TextBox>().First(x => x.PlaceholderText == L.T("备注", "Notes"));
            editor.Text = "Unsaved notes draft"; editor.Focus(); editor.SelectionStart = editor.SelectionEnd = 6;
            await window.RefreshAsync(); window.ShowDetails(selected);
            Check(window.GetVisualDescendants().OfType<TextBox>().First(x => x.PlaceholderText == L.T("备注", "Notes")).Text == "Unsaved notes draft", "Metadata draft survives background refresh");
            Check(editor.IsFocused && editor.SelectionStart == 6, "Metadata focus and caret survive OCR refresh");
            window.ResizeClient(new Size(760, window.ClientSize.Height));
            for (int attempt = 0; attempt < 20 && window.ClientSize.Width > 800; attempt++) { await Task.Delay(100); window.ResizeClient(new Size(760, window.ClientSize.Height)); }
            Check(window.ClientSize.Width <= 800, $"Narrow window layout (client {window.ClientSize.Width:0})");
            await Task.Delay(150);
            var xdisplay = ScreenshotBox.Linux.Native.X11.XOpenDisplay(IntPtr.Zero);
            try
            {
                Check(ScreenshotBox.Linux.Native.X11.XGetGeometry(xdisplay, (nuint)window.TryGetPlatformHandle()!.Handle,
                    out _, out _, out _, out var actualWidth, out _, out _, out _) != 0 && actualWidth <= 800 * window.RenderScaling,
                    "X11 confirms actual narrow native window pixels");
            }
            finally { ScreenshotBox.Linux.Native.X11.XCloseDisplay(xdisplay); }
            using (var narrow = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height)))
            { narrow.Render(window); narrow.Save(Path.ChangeExtension(output, ".narrow.png"), PngBitmapEncoderOptions.Default); }
            window.ResizeClient(new Size(1200, window.ClientSize.Height)); await Task.Delay(150);
            await window.RefreshAsync(); await Task.Delay(250);
            using var render = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height)); render.Render(window);
            render.Save(Path.ChangeExtension(output, ".png"), PngBitmapEncoderOptions.Default);
            Check(render.PixelSize.Width > 600 && render.PixelSize.Height > 400, "Native client rendering");
            File.WriteAllText(output, JsonSerializer.Serialize(new { success = true, checks, environment = Environment.OSVersion.ToString(), language = L.Language, scope = "App component checks; no external mouse/keyboard automation" }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { File.WriteAllText(output, JsonSerializer.Serialize(new { success = false, checks, error = ex.ToString() })); App.TestExitCode = 1; }
        finally { window.Close(); }
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks.Add(name); }
    }
}
