using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ScreenshotTool.Helpers;
using WpfCursor = System.Windows.Input.Cursor;
using WpfCursors = System.Windows.Input.Cursors;
using DrawingColor = System.Drawing.Color;
using DrawingPen = System.Drawing.Pen;

namespace ScreenshotTool.Services;

/// <summary>
/// Provides a crosshair cursor that contrasts with the background underneath it:
/// black on light backgrounds, white on dark backgrounds. Two cursors are built once;
/// <see cref="PickFor"/> samples the screenshot under the pointer and returns the
/// contrasting one (with hysteresis to avoid flicker on mid-grey areas).
/// </summary>
public sealed class AdaptiveCursorService : IDisposable
{
    // Crosshair geometry (logical pixels).
    private const int CursorSize = 32;
    private const int Hotspot = CursorSize / 2;
    private const int CenterGap = 3;       // half-width of the empty gap at the center

    // Sampling / decision constants.
    private const int SampleBlock = 9;     // NxN physical pixels averaged under the pointer
    private const double SwitchToWhiteBelow = 110; // luminance below this => white crosshair
    private const double SwitchToBlackAbove = 145; // luminance above this => black crosshair
    private const double InitialThreshold = 128;

    private readonly BitmapSource _screenshot;
    private readonly WpfCursor _blackCursor;
    private readonly WpfCursor _whiteCursor;
    private readonly SafeCursorHandle? _blackHandle;
    private readonly SafeCursorHandle? _whiteHandle;

    private bool? _lastWasWhite; // null until first decision
    private bool _disposed;

    /// <summary>DIP-to-physical-pixel scale. Settable because WPF may reassign the
    /// window's DPI after it loads (see OverlayWindow.OnWindowLoaded).</summary>
    public double DpiScale { get; set; }

    public AdaptiveCursorService(BitmapSource screenshot, double dpiScale)
    {
        _screenshot = screenshot;
        DpiScale = dpiScale;

        (_blackCursor, _blackHandle) = BuildCursor(armColor: DrawingColor.Black, outlineColor: DrawingColor.White);
        (_whiteCursor, _whiteHandle) = BuildCursor(armColor: DrawingColor.White, outlineColor: DrawingColor.Black);
    }

    private static (WpfCursor, SafeCursorHandle?) BuildCursor(DrawingColor armColor, DrawingColor outlineColor)
    {
        try
        {
            using var bmp = new Bitmap(CursorSize, CursorSize, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                g.Clear(DrawingColor.Transparent);

                using var outlinePen = new DrawingPen(outlineColor, 3f);
                using var armPen = new DrawingPen(armColor, 1f);

                // Draw outline first (thick), then the arm on top (thin) => 1px outline.
                foreach (var pen in new[] { outlinePen, armPen })
                {
                    // Horizontal arms (left and right of the center gap)
                    g.DrawLine(pen, 0, Hotspot, Hotspot - CenterGap, Hotspot);
                    g.DrawLine(pen, Hotspot + CenterGap, Hotspot, CursorSize - 1, Hotspot);
                    // Vertical arms (above and below the center gap)
                    g.DrawLine(pen, Hotspot, 0, Hotspot, Hotspot - CenterGap);
                    g.DrawLine(pen, Hotspot, Hotspot + CenterGap, Hotspot, CursorSize - 1);
                }
            }

            return CursorFromBitmap(bmp, Hotspot, Hotspot);
        }
        catch
        {
            // If anything goes wrong, fall back to the standard crosshair.
            return (WpfCursors.Cross, null);
        }
    }

    /// <summary>Converts a 32bpp ARGB bitmap into a WPF cursor with the given hotspot.</summary>
    private static (WpfCursor, SafeCursorHandle?) CursorFromBitmap(Bitmap bmp, int xHotspot, int yHotspot)
    {
        IntPtr hIcon = bmp.GetHicon(); // temporary icon; gives us the mask/color bitmaps
        var info = new Win32Interop.ICONINFO();
        try
        {
            if (!Win32Interop.GetIconInfo(hIcon, ref info))
                return (WpfCursors.Cross, null);

            info.fIcon = false;     // make it a cursor
            info.xHotspot = xHotspot;
            info.yHotspot = yHotspot;

            IntPtr hCursor = Win32Interop.CreateIconIndirect(ref info);
            if (hCursor == IntPtr.Zero)
                return (WpfCursors.Cross, null);

            var safe = new SafeCursorHandle(hCursor);
            var cursor = CursorInteropHelper.Create(safe);
            return (cursor, safe);
        }
        finally
        {
            // Free the GDI bitmaps from GetIconInfo and the temporary icon.
            if (info.hbmColor != IntPtr.Zero) Win32Interop.DeleteObject(info.hbmColor);
            if (info.hbmMask != IntPtr.Zero) Win32Interop.DeleteObject(info.hbmMask);
            Win32Interop.DestroyIcon(hIcon);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _blackHandle?.Dispose();
        _whiteHandle?.Dispose();
    }

    /// <summary>SafeHandle that destroys the native HCURSOR on release.</summary>
    private sealed class SafeCursorHandle : SafeHandle
    {
        public SafeCursorHandle(IntPtr handle) : base(IntPtr.Zero, true) => SetHandle(handle);
        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle() => Win32Interop.DestroyCursor(handle);
    }
}
