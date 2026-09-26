using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace ScreenshotBox.App.Native;

/// <summary>Registers a system shortcut without losing the previous one if replacement fails.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint NoRepeat = 0x4000;
    private readonly Window _owner;
    private readonly Action _trigger;
    private readonly IntPtr _window;
    private readonly HwndSource _source;
    private int _registration;
    private int _nextId = 0x5100;
    private bool _disposed;
    public string CurrentShortcut { get; private set; } = "";

    public HotkeyService(Window owner, Action trigger)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
        owner.Dispatcher.VerifyAccess();
        _window = new WindowInteropHelper(owner).EnsureHandle();
        _source = HwndSource.FromHwnd(_window) ?? throw new InvalidOperationException(L.T("窗口尚未初始化。", "The window has not been initialized."));
        _source.AddHook(OnMessage);
    }

    public bool TrySet(string shortcut, out string error)
    {
        _owner.Dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryParse(shortcut, out var modifiers, out var key, out var canonical, out error))
            return false;
        if (canonical == CurrentShortcut) return true;
        // Both registrations coexist briefly. A failed replacement leaves the old shortcut intact.
        int newId = _nextId++;
        if (_nextId > 0xBFFF) _nextId = 0x5100;
        if (!RegisterHotKey(_window, newId, modifiers | NoRepeat, key))
        {
            int code = Marshal.GetLastWin32Error();
            error = code == 1409
                ? L.T("这个快捷键已被系统或其他应用占用，请换一个组合。原快捷键仍然有效。", "This shortcut is already registered by Windows or another app. Choose another combination. Your current shortcut stays active.")
                : L.F("无法注册快捷键：{0}。原快捷键仍然有效。", "Could not register the shortcut: {0}. Your current shortcut stays active.",new Win32Exception(code).Message);
            return false;
        }
        int previous = _registration;
        _registration = newId;
        CurrentShortcut = canonical;
        if (previous != 0) UnregisterHotKey(_window, previous);
        return true;
    }

    internal static bool TryParse(string? text, out uint modifiers, out uint virtualKey,
        out string canonical, out string error)
    {
        modifiers = 0;
        virtualKey = 0;
        canonical = "";
        error = L.T("请输入组合快捷键，例如 Ctrl+Alt+S 或 Alt+A。", "Enter a key combination, such as Ctrl+Alt+S or Alt+A.");
        if (string.IsNullOrWhiteSpace(text)) return false;
        string[] parts = text.Split('+', StringSplitOptions.TrimEntries);
        string? keyPart = null;
        foreach (string part in parts)
        {
            if (part.Length == 0) return false;
            uint modifier = part.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => 2,
                "ALT" => 1,
                "SHIFT" => 4,
                "WIN" or "WINDOWS" => 8,
                _ => 0
            };
            if (modifier != 0)
            {
                if ((modifiers & modifier) != 0) { error = L.T("修饰键重复了。", "A modifier key was entered more than once."); return false; }
                modifiers |= modifier;
            }
            else if (keyPart is null) keyPart = part;
            else { error = L.T("一个快捷键只能包含一个主键。", "A shortcut can contain only one non-modifier key."); return false; }
        }
        if ((modifiers & 8) != 0)
        {
            error = L.T("Win 组合键通常用于 Windows 系统功能，请改用 Ctrl、Alt 或 Shift 的组合。", "Windows key combinations are reserved for system features. Use Ctrl, Alt or Shift instead.");
            return false;
        }
        if (modifiers == 0 || keyPart is null) return false;
        string normalizedKey = keyPart.ToUpperInvariant();
        if (normalizedKey.Length == 2 && normalizedKey[0] == 'D' && normalizedKey[1] >= '0' && normalizedKey[1] <= '9')
            normalizedKey = normalizedKey[1].ToString();
        if (normalizedKey.Length == 1 && normalizedKey[0] >= 'A' && normalizedKey[0] <= 'Z')
            virtualKey = normalizedKey[0];
        else if (normalizedKey.Length == 1 && normalizedKey[0] >= '0' && normalizedKey[0] <= '9')
            virtualKey = normalizedKey[0];
        else if (normalizedKey.StartsWith("NUMPAD", StringComparison.Ordinal) && normalizedKey.Length == 7
                 && normalizedKey[6] >= '0' && normalizedKey[6] <= '9')
        {
            virtualKey = (uint)(0x60 + normalizedKey[6] - '0');
            normalizedKey = "NumPad" + normalizedKey[6];
        }
        else if (normalizedKey.StartsWith('F') && int.TryParse(normalizedKey.AsSpan(1), out int function)
                 && function >= 1 && function <= 24)
            virtualKey = (uint)(0x70 + function - 1);
        else
        {
            var names = new Dictionary<string, (uint Key, string Name)>(StringComparer.OrdinalIgnoreCase)
            {
                ["SPACE"] = (0x20, "Space"), ["TAB"] = (9, "Tab"),
                ["ENTER"] = (13, "Enter"), ["RETURN"] = (13, "Enter"),
                ["ESC"] = (27, "Esc"), ["ESCAPE"] = (27, "Esc"),
                ["HOME"] = (0x24, "Home"), ["END"] = (0x23, "End"),
                ["PAGEUP"] = (0x21, "PageUp"), ["PAGEDOWN"] = (0x22, "PageDown"),
                ["INSERT"] = (0x2D, "Insert"), ["DELETE"] = (0x2E, "Delete"),
                ["DEL"] = (0x2E, "Delete"), ["BACKSPACE"] = (8, "Backspace"),
                ["UP"] = (0x26, "Up"), ["DOWN"] = (0x28, "Down"),
                ["LEFT"] = (0x25, "Left"), ["RIGHT"] = (0x27, "Right")
            };
            if (!names.TryGetValue(normalizedKey, out var named))
            {
                error = L.T("主键支持字母、数字、F1–F24，以及 Space、Tab、Enter、方向键等常用按键。", "Use a letter, digit, F1–F24, or a common key such as Space, Tab, Enter or an arrow key.");
                return false;
            }
            virtualKey = named.Key;
            normalizedKey = named.Name;
        }
        if ((modifiers & 1) != 0 && (virtualKey is 9 or 0x73 or 0x20 or 27)
            || (modifiers & 3) == 3 && virtualKey == 0x2E
            || (modifiers & 6) == 6 && virtualKey == 27
            || (modifiers & 2) != 0 && virtualKey == 27
            || virtualKey == 0x7B)
        {
            error = L.T("这个组合用于系统操作，请选择其他快捷键。", "This combination is reserved for Windows. Choose another shortcut.");
            return false;
        }
        var pieces = new List<string>();
        if ((modifiers & 2) != 0) pieces.Add("Ctrl");
        if ((modifiers & 1) != 0) pieces.Add("Alt");
        if ((modifiers & 4) != 0) pieces.Add("Shift");
        pieces.Add(normalizedKey);
        canonical = string.Join('+', pieces);
        error = "";
        return true;
    }

    private IntPtr OnMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (!_disposed && message == WmHotkey && wParam.ToInt32() == _registration)
        {
            handled = true;
            // Run outside the native hook, so capture/dialog creation cannot reenter a window procedure.
            _owner.Dispatcher.BeginInvoke(_trigger);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _owner.Dispatcher.VerifyAccess();
        _disposed = true;
        if (_registration != 0) UnregisterHotKey(_window, _registration);
        _source.RemoveHook(OnMessage);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
