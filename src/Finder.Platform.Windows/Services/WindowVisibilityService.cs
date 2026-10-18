using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Finder.Platform.Windows.Services;

public sealed class WindowVisibilityService
{
    private readonly Window _window;

    public WindowVisibilityService(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public void Show()
    {
        if (!_window.IsVisible)
        {
            _window.Show();
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();

        var handle = new WindowInteropHelper(_window).Handle;

        if (handle != IntPtr.Zero)
        {
            NativeMethods.SetForegroundWindow(handle);
        }

        _window.Dispatcher.BeginInvoke(() =>
        {
            _window.Activate();

            if (_window.FindName("SearchInput") is IInputElement searchInput)
            {
                Keyboard.Focus(searchInput);
            }
        });
    }

    public void Hide()
    {
        if (_window.IsVisible)
        {
            _window.Hide();
        }
    }

    public void Toggle()
    {
        if (_window.IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}