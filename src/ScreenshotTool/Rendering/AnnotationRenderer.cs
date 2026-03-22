using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotTool.Models;

namespace ScreenshotTool.Rendering;

public static class AnnotationRenderer
{
    public static BitmapSource RenderToImage(BitmapSource screenshot, Rect selectionBounds,
        List<AnnotationBase> annotations, double dpi = 96, double dpiScale = 1.0)
    {
        // Convert DIP selection bounds to physical pixel coordinates
        var pixelX = (int)(selectionBounds.X * dpiScale);
        var pixelY = (int)(selectionBounds.Y * dpiScale);
        var pixelW = (int)(selectionBounds.Width * dpiScale);
        var pixelH = (int)(selectionBounds.Height * dpiScale);

        if (pixelW <= 0 || pixelH <= 0) return screenshot;

        var renderTarget = new RenderTargetBitmap(pixelW, pixelH, dpi, dpi, PixelFormats.Pbgra32);

        var drawingVisual = new DrawingVisual();
        using (var dc = drawingVisual.RenderOpen())
        {
            // Draw the cropped screenshot at physical pixel coordinates
            var sourceRect = new Int32Rect(
                pixelX, pixelY,
                Math.Min(pixelW, screenshot.PixelWidth - pixelX),
                Math.Min(pixelH, screenshot.PixelHeight - pixelY));

            if (sourceRect.Width > 0 && sourceRect.Height > 0)
            {
                var cropped = new CroppedBitmap(screenshot, sourceRect);
                dc.DrawImage(cropped, new Rect(0, 0, pixelW, pixelH));
            }

            // Draw annotations scaled from DIPs to physical pixels
            dc.PushTransform(new ScaleTransform(dpiScale, dpiScale));
            dc.PushTransform(new TranslateTransform(-selectionBounds.X, -selectionBounds.Y));
            foreach (var annotation in annotations)
            {
                annotation.Render(dc);
            }
            dc.Pop();
            dc.Pop();
        }

        renderTarget.Render(drawingVisual);
        renderTarget.Freeze();
        return renderTarget;
    }
}
