using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using ScreenshotTool.Helpers;

namespace ScreenshotTool.Services;

public class ScreenCaptureService
{
    public BitmapSource CaptureAllScreens(System.Drawing.Rectangle virtualDesktopBounds)
    {
        var bitmap = new System.Drawing.Bitmap(virtualDesktopBounds.Width, virtualDesktopBounds.Height, PixelFormat.Format32bppArgb);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(virtualDesktopBounds.Location, System.Drawing.Point.Empty, virtualDesktopBounds.Size);

        return ConvertToBitmapSource(bitmap);
    }

    private static BitmapSource ConvertToBitmapSource(System.Drawing.Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            Win32Interop.DeleteObject(hBitmap);
            bitmap.Dispose();
        }
    }
}
