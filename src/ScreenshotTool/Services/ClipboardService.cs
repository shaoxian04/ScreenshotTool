using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ScreenshotTool.Models;
using ScreenshotTool.Rendering;

namespace ScreenshotTool.Services;

public static class ClipboardService
{
    public static void CopyToClipboard(BitmapSource screenshot, Rect selectionBounds,
        List<AnnotationBase> annotations, double dpiScale = 1.0)
    {
        var rendered = AnnotationRenderer.RenderToImage(screenshot, selectionBounds, annotations, dpiScale: dpiScale);

        // Create a PNG stream for better compatibility
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rendered));

        using var stream = new MemoryStream();
        encoder.Save(stream);

        var dataObject = new DataObject();
        dataObject.SetImage(rendered);
        dataObject.SetData("PNG", stream);

        Clipboard.SetDataObject(dataObject, true);
    }
}
