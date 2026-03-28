using System.IO;
using System.Windows;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using WpfBitmapDecoder = System.Windows.Media.Imaging.BitmapDecoder;
using WpfBitmapEncoder = System.Windows.Media.Imaging.PngBitmapEncoder;
using WpfBitmapFrame = System.Windows.Media.Imaging.BitmapFrame;
using WpfCroppedBitmap = System.Windows.Media.Imaging.CroppedBitmap;

namespace ScreenshotTool.Services;

public static class OcrService
{
    /// <summary>
    /// Runs OCR on the selected region of the screenshot and returns the extracted text.
    /// Returns null if the OCR engine is unavailable, empty string if no text was found.
    /// </summary>
    public static async Task<string?> RecognizeAsync(System.Windows.Media.Imaging.BitmapSource screenshot, Rect selectionBounds, double dpiScale)
    {
        var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (ocrEngine == null) return null;

        // Crop to the selected region in physical pixels
        var cropRect = new Int32Rect(
            (int)(selectionBounds.X * dpiScale),
            (int)(selectionBounds.Y * dpiScale),
            Math.Max(1, (int)(selectionBounds.Width * dpiScale)),
            Math.Max(1, (int)(selectionBounds.Height * dpiScale)));

        // Clamp to screenshot bounds
        cropRect = new Int32Rect(
            Math.Max(0, cropRect.X),
            Math.Max(0, cropRect.Y),
            Math.Min(cropRect.Width, screenshot.PixelWidth - cropRect.X),
            Math.Min(cropRect.Height, screenshot.PixelHeight - cropRect.Y));

        var cropped = new WpfCroppedBitmap(screenshot, cropRect);

        // Encode to PNG in memory, then decode to WinRT SoftwareBitmap
        using var ms = new MemoryStream();
        var pngEncoder = new WpfBitmapEncoder();
        pngEncoder.Frames.Add(WpfBitmapFrame.Create(cropped));
        pngEncoder.Save(ms);

        ms.Position = 0;
        using var ras = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(ras.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(ms.ToArray());
            await writer.StoreAsync();
        }
        ras.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(ras);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        var result = await ocrEngine.RecognizeAsync(softwareBitmap);

        return string.Join("\n", result.Lines.Select(l => l.Text));
    }
}
