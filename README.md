# Screenshot Tool

A lightweight Windows screenshot tool with built-in annotation support. Designed as a fast, keyboard-driven alternative to the Windows Snipping Tool.

Press **Ctrl+Shift+S** anywhere to start capturing.

## Features

- **Region or full-screen capture** - drag to select a region, or click to capture the entire monitor
- **7 annotation tools** - Arrow, Rectangle, Ellipse, Line, Text, Blur, and Freehand drawing
- **Resize and move annotations** - select any annotation after drawing to reposition or resize it
- **Color and thickness picker** - customize annotation appearance with 10 preset colors and adjustable stroke width
- **Undo / Redo** - Ctrl+Z / Ctrl+Y support via command history
- **Clipboard export** - copies the annotated screenshot to clipboard on confirm
- **Multi-monitor and high-DPI support** - works correctly across multiple displays at different scaling levels
- **System tray app** - runs quietly in the background with minimal resource usage

## Installation

### Option 1: Download the pre-built exe (recommended)

1. Go to the [Releases](https://github.com/shaoxian04/ScreenshotTool/releases) page
2. Download **ScreenshotTool.exe** from the latest release
3. Run the exe - no installation or .NET runtime required (self-contained)
4. The app starts in the **system tray** (notification area near the clock)

### Option 2: Build from source

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

```bash
git clone https://github.com/shaoxian04/ScreenshotTool.git
cd ScreenshotTool
dotnet build
dotnet run --project src/ScreenshotTool
```

To publish a self-contained single-file exe:

```bash
dotnet publish src/ScreenshotTool/ScreenshotTool.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
```

## Usage

| Action | How |
|---|---|
| **Start capture** | Press `Ctrl+Shift+S` (or right-click the tray icon) |
| **Select region** | Click and drag on the screen |
| **Capture full monitor** | Click without dragging |
| **Resize selection** | Drag the handles on the selection border |
| **Draw annotation** | Pick a tool from the toolbar, then draw on the selection |
| **Select annotation** | Click on an annotation (auto-selected after drawing) |
| **Move annotation** | Drag a selected annotation |
| **Resize annotation** | Drag the handles on a selected annotation |
| **Change color/thickness** | Hover over the color swatch or adjust the slider (also applies to selected annotations) |
| **Delete annotation** | Select it and press `Delete` |
| **Undo / Redo** | `Ctrl+Z` / `Ctrl+Y` |
| **Confirm capture** | Press `Enter` or click the green checkmark |
| **Cancel** | Press `Esc` or click the red X |

## Keyboard Shortcuts

| Key | Action |
|---|---|
| `Ctrl+Shift+S` | Global hotkey - start capture |
| `Enter` | Confirm and copy to clipboard |
| `Esc` | Cancel capture |
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Delete` | Remove selected annotation |

## System Requirements

- Windows 10 or later (x64)
- No additional runtime needed when using the pre-built exe

## License

MIT
