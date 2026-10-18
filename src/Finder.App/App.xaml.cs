using System.Windows;
using Finder.Core.Interfaces;
using Finder.Core.Services;
using Finder.Platform.Windows.Hotkeys;
using Finder.Platform.Windows.Services;
using Finder.Platform.Windows.Theming;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using Finder.Search.Apps;
using Finder.Search.Files;
using Finder.Search.Settings;
using Finder.Search.Web;
using Finder.App.Views;

namespace Finder.App;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private GlobalHotkeyManager? _hotkeyManager;
    private WindowVisibilityService? _visibilityService;
    private SearchWindow? _searchWindow;
    private TrayIconService? _trayIcon;
    private WindowsThemeMonitor? _themeMonitor;

    public SearchService SearchService { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                name: $"Local\\Finder-{Environment.UserName}",
                createdNew: out var isFirstInstance);
            if (!isFirstInstance)
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                Shutdown();
                return;
            }

            _themeMonitor = new WindowsThemeMonitor();
            ApplyTheme(_themeMonitor.IsLightTheme);
            _themeMonitor.ThemeChanged += ThemeMonitor_ThemeChanged;
            SearchService = CreateSearchService();
            _searchWindow = new SearchWindow();
            _searchWindow.Show();
            _searchWindow.Hide();
            _visibilityService = new WindowVisibilityService(_searchWindow);
            _trayIcon = new TrayIconService(() => _visibilityService.Show(), () => Shutdown());
            _hotkeyManager = new GlobalHotkeyManager(_searchWindow);
            _hotkeyManager.HotkeyPressed += HotkeyManager_HotkeyPressed;
            var registered = _hotkeyManager.RegisterPreferredOrFallback();
            _trayIcon?.SetActiveShortcut(_hotkeyManager.ActiveShortcut);
            if (!registered)
            {
                System.Diagnostics.Trace.TraceError(
                    "Finder could not register Alt+Space or the configured fallback. The tray menu remains available.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Finder startup failed: {ex}");
            System.Windows.MessageBox.Show($"Finder could not start.\n\n{ex.Message}", "Finder startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_hotkeyManager is not null)
        {
            _hotkeyManager.HotkeyPressed -= HotkeyManager_HotkeyPressed;
            _hotkeyManager.Dispose();
        }
        _searchWindow?.Close();
        _trayIcon?.Dispose();
        if (_themeMonitor is not null)
        {
            _themeMonitor.ThemeChanged -= ThemeMonitor_ThemeChanged;
            _themeMonitor.Dispose();
        }
        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }
        base.OnExit(e);
    }

    private void HotkeyManager_HotkeyPressed(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() => _visibilityService?.Toggle());

    private static SearchService CreateSearchService() => new(
        new ISearchProvider[]
        {
            new AppSearchProvider(), new FileSearchProvider(),
            new SettingsSearchProvider(), new WebSearchProvider()
        },
        (name, error) => System.Diagnostics.Trace.TraceError($"Search provider {name} failed: {error}"));

    private void ThemeMonitor_ThemeChanged(bool isLight) =>
        Dispatcher.BeginInvoke(() => ApplyTheme(isLight));

    private void ApplyTheme(bool isLight)
    {
        var colors = isLight
            ? new Dictionary<string, object>
            {
                ["FinderBackgroundBrush"] = Brush(0xF2, 0xF4, 0xF7, 0xFA),
                ["FinderSurfaceBrush"] = Brush(0xFF, 0xF4, 0xF7, 0xFA),
                ["FinderTextBrush"] = Brush(0xFF, 0x20, 0x21, 0x24),
                ["FinderMutedTextBrush"] = Brush(0xFF, 0x66, 0x6B, 0x73),
                ["FinderPlaceholderBrush"] = Brush(0xFF, 0x85, 0x8B, 0x93),
                ["FinderAccentBrush"] = Brush(0xFF, 0x42, 0x67, 0xB2),
                ["FinderTextSelectionBrush"] = Brush(0x66, 0x42, 0x67, 0xB2),
                ["FinderBorderBrush"] = Brush(0x40, 0x7B, 0x84, 0x90),
                ["FinderSelectionBrush"] = Brush(0xFF, 0xE5, 0xEA, 0xF1),
                ["FinderHoverBrush"] = Brush(0xFF, 0xEC, 0xEF, 0xF3),
                ["FinderShadowColor"] = MediaColor.FromRgb(0x27, 0x31, 0x3F)
            }
            : new Dictionary<string, object>
            {
                ["FinderBackgroundBrush"] = Brush(0xF4, 0x20, 0x23, 0x28),
                ["FinderSurfaceBrush"] = Brush(0xFF, 0x20, 0x23, 0x28),
                ["FinderTextBrush"] = Brush(0xFF, 0xF2, 0xF4, 0xF7),
                ["FinderMutedTextBrush"] = Brush(0xFF, 0xB0, 0xB7, 0xC2),
                ["FinderPlaceholderBrush"] = Brush(0xFF, 0x98, 0xA0, 0xAB),
                ["FinderAccentBrush"] = Brush(0xFF, 0x91, 0xAD, 0xFF),
                ["FinderTextSelectionBrush"] = Brush(0x66, 0x91, 0xAD, 0xFF),
                ["FinderBorderBrush"] = Brush(0x50, 0xFF, 0xFF, 0xFF),
                ["FinderSelectionBrush"] = Brush(0xFF, 0x38, 0x3E, 0x48),
                ["FinderHoverBrush"] = Brush(0xFF, 0x2D, 0x32, 0x3B),
                ["FinderShadowColor"] = MediaColor.FromRgb(0x00, 0x00, 0x00)
            };

        foreach (var (key, value) in colors) Resources[key] = value;
    }

    private static SolidColorBrush Brush(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(MediaColor.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
