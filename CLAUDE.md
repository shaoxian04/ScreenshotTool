# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build
dotnet build

# Run
dotnet run --project src/ScreenshotTool
```

No test project exists yet. Testing is manual via the UI.

## Architecture

WPF system tray application (.NET 8) — no main window. The app lives in the system tray and opens a full-screen transparent overlay when the user presses `Ctrl+Shift+S`.

**Entry point flow:** `Program.cs` (single-instance Mutex) → `App.xaml.cs` (registers hotkey + tray icon, `ShutdownMode=OnExplicitShutdown`) → `StartCapture()` creates `OverlayWindow`.

**Capture state machine** (`CaptureState` enum): `Idle → Selecting → Selected → Annotating`. Click = full-monitor capture; drag = region capture. Selection is resizable with 8 handles before confirming.

### Key layers

- **Views/** — `OverlayWindow` is the core UI: a layered Canvas (screenshot background → dark overlay with CombinedGeometry cutout → selection border → resize handles → annotation canvas → toolbar → text input). `ToolbarControl` fires events consumed by OverlayWindow. `TrayIconManager` wraps WinForms NotifyIcon.
- **Models/** — `AnnotationBase` abstract class with `Render(DrawingContext)` and `HitTest(Point)`. Seven implementations: Arrow, Rectangle, Ellipse, Line, Text, Freehand, Blur. Each renders via WPF DrawingContext (not Shape elements).
- **Commands/** — Undo/redo via Command pattern. `CommandHistory` manages two stacks. `AddAnnotationCommand` / `RemoveAnnotationCommand` implement `IUndoableCommand`.
- **Services/** — `HotkeyService` (Win32 RegisterHotKey P/Invoke), `ScreenCaptureService` (GDI+ CopyFromScreen → BitmapSource), `MonitorService` (Screen enumeration), `ClipboardService` (composites screenshot + annotations → clipboard PNG).
- **Rendering/** — `AnnotationRenderer` composites final image via RenderTargetBitmap. `BlurRenderer` pixelates by block-averaging pixels.
- **Helpers/** — `Win32Interop` contains P/Invoke declarations.

### Important patterns

- **WPF + WinForms coexistence**: `UseWindowsForms=true` in csproj for Screen enumeration and NotifyIcon. `GlobalUsings.cs` resolves all ambiguous types (Point, Color, Cursors, etc.) to WPF equivalents. When using System.Drawing types, always fully qualify them (e.g., `System.Drawing.Point`).
- **No ViewModels in use** despite CommunityToolkit.Mvvm dependency. OverlayWindow uses direct code-behind with event handlers.
- **Bitmap lifecycle**: Always call `Freeze()` on BitmapSource objects. Always call `Win32Interop.DeleteObject()` on HBitmap handles.
