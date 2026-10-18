using System.Diagnostics;
using System.IO;

namespace Finder.Platform.Windows.Services;

public sealed class WindowsLauncher
{
    public bool Launch(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool LaunchApplicationShortcut(string path) =>
        File.Exists(path) && Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase) && Launch(path);

    public bool OpenFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        return Launch(path);
    }

    public bool OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        return Launch(path);
    }

    public bool OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp &&
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return Launch(uri.AbsoluteUri);
    }

    public bool OpenWindowsSettings(string settingsUri)
    {
        if (!Uri.TryCreate(settingsUri, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Scheme.Equals(
                "ms-settings",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Launch(settingsUri);
    }
}
