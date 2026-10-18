
using Finder.Core.Interfaces;
using Finder.Core.Models;

namespace Finder.Search.Settings;

public sealed class SettingsSearchProvider : ISearchProvider
{
    private static readonly (string[] Keywords, string Title, string Uri)[] Settings =
    [
        (["display", "screen", "brightness"], "Display settings", "ms-settings:display"),
        (["sound", "audio", "volume"], "Sound settings", "ms-settings:sound"),
        (["wifi", "wi-fi", "network", "internet"], "Network & internet settings", "ms-settings:network"),
        (["bluetooth"], "Bluetooth settings", "ms-settings:bluetooth"),
        (["windows update", "update"], "Windows Update", "ms-settings:windowsupdate"),
        (["installed apps", "apps", "uninstall"], "Installed apps", "ms-settings:appsfeatures"),
        (["default apps"], "Default apps", "ms-settings:defaultapps"),
        (["personalization", "wallpaper", "background", "theme"], "Personalization settings", "ms-settings:personalization"),
        (["privacy", "permissions"], "Privacy settings", "ms-settings:privacy"),
        (["storage", "disk space"], "Storage settings", "ms-settings:storagesense"),
        (["keyboard", "typing"], "Typing settings", "ms-settings:typing"),
        (["mouse", "touchpad"], "Mouse settings", "ms-settings:mousetouchpad"),
        (["accounts", "sign in"], "Account settings", "ms-settings:yourinfo"),
        (["startup apps", "startup"], "Startup apps", "ms-settings:startupapps")
    ];

    public string Name => "Settings";

    public Task<IEnumerable<SearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IEnumerable<SearchResult>>(
                Array.Empty<SearchResult>());
        }

        var normalizedQuery = query.Trim();

        var results = Settings
            .Where(setting => setting.Keywords.Any(keyword =>
                keyword.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                normalizedQuery.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .Select(setting => new SearchResult(
                Title: setting.Title,
                Description: "Windows Settings",
                Kind: "Settings",
                Target: setting.Uri))
            .DistinctBy(result => result.Target)
            .Take(10)
            .ToList();

        return Task.FromResult<IEnumerable<SearchResult>>(results);
    }
}
