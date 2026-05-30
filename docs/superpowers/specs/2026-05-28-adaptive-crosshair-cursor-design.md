# Adaptive Crosshair Cursor — Design

**Date:** 2026-05-28
**Status:** Approved (pending spec review)
**Branch:** add-ocr-feature

## Problem

When the screenshot overlay opens over a light/white background, the crosshair
cursor is hard or impossible to see. The cursor color is fixed and does not
contrast with light backgrounds.

## Goal

Make the **crosshair cursor** adapt its color to the background underneath it:
- Light background → black crosshair
- Dark background → white crosshair

Scope is the crosshair only (used while selecting a region, drawing annotations,
and selecting an OCR region). Resize/move/arrow/hand cursors are unchanged —
they remain the standard Windows cursors.

## Approach

Approach A (chosen): build two custom crosshair cursors once (black-armed and
white-armed), and on mouse move sample the screenshot pixels under the cursor to
pick whichever contrasts. The OS cursor moves natively, so there is no lag.

Rejected alternatives:
- **Canvas-drawn crosshair following the mouse** — visibly lags the real pointer
  and complicates the existing hit-test/annotation logic.
- **Single-pixel sampling + single threshold** — flickers over grey areas and
  near light/dark edges.

## Components

### New: `Services/AdaptiveCursorService.cs`

Owns all cursor construction and selection so `OverlayWindow` stays focused.
`IDisposable` to release native cursor handles.

**Construction**
- Takes the screenshot `BitmapSource` (physical pixels, covers virtual desktop)
  and the DPI scale.
- Draws a crosshair into a small bitmap twice — once with black arms, once with
  white arms — each with a 1px contrasting outline so a white crosshair stays
  visible on light-grey and a black crosshair on dark-grey.
- Converts each bitmap to a `System.Windows.Input.Cursor` via `CreateIconIndirect`
  (ICONINFO with `fIcon = false` so we control the hotspot, set to the crosshair
  center) and `CursorInteropHelper.Create`. Holds the two `Cursor` objects and
  their native handles for disposal.

**`Cursor PickFor(Point dipPos)`**
- Convert the DIP canvas point to physical pixels: `px = dipPos.X * dpiScale`,
  `py = dipPos.Y * dpiScale`.
- Build a ~9×9 physical-pixel rect centered on `(px, py)`, clamped to the
  screenshot bounds.
- Crop via `CroppedBitmap` + `CopyPixels` into a BGRA buffer; average B, G, R.
- Perceptual luminance: `L = 0.299*R + 0.587*G + 0.114*B`.
- Hysteresis to avoid flicker on mid-grey:
  - currently black → switch to white only if `L < 110`
  - currently white → switch to black only if `L > 145`
  - between 110 and 145 → keep current choice
  - initial state: pick by `L < 128`
- On any failure (sampling/crop error), return the black cursor (safe default).
  Never throw — this runs on every mouse move.

### Modified: `Views/OverlayWindow.xaml.cs`

- Construct `AdaptiveCursorService` after the screenshot is assigned; store in a
  field. Dispose it when the window closes.
- In `UpdateCursor(Point pos)`, every assignment that currently sets
  `Cursors.Cross` is replaced with `Cursor = _cursorService.PickFor(pos)`.
  Resize/move/hand branches are untouched.
- `StartOcrSelection()` sets the crosshair via the service instead of
  `Cursors.Cross`. `CancelOcrSelection` / `FinishOcrSelectionAsync` restore the
  arrow as today.
- While actively dragging (state `Selecting` or drawing an annotation),
  `OnMouseMove` returns before reaching `UpdateCursor`. Add a lightweight call to
  refresh the crosshair color on those drag paths so it keeps adapting as the
  pointer crosses a light/dark boundary mid-drag.

### Constants

Magic numbers (sample block size, hysteresis thresholds, crosshair dimensions)
are named constants in `AdaptiveCursorService`, tunable in one place.

## Data Flow

```
MouseMove (DIP point on MainCanvas)
  → AdaptiveCursorService.PickFor(pos)
      → DIP → physical pixel
      → crop 9x9 from screenshot BitmapSource
      → average BGR → luminance
      → hysteresis decision → black or white Cursor
  → OverlayWindow assigns Window.Cursor
```

## Error Handling

- Cursor creation failure in the constructor: fall back so `PickFor` returns
  `Cursors.Cross`; the feature degrades to today's behavior rather than crashing.
- Sampling/crop failure: return the black cursor.
- Native handles (HICON/HCURSOR) freed in `Dispose`, consistent with the
  project's bitmap/HBITMAP lifecycle rules.

## Testing

No test project exists; verification is manual:
1. Trigger capture over a white window → crosshair is black.
2. Trigger capture over a dark window → crosshair is white.
3. Drag a selection across a light/dark boundary → cursor flips color without
   rapid flicker.
4. OCR region selection shows the adaptive crosshair.
5. Resize/move/hand cursors over handles and annotations are unchanged.

## Out of Scope

- Recoloring resize/move/arrow/hand cursors.
- Non-binary (gradient/inverted) cursor coloring.
