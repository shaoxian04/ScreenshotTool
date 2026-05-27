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

    private readonly WpfCursor _blackCursor;
    private readonly WpfCursor _whiteCursor;
    private readonly SafeCursorHandle? _blackHandle;
    private readonly SafeCursorHandle? _whiteHandle;

    // The screenshot pixels are copied once into a flat BGRA32 buffer so per-mouse-move
    // sampling indexes an array instead of allocating WPF imaging objects on the hot path.
    private readonly byte[]? _pixels;
    private readonly int _pixelW;
    private readonly int _pixelH;
    private readonly int _stride;

    private bool? _lastWasWhite; // null until first decision
    private bool _disposed;

    /// <summary>DIP-to-physical-pixel scale. Settable because WPF may reassign the
    /// window's DPI after it loads (see OverlayWindow.OnWindowLoaded).</summary>
    public double DpiScale { get; set; }

    public AdaptiveCursorService(BitmapSource screenshot, double dpiScale)
    {
        DpiScale = dpiScale;

        // Copy the screenshot once into a flat BGRA32 buffer. If this fails, _pixels stays
        // null and sampling falls back to the black cursor (a safe default).
        try
        {
            var converted = new FormatConvertedBitmap(screenshot, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            _pixelW = converted.PixelWidth;
            _pixelH = converted.PixelHeight;
            _stride = _pixelW * 4;
            _pixels = new byte[_stride * _pixelH];
            converted.CopyPixels(_pixels, _stride, 0);
        }
        catch
        {
            _pixels = null;
        }

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

    /// <summary>
    /// Returns the crosshair cursor that best contrasts with the screenshot under the
    /// given point (in DIP coordinates on the overlay canvas). Never throws.
    /// </summary>
    public WpfCursor PickFor(System.Windows.Point dipPos)
    {
        double? luminance = SampleLuminance(dipPos);
        if (luminance is null)
            return _blackCursor; // safe default

        bool wantWhite;
        if (_lastWasWhite is null)
            wantWhite = luminance.Value < InitialThreshold;
        else if (_lastWasWhite.Value)
            wantWhite = luminance.Value < SwitchToBlackAbove;   // currently white: switch to black only when clearly light (>=145)
        else
            wantWhite = luminance.Value < SwitchToWhiteBelow;   // currently black: switch to white only when clearly dark (<110)

        _lastWasWhite = wantWhite;
        return wantWhite ? _whiteCursor : _blackCursor;
    }

    /// <summary>
    /// Averages an NxN physical-pixel block of the screenshot centered on the pointer and
    /// returns its perceptual luminance (0..255), or null if sampling is not possible.
    /// </summary>
    private double? SampleLuminance(System.Windows.Point dipPos)
    {
        var pixels = _pixels;
        if (pixels == null) return null;

        try
        {
            int cx = (int)(dipPos.X * DpiScale);
            int cy = (int)(dipPos.Y * DpiScale);
            int half = SampleBlock / 2;

            int x0 = Math.Clamp(cx - half, 0, _pixelW - 1);
            int y0 = Math.Clamp(cy - half, 0, _pixelH - 1);
            int x1 = Math.Min(x0 + SampleBlock, _pixelW);
            int y1 = Math.Min(y0 + SampleBlock, _pixelH);

            long totalB = 0, totalG = 0, totalR = 0;
            int count = 0;
            for (int yy = y0; yy < y1; yy++)
            {
                int row = yy * _stride;
                for (int xx = x0; xx < x1; xx++)
                {
                    int idx = row + xx * 4; // BGRA32: B, G, R, A
                    totalB += pixels[idx];
                    totalG += pixels[idx + 1];
                    totalR += pixels[idx + 2];
                    count++;
                }
            }

            if (count == 0) return null;

            double r = (double)totalR / count;
            double gr = (double)totalG / count;
            double b = (double)totalB / count;
            return 0.299 * r + 0.587 * gr + 0.114 * b;
        }
        catch
        {
            return null;
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
