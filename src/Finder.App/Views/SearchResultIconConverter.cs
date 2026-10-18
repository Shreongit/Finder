using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Finder.Core.Models;

namespace Finder.App.Views;

public sealed class SearchResultIconConverter : IValueConverter
{
    private const int CacheLimit = 256;
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> CacheOrder = new();

    public object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is not SearchResult { Target: { Length: > 0 } target } result || result.Kind is not ("App" or "File" or "Folder"))
            return null;

        var key = $"{result.Kind}\0{target}";
        lock (CacheLock)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var icon = LoadIcon(result.Kind, target);
            Cache[key] = icon;
            CacheOrder.Enqueue(key);
            while (Cache.Count > CacheLimit) Cache.Remove(CacheOrder.Dequeue());
            return icon;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();

    private static ImageSource? LoadIcon(string kind, string path)
    {
        try
        {
            var iconPath = kind == "App" ? ResolveShortcutTarget(path) : path;
            if (string.IsNullOrWhiteSpace(iconPath)) iconPath = path;
            var attributes = kind == "Folder" ? FileAttributes.Directory : (FileAttributes)0;
            var result = SHGetFileInfo(iconPath, attributes, out var info, (uint)Marshal.SizeOf<ShellFileInfo>(), 0x100);
            if (result == IntPtr.Zero || info.Icon == IntPtr.Zero) return null;
            try
            {
                var bitmap = Imaging.CreateBitmapSourceFromHIcon(info.Icon, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
                bitmap.Freeze();
                return bitmap;
            }
            finally { DestroyIcon(info.Icon); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string? ResolveShortcutTarget(string shortcut)
    {
        if (!Path.GetExtension(shortcut).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) return shortcut;
        object? shell = null;
        object? link = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return null;
            shell = Activator.CreateInstance(shellType);
            if (shell is null) return null;
            dynamic automation = shell;
            link = automation.CreateShortcut(shortcut);
            dynamic shortcutInfo = link;
            string target = shortcutInfo.TargetPath;
            return File.Exists(target) ? target : null;
        }
        catch { return null; }
        finally
        {
            if (link is not null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, FileAttributes attributes, out ShellFileInfo info, uint fileInfoSize, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
