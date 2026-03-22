using System.Windows.Forms;

namespace ScreenshotTool.Views;

public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public event EventHandler? ExitRequested;
    public event EventHandler? CaptureRequested;

    public TrayIconManager()
    {
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Capture (Ctrl+Shift+S)", null, (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "Screenshot Tool",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.DoubleClick += (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    private static System.Drawing.Icon CreateDefaultIcon()
    {
        var bitmap = new System.Drawing.Bitmap(16, 16);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        g.Clear(System.Drawing.Color.FromArgb(70, 130, 180));
        g.DrawRectangle(System.Drawing.Pens.White, 2, 2, 11, 11);
        g.DrawLine(System.Drawing.Pens.White, 8, 0, 8, 5);
        g.DrawLine(System.Drawing.Pens.White, 6, 3, 10, 3);
        return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
