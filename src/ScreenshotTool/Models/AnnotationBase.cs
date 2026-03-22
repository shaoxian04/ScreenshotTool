using System.Windows;
using System.Windows.Media;

namespace ScreenshotTool.Models;

public abstract class AnnotationBase
{
    public Color StrokeColor { get; set; } = Colors.Red;
    public double StrokeWidth { get; set; } = 2;
    public Point StartPoint { get; set; }
    public Point EndPoint { get; set; }

    public Rect Bounds
    {
        get
        {
            var x = Math.Min(StartPoint.X, EndPoint.X);
            var y = Math.Min(StartPoint.Y, EndPoint.Y);
            var w = Math.Abs(EndPoint.X - StartPoint.X);
            var h = Math.Abs(EndPoint.Y - StartPoint.Y);
            return new Rect(x, y, w, h);
        }
    }

    public abstract void Render(DrawingContext dc);

    public virtual bool HitTest(Point point, double tolerance = 5)
    {
        var inflated = Bounds;
        inflated.Inflate(tolerance, tolerance);
        return inflated.Contains(point);
    }

    protected Pen CreatePen()
    {
        var pen = new Pen(new SolidColorBrush(StrokeColor), StrokeWidth);
        pen.Freeze();
        return pen;
    }
}
