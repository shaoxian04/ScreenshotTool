using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace ScreenshotTool.Helpers;

public static class Win32Interop
{
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForSystem();

    // Virtual screen metrics (physical pixels in PerMonitorV2)
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, uint dwFlags);

    public const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public System.Drawing.Rectangle ToRectangle() =>
            System.Drawing.Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    public const uint MONITORINFOF_PRIMARY = 1;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    // GDI capture via GetDC(NULL) — uses physical pixel coords in PerMonitorV2
    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height,
        IntPtr hdcSrc, int xSrc, int ySrc, uint dwRop);

    public const uint SRCCOPY = 0x00CC0020;

    [DllImport("gdi32.dll")]
    public static extern bool StretchBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, uint dwRop);

    [DllImport("gdi32.dll")]
    public static extern int SetStretchBltMode(IntPtr hdc, int mode);

    public const int HALFTONE = 4;
    public const int COLORONCOLOR = 3;

    [DllImport("gdi32.dll")]
    public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    public const int HORZRES = 8;
    public const int VERTRES = 10;

    // CreateDC for per-monitor capture (driver may be null when using device name)
    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr CreateDC(string? lpszDriver, string? lpszDevice, string? lpszOutput, IntPtr lpInitData);

    // EnumDisplayMonitors to enumerate all monitors
    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    // DPI awareness context switching
    [DllImport("user32.dll")]
    public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    public static readonly IntPtr DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = (IntPtr)(-2);
    public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = (IntPtr)(-3);
    public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = (IntPtr)(-4);

    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint VK_S = 0x53;

    /// <summary>Returns the system DPI scale (e.g. 1.25 for 125%).</summary>
    public static double GetDpiScale()
    {
        return GetDpiForSystem() / 96.0;
    }

    /// <summary>
    /// Returns the virtual desktop bounding rectangle in physical pixels.
    /// Explicitly uses PerMonitorV2 DPI context so GetSystemMetrics returns true physical pixels,
    /// not SYSTEM_AWARE logical pixels (WPF can switch the thread context unexpectedly).
    /// </summary>
    public static System.Drawing.Rectangle GetVirtualScreenPhysicalBounds()
    {
        var prevCtx = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        try
        {
            return new System.Drawing.Rectangle(
                GetSystemMetrics(SM_XVIRTUALSCREEN),
                GetSystemMetrics(SM_YVIRTUALSCREEN),
                GetSystemMetrics(SM_CXVIRTUALSCREEN),
                GetSystemMetrics(SM_CYVIRTUALSCREEN));
        }
        finally
        {
            SetThreadDpiAwarenessContext(prevCtx);
        }
    }

    /// <summary>
    /// Returns the physical pixel bounds of the monitor nearest to the given physical-pixel point.
    /// Wraps in PER_MONITOR_AWARE_V2 so GetMonitorInfo returns physical pixel bounds regardless of
    /// what DPI context WPF left on the thread.
    /// </summary>
    public static System.Drawing.Rectangle GetMonitorPhysicalBounds(System.Drawing.Point physicalPoint)
    {
        var prevCtx = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        try
        {
            var hMon = MonitorFromPoint(physicalPoint, MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            return GetMonitorInfo(hMon, ref info) ? info.rcMonitor.ToRectangle() : System.Drawing.Rectangle.Empty;
        }
        finally
        {
            SetThreadDpiAwarenessContext(prevCtx);
        }
    }

    /// <summary>Returns true if the monitor at the given physical-pixel point is the primary monitor.</summary>
    public static bool IsMonitorPrimary(System.Drawing.Point physicalPoint)
    {
        var prevCtx = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        try
        {
            var hMon = MonitorFromPoint(physicalPoint, MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            return GetMonitorInfo(hMon, ref info) && (info.dwFlags & MONITORINFOF_PRIMARY) != 0;
        }
        finally
        {
            SetThreadDpiAwarenessContext(prevCtx);
        }
    }

    /// <summary>
    /// Captures the entire virtual desktop (all monitors) into a single physical-pixel bitmap.
    ///
    /// Enumerates each monitor individually and uses per-monitor DCs (CreateDC with device name).
    /// A per-monitor DC's origin is always (0,0) = that monitor's top-left physical pixel, so
    /// there are no negative-coordinate or virtual-screen-origin ambiguities.
    ///
    /// Each monitor is blitted into its correct position in the virtual desktop bitmap using:
    ///   destX = monitorBounds.X - virtualPhysBounds.X
    ///   destY = monitorBounds.Y - virtualPhysBounds.Y
    ///
    /// If the per-monitor DC reports logical pixels (HORZRES ≠ physical width), StretchBlt
    /// is used to scale to the correct physical pixel size.
    /// </summary>
    public static BitmapSource CaptureAllMonitors(System.Drawing.Rectangle virtualPhysBounds)
    {
        // Switch to PerMonitorV2 so all GDI/Win32 calls use physical pixel coordinates.
        // WPF can leave the thread in SYSTEM_AWARE context, which causes GetMonitorInfo and
        // per-monitor DCs to return DPI-scaled logical pixels instead of physical pixels,
        // misaligning each monitor's destX/destY in the virtual desktop bitmap.
        var prevCtx = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        try
        {
        // Enumerate all monitors; collect bounds (physical pixels) and device names
        var monitorList = new List<(System.Drawing.Rectangle bounds, string deviceName)>();
        bool EnumMonitor(IntPtr hMon, IntPtr hdcIgnored, ref RECT lprcIgnored, IntPtr dataIgnored)
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMon, ref info))
                monitorList.Add((info.rcMonitor.ToRectangle(), info.szDevice));
            return true;
        }
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, EnumMonitor, IntPtr.Zero);

        // Target bitmap: virtual desktop dimensions in physical pixels
        var hdcPrimary = GetDC(IntPtr.Zero);
        var hdcMem = CreateCompatibleDC(hdcPrimary);
        var hBitmap = CreateCompatibleBitmap(hdcPrimary, virtualPhysBounds.Width, virtualPhysBounds.Height);
        var hOld = SelectObject(hdcMem, hBitmap);

        foreach (var (monBounds, deviceName) in monitorList)
        {
            // Per-monitor DC: (0,0) is that monitor's physical top-left — no offset math on source
            var hdcMonitor = CreateDC(null, deviceName, null, IntPtr.Zero);
            if (hdcMonitor == IntPtr.Zero) continue;

            // Destination position within the virtual desktop bitmap
            int destX = monBounds.X - virtualPhysBounds.X;
            int destY = monBounds.Y - virtualPhysBounds.Y;
            int physW = monBounds.Width;
            int physH = monBounds.Height;

            // DC may report logical pixels if DPI virtualization is active; handle both cases
            int dcW = GetDeviceCaps(hdcMonitor, HORZRES);
            int dcH = GetDeviceCaps(hdcMonitor, VERTRES);

            if (dcW == physW && dcH == physH)
            {
                BitBlt(hdcMem, destX, destY, physW, physH, hdcMonitor, 0, 0, SRCCOPY);
            }
            else
            {
                // DC is in logical pixels; stretch-blt to physical pixel dimensions
                SetStretchBltMode(hdcMem, HALFTONE);
                StretchBlt(hdcMem, destX, destY, physW, physH,
                           hdcMonitor, 0, 0, dcW, dcH, SRCCOPY);
            }

            DeleteDC(hdcMonitor);
        }

        SelectObject(hdcMem, hOld);
        DeleteDC(hdcMem);
        ReleaseDC(IntPtr.Zero, hdcPrimary);

        var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
            hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        source.Freeze();
        DeleteObject(hBitmap);
        return source;
        } // end try
        finally
        {
            SetThreadDpiAwarenessContext(prevCtx);
        }
    }
}
