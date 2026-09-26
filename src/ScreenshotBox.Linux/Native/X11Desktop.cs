using System.Runtime.InteropServices;
using SkiaSharp;
using ScreenshotBox.App.Native;

namespace ScreenshotBox.Linux.Native;

public sealed class DesktopFrame : IDisposable
{
    public SKBitmap Bitmap { get; }
    public PixelRect Bounds => new(0, 0, Bitmap.Width, Bitmap.Height);
    public DesktopFrame(SKBitmap bitmap) => Bitmap = bitmap;
    public void Dispose() => Bitmap.Dispose();
}

public static class X11Desktop
{
    public static DesktopFrame Capture()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("X11 capture requires Linux.");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            throw new InvalidOperationException("An X11 DISPLAY is required. Wayland capture is not supported.");
        var display = X11.XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero) throw new InvalidOperationException("Cannot connect to the X11 display.");
        IntPtr image = IntPtr.Zero;
        try
        {
            var root = X11.XDefaultRootWindow(display);
            if (X11.XGetGeometry(display, root, out _, out _, out _, out var width, out var height, out _, out _) == 0 || width == 0 || height == 0)
                throw new InvalidOperationException("Cannot read the X11 screen dimensions.");
            if (width > 32768 || height > 32768 || (long)width * height > 100_000_000)
                throw new InvalidOperationException("The X11 screen exceeds the capture size limit.");
            image = X11.XGetImage(display, root, 0, 0, width, height, nuint.MaxValue, 2);
            if (image == IntPtr.Zero) throw new InvalidOperationException("The X11 server did not return an image.");
            var source = Marshal.PtrToStructure<X11.XImage>(image);
            if (source.BitsPerPixel is not (16 or 24 or 32) || source.RedMask == 0 || source.GreenMask == 0 || source.BlueMask == 0)
                throw new NotSupportedException("The X11 visual must use a 16-, 24- or 32-bit TrueColor format.");
            var bytesPerPixel = source.BitsPerPixel / 8;
            var sourceBytes = new byte[checked(source.BytesPerLine * source.Height)];
            Marshal.Copy(source.Data, sourceBytes, 0, sourceBytes.Length);
            var bitmap = new SKBitmap((int)width, (int)height, SKColorType.Bgra8888, SKAlphaType.Opaque);
            try
            {
                var output = new byte[checked(bitmap.RowBytes * bitmap.Height)];
                for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var offset = y * source.BytesPerLine + x * bytesPerPixel;
                    uint value = 0;
                    for (var b = 0; b < bytesPerPixel; b++)
                        value |= (uint)sourceBytes[offset + b] << (8 * (source.ByteOrder == 0 ? b : bytesPerPixel - 1 - b));
                    var target = y * bitmap.RowBytes + 4 * x;
                    output[target] = Channel(value, source.BlueMask);
                    output[target + 1] = Channel(value, source.GreenMask);
                    output[target + 2] = Channel(value, source.RedMask);
                    output[target + 3] = 255;
                }
                Marshal.Copy(output, 0, bitmap.GetPixels(), output.Length);
                return new DesktopFrame(bitmap);
            }
            catch { bitmap.Dispose(); throw; }
        }
        finally
        {
            if (image != IntPtr.Zero) X11.XDestroyImage(image);
            X11.XCloseDisplay(display);
        }
    }

    private static byte Channel(uint value, nuint mask)
    {
        var bits = (uint)mask;
        var shift = System.Numerics.BitOperations.TrailingZeroCount(bits);
        var maximum = bits >> shift;
        return (byte)((((value & bits) >> shift) * 255UL + maximum / 2UL) / maximum);
    }
}

internal static class X11
{
    internal const string Library = "libX11.so.6";
    [DllImport(Library)] internal static extern IntPtr XOpenDisplay(IntPtr name);
    [DllImport(Library)] internal static extern int XCloseDisplay(IntPtr display);
    [DllImport(Library)] internal static extern nuint XDefaultRootWindow(IntPtr display);
    [DllImport(Library)] internal static extern int XGetGeometry(IntPtr display, nuint drawable, out nuint root, out int x, out int y, out uint width, out uint height, out uint border, out uint depth);
    [DllImport(Library)] internal static extern IntPtr XGetImage(IntPtr display, nuint drawable, int x, int y, uint width, uint height, nuint planes, int format);
    [DllImport(Library)] internal static extern int XDestroyImage(IntPtr image);
    [DllImport(Library)] internal static extern int XSync(IntPtr display, int discard);
    [DllImport(Library)] internal static extern int XPending(IntPtr display);
    [DllImport(Library)] internal static extern int XNextEvent(IntPtr display, IntPtr @event);
    [DllImport(Library)] internal static extern nuint XStringToKeysym([MarshalAs(UnmanagedType.LPStr)] string name);
    [DllImport(Library)] internal static extern byte XKeysymToKeycode(IntPtr display, nuint symbol);
    [DllImport(Library)] internal static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, nuint window, int ownerEvents, int pointerMode, int keyboardMode);
    [DllImport(Library)] internal static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, nuint window);
    [DllImport(Library)] internal static extern IntPtr XGetModifierMapping(IntPtr display);
    [DllImport(Library)] internal static extern int XFreeModifiermap(IntPtr map);
    [DllImport(Library)] internal static extern IntPtr XSetErrorHandler(IntPtr handler);
    [StructLayout(LayoutKind.Sequential)] internal struct XImage
    {
        internal int Width, Height, XOffset, Format;
        internal IntPtr Data;
        internal int ByteOrder, BitmapUnit, BitmapBitOrder, BitmapPad, Depth, BytesPerLine, BitsPerPixel;
        internal nuint RedMask, GreenMask, BlueMask;
        internal IntPtr ObData;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct XModifierKeymap
    {
        internal int MaxKeysPerModifier;
        internal IntPtr ModifierMap;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct XErrorEvent
    {
        internal int Type;
        internal IntPtr Display;
        internal nuint ResourceId, Serial;
        internal byte ErrorCode, RequestCode, MinorCode;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ErrorHandler(IntPtr display, IntPtr error);
}
