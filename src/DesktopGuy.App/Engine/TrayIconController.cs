using System;
using System.IO;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DesktopGuy.App.Engine;

/// <summary>
/// A classic system-tray icon so the character's right-click menu stays
/// reachable even when the window itself is hidden (e.g. auto-hidden
/// behind a fullscreen game - see FullscreenWatcher) or has been dragged
/// somewhere awkward. Deliberately exposes only plain C# events
/// (LeftClicked/RightClicked) rather than any System.Windows.Forms type,
/// so nothing outside this file needs a `using System.Windows.Forms;` -
/// that namespace and WPF's own System.Windows both define types like
/// MessageBox, so keeping the WinForms usage contained here avoids any
/// ambiguity in the rest of the app.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Drawing.Icon _icon;

    public event Action? LeftClicked;
    public event Action? RightClicked;

    public TrayIconController(BitmapSource iconFrame, string tooltipText)
    {
        _icon = CreateIcon(iconFrame);
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = Truncate(tooltipText, 63), // NotifyIcon.Text throws past 63 chars
            Visible = true,
        };
        _notifyIcon.MouseClick += OnMouseClick;
    }

    private void OnMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            LeftClicked?.Invoke();
        }
        else if (e.Button == Forms.MouseButtons.Right)
        {
            RightClicked?.Invoke();
        }
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    /// <summary>
    /// Renders the given sprite frame down to a 32x32 icon. GetHicon()
    /// hands back a GDI icon handle the caller owns - Icon.FromHandle()
    /// only wraps it, so the wrapped copy is cloned into a fully managed
    /// Icon and the original handle is explicitly destroyed, rather than
    /// leaking a GDI handle for the process's whole lifetime.
    /// </summary>
    private static Drawing.Icon CreateIcon(BitmapSource frame)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;

        using var bitmap = new Drawing.Bitmap(stream);
        using var resized = new Drawing.Bitmap(bitmap, new Drawing.Size(32, 32));

        IntPtr hIcon = resized.GetHicon();
        try
        {
            using var temp = Drawing.Icon.FromHandle(hIcon);
            return (Drawing.Icon)temp.Clone();
        }
        finally
        {
            Win32Interop.DestroyIconHandle(hIcon);
        }
    }

    public void Dispose()
    {
        _notifyIcon.MouseClick -= OnMouseClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon.Dispose();
    }
}
