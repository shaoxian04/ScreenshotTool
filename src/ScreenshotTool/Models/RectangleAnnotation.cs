using System.Windows.Media;

namespace ScreenshotTool.Models;

public class RectangleAnnotation : AnnotationBase
{
    public override void Render(DrawingContext dc)
    {
        dc.DrawRectangle(null, CreatePen(), Bounds);
    }
}
