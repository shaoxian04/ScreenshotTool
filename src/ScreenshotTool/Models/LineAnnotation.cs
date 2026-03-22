using System.Windows.Media;

namespace ScreenshotTool.Models;

public class LineAnnotation : AnnotationBase
{
    public override void Render(DrawingContext dc)
    {
        dc.DrawLine(CreatePen(), StartPoint, EndPoint);
    }
}
