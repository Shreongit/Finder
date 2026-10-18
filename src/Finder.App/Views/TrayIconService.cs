using System.Drawing;
using System.IO;
using Forms = System.Windows.Forms;

namespace Finder.App.Views;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Stream _iconStream;
    private readonly Icon _applicationIcon;
    private readonly Forms.ToolStripMenuItem _openItem;
    private readonly Action _show;
    private readonly Action _exit;

    public TrayIconService(Action show, Action exit)
    {
        _show = show ?? throw new ArgumentNullException(nameof(show));
        _exit = exit ?? throw new ArgumentNullException(nameof(exit));
        var iconResource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/Icons/Finder.ico", UriKind.Absolute))
            ?? throw new FileNotFoundException("Finder's application icon resource was not found.");
        _iconStream = iconResource.Stream;
        try
        {
            _applicationIcon = new Icon(_iconStream);
        }
        catch
        {
            _iconStream.Dispose();
            throw;
        }

        var menu = new Forms.ContextMenuStrip();
        _openItem = new Forms.ToolStripMenuItem("Open Finder", null, (_, _) => _show());
        menu.Items.Add(_openItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => _exit());
        _icon = new Forms.NotifyIcon
        {
            Text = "Finder",
            Icon = _applicationIcon,
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => _show();
    }

    public void SetActiveShortcut(string? shortcut)
    {
        _openItem.Text = string.IsNullOrWhiteSpace(shortcut)
            ? "Open Finder"
            : $"Open Finder ({shortcut})";
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _applicationIcon.Dispose();
        _iconStream.Dispose();
    }
}
