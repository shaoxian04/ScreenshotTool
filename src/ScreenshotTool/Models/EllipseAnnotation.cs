using System.Windows;
using System.Windows.Media;

namespace ScreenshotTool.Models;

public class EllipseAnnotation : AnnotationBase
{
    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        dc.DrawEllipse(null, CreatePen(), center, bounds.Width / 2, bounds.Height / 2);
    }
}
