using System.Windows;
using System.Windows.Media;

namespace ScreenshotTool.Models;

public class FreehandAnnotation : AnnotationBase
{
    public List<Point> Points { get; set; } = new();

    public override void Render(DrawingContext dc)
    {
        if (Points.Count < 2) return;

        var pen = CreatePen();
        for (int i = 1; i < Points.Count; i++)
        {
            dc.DrawLine(pen, Points[i - 1], Points[i]);
        }
    }

    public override bool HitTest(Point point, double tolerance = 5)
    {
        for (int i = 1; i < Points.Count; i++)
        {
            var dist = DistanceToSegment(point, Points[i - 1], Points[i]);
            if (dist <= tolerance) return true;
        }
        return false;
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        var ab = b - a;
        var ap = p - a;
        var lengthSq = ab.LengthSquared;
        if (lengthSq < 0.0001) return (p - a).Length;

        var t = Math.Clamp((ap.X * ab.X + ap.Y * ab.Y) / lengthSq, 0, 1);
        var closest = a + ab * t;
        return (p - closest).Length;
    }
}
