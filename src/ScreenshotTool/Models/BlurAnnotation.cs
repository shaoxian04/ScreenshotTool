using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenshotTool.Models;

public class BlurAnnotation : AnnotationBase
{
    public BitmapSource? PixelatedBitmap { get; set; }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        if (bounds.Width < 2 || bounds.Height < 2) return;

        if (PixelatedBitmap != null)
        {
            dc.DrawImage(PixelatedBitmap, bounds);
        }
        else
        {
            // Fallback: semi-transparent fill with dashed outline during drawing
            var brush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(180, 100, 100, 100)), 1.5)
            {
                DashStyle = DashStyles.Dash
            };
            dc.DrawRectangle(brush, pen, bounds);
        }
    }
}
