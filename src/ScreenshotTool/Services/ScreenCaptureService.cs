using System.Windows.Media.Imaging;
using ScreenshotTool.Helpers;

namespace ScreenshotTool.Services;

public class ScreenCaptureService
{
    /// <summary>
    /// Captures the virtual desktop at physical pixel resolution using per-monitor DCs.
    ///
    /// Each monitor is captured via CreateDC(deviceName), which starts at (0,0) in that
    /// monitor's own coordinate space — completely avoiding the virtual-screen coordinate
    /// issues that cause black gaps when using CopyFromScreen with global coords.
    ///
    /// The resulting bitmap is at physical resolution (e.g. 4320×1350 for a 125%-DPI
    /// dual-monitor setup). CreateBitmapSourceFromHBitmap tags it with the screen DPI
    /// (120), so its natural WPF size = physW/1.25 × physH/1.25 = logical DIP size =
    /// exactly the overlay window size → no zoom, no black gaps.
    ///
    /// Callers that access bitmap pixels (OcrService, AnnotationRenderer, blur) must
    /// multiply DIP coordinates by dpiScale to get pixel coordinates.
    /// </summary>
    public (BitmapSource Bitmap, System.Drawing.Rectangle Bounds) CaptureAllScreens()
    {
        var physBounds = Win32Interop.GetVirtualScreenPhysicalBounds();
        var bitmap = Win32Interop.CaptureAllMonitors(physBounds);
        return (bitmap, physBounds);
    }
}
