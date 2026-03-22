using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ScreenshotTool.Models;

public class TextAnnotation : AnnotationBase
{
    public string Text { get; set; } = string.Empty;
    public double FontSize { get; set; } = 16;

    public override void Render(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Text)) return;

        var formattedText = CreateFormattedText();
        dc.DrawText(formattedText, StartPoint);
    }

    public override bool HitTest(Point point, double tolerance = 5)
    {
        var rect = GetTextBounds();
        rect.Inflate(tolerance, tolerance);
        return rect.Contains(point);
    }

    public Rect GetTextBounds()
    {
        if (string.IsNullOrEmpty(Text))
            return new Rect(StartPoint, new Size(1, 1));

        var formattedText = CreateFormattedText();
        return new Rect(StartPoint, new Size(formattedText.Width, formattedText.Height));
    }

    private FormattedText CreateFormattedText()
    {
        return new FormattedText(
            Text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            FontSize,
            new SolidColorBrush(StrokeColor),
            96);
    }
}
