using System.Windows;
using ScreenshotTool.Models;

namespace ScreenshotTool.Commands;

public class MoveAnnotationCommand : IUndoableCommand
{
    private readonly AnnotationBase _annotation;
    private readonly Point _oldStart;
    private readonly Point _oldEnd;
    private readonly Point _newStart;
    private readonly Point _newEnd;
    private readonly List<Point>? _oldPoints;
    private readonly List<Point>? _newPoints;
    private readonly double _oldFontSize;
    private readonly double _newFontSize;

    public MoveAnnotationCommand(AnnotationBase annotation,
        Point oldStart, Point oldEnd,
        Point newStart, Point newEnd,
        List<Point>? oldPoints = null, List<Point>? newPoints = null,
        double oldFontSize = 0, double newFontSize = 0)
    {
        _annotation = annotation;
        _oldStart = oldStart;
        _oldEnd = oldEnd;
        _newStart = newStart;
        _newEnd = newEnd;
        _oldPoints = oldPoints;
        _newPoints = newPoints;
        _oldFontSize = oldFontSize;
        _newFontSize = newFontSize;
    }

    public void Execute()
    {
        _annotation.StartPoint = _newStart;
        _annotation.EndPoint = _newEnd;
        ApplyPoints(_newPoints);
        if (_annotation is TextAnnotation text && _newFontSize > 0)
            text.FontSize = _newFontSize;
    }

    public void Undo()
    {
        _annotation.StartPoint = _oldStart;
        _annotation.EndPoint = _oldEnd;
        ApplyPoints(_oldPoints);
        if (_annotation is TextAnnotation text && _oldFontSize > 0)
            text.FontSize = _oldFontSize;
    }

    private void ApplyPoints(List<Point>? points)
    {
        if (points == null) return;
        if (_annotation is FreehandAnnotation freehand)
        {
            freehand.Points.Clear();
            freehand.Points.AddRange(points);
        }
    }
}
