using System.Windows;

namespace ScreenshotTool.Models;

public class SelectionRegion
{
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
            return new Rect(x, y, Math.Max(w, 1), Math.Max(h, 1));
        }
    }

    public void SetFromRect(Rect rect)
    {
        StartPoint = rect.TopLeft;
        EndPoint = rect.BottomRight;
    }

    public bool IsSignificantDrag => Bounds.Width > 5 || Bounds.Height > 5;
}
