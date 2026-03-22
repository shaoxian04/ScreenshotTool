using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenshotTool.Rendering;

public static class BlurRenderer
{
    public static BitmapSource CreatePixelatedBitmap(BitmapSource source, Int32Rect region, int blockSize = 10)
    {
        // Clamp region to source bounds
        var x = Math.Max(0, region.X);
        var y = Math.Max(0, region.Y);
        var w = Math.Min(region.Width, source.PixelWidth - x);
        var h = Math.Min(region.Height, source.PixelHeight - y);

        if (w <= 0 || h <= 0) return source;

        var clampedRegion = new Int32Rect(x, y, w, h);
        var cropped = new CroppedBitmap(source, clampedRegion);

        // Convert to a writable format
        var format = PixelFormats.Bgra32;
        var stride = w * 4;
        var pixels = new byte[stride * h];

        var converted = new FormatConvertedBitmap(cropped, format, null, 0);
        converted.CopyPixels(pixels, stride, 0);

        // Pixelate by averaging blocks
        for (int by = 0; by < h; by += blockSize)
        {
            for (int bx = 0; bx < w; bx += blockSize)
            {
                int blockW = Math.Min(blockSize, w - bx);
                int blockH = Math.Min(blockSize, h - by);

                long totalB = 0, totalG = 0, totalR = 0, totalA = 0;
                int count = blockW * blockH;

                for (int py = by; py < by + blockH; py++)
                {
                    for (int px = bx; px < bx + blockW; px++)
                    {
                        int idx = py * stride + px * 4;
                        totalB += pixels[idx];
                        totalG += pixels[idx + 1];
                        totalR += pixels[idx + 2];
                        totalA += pixels[idx + 3];
                    }
                }

                byte avgB = (byte)(totalB / count);
                byte avgG = (byte)(totalG / count);
                byte avgR = (byte)(totalR / count);
                byte avgA = (byte)(totalA / count);

                for (int py = by; py < by + blockH; py++)
                {
                    for (int px = bx; px < bx + blockW; px++)
                    {
                        int idx = py * stride + px * 4;
                        pixels[idx] = avgB;
                        pixels[idx + 1] = avgG;
                        pixels[idx + 2] = avgR;
                        pixels[idx + 3] = avgA;
                    }
                }
            }
        }

        var result = BitmapSource.Create(w, h, source.DpiX, source.DpiY, format, null, pixels, stride);
        result.Freeze();
        return result;
    }
}
