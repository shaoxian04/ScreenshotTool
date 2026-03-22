using System.Windows.Forms;

namespace ScreenshotTool.Services;

public class MonitorInfo
{
    public required System.Drawing.Rectangle Bounds { get; init; }
    public required bool IsPrimary { get; init; }
    public required string DeviceName { get; init; }
}

public class MonitorService
{
    public List<MonitorInfo> GetMonitors()
    {
        return Screen.AllScreens.Select(s => new MonitorInfo
        {
            Bounds = s.Bounds,
            IsPrimary = s.Primary,
            DeviceName = s.DeviceName
        }).ToList();
    }

    public System.Drawing.Rectangle GetVirtualDesktopBounds()
    {
        return SystemInformation.VirtualScreen;
    }

    public MonitorInfo? GetMonitorFromPoint(System.Drawing.Point point)
    {
        var screen = Screen.FromPoint(point);
        return new MonitorInfo
        {
            Bounds = screen.Bounds,
            IsPrimary = screen.Primary,
            DeviceName = screen.DeviceName
        };
    }
}
