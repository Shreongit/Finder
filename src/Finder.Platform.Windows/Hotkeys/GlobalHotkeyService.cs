
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Finder.Platform.Windows.Hotkeys;

public readonly record struct HotkeyBinding(string DisplayName, uint Modifiers, uint VirtualKey);

public sealed class GlobalHotkeyManager : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const int ErrorHotkeyAlreadyRegistered = 1409;
    private const int HotkeyId = 0x4649;
    private static readonly HotkeyBinding PreferredHotkey = new("Alt+Space", ModAlt | ModNoRepeat, 0x20);
    private static readonly HotkeyBinding DefaultFallbackHotkey = new("Ctrl+Alt+Space", ModControl | ModAlt | ModNoRepeat, 0x20);

    private readonly Window _window;
    private readonly HwndSource _source;
    private readonly HotkeyBinding _fallbackHotkey;
    private bool _registrationAttempted;
    private bool _registered;
    private bool _disposed;

    public event EventHandler? HotkeyPressed;
    public string? ActiveShortcut { get; private set; }

    public GlobalHotkeyManager(Window window, HotkeyBinding? fallbackHotkey = null)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _fallbackHotkey = fallbackHotkey ?? DefaultFallbackHotkey;
        if (string.IsNullOrWhiteSpace(_fallbackHotkey.DisplayName) || _fallbackHotkey.VirtualKey == 0)
        {
            throw new ArgumentException("A fallback hotkey must have a display name and virtual key.", nameof(fallbackHotkey));
        }

        var helper = new WindowInteropHelper(_window);
        var handle = helper.Handle;

        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "The window handle is not available. Show the window before registering a hotkey.");
        }

        _source = HwndSource.FromHwnd(handle)
            ?? throw new InvalidOperationException(
                "Unable to obtain the window message source.");

        _source.AddHook(WindowProc);
    }

    public bool RegisterPreferredOrFallback()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_registrationAttempted)
        {
            return _registered;
        }

        _registrationAttempted = true;
        var handle = new WindowInteropHelper(_window).Handle;

        if (TryRegister(handle, PreferredHotkey, out _))
        {
            return true;
        }

        if (!HasSameKey(_fallbackHotkey, PreferredHotkey) && TryRegister(handle, _fallbackHotkey, out _))
        {
            return true;
        }

        // Each failed RegisterHotKey call has already logged its Win32 error code.
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _source.RemoveHook(WindowProc);

        if (_registered)
        {
            if (!UnregisterHotKey(new WindowInteropHelper(_window).Handle, HotkeyId))
            {
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Trace.TraceError(
                    "Finder failed to unregister {0}; Win32 error {1}: {2}.",
                    ActiveShortcut, error, new Win32Exception(error).Message);
            }
            _registered = false;
            ActiveShortcut = null;
        }
    }

    private bool TryRegister(IntPtr handle, HotkeyBinding hotkey, out int errorCode)
    {
        if (RegisterHotKey(handle, HotkeyId, hotkey.Modifiers, hotkey.VirtualKey))
        {
            _registered = true;
            ActiveShortcut = hotkey.DisplayName;
            errorCode = 0;
            return true;
        }

        errorCode = Marshal.GetLastWin32Error();
        var reason = errorCode == ErrorHotkeyAlreadyRegistered
            ? "already registered by another application or thread"
            : "registration failed";
        System.Diagnostics.Trace.TraceWarning(
            "Finder hotkey {0} {1}; Win32 error {2}: {3}.",
            hotkey.DisplayName, reason, errorCode, new Win32Exception(errorCode).Message);
        return false;
    }

    private static bool HasSameKey(HotkeyBinding left, HotkeyBinding right) =>
        left.Modifiers == right.Modifiers && left.VirtualKey == right.VirtualKey;

    private IntPtr WindowProc(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        IntPtr hWnd,
        int id,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(
        IntPtr hWnd,
        int id);
}
