# Adaptive Crosshair Cursor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the overlay's crosshair cursor automatically switch between black (on light backgrounds) and white (on dark backgrounds) by sampling the screenshot under the pointer.

**Architecture:** A new `AdaptiveCursorService` builds two custom crosshair cursors once (black-armed, white-armed) and exposes `PickFor(dipPoint)`, which samples a 9×9 physical-pixel block of the screenshot under the pointer, computes luminance, and returns the contrasting cursor with hysteresis to prevent flicker. `OverlayWindow` calls it wherever it currently sets `Cursors.Cross`.

**Tech Stack:** .NET 8 WPF, System.Drawing (GDI+ for drawing the cursor bitmaps), Win32 P/Invoke (`CreateIconIndirect`/`GetIconInfo`) for custom cursors with a precise hotspot, `CursorInteropHelper` to wrap the native handle as a WPF `Cursor`.

**Testing note:** This repo has no test project; testing is manual via the UI per `CLAUDE.md`. Each task is gated by `dotnet build` succeeding, and the feature is verified with the manual checklist in Task 6.

---

### Task 1: Add Win32 cursor-creation P/Invoke to Win32Interop

**Files:**
- Modify: `src/ScreenshotTool/Helpers/Win32Interop.cs`

Project convention (per `CLAUDE.md`) is that all P/Invoke lives in `Win32Interop`. Add the declarations the service needs to turn a bitmap into a cursor with a custom hotspot.

- [ ] **Step 1: Add the `ICONINFO` struct and P/Invoke declarations**

Add the following members inside the `Win32Interop` class (anywhere among the other declarations, e.g. just before `GetDpiScale`):

```csharp
    [StructLayout(LayoutKind.Sequential)]
    public struct ICONINFO
    {
        public bool fIcon;       // false => cursor (lets us set a hotspot)
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CreateIconIndirect(ref ICONINFO icon);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetIconInfo(IntPtr hIcon, ref ICONINFO pIconInfo);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyCursor(IntPtr hCursor);
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/ScreenshotTool/Helpers/Win32Interop.cs
git commit -m "feat: add Win32 cursor-creation P/Invoke declarations"
```

---

### Task 2: Create AdaptiveCursorService with the two crosshair cursors

**Files:**
- Create: `src/ScreenshotTool/Services/AdaptiveCursorService.cs`

This task builds the service skeleton: it draws the black and white crosshair bitmaps and converts them to WPF `Cursor` objects at construction. Sampling/`PickFor` is added in Task 3. Until then it exposes a temporary `BlackCursor`/`WhiteCursor` so the build stays green.

- [ ] **Step 1: Create the service file**

Create `src/ScreenshotTool/Services/AdaptiveCursorService.cs` with this exact content:

```csharp
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ScreenshotTool.Helpers;
using WpfCursor = System.Windows.Input.Cursor;
using WpfCursors = System.Windows.Input.Cursors;
using DrawingColor = System.Drawing.Color;

namespace ScreenshotTool.Services;

/// <summary>
/// Provides a crosshair cursor that contrasts with the background underneath it:
/// black on light backgrounds, white on dark backgrounds. Two cursors are built once;
/// <see cref="PickFor"/> samples the screenshot under the pointer and returns the
/// contrasting one (with hysteresis to avoid flicker on mid-grey areas).
/// </summary>
public sealed class AdaptiveCursorService : IDisposable
{
    // Crosshair geometry (logical pixels).
    private const int CursorSize = 32;
    private const int Hotspot = CursorSize / 2;
    private const int CenterGap = 3;       // half-width of the empty gap at the center

    // Sampling / decision constants.
    private const int SampleBlock = 9;     // NxN physical pixels averaged under the pointer
    private const double SwitchToWhiteBelow = 110; // luminance below this => white crosshair
    private const double SwitchToBlackAbove = 145; // luminance above this => black crosshair
    private const double InitialThreshold = 128;

    private readonly BitmapSource _screenshot;
    private readonly WpfCursor _blackCursor;
    private readonly WpfCursor _whiteCursor;
    private readonly SafeCursorHandle? _blackHandle;
    private readonly SafeCursorHandle? _whiteHandle;

    private bool? _lastWasWhite; // null until first decision
    private bool _disposed;

    /// <summary>DIP-to-physical-pixel scale. Settable because WPF may reassign the
    /// window's DPI after it loads (see OverlayWindow.OnWindowLoaded).</summary>
    public double DpiScale { get; set; }

    public AdaptiveCursorService(BitmapSource screenshot, double dpiScale)
    {
        _screenshot = screenshot;
        DpiScale = dpiScale;

        (_blackCursor, _blackHandle) = BuildCursor(armColor: DrawingColor.Black, outlineColor: DrawingColor.White);
        (_whiteCursor, _whiteHandle) = BuildCursor(armColor: DrawingColor.White, outlineColor: DrawingColor.Black);
    }

    private static (WpfCursor, SafeCursorHandle?) BuildCursor(DrawingColor armColor, DrawingColor outlineColor)
    {
        try
        {
            using var bmp = new Bitmap(CursorSize, CursorSize, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                g.Clear(DrawingColor.Transparent);

                using var outlinePen = new Pen(outlineColor, 3f);
                using var armPen = new Pen(armColor, 1f);

                // Draw outline first (thick), then the arm on top (thin) => 1px outline.
                foreach (var pen in new[] { outlinePen, armPen })
                {
                    // Horizontal arms (left and right of the center gap)
                    g.DrawLine(pen, 0, Hotspot, Hotspot - CenterGap, Hotspot);
                    g.DrawLine(pen, Hotspot + CenterGap, Hotspot, CursorSize - 1, Hotspot);
                    // Vertical arms (above and below the center gap)
                    g.DrawLine(pen, Hotspot, 0, Hotspot, Hotspot - CenterGap);
                    g.DrawLine(pen, Hotspot, Hotspot + CenterGap, Hotspot, CursorSize - 1);
                }
            }

            return CursorFromBitmap(bmp, Hotspot, Hotspot);
        }
        catch
        {
            // If anything goes wrong, fall back to the standard crosshair.
            return (WpfCursors.Cross, null);
        }
    }

    /// <summary>Converts a 32bpp ARGB bitmap into a WPF cursor with the given hotspot.</summary>
    private static (WpfCursor, SafeCursorHandle?) CursorFromBitmap(Bitmap bmp, int xHotspot, int yHotspot)
    {
        IntPtr hIcon = bmp.GetHicon(); // temporary icon; gives us the mask/color bitmaps
        var info = new Win32Interop.ICONINFO();
        try
        {
            if (!Win32Interop.GetIconInfo(hIcon, ref info))
                return (WpfCursors.Cross, null);

            info.fIcon = false;     // make it a cursor
            info.xHotspot = xHotspot;
            info.yHotspot = yHotspot;

            IntPtr hCursor = Win32Interop.CreateIconIndirect(ref info);
            if (hCursor == IntPtr.Zero)
                return (WpfCursors.Cross, null);

            var safe = new SafeCursorHandle(hCursor);
            var cursor = CursorInteropHelper.Create(safe);
            return (cursor, safe);
        }
        finally
        {
            // Free the GDI bitmaps from GetIconInfo and the temporary icon.
            if (info.hbmColor != IntPtr.Zero) Win32Interop.DeleteObject(info.hbmColor);
            if (info.hbmMask != IntPtr.Zero) Win32Interop.DeleteObject(info.hbmMask);
            Win32Interop.DestroyIcon(hIcon);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _blackHandle?.Dispose();
        _whiteHandle?.Dispose();
    }

    /// <summary>SafeHandle that destroys the native HCURSOR on release.</summary>
    private sealed class SafeCursorHandle : SafeHandle
    {
        public SafeCursorHandle(IntPtr handle) : base(IntPtr.Zero, true) => SetHandle(handle);
        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle() => Win32Interop.DestroyCursor(handle);
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors. (Two unused-field warnings for `_screenshot`/`_lastWasWhite`/constants are acceptable — they are used in Task 3.)

- [ ] **Step 3: Commit**

```bash
git add src/ScreenshotTool/Services/AdaptiveCursorService.cs
git commit -m "feat: add AdaptiveCursorService with black/white crosshair cursors"
```

---

### Task 3: Implement PickFor — sampling, luminance, hysteresis

**Files:**
- Modify: `src/ScreenshotTool/Services/AdaptiveCursorService.cs`

- [ ] **Step 1: Add the `PickFor` and `SampleLuminance` methods**

Insert these two methods into `AdaptiveCursorService`, immediately before the `Dispose()` method:

```csharp
    /// <summary>
    /// Returns the crosshair cursor that best contrasts with the screenshot under the
    /// given point (in DIP coordinates on the overlay canvas). Never throws.
    /// </summary>
    public WpfCursor PickFor(System.Windows.Point dipPos)
    {
        double? luminance = SampleLuminance(dipPos);
        if (luminance is null)
            return _blackCursor; // safe default

        bool wantWhite;
        if (_lastWasWhite is null)
            wantWhite = luminance.Value < InitialThreshold;
        else if (_lastWasWhite.Value)
            wantWhite = luminance.Value < SwitchToBlackAbove;   // stay white until clearly light
        else
            wantWhite = luminance.Value < SwitchToWhiteBelow;   // stay black until clearly dark

        _lastWasWhite = wantWhite;
        return wantWhite ? _whiteCursor : _blackCursor;
    }

    /// <summary>
    /// Averages an NxN physical-pixel block of the screenshot centered on the pointer and
    /// returns its perceptual luminance (0..255), or null if sampling is not possible.
    /// </summary>
    private double? SampleLuminance(System.Windows.Point dipPos)
    {
        try
        {
            int px = (int)(dipPos.X * DpiScale);
            int py = (int)(dipPos.Y * DpiScale);
            int half = SampleBlock / 2;

            int x = Math.Clamp(px - half, 0, _screenshot.PixelWidth - 1);
            int y = Math.Clamp(py - half, 0, _screenshot.PixelHeight - 1);
            int w = Math.Min(SampleBlock, _screenshot.PixelWidth - x);
            int h = Math.Min(SampleBlock, _screenshot.PixelHeight - y);
            if (w <= 0 || h <= 0) return null;

            var region = new System.Windows.Int32Rect(x, y, w, h);
            var cropped = new CroppedBitmap(_screenshot, region);

            // Normalize to BGRA32 so byte layout is predictable.
            var converted = new FormatConvertedBitmap(cropped, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            int stride = w * 4;
            var pixels = new byte[stride * h];
            converted.CopyPixels(pixels, stride, 0);

            long totalB = 0, totalG = 0, totalR = 0;
            int count = w * h;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                totalB += pixels[i];
                totalG += pixels[i + 1];
                totalR += pixels[i + 2];
            }

            double r = (double)totalR / count;
            double gr = (double)totalG / count;
            double b = (double)totalB / count;
            return 0.299 * r + 0.587 * gr + 0.114 * b;
        }
        catch
        {
            return null;
        }
    }
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/ScreenshotTool/Services/AdaptiveCursorService.cs
git commit -m "feat: add luminance sampling and hysteresis to AdaptiveCursorService"
```

---

### Task 4: Wire the service into OverlayWindow (construct + dispose)

**Files:**
- Modify: `src/ScreenshotTool/Views/OverlayWindow.xaml.cs`

- [ ] **Step 1: Add the service field**

After the existing field `private double _dpiScale;` (around line 20), add:

```csharp
    private AdaptiveCursorService? _cursorService;
```

- [ ] **Step 2: Construct the service and hook disposal in the constructor**

In the constructor, immediately after the block that sets `ScreenshotImage.Source = screenshot;` and its Canvas positioning (just before `// Draw initial dark overlay`), add:

```csharp
        // Adaptive crosshair: black on light backgrounds, white on dark.
        _cursorService = new AdaptiveCursorService(screenshot, _dpiScale);
        Closed += (_, _) => _cursorService?.Dispose();
```

- [ ] **Step 3: Keep DpiScale in sync when WPF reassigns DPI**

In `OnWindowLoaded`, after the line `_dpiScale = actualDpiScale;`, add:

```csharp
        if (_cursorService != null) _cursorService.DpiScale = _dpiScale;
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/ScreenshotTool/Views/OverlayWindow.xaml.cs
git commit -m "feat: construct and dispose AdaptiveCursorService in OverlayWindow"
```

---

### Task 5: Use the adaptive crosshair everywhere the background crosshair is shown

**Files:**
- Modify: `src/ScreenshotTool/Views/OverlayWindow.xaml.cs`

There are three background-crosshair sites plus two drag paths. The annotation
endpoint-handle crosshair (`"Start" or "End"` at ~line 659) is a handle cursor and
stays a standard `Cursors.Cross` (out of scope per the spec).

- [ ] **Step 1: Replace the draw-tool crosshair (in `UpdateCursor`)**

Find (around line 683-685):

```csharp
            if (_currentTool != AnnotationTool.None)
            {
                Cursor = System.Windows.Input.Cursors.Cross;
            }
```

Replace the assignment line with:

```csharp
            if (_currentTool != AnnotationTool.None)
            {
                Cursor = _cursorService?.PickFor(pos) ?? System.Windows.Input.Cursors.Cross;
            }
```

- [ ] **Step 2: Replace the selection crosshair (end of `UpdateCursor`)**

Find (around line 702), the final line of `UpdateCursor`:

```csharp
        Cursor = System.Windows.Input.Cursors.Cross;
```

Replace with:

```csharp
        Cursor = _cursorService?.PickFor(pos) ?? System.Windows.Input.Cursors.Cross;
```

- [ ] **Step 3: Replace the OCR-selection crosshair (in `StartOcrSelection`)**

Find (around line 1269):

```csharp
        Cursor = System.Windows.Input.Cursors.Cross;
```

(Note: this is the one inside `StartOcrSelection`, immediately after `Toolbar.Visibility = Visibility.Collapsed;`.) Replace with:

```csharp
        Cursor = _cursorService?.PickFor(Mouse.GetPosition(MainCanvas))
            ?? System.Windows.Input.Cursors.Cross;
```

`Mouse` resolves to `System.Windows.Input.Mouse` (already imported via `System.Windows.Input`).

- [ ] **Step 4: Keep the crosshair adapting while dragging a selection**

In `OnMouseMove`, find the `Selecting` branch (around line 307):

```csharp
        if (_state == CaptureState.Selecting && e.LeftButton == MouseButtonState.Pressed)
        {
            _selection.EndPoint = pos;
            UpdateSelectionVisuals();
        }
```

Add the cursor refresh as the last line inside that block:

```csharp
        if (_state == CaptureState.Selecting && e.LeftButton == MouseButtonState.Pressed)
        {
            _selection.EndPoint = pos;
            UpdateSelectionVisuals();
            Cursor = _cursorService?.PickFor(pos) ?? System.Windows.Input.Cursors.Cross;
        }
```

- [ ] **Step 5: Keep the crosshair adapting while drawing an annotation**

In the same `OnMouseMove`, find the drawing branch (around line 335):

```csharp
        else if (_isDrawingAnnotation && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateAnnotation(pos);
        }
```

Add the cursor refresh:

```csharp
        else if (_isDrawingAnnotation && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateAnnotation(pos);
            Cursor = _cursorService?.PickFor(pos) ?? System.Windows.Input.Cursors.Cross;
        }
```

- [ ] **Step 6: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/ScreenshotTool/Views/OverlayWindow.xaml.cs
git commit -m "feat: use adaptive crosshair cursor in overlay selection, drawing, and OCR"
```

---

### Task 6: Manual verification

**Files:** none (manual UI testing per `CLAUDE.md`).

- [ ] **Step 1: Run the app**

Run: `dotnet run --project src/ScreenshotTool`
Then press `Ctrl+Shift+S` to open the overlay over different backgrounds.

- [ ] **Step 2: Verify the checklist**

- [ ] Over a white/light window, the crosshair is **black** and clearly visible.
- [ ] Over a dark window, the crosshair is **white** and clearly visible.
- [ ] Dragging a selection from a light area into a dark area flips the cursor color, without rapid flicker over grey areas.
- [ ] Selecting a draw tool (e.g. Rectangle) and hovering shows the adaptive crosshair; drawing keeps it adapting.
- [ ] Clicking OCR and entering region-select shows the adaptive crosshair.
- [ ] Hovering selection resize handles and existing annotations still shows the normal resize/move/hand cursors (unchanged).
- [ ] Closing the overlay (Esc) and reopening repeatedly does not leak or crash (cursors disposed correctly).

- [ ] **Step 3: If all pass, the feature is complete.**

No commit needed (no file changes). If a check fails, debug using superpowers:systematic-debugging before declaring done.

---

## Notes for the implementer

- `System.Drawing` and WPF both define `Color`, `Pen`, `Point`, `Size`, `Rectangle`, `Cursor(s)`. The service file uses explicit aliases (`DrawingColor`, `WpfCursor`, `WpfCursors`) and fully-qualified `System.Windows.Point`/`System.Windows.Int32Rect` to avoid clashing with the project's global WPF usings. Do not "simplify" these aliases away.
- `bmp.GetHicon()` returns a temporary icon handle that MUST be destroyed (`DestroyIcon`); the mask/color bitmaps from `GetIconInfo` MUST be freed (`DeleteObject`). Both are handled in `CursorFromBitmap`'s `finally`.
- Per `CLAUDE.md`, all P/Invoke belongs in `Win32Interop` — keep it there, not in the service.
