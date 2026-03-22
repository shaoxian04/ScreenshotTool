using ScreenshotTool.Models;

namespace ScreenshotTool.Commands;

public class AddAnnotationCommand : IUndoableCommand
{
    private readonly List<AnnotationBase> _annotations;
    private readonly AnnotationBase _annotation;

    public AddAnnotationCommand(List<AnnotationBase> annotations, AnnotationBase annotation)
    {
        _annotations = annotations;
        _annotation = annotation;
    }

    public void Execute() => _annotations.Add(_annotation);
    public void Undo() => _annotations.Remove(_annotation);
}
