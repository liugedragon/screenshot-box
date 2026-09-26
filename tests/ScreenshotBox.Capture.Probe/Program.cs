using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotBox.App.Native;

namespace ScreenshotBox.Capture.Probe;

internal static class Program
{
    private static int _passed;
    private static int _failed;
    private static readonly Type SessionType = typeof(CaptureService).GetNestedType("CaptureSession", BindingFlags.NonPublic)!;
    private static readonly Type ModeType = typeof(CaptureService).GetNestedType("EditMode", BindingFlags.NonPublic)!;
    private static readonly Color Red = Color.FromRgb(230, 55, 55);
    private static readonly Color[] ColorsToTest = [Red, Color.FromRgb(23, 105, 194), Color.FromRgb(24, 167, 91), Color.FromRgb(244, 196, 48), Colors.White, Colors.Black];

    [STAThread]
    private static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            Test("preferences: persistence, bounds, invalid inputs", Preferences);
            foreach (var origin in new[] { new PixelPoint(0, 0), new PixelPoint(-320, -140) })
            {
                string label = $"desktop ({origin.X},{origin.Y})";
                Test($"{label}: exact crop/source alignment", () => AssertOriginal(new Script(origin).Export()));
                foreach (Color color in ColorsToTest)
                    Test($"{label}: pen RGB #{color.R:X2}{color.G:X2}{color.B:X2}", () => PenColor(origin, color));
                Test($"{label}: 7.5 px and 32 px pen widths", () => PenWidths(origin));
                Test($"{label}: arrow shaft and head", () => Arrow(origin));
                Test($"{label}: reverse rectangle edges and unfilled interior", () => Rectangle(origin));
                foreach (int block in new[] { 6, 32 })
                    Test($"{label}: mosaic {block} px block averages", () => Mosaic(origin, block));
                foreach (string mode in new[] { "Pen", "Mosaic", "Arrow", "Rectangle" })
                    Test($"{label}: eraser restores {mode}, undo/redo", () => Eraser(origin, mode));
                Test($"{label}: clear all, undo/redo, new action invalidates redo", () => History(origin));
            }
            Console.WriteLine($"RESULT: {_passed} passed, {_failed} failed. Synthetic pixels only; no desktop capture, files, clipboard, hotkeys, or overlay windows.");
            return _failed == 0 ? 0 : 1;
        }
        finally { app.Shutdown(); }
    }

    private static void Test(string name, Action test)
    {
        try { test(); _passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception ex)
        {
            _failed++;
            if (ex is TargetInvocationException { InnerException: not null } invocation) ex = invocation.InnerException!;
            Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
        }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private static void Preferences()
    {
        CaptureService.ConfigureTools("#1234AB", 7.5, 47.5, 17);
        var saved = CaptureService.GetToolPreferences();
        Require(saved == new CaptureToolPreferences("#1234AB", 7.5, 47.5, 17), "Custom preferences were not retained.");
        object session = NewSession(new PixelPoint(0, 0));
        Require(Get<double>(session, "PenWidth") == 7.5 && Get<double>(session, "EraserWidth") == 47.5 && Get<int>(session, "MosaicBlock") == 17,
            "New session did not inherit preferences.");
        Color color = Get<Color>(session, "PenColor");
        Require(color == Color.FromRgb(0x12, 0x34, 0xAB), "Session color differs from saved color.");
        Invoke(session, "SetPenWidth", 11.5); Invoke(session, "SetEraserWidth", 30d); Invoke(session, "SetMosaicBlock", 9d);
        Invoke(session, "SetPenColor", Color.FromArgb(0, 0x22, 0x77, 0x88));
        Require(CaptureService.GetToolPreferences() == new CaptureToolPreferences("#227788", 11.5, 30, 9), "Tool setters did not persist RGB preferences.");
        CaptureService.ConfigureTools("not-a-color", double.NaN, double.PositiveInfinity, 100);
        Require(CaptureService.GetToolPreferences() == new CaptureToolPreferences("#E63737", 2, 24, 32), "Invalid values did not fall back safely.");
        CaptureService.ConfigureTools("#000000", -9, 999, -8);
        Require(CaptureService.GetToolPreferences() == new CaptureToolPreferences("#000000", 1, 80, 6), "Configuration ranges were not clamped.");
        Invoke(session, "SetPenWidth", double.NaN); Invoke(session, "SetEraserWidth", double.PositiveInfinity); Invoke(session, "SetMosaicBlock", double.NaN);
        Require(Get<double>(session, "PenWidth") == 11.5 && Get<double>(session, "EraserWidth") == 30 && Get<int>(session, "MosaicBlock") == 9, "Session accepted non-finite values.");
        Invoke(session, "SetPenWidth", 99d); Invoke(session, "SetEraserWidth", -1d); Invoke(session, "SetMosaicBlock", 2d);
        Require(Get<double>(session, "PenWidth") == 32 && Get<double>(session, "EraserWidth") == 4 && Get<int>(session, "MosaicBlock") == 6, "Session setter bounds were not clamped.");
    }

    private static void PenColor(PixelPoint origin, Color color)
    {
        var script = new Script(origin).Color(color).Width(7.5).Draw("Pen", 35, 60, 150, 60);
        var image = script.Export();
        AssertColor(image, 90, 60, color);
        Require(image.At(90, 60) != Original(90, 60), "Pen did not change the image.");
        AssertOriginal(image, new PixelRect(0, 90, image.Width, 40));
    }

    private static void PenWidths(PixelPoint origin)
    {
        var narrow = new Script(origin).Color(Colors.Black).Width(7.5).Draw("Pen", 35, 60, 170, 60).Export();
        var wide = new Script(origin).Color(Colors.Black).Width(32).Draw("Pen", 35, 60, 170, 60).Export();
        int narrowRows = ChangedRows(narrow, 100, 30, 90), wideRows = ChangedRows(wide, 100, 30, 90);
        Require(narrowRows >= 7 && narrowRows <= 10, $"7.5 px pen changed {narrowRows} rows.");
        Require(wideRows >= 32 && wideRows <= 34, $"32 px pen changed {wideRows} rows.");
        AssertColor(wide, 100, 46, Colors.Black); AssertColor(wide, 100, 73, Colors.Black);
        Require(wide.At(100, 42) == Original(100, 42) && wide.At(100, 78) == Original(100, 78), "Wide pen leaked outside its width.");
    }

    private static int ChangedRows(Pixels image, int x, int top, int bottom)
    {
        int count = 0;
        for (int y = top; y < bottom; y++) if (image.At(x, y) != Original(x, y)) count++;
        return count;
    }

    private static void Arrow(PixelPoint origin)
    {
        var image = new Script(origin).Color(Red).Width(8).Draw("Arrow", 35, 85, 160, 85).Export();
        AssertColor(image, 90, 85, Red); AssertColor(image, 149, 81, Red);
        Require(image.At(149, 81) != Original(149, 81), "Arrowhead was not filled.");
        AssertOriginal(image, new PixelRect(45, 105, 80, 20));
    }

    private static void Rectangle(PixelPoint origin)
    {
        var image = new Script(origin).Color(Red).Width(8).Draw("Rectangle", 170, 130, 45, 45).Export();
        AssertColor(image, 90, 45, Red); AssertColor(image, 45, 90, Red);
        AssertColor(image, 170, 90, Red); AssertColor(image, 90, 130, Red);
        AssertOriginal(image, new PixelRect(55, 55, 100, 60));
    }

    private static void Mosaic(PixelPoint origin, int block)
    {
        var image = new Script(origin).MosaicSize(block).Draw("Mosaic", 37, 41, 142, 120).Export();
        Require(image.At(40, 45) != Original(40, 45), "Mosaic did not change the source.");
        for (int by = 41; by < 120; by += block)
            for (int bx = 37; bx < 142; bx += block)
            {
                int right = Math.Min(142, bx + block), bottom = Math.Min(120, by + block);
                long red = 0, green = 0, blue = 0; int count = (right - bx) * (bottom - by);
                for (int y = by; y < bottom; y++) for (int x = bx; x < right; x++)
                { var p = Original(x, y); red += p.R; green += p.G; blue += p.B; }
                var expected = new Rgb((byte)(red / count), (byte)(green / count), (byte)(blue / count));
                for (int y = by; y < bottom; y++) for (int x = bx; x < right; x++)
                    Require(image.At(x, y) == expected, $"Mosaic {block} block ({bx},{by}) wrong at ({x},{y}): {image.At(x, y)} vs {expected}.");
            }
        AssertOriginal(image, new PixelRect(0, 0, image.Width, 30));
        AssertOriginal(image, new PixelRect(150, 40, 60, 90));
    }

    private static void Eraser(PixelPoint origin, string mode)
    {
        var script = new Script(origin).Color(Red).Width(16);
        if (mode == "Mosaic") script.MosaicSize(12).Draw(mode, 35, 55, 150, 110);
        else if (mode == "Rectangle") script.Draw(mode, 150, 100, 35, 85);
        else script.Draw(mode, 35, 85, 150, 85);
        var before = script.Export();
        Require(before.At(90, 85) != Original(90, 85), $"{mode} setup did not affect the eraser center.");
        script.EraserWidth(24).Draw("Eraser", 90, 85, 90, 85);
        var erased = script.Export();
        AssertOriginal(erased, new PixelRect(85, 80, 10, 10));
        Require(erased.At(90, 85) != new Rgb(255, 255, 255), "Eraser painted white instead of restoring RGB.");
        for (int y = 0; y < erased.Height; y++) for (int x = 0; x < erased.Width; x++)
            if ((x - 90) * (x - 90) + (y - 85) * (y - 85) > 14 * 14)
                Require(erased.At(x, y) == before.At(x, y), $"Eraser changed unrelated pixel ({x},{y}).");
        script.Call("Undo"); AssertEqual(script.Export(), before, "Undo eraser");
        script.Call("Redo"); AssertEqual(script.Export(), erased, "Redo eraser");
        // A dragged eraser path must restore the original along its center, too.
        script.EraserWidth(10).Draw("Eraser", 55, 85, 75, 85);
        AssertOriginal(script.Export(), new PixelRect(55, 83, 20, 4));
    }

    private static void History(PixelPoint origin)
    {
        var script = new Script(origin).Color(Red).Width(8)
            .Draw("Pen", 30, 35, 100, 35)
            .Draw("Arrow", 35, 80, 150, 80)
            .Draw("Rectangle", 195, 150, 170, 120)
            .MosaicSize(6).Draw("Mosaic", 40, 130, 100, 185);
        var marked = script.Export();
        Require(marked.At(80, 35) != Original(80, 35) && marked.At(70, 150) != Original(70, 150), "History setup lacks annotations.");
        script.Call("ClearAnnotations"); AssertOriginal(script.Export());
        script.Call("Undo"); AssertEqual(script.Export(), marked, "Undo clear");
        script.Call("Redo"); AssertOriginal(script.Export());
        script.Call("Undo").Color(Colors.Black).Draw("Pen", 180, 185, 230, 185);
        var branched = script.Export();
        Require(branched.At(205, 185) != marked.At(205, 185), "New action was not committed.");
        script.CheckRedoDisabled().Call("Redo"); AssertEqual(script.Export(), branched, "Redo after new action");
    }

    private static object NewSession(PixelPoint origin)
    {
        var desktop = new PixelRect(origin.X, origin.Y, 320, 260);
        return Activator.CreateInstance(SessionType, BindingFlags.Instance | BindingFlags.Public, null,
            [Synthetic(), desktop, new[] { desktop }], null)!;
    }

    private static BitmapSource Synthetic()
    {
        const int width = 320, height = 260; var data = new byte[width * height * 4];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            var p = Source(x, y); int i = (y * width + x) * 4;
            data[i] = p.B; data[i + 1] = p.G; data[i + 2] = p.R;
            // This byte is deliberately not alpha. Real GDI captures also use Bgr32.
            data[i + 3] = (byte)((x + y) % 256);
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, data, width * 4);
        bitmap.Freeze(); return bitmap;
    }

    private static Rgb Source(int x, int y) => new((byte)(20 + (x * 3 + y * 11) % 180), (byte)(30 + (x * 5 + y * 7) % 170), (byte)(40 + (x * 7 + y * 3) % 160));
    private static Rgb Original(int x, int y) => Source(x + 24, y + 22);
    private static object? Invoke(object target, string name, params object[] args) => SessionType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public)!.Invoke(target, args);
    private static T Get<T>(object target, string name) => (T)SessionType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)!.GetValue(target)!;

    private static void AssertColor(Pixels image, int x, int y, Color expected) => Require(image.At(x, y) == new Rgb(expected.R, expected.G, expected.B), $"Pixel ({x},{y}) is {image.At(x, y)}, expected #{expected.R:X2}{expected.G:X2}{expected.B:X2}.");
    private static void AssertOriginal(Pixels image, PixelRect? area = null)
    {
        var region = area ?? new PixelRect(0, 0, image.Width, image.Height);
        for (int y = region.Top; y < region.Bottom; y++) for (int x = region.Left; x < region.Right; x++)
            Require(image.At(x, y) == Original(x, y), $"Original RGB not restored at ({x},{y}): {image.At(x, y)} vs {Original(x, y)}.");
    }
    private static void AssertEqual(Pixels actual, Pixels expected, string operation)
    {
        Require(actual.Width == expected.Width && actual.Height == expected.Height, $"{operation} changed image size.");
        for (int y = 0; y < actual.Height; y++) for (int x = 0; x < actual.Width; x++)
            Require(actual.At(x, y) == expected.At(x, y), $"{operation} differs at ({x},{y}).");
    }

    private readonly record struct Rgb(byte R, byte G, byte B);
    private sealed class Pixels
    {
        public readonly int Width, Height;
        private readonly byte[] _data;
        public Pixels(BitmapSource image)
        {
            Width = image.PixelWidth; Height = image.PixelHeight;
            var rgb = new FormatConvertedBitmap(image, PixelFormats.Bgr32, null, 0);
            _data = new byte[Width * Height * 4]; rgb.CopyPixels(_data, Width * 4, 0);
        }
        public Rgb At(int x, int y)
        { int i = (y * Width + x) * 4; return new(_data[i + 2], _data[i + 1], _data[i]); }
    }

    // Each export constructs a fresh real CaptureSession and replays operations, allowing
    // comparisons before/after history changes without bypassing the product's Accept path.
    private sealed class Script(PixelPoint origin)
    {
        private readonly List<Action<object>> _operations = [];
        public Script Call(string method, params object[] args) { _operations.Add(session => Invoke(session, method, args)); return this; }
        public Script Color(Color color) => Call("SetPenColor", color);
        public Script Width(double value) => Call("SetPenWidth", value);
        public Script EraserWidth(double value) => Call("SetEraserWidth", value);
        public Script MosaicSize(int value) => Call("SetMosaicBlock", (double)value);
        private PixelPoint Point(int x, int y) => new(origin.X + 24 + x, origin.Y + 22 + y);
        public Script Draw(string mode, int x1, int y1, int x2, int y2)
        {
            Call("SetMode", Enum.Parse(ModeType, mode));
            Call("Begin", Point(x1, y1)); Call("Update", Point(x2, y2)); Call("End", Point(x2, y2));
            return this;
        }
        public Script CheckRedoDisabled() { _operations.Add(session => Require(!Get<bool>(session, "CanRedo"), "Redo stack survived a new operation.")); return this; }
        public Pixels Export()
        {
            CaptureService.ConfigureTools("#E63737", 2, 24, 12);
            object session = NewSession(origin);
            Invoke(session, "Begin", Point(0, 0)); Invoke(session, "End", Point(272, 212));
            foreach (var operation in _operations) operation(session);
            Invoke(session, "Accept", "Copy");
            var completion = (TaskCompletionSource<CaptureResult?>)SessionType.GetField("_completion", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            Require(completion.Task.IsCompleted, "Accept did not complete the capture session.");
            CaptureResult result = completion.Task.GetAwaiter().GetResult() ?? throw new InvalidOperationException("Capture unexpectedly canceled.");
            Require(result.Action == "Copy" && result.Image.PixelWidth == 272 && result.Image.PixelHeight == 212, "Capture result has the wrong action or crop dimensions.");
            return new Pixels(result.Image);
        }
    }
}
