namespace ScreenshotTool.Commands;

public interface IUndoableCommand
{
    void Execute();
    void Undo();
}
