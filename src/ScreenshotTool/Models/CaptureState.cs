namespace ScreenshotTool.Models;

public enum CaptureState
{
    Idle,
    Selecting,
    Selected,
    Annotating,
    OcrSelecting
}

public enum AnnotationTool
{
    None,
    Arrow,
    Rectangle,
    Ellipse,
    Line,
    Text,
    Blur,
    Freehand
}
