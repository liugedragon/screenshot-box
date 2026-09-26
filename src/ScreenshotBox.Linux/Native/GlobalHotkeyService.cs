using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace ScreenshotBox.Linux.Native;

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly Action _callback;
    private readonly object _gate = new();
    private static readonly object ErrorHandlerGate = new();
    private readonly IntPtr _display;
    private readonly nuint _root;
    private readonly Thread? _thread;
    private volatile bool _disposed;
    private byte _key;
    private uint _modifiers;
    private uint[] _lockVariants = [0];
    public string CurrentShortcut { get; private set; } = "";

    public GlobalHotkeyService(Action callback)
    {
        _callback = callback;
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") == "wayland" || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))) return;
        _display = X11.XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero) return;
        _root = X11.XDefaultRootWindow(_display);
        _thread = new Thread(Pump) { IsBackground = true, Name = "ScreenshotBox X11 hotkey" };
        _thread.Start();
    }

    public bool TrySet(string shortcut, out string error)
    {
        error = "";
        lock (_gate)
        {
            if (_disposed || _display == IntPtr.Zero)
            {
                error = L.T("无法连接 X11；Wayland 不支持此快捷键注册。", "Cannot connect to X11; this hotkey registration does not support Wayland.");
                return false;
            }
            if (!Parse(shortcut, out var symbol, out var modifiers, out var normalized))
            {
                error = L.T("请输入组合键，例如 Ctrl+Alt+S 或 Alt+A。", "Enter a shortcut such as Ctrl+Alt+S or Alt+A.");
                return false;
            }
            if (normalized is "Ctrl+Alt+Delete" or "Ctrl+Alt+BackSpace" or "Super+L")
            {
                error = L.T("该组合通常由桌面环境保留，请选择其他快捷键。", "This combination is normally reserved by the desktop. Choose another shortcut.");
                return false;
            }
            var key = X11.XKeysymToKeycode(_display, X11.XStringToKeysym(symbol));
            if (key == 0)
            {
                error = L.T("当前 X11 键盘布局没有这个按键。", "This key is unavailable in the current X11 keyboard layout.");
                return false;
            }
            if (_key == key && _modifiers == modifiers) { CurrentShortcut = normalized; return true; }
            var variants = LockVariants();
            var badAccess = false;
            var otherError = false;
            lock (ErrorHandlerGate)
            {
                IntPtr previous = IntPtr.Zero;
                X11.ErrorHandler handler = (display, pointer) =>
                {
                    var e = Marshal.PtrToStructure<X11.XErrorEvent>(pointer);
                    if (display == _display)
                    {
                        if (e.ErrorCode == 10) badAccess = true;
                        else otherError = true;
                        return 0;
                    }
                    if (previous != IntPtr.Zero) return Marshal.GetDelegateForFunctionPointer<X11.ErrorHandler>(previous)(display, pointer);
                    return 0;
                };
                // XGrabKey reports asynchronous errors: force delivery before restoring the handler.
                X11.XSync(_display, 0);
                previous = X11.XSetErrorHandler(Marshal.GetFunctionPointerForDelegate(handler));
                try
                {
                    foreach (var variant in variants) X11.XGrabKey(_display, key, modifiers | variant, _root, 0, 1, 1);
                    X11.XSync(_display, 0);
                    if (badAccess || otherError)
                    {
                        foreach (var variant in variants) X11.XUngrabKey(_display, key, modifiers | variant, _root);
                        X11.XSync(_display, 0);
                    }
                }
                finally { X11.XSetErrorHandler(previous); GC.KeepAlive(handler); }
            }
            if (badAccess || otherError)
            {
                error = badAccess
                    ? L.T("快捷键已被 X11 应用或桌面环境占用；原快捷键保留。", "The shortcut is already grabbed by an X11 application or desktop; the previous shortcut is unchanged.")
                    : L.T("X11 无法注册这个快捷键；原快捷键保留。", "X11 could not register this shortcut; the previous shortcut is unchanged.");
                return false;
            }
            UngrabCurrent();
            _key = key; _modifiers = modifiers; _lockVariants = variants;
            CurrentShortcut = normalized;
            return true;
        }
    }

    private static bool Parse(string text, out string symbol, out uint modifiers, out string normalized)
    {
        symbol = ""; modifiers = 0; normalized = "";
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        foreach (var p in parts[..^1])
        {
            uint mask = p.ToLowerInvariant() switch { "ctrl" or "control" => 4, "alt" => 8, "shift" => 1, "super" or "win" => 64, _ => 0 };
            if (mask == 0 || (modifiers & mask) != 0) return false;
            modifiers |= mask;
        }
        var key = parts[^1];
        if (key.Length == 1 && char.IsAsciiLetterOrDigit(key[0])) symbol = key.ToLowerInvariant();
        else if (key.StartsWith('F') || key.StartsWith('f'))
        {
            if (!int.TryParse(key[1..], out var f) || f is < 1 or > 24) return false;
            symbol = "F" + f;
        }
        else symbol = key.ToLowerInvariant() switch { "print" or "prtsc" or "printscreen" => "Print", "space" => "space", "delete" or "del" => "Delete", "backspace" => "BackSpace", "insert" => "Insert", "home" => "Home", "end" => "End", _ => "" };
        if (symbol.Length == 0) return false;
        var names = new List<string>();
        if ((modifiers & 4) != 0) names.Add("Ctrl");
        if ((modifiers & 8) != 0) names.Add("Alt");
        if ((modifiers & 1) != 0) names.Add("Shift");
        if ((modifiers & 64) != 0) names.Add("Super");
        names.Add(symbol.Length == 1 ? symbol.ToUpperInvariant() : symbol);
        normalized = string.Join('+', names);
        return true;
    }

    private uint[] LockVariants()
    {
        var masks = new HashSet<uint> { 2 }; // CapsLock
        var map = X11.XGetModifierMapping(_display);
        try
        {
            if (map != IntPtr.Zero)
            {
                var keys = Marshal.PtrToStructure<X11.XModifierKeymap>(map);
                foreach (var name in new[] { "Num_Lock", "Scroll_Lock" })
                {
                    var code = X11.XKeysymToKeycode(_display, X11.XStringToKeysym(name));
                    if (code == 0) continue;
                    for (var modifier = 0; modifier < 8; modifier++)
                    for (var slot = 0; slot < keys.MaxKeysPerModifier; slot++)
                        if (Marshal.ReadByte(keys.ModifierMap, modifier * keys.MaxKeysPerModifier + slot) == code) masks.Add((uint)1 << modifier);
                }
            }
        }
        finally { if (map != IntPtr.Zero) X11.XFreeModifiermap(map); }
        var variants = new HashSet<uint> { 0 };
        foreach (var mask in masks) foreach (var value in variants.ToArray()) variants.Add(value | mask);
        return variants.Order().ToArray();
    }

    private void Pump()
    {
        var buffer = Marshal.AllocHGlobal(192); // sizeof(XEvent) on Linux x64
        try
        {
            while (!_disposed)
            {
                var fire = false;
                lock (_gate)
                {
                    if (_disposed) break;
                    while (X11.XPending(_display) > 0)
                    {
                        X11.XNextEvent(_display, buffer);
                        if (Marshal.ReadInt32(buffer) == 2 && Marshal.ReadInt32(buffer, 84) == _key)
                        {
                            var state = (uint)Marshal.ReadInt32(buffer, 80);
                            var locks = _lockVariants.Aggregate(0u, (a, b) => a | b);
                            if ((state & ~locks & 255) == _modifiers) fire = true;
                        }
                    }
                }
                if (fire) Dispatcher.UIThread.Post(() => { if (!_disposed) _callback(); });
                Thread.Sleep(25);
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private void UngrabCurrent()
    {
        if (_key == 0) return;
        foreach (var variant in _lockVariants) X11.XUngrabKey(_display, _key, _modifiers | variant, _root);
        X11.XSync(_display, 0);
    }
    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; }
        _thread?.Join(1000);
        lock (_gate)
        {
            if (_display != IntPtr.Zero) { UngrabCurrent(); X11.XCloseDisplay(_display); }
        }
    }
}
