using ScreenshotTool.Models;

namespace ScreenshotTool.Commands;

public class RemoveAnnotationCommand : IUndoableCommand
{
    private readonly List<AnnotationBase> _annotations;
    private readonly AnnotationBase _annotation;
    private int _index;

    public RemoveAnnotationCommand(List<AnnotationBase> annotations, AnnotationBase annotation)
    {
        _annotations = annotations;
        _annotation = annotation;
    }

    public void Execute()
    {
        _index = _annotations.IndexOf(_annotation);
        _annotations.Remove(_annotation);
    }

    public void Undo()
    {
        if (_index >= 0 && _index <= _annotations.Count)
            _annotations.Insert(_index, _annotation);
        else
            _annotations.Add(_annotation);
    }
}
