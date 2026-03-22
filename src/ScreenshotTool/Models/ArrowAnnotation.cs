using System.Windows;
using System.Windows.Media;

namespace ScreenshotTool.Models;

public class ArrowAnnotation : AnnotationBase
{
    public override void Render(DrawingContext dc)
    {
        var pen = CreatePen();

        var direction = EndPoint - StartPoint;
        var length = direction.Length;
        if (length < 1) return;

        direction.Normalize();

        // Scale arrowhead with stroke width for visibility at all sizes
        var arrowSize = Math.Min(10 + StrokeWidth * 3, length / 2);
        var arrowWidth = arrowSize * 0.5;

        var perpendicular = new Vector(-direction.Y, direction.X);
        var arrowPoint1 = EndPoint - direction * arrowSize + perpendicular * arrowWidth;
        var arrowPoint2 = EndPoint - direction * arrowSize - perpendicular * arrowWidth;

        // Draw the line stopping short of the arrowhead base so it doesn't poke through
        var lineEnd = EndPoint - direction * arrowSize * 0.3;
        dc.DrawLine(pen, StartPoint, lineEnd);

        // Draw filled arrowhead
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(EndPoint, true, true);
            ctx.LineTo(arrowPoint1, false, false);
            ctx.LineTo(arrowPoint2, false, false);
        }
        geometry.Freeze();

        dc.DrawGeometry(new SolidColorBrush(StrokeColor), null, geometry);
    }
}
