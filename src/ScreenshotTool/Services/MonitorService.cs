using System.Windows.Forms;
using ScreenshotTool.Helpers;

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

    /// <summary>
    /// Returns the virtual desktop bounds in physical pixels, bypassing WinForms DPI virtualization.
    /// </summary>
    public System.Drawing.Rectangle GetVirtualDesktopBounds()
    {
        return Win32Interop.GetVirtualScreenPhysicalBounds();
    }

    /// <summary>
    /// Returns the monitor containing the given physical-pixel point, with bounds in physical pixels.
    /// </summary>
    public MonitorInfo? GetMonitorFromPoint(System.Drawing.Point point)
    {
        var bounds = Win32Interop.GetMonitorPhysicalBounds(point);
        if (bounds.IsEmpty) return null;
        return new MonitorInfo
        {
            Bounds = bounds,
            IsPrimary = Win32Interop.IsMonitorPrimary(point),
            DeviceName = string.Empty
        };
    }
}
