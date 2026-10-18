using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Finder.Platform.Windows.Theming;

/// <summary>Observes the current user's Windows app theme without polling the registry.</summary>
public sealed class WindowsThemeMonitor : IDisposable
{
    private const string PersonalizationKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";
    private const uint RegNotifyChangeLastSet = 0x00000004;

    private readonly RegistryKey? _key;
    private readonly AutoResetEvent _changed = new(false);
    private readonly ManualResetEvent _stop = new(false);
    private readonly Thread? _watcher;
    private bool _disposed;

    public bool IsLightTheme => ReadPreference(fallback: true);
    public event Action<bool>? ThemeChanged;

    public WindowsThemeMonitor()
    {
        try { _key = Registry.CurrentUser.OpenSubKey(PersonalizationKey, writable: false); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { }

        if (_key is not null)
        {
            _watcher = new Thread(WatchRegistry) { IsBackground = true, Name = "Finder theme monitor" };
            _watcher.Start();
        }
    }

    private bool ReadPreference(bool fallback)
    {
        try
        {
            var value = _key?.GetValue(AppsUseLightThemeValue);
            return ParsePreference(value, fallback);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return fallback;
        }
    }

    internal static bool ParsePreference(object? value, bool fallback) => value switch
    {
        int number => number != 0,
        long number => number != 0,
        byte number => number != 0,
        _ => fallback
    };

    private void WatchRegistry()
    {
        var previous = ReadPreference(fallback: true);
        var waits = new WaitHandle[] { _changed, _stop };
        while (!_stop.WaitOne(0))
        {
            _changed.Reset();
            if (_key is null || RegNotifyChangeKeyValue(_key.Handle, false, RegNotifyChangeLastSet,
                    _changed.SafeWaitHandle, true) != 0)
                return;

            var signaled = WaitHandle.WaitAny(waits);
            if (signaled == 1) return;
            var current = ReadPreference(previous);
            if (current != previous)
            {
                previous = current;
                ThemeChanged?.Invoke(current);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stop.Set();
        _watcher?.Join();
        _key?.Dispose();
        _changed.Dispose();
        _stop.Dispose();
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegNotifyChangeKeyValue(
        SafeRegistryHandle key,
        [MarshalAs(UnmanagedType.Bool)] bool watchSubtree,
        uint notifyFilter,
        SafeWaitHandle eventHandle,
        [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
}
