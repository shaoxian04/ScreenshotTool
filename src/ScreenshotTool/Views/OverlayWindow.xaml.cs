using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotTool.Commands;
using ScreenshotTool.Helpers;
using ScreenshotTool.Models;
using ScreenshotTool.Rendering;
using ScreenshotTool.Services;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace ScreenshotTool.Views;

public partial class OverlayWindow : Window
{
    private readonly BitmapSource _screenshot;
    private readonly List<MonitorInfo> _monitors;
    private readonly System.Drawing.Rectangle _virtualBounds;
    private double _dpiScale;

    // State
    private CaptureState _state = CaptureState.Idle;
    private readonly SelectionRegion _selection = new();
    private AnnotationTool _currentTool = AnnotationTool.None;
    private Color _currentColor = Colors.Red;
    private double _currentStrokeWidth = 2;

    // Annotations
    private readonly List<AnnotationBase> _annotations = new();
    private readonly CommandHistory _commandHistory = new();
    private AnnotationBase? _currentAnnotation;
    private bool _isDrawingAnnotation;

    // Selection drag/resize
    private bool _isDragging;
    private bool _isResizing;
    private Point _mouseDownPoint;
    private Point _selectionDragOffset;
    private string? _resizeHandle;
    private Rect _originalSelectionRect;

    // Resize handle rectangles
    private readonly Dictionary<string, Rect> _handleRects = new();
    private const double HandleSize = 8;

    // Annotation selection/move/resize
    private AnnotationBase? _selectedAnnotation;
    private bool _isMovingAnnotation;
    private bool _isResizingAnnotation;
    private string? _annotationResizeHandle;
    private Point _annotationOriginalStart;
    private Point _annotationOriginalEnd;
    private List<Point>? _annotationOriginalPoints;
    private double _annotationOriginalFontSize;
    private Point _annotationDragOffset;
    private readonly Dictionary<string, Rect> _annotationHandleRects = new();
    private const double AnnotationHandleSize = 8;

    // OCR region selection
    private Point _ocrRegionStart;
    private System.Windows.Shapes.Rectangle? _ocrRectVisual;
    private Border? _ocrHintLabel;
    private CaptureState _stateBeforeOcr;

    // OCR result panel drag
    private bool _isDraggingOcrPanel;
    private Point _ocrPanelDragOffset;

    public OverlayWindow(BitmapSource screenshot, List<MonitorInfo> monitors,
        System.Drawing.Rectangle virtualBounds)
    {
        InitializeComponent();

        _screenshot = screenshot;
        _monitors = monitors;
        _virtualBounds = virtualBounds;
        _dpiScale = Win32Interop.GetDpiScale();

        // Position window to cover virtual desktop (convert physical pixels to DIPs)
        PositionWindowForDpi();

        // Set screenshot as background (Stretch="Fill" maps physical pixels to DIP-sized window)
        ScreenshotImage.Source = screenshot;
        ScreenshotImage.Width = Width;
        ScreenshotImage.Height = Height;
        Canvas.SetLeft(ScreenshotImage, 0);
        Canvas.SetTop(ScreenshotImage, 0);

        // Draw initial dark overlay
        DrawOverlay(null);

        // After the window is shown, Windows assigns its rendering DPI based on the monitor
        // containing its top-left corner. This may differ from GetDpiForSystem() in multi-monitor
        // setups with mixed DPI. Re-read the actual DPI and reposition to ensure correct coverage.
        Loaded += OnWindowLoaded;

        // Wire toolbar events
        Toolbar.ToolSelected += OnToolSelected;
        Toolbar.ColorChanged += OnColorChanged;
        Toolbar.StrokeWidthChanged += OnStrokeWidthChanged;
        Toolbar.ConfirmClicked += (_, _) => ConfirmCapture();
        Toolbar.CancelClicked += (_, _) => CancelCapture();
        Toolbar.OcrClicked += (_, _) => RunOcrAsync();

        MouseLeftButtonDown += OnMouseDown;
        MouseLeftButtonUp += OnMouseUp;
        MouseMove += OnMouseMove;
        PreviewKeyDown += OnKeyDown;
    }

    private void PositionWindowForDpi()
    {
        Left = _virtualBounds.X / _dpiScale;
        Top = _virtualBounds.Y / _dpiScale;
        Width = _virtualBounds.Width / _dpiScale;
        Height = _virtualBounds.Height / _dpiScale;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Get the actual DPI that Windows assigned to this window based on the monitor
        // containing its top-left corner. This may differ from GetDpiForSystem() in multi-monitor
        // setups with mixed DPI. Re-read the actual DPI and reposition to ensure correct coverage.
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget == null) return;

        var actualDpiScale = source.CompositionTarget.TransformToDevice.M11;
        if (Math.Abs(actualDpiScale - _dpiScale) < 0.001) return; // already correct

        _dpiScale = actualDpiScale;

        PositionWindowForDpi();

        ScreenshotImage.Width = Width;
        ScreenshotImage.Height = Height;

        DrawOverlay(null);
    }

    #region Overlay Drawing

    private void DrawOverlay(Rect? selectionRect)
    {
        OverlayCanvas.Children.Clear();

        var overlay = new System.Windows.Shapes.Path
        {
            Fill = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
            IsHitTestVisible = false
        };

        var fullRect = new RectangleGeometry(new Rect(0, 0, Width, Height));

        if (selectionRect.HasValue && selectionRect.Value.Width > 0 && selectionRect.Value.Height > 0)
        {
            var selGeo = new RectangleGeometry(selectionRect.Value);
            overlay.Data = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, selGeo);
        }
        else
        {
            overlay.Data = fullRect;
        }

        OverlayCanvas.Children.Add(overlay);
    }

    #endregion

    #region Mouse Handling

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownPoint = e.GetPosition(MainCanvas);

        // If text input is active, commit it and consume this click
        if (TextInputBox.Visibility == Visibility.Visible)
        {
            CommitTextInput();
            e.Handled = true;
            return;
        }

        if (_state == CaptureState.OcrSelecting)
        {
            _ocrRegionStart = _mouseDownPoint;
            // Create dashed OCR region rectangle
            _ocrRectVisual = new System.Windows.Shapes.Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xBF, 0xFF)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(0x22, 0x00, 0xBF, 0xFF)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(_ocrRectVisual, _ocrRegionStart.X);
            Canvas.SetTop(_ocrRectVisual, _ocrRegionStart.Y);
            MainCanvas.Children.Add(_ocrRectVisual);
            CaptureMouse();
            return;
        }

        if (_state == CaptureState.Selected || _state == CaptureState.Annotating)
        {
            // Check if clicking an annotation resize handle
            if (_selectedAnnotation != null)
            {
                var annHandle = GetHitAnnotationHandle(_mouseDownPoint);
                if (annHandle != null)
                {
                    _isResizingAnnotation = true;
                    _annotationResizeHandle = annHandle;
                    _annotationOriginalStart = _selectedAnnotation.StartPoint;
                    _annotationOriginalEnd = _selectedAnnotation.EndPoint;
                    if (_selectedAnnotation is FreehandAnnotation fh)
                        _annotationOriginalPoints = new List<Point>(fh.Points);
                    if (_selectedAnnotation is TextAnnotation txtR)
                        _annotationOriginalFontSize = txtR.FontSize;
                    CaptureMouse();
                    return;
                }
            }

            // Check if clicking a selection resize handle
            var handle = GetHitHandle(_mouseDownPoint);
            if (handle != null)
            {
                DeselectAnnotation();
                _isResizing = true;
                _resizeHandle = handle;
                _originalSelectionRect = _selection.Bounds;
                CaptureMouse();
                return;
            }

            // Check if clicking inside selection
            if (_selection.Bounds.Contains(_mouseDownPoint))
            {
                if (_currentTool != AnnotationTool.None)
                {
                    DeselectAnnotation();
                    StartAnnotation(_mouseDownPoint);
                    return;
                }

                // Try to hit-test an annotation for selection/move
                var hitAnnotation = HitTestAnnotations(_mouseDownPoint);
                if (hitAnnotation != null)
                {
                    SelectAnnotation(hitAnnotation);
                    _isMovingAnnotation = true;
                    _annotationOriginalStart = hitAnnotation.StartPoint;
                    _annotationOriginalEnd = hitAnnotation.EndPoint;
                    if (hitAnnotation is FreehandAnnotation fh)
                        _annotationOriginalPoints = new List<Point>(fh.Points);
                    if (hitAnnotation is TextAnnotation txtM)
                        _annotationOriginalFontSize = txtM.FontSize;
                    var annBounds = hitAnnotation is TextAnnotation txtHit
                        ? txtHit.GetTextBounds() : hitAnnotation.Bounds;
                    _annotationDragOffset = new Point(
                        _mouseDownPoint.X - annBounds.X,
                        _mouseDownPoint.Y - annBounds.Y);
                    CaptureMouse();
                    return;
                }

                // Clicked empty space inside selection — just deselect annotation, stay in Selected state
                DeselectAnnotation();
                return;
            }

            // Clicking outside selection — reset if no tool active
            DeselectAnnotation();
            if (_currentTool == AnnotationTool.None)
            {
                _state = CaptureState.Idle;
                HideSelectionUI();
            }
        }

        if (_state == CaptureState.Idle)
        {
            _state = CaptureState.Selecting;
            _selection.StartPoint = _mouseDownPoint;
            _selection.EndPoint = _mouseDownPoint;
            CaptureMouse();
        }
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var pos = e.GetPosition(MainCanvas);

        if (_state == CaptureState.OcrSelecting && e.LeftButton == MouseButtonState.Pressed && _ocrRectVisual != null)
        {
            var x = Math.Min(pos.X, _ocrRegionStart.X);
            var y = Math.Min(pos.Y, _ocrRegionStart.Y);
            var w = Math.Abs(pos.X - _ocrRegionStart.X);
            var h = Math.Abs(pos.Y - _ocrRegionStart.Y);
            Canvas.SetLeft(_ocrRectVisual, x);
            Canvas.SetTop(_ocrRectVisual, y);
            _ocrRectVisual.Width = w;
            _ocrRectVisual.Height = h;
            return;
        }

        if (_state == CaptureState.Selecting && e.LeftButton == MouseButtonState.Pressed)
        {
            _selection.EndPoint = pos;
            UpdateSelectionVisuals();
        }
        else if (_isMovingAnnotation && e.LeftButton == MouseButtonState.Pressed)
        {
            MoveAnnotationTo(pos);
        }
        else if (_isResizingAnnotation && e.LeftButton == MouseButtonState.Pressed)
        {
            ResizeAnnotation(pos);
        }
        else if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            var newX = pos.X - _selectionDragOffset.X;
            var newY = pos.Y - _selectionDragOffset.Y;
            var bounds = _selection.Bounds;
            _selection.SetFromRect(new Rect(newX, newY, bounds.Width, bounds.Height));
            UpdateSelectionVisuals();
            UpdateToolbarPosition();
        }
        else if (_isResizing && e.LeftButton == MouseButtonState.Pressed)
        {
            ResizeSelection(pos);
            UpdateSelectionVisuals();
            UpdateToolbarPosition();
        }
        else if (_isDrawingAnnotation && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateAnnotation(pos);
        }
        else if (_state == CaptureState.Selected || _state == CaptureState.Annotating)
        {
            UpdateCursor(pos);
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(MainCanvas);
        ReleaseMouseCapture();

        if (_state == CaptureState.OcrSelecting)
        {
            var ocrRegion = new Rect(
                Math.Min(pos.X, _ocrRegionStart.X),
                Math.Min(pos.Y, _ocrRegionStart.Y),
                Math.Abs(pos.X - _ocrRegionStart.X),
                Math.Abs(pos.Y - _ocrRegionStart.Y));
            _ = FinishOcrSelectionAsync(ocrRegion);
            return;
        }

        if (_state == CaptureState.Selecting)
        {
            _selection.EndPoint = pos;

            if (!_selection.IsSignificantDrag)
            {
                // Click — full screen capture of the clicked monitor
                // Convert DIP mouse coords back to physical pixels for monitor lookup
                var screenPoint = new System.Drawing.Point(
                    (int)(_mouseDownPoint.X * _dpiScale + _virtualBounds.X),
                    (int)(_mouseDownPoint.Y * _dpiScale + _virtualBounds.Y));

                var monitorService = new MonitorService();
                var monitor = monitorService.GetMonitorFromPoint(screenPoint);
                if (monitor != null)
                {
                    // Convert physical pixel monitor bounds to DIPs
                    var monBounds = monitor.Bounds;
                    _selection.SetFromRect(new Rect(
                        (monBounds.X - _virtualBounds.X) / _dpiScale,
                        (monBounds.Y - _virtualBounds.Y) / _dpiScale,
                        monBounds.Width / _dpiScale,
                        monBounds.Height / _dpiScale));
                }
            }

            _state = CaptureState.Selected;
            UpdateSelectionVisuals();
            ShowSelectionUI();
        }
        else if (_isMovingAnnotation)
        {
            _isMovingAnnotation = false;
            CommitAnnotationMoveResize();
        }
        else if (_isResizingAnnotation)
        {
            _isResizingAnnotation = false;
            _annotationResizeHandle = null;
            CommitAnnotationMoveResize();
        }
        else if (_isDragging)
        {
            _isDragging = false;
        }
        else if (_isResizing)
        {
            _isResizing = false;
            _resizeHandle = null;
        }
        else if (_isDrawingAnnotation)
        {
            FinishAnnotation(pos);
        }
    }

    #endregion

    #region Keyboard Handling

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_state == CaptureState.OcrSelecting)
            {
                CancelOcrSelection();
                e.Handled = true;
                return;
            }
            if (OcrResultPanel.Visibility == Visibility.Visible)
            {
                OcrClose_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            CancelCapture();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (_state == CaptureState.Selected || _state == CaptureState.Annotating)
            {
                ConfirmCapture();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _commandHistory.Undo();
            DeselectAnnotation();
            RedrawAnnotations();
            e.Handled = true;
        }
        else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _commandHistory.Redo();
            DeselectAnnotation();
            RedrawAnnotations();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && _selectedAnnotation != null)
        {
            var cmd = new RemoveAnnotationCommand(_annotations, _selectedAnnotation);
            _commandHistory.Execute(cmd);
            DeselectAnnotation();
            RedrawAnnotations();
            e.Handled = true;
        }
    }

    #endregion

    #region Selection UI

    private void UpdateSelectionVisuals()
    {
        var bounds = _selection.Bounds;
        DrawOverlay(bounds);

        // Position selection border
        SelectionBorder.Visibility = Visibility.Visible;
        SelectionBorder.Width = bounds.Width;
        SelectionBorder.Height = bounds.Height;
        Canvas.SetLeft(SelectionBorder, bounds.X);
        Canvas.SetTop(SelectionBorder, bounds.Y);

        // Update dimension label
        DimensionLabel.Visibility = Visibility.Visible;
        DimensionText.Text = $"{(int)(bounds.Width * _dpiScale)} \u00d7 {(int)(bounds.Height * _dpiScale)}";
        Canvas.SetLeft(DimensionLabel, bounds.X);
        Canvas.SetTop(DimensionLabel, bounds.Bottom + 8);

        if (bounds.Bottom + 30 > Height)
        {
            Canvas.SetTop(DimensionLabel, bounds.Y - 28);
        }

        UpdateHandles(bounds);
    }

    private void ShowSelectionUI()
    {
        SelectionBorder.Visibility = Visibility.Visible;
        HandlesCanvas.Visibility = Visibility.Visible;
        Toolbar.Visibility = Visibility.Visible;
        DimensionLabel.Visibility = Visibility.Visible;
        UpdateToolbarPosition();
        UpdateHandles(_selection.Bounds);
    }

    private void HideSelectionUI()
    {
        SelectionBorder.Visibility = Visibility.Collapsed;
        HandlesCanvas.Visibility = Visibility.Collapsed;
        Toolbar.Visibility = Visibility.Collapsed;
        DimensionLabel.Visibility = Visibility.Collapsed;
        DrawOverlay(null);
    }

    private void UpdateToolbarPosition()
    {
        var bounds = _selection.Bounds;
        var barSize = Toolbar.BarSize;
        var toolbarWidth = barSize.Width;
        var toolbarHeight = barSize.Height;

        // Center horizontally over selection, clamped to window
        var left = bounds.X + (bounds.Width - toolbarWidth) / 2;
        left = Math.Max(4, Math.Min(left, Width - toolbarWidth - 4));

        // Find the monitor that contains the selection center, and get its bottom edge in DIPs.
        // This prevents the toolbar from being placed in a dead zone between monitors.
        var selCenterPhys = new System.Drawing.Point(
            (int)((bounds.X + bounds.Width / 2) * _dpiScale + _virtualBounds.X),
            (int)((bounds.Y + bounds.Height / 2) * _dpiScale + _virtualBounds.Y));
        var monBounds = Win32Interop.GetMonitorPhysicalBounds(selCenterPhys);
        var monBottomDip = (monBounds.Y + monBounds.Height - _virtualBounds.Y) / _dpiScale;
        var monTopDip = (monBounds.Y - _virtualBounds.Y) / _dpiScale;

        // Prefer below the selection; flip above if it would go below the monitor
        var top = bounds.Bottom + 8;
        if (top + toolbarHeight > monBottomDip - 4)
            top = bounds.Y - toolbarHeight - 8;

        // If above would go above the monitor, place inside selection near top
        // (near-bottom can land in the region below the primary monitor's height, which
        // may not render reliably in multi-monitor setups with different screen heights)
        if (top < monTopDip + 4)
            top = bounds.Y + 8;

        Canvas.SetLeft(Toolbar, left);
        Canvas.SetTop(Toolbar, top);
    }

    #endregion

    #region Resize Handles

    private void UpdateHandles(Rect bounds)
    {
        HandlesCanvas.Children.Clear();
        _handleRects.Clear();

        if (bounds.Width < 1 || bounds.Height < 1) return;

        HandlesCanvas.Visibility = Visibility.Visible;
        var half = HandleSize / 2;

        var positions = new Dictionary<string, Point>
        {
            ["NW"] = new(bounds.Left, bounds.Top),
            ["N"] = new(bounds.Left + bounds.Width / 2, bounds.Top),
            ["NE"] = new(bounds.Right, bounds.Top),
            ["W"] = new(bounds.Left, bounds.Top + bounds.Height / 2),
            ["E"] = new(bounds.Right, bounds.Top + bounds.Height / 2),
            ["SW"] = new(bounds.Left, bounds.Bottom),
            ["S"] = new(bounds.Left + bounds.Width / 2, bounds.Bottom),
            ["SE"] = new(bounds.Right, bounds.Bottom)
        };

        foreach (var (name, point) in positions)
        {
            var rect = new Rect(point.X - half, point.Y - half, HandleSize, HandleSize);
            _handleRects[name] = rect;

            var handle = new WpfRectangle
            {
                Width = HandleSize,
                Height = HandleSize,
                Fill = System.Windows.Media.Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                StrokeThickness = 1,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(handle, rect.X);
            Canvas.SetTop(handle, rect.Y);
            HandlesCanvas.Children.Add(handle);
        }
    }

    private string? GetHitHandle(Point point)
    {
        foreach (var (name, rect) in _handleRects)
        {
            var inflated = rect;
            inflated.Inflate(4, 4);
            if (inflated.Contains(point)) return name;
        }
        return null;
    }

    private void ResizeSelection(Point pos)
    {
        var rect = _originalSelectionRect;

        switch (_resizeHandle)
        {
            case "NW":
                _selection.SetFromRect(new Rect(pos.X, pos.Y, rect.Right - pos.X, rect.Bottom - pos.Y));
                break;
            case "N":
                _selection.SetFromRect(new Rect(rect.X, pos.Y, rect.Width, rect.Bottom - pos.Y));
                break;
            case "NE":
                _selection.SetFromRect(new Rect(rect.X, pos.Y, pos.X - rect.X, rect.Bottom - pos.Y));
                break;
            case "W":
                _selection.SetFromRect(new Rect(pos.X, rect.Y, rect.Right - pos.X, rect.Height));
                break;
            case "E":
                _selection.SetFromRect(new Rect(rect.X, rect.Y, pos.X - rect.X, rect.Height));
                break;
            case "SW":
                _selection.SetFromRect(new Rect(pos.X, rect.Y, rect.Right - pos.X, pos.Y - rect.Y));
                break;
            case "S":
                _selection.SetFromRect(new Rect(rect.X, rect.Y, rect.Width, pos.Y - rect.Y));
                break;
            case "SE":
                _selection.SetFromRect(new Rect(rect.X, rect.Y, pos.X - rect.X, pos.Y - rect.Y));
                break;
        }
    }

    private void UpdateCursor(Point pos)
    {
        // Check annotation handles first
        if (_selectedAnnotation != null)
        {
            var annHandle = GetHitAnnotationHandle(pos);
            if (annHandle != null)
            {
                Cursor = annHandle switch
                {
                    "NW" or "SE" => System.Windows.Input.Cursors.SizeNWSE,
                    "NE" or "SW" => System.Windows.Input.Cursors.SizeNESW,
                    "Start" or "End" => System.Windows.Input.Cursors.Cross,
                    _ => System.Windows.Input.Cursors.SizeAll
                };
                return;
            }
        }

        // Check selection handles
        var handle = GetHitHandle(pos);
        if (handle != null)
        {
            Cursor = handle switch
            {
                "NW" or "SE" => System.Windows.Input.Cursors.SizeNWSE,
                "NE" or "SW" => System.Windows.Input.Cursors.SizeNESW,
                "N" or "S" => System.Windows.Input.Cursors.SizeNS,
                "E" or "W" => System.Windows.Input.Cursors.SizeWE,
                _ => System.Windows.Input.Cursors.Arrow
            };
            return;
        }

        if (_selection.Bounds.Contains(pos))
        {
            if (_currentTool != AnnotationTool.None)
            {
                Cursor = System.Windows.Input.Cursors.Cross;
            }
            else if (_selectedAnnotation != null && _selectedAnnotation.HitTest(pos))
            {
                Cursor = System.Windows.Input.Cursors.SizeAll;
            }
            else
            {
                // Show hand cursor when hovering over any annotation (hints it's selectable)
                var hitAnn = HitTestAnnotations(pos);
                Cursor = hitAnn != null
                    ? System.Windows.Input.Cursors.Hand
                    : System.Windows.Input.Cursors.SizeAll;
            }
            return;
        }

        Cursor = System.Windows.Input.Cursors.Cross;
    }

    #endregion

    #region Annotations

    private void OnToolSelected(object? sender, AnnotationTool tool)
    {
        _currentTool = tool;
        DeselectAnnotation();
        if (tool != AnnotationTool.None)
            _state = CaptureState.Annotating;
        else if (_state == CaptureState.Annotating)
            _state = CaptureState.Selected;
    }

    private void OnColorChanged(object? sender, Color color)
    {
        _currentColor = color;

        // Apply to selected annotation immediately
        if (_selectedAnnotation != null)
        {
            _selectedAnnotation.StrokeColor = color;
            RedrawAnnotations();
        }
    }

    private void OnStrokeWidthChanged(object? sender, double width)
    {
        _currentStrokeWidth = width;

        // Apply to selected annotation immediately
        if (_selectedAnnotation != null)
        {
            _selectedAnnotation.StrokeWidth = width;
            RedrawAnnotations();
        }
    }

    private Point ClampToSelection(Point pos)
    {
        var bounds = _selection.Bounds;
        return new Point(
            Math.Clamp(pos.X, bounds.X, bounds.Right),
            Math.Clamp(pos.Y, bounds.Y, bounds.Bottom));
    }

    private void StartAnnotation(Point pos)
    {
        pos = ClampToSelection(pos);

        if (_currentTool == AnnotationTool.Text)
        {
            ShowTextInput(pos);
            return;
        }

        _isDrawingAnnotation = true;
        _currentAnnotation = _currentTool switch
        {
            AnnotationTool.Arrow => new ArrowAnnotation(),
            AnnotationTool.Rectangle => new RectangleAnnotation(),
            AnnotationTool.Ellipse => new EllipseAnnotation(),
            AnnotationTool.Line => new LineAnnotation(),
            AnnotationTool.Blur => new BlurAnnotation(),
            AnnotationTool.Freehand => new FreehandAnnotation(),
            _ => null
        };

        if (_currentAnnotation != null)
        {
            _currentAnnotation.StrokeColor = _currentColor;
            _currentAnnotation.StrokeWidth = _currentStrokeWidth;
            _currentAnnotation.StartPoint = pos;
            _currentAnnotation.EndPoint = pos;

            if (_currentAnnotation is FreehandAnnotation freehand)
            {
                freehand.Points.Add(pos);
            }
        }

        CaptureMouse();
    }

    private void UpdateAnnotation(Point pos)
    {
        if (_currentAnnotation == null) return;

        pos = ClampToSelection(pos);
        _currentAnnotation.EndPoint = pos;

        if (_currentAnnotation is FreehandAnnotation freehand)
        {
            freehand.Points.Add(pos);
        }

        RedrawAnnotations(_currentAnnotation);
    }

    private void FinishAnnotation(Point pos)
    {
        _isDrawingAnnotation = false;
        ReleaseMouseCapture();

        if (_currentAnnotation == null) return;

        pos = ClampToSelection(pos);
        _currentAnnotation.EndPoint = pos;

        bool isValid = _currentAnnotation switch
        {
            FreehandAnnotation fh => fh.Points.Count > 2,
            _ => _currentAnnotation.Bounds.Width > 2 || _currentAnnotation.Bounds.Height > 2
        };

        if (isValid)
        {
            if (_currentAnnotation is BlurAnnotation blur)
            {
                UpdateBlurPreview(blur);
            }

            var cmd = new AddAnnotationCommand(_annotations, _currentAnnotation);
            _commandHistory.Execute(cmd);

            // Auto-select the just-drawn annotation so user sees it can be moved/resized
            var finishedAnnotation = _currentAnnotation;
            _currentAnnotation = null;
            _currentTool = AnnotationTool.None;
            _state = CaptureState.Selected;
            Toolbar.ResetTools();
            SelectAnnotation(finishedAnnotation);
        }
        else
        {
            _currentAnnotation = null;
            _currentTool = AnnotationTool.None;
            _state = CaptureState.Selected;
            Toolbar.ResetTools();
            RedrawAnnotations();
        }
    }

    private void UpdateBlurPreview(BlurAnnotation blur)
    {
        var bounds = blur.Bounds;
        if (bounds.Width < 2 || bounds.Height < 2) return;

        var region = new System.Windows.Int32Rect(
            (int)(bounds.X * _dpiScale), (int)(bounds.Y * _dpiScale),
            (int)(bounds.Width * _dpiScale), (int)(bounds.Height * _dpiScale));

        try
        {
            blur.PixelatedBitmap = BlurRenderer.CreatePixelatedBitmap(_screenshot, region);
        }
        catch
        {
            // Silently fail — fallback rendering will be used
        }
    }

    private void ShowTextInput(Point pos)
    {
        pos = ClampToSelection(pos);
        TextInputBox.Visibility = Visibility.Visible;
        TextInputBox.Text = "";
        TextInputBox.FontSize = 16;
        TextInputBox.Foreground = new SolidColorBrush(_currentColor);
        Canvas.SetLeft(TextInputBox, pos.X);
        Canvas.SetTop(TextInputBox, pos.Y);
        TextInputBox.Tag = pos;
        TextInputBox.Focus();
    }

    private void TextInput_LostFocus(object sender, RoutedEventArgs e)
    {
        // Only commit on LostFocus if the TextBox is still visible
        // (OnMouseDown may have already committed and hidden it)
        if (TextInputBox.Visibility == Visibility.Visible)
            CommitTextInput();
    }

    private void TextInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            TextInputBox.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            CommitTextInput();
            e.Handled = true;
        }
    }

    private void CommitTextInput()
    {
        if (TextInputBox.Visibility != Visibility.Visible) return;
        TextInputBox.Visibility = Visibility.Collapsed;

        var text = TextInputBox.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var pos = (Point)TextInputBox.Tag;
        var annotation = new TextAnnotation
        {
            Text = text,
            StartPoint = pos,
            EndPoint = pos,
            StrokeColor = _currentColor,
            FontSize = 16
        };

        var cmd = new AddAnnotationCommand(_annotations, annotation);
        _commandHistory.Execute(cmd);

        // Auto-select the just-created text annotation
        _currentTool = AnnotationTool.None;
        _state = CaptureState.Selected;
        Toolbar.ResetTools();
        SelectAnnotation(annotation);
    }

    private AnnotationBase? HitTestAnnotations(Point point)
    {
        // Test in reverse order (top-most first)
        for (int i = _annotations.Count - 1; i >= 0; i--)
        {
            if (_annotations[i].HitTest(point))
                return _annotations[i];
        }
        return null;
    }

    private void SelectAnnotation(AnnotationBase annotation)
    {
        _selectedAnnotation = annotation;
        RedrawAnnotations();

        // Return keyboard focus to the window so Enter/Escape/Delete work
        Keyboard.Focus(this);
    }

    private void DeselectAnnotation()
    {
        if (_selectedAnnotation == null) return;
        _selectedAnnotation = null;
        _annotationHandleRects.Clear();
        RedrawAnnotations();

        // Return keyboard focus to the window so Enter/Escape work
        Keyboard.Focus(this);
    }

    private string? GetHitAnnotationHandle(Point point)
    {
        foreach (var (name, rect) in _annotationHandleRects)
        {
            var inflated = rect;
            inflated.Inflate(4, 4);
            if (inflated.Contains(point)) return name;
        }
        return null;
    }

    private void MoveAnnotationTo(Point pos)
    {
        if (_selectedAnnotation == null) return;

        var annBounds = _selectedAnnotation is TextAnnotation txt
            ? txt.GetTextBounds() : _selectedAnnotation.Bounds;
        var newX = pos.X - _annotationDragOffset.X;
        var newY = pos.Y - _annotationDragOffset.Y;
        var dx = newX - annBounds.X;
        var dy = newY - annBounds.Y;

        _selectedAnnotation.StartPoint = new Point(
            _selectedAnnotation.StartPoint.X + dx,
            _selectedAnnotation.StartPoint.Y + dy);
        _selectedAnnotation.EndPoint = new Point(
            _selectedAnnotation.EndPoint.X + dx,
            _selectedAnnotation.EndPoint.Y + dy);

        if (_selectedAnnotation is FreehandAnnotation freehand)
        {
            for (int i = 0; i < freehand.Points.Count; i++)
            {
                freehand.Points[i] = new Point(
                    freehand.Points[i].X + dx,
                    freehand.Points[i].Y + dy);
            }
        }
        else if (_selectedAnnotation is BlurAnnotation blur)
        {
            UpdateBlurPreview(blur);
        }

        RedrawAnnotations();
    }

    private void ResizeAnnotation(Point pos)
    {
        if (_selectedAnnotation == null || _annotationResizeHandle == null) return;

        // For freehand, only support move, not resize
        if (_selectedAnnotation is FreehandAnnotation) return;

        // Text resize: scale FontSize based on dragged height
        if (_selectedAnnotation is TextAnnotation textAnn)
        {
            var origBounds = textAnn.GetTextBounds();
            double newHeight;

            switch (_annotationResizeHandle)
            {
                case "NW":
                case "NE":
                    // Dragging top edge: bottom stays fixed
                    newHeight = origBounds.Bottom - pos.Y;
                    if (newHeight > 8)
                    {
                        var scale = newHeight / origBounds.Height;
                        textAnn.FontSize = Math.Clamp(textAnn.FontSize * scale, 8, 200);
                        // Anchor bottom-left, recalculate top
                        var newBounds = textAnn.GetTextBounds();
                        textAnn.StartPoint = new Point(
                            _annotationResizeHandle == "NW" ? pos.X : textAnn.StartPoint.X,
                            origBounds.Bottom - newBounds.Height);
                    }
                    break;
                case "SW":
                case "SE":
                    // Dragging bottom edge: top stays fixed
                    newHeight = pos.Y - origBounds.Top;
                    if (newHeight > 8)
                    {
                        var scale = newHeight / origBounds.Height;
                        textAnn.FontSize = Math.Clamp(textAnn.FontSize * scale, 8, 200);
                        if (_annotationResizeHandle == "SW")
                            textAnn.StartPoint = new Point(pos.X, textAnn.StartPoint.Y);
                    }
                    break;
            }

            RedrawAnnotations();
            return;
        }

        var start = _selectedAnnotation.StartPoint;
        var end = _selectedAnnotation.EndPoint;

        switch (_annotationResizeHandle)
        {
            case "Start":
                _selectedAnnotation.StartPoint = pos;
                break;
            case "End":
                _selectedAnnotation.EndPoint = pos;
                break;
            case "NW":
                _selectedAnnotation.StartPoint = new Point(pos.X, pos.Y);
                break;
            case "NE":
                _selectedAnnotation.StartPoint = new Point(start.X, pos.Y);
                _selectedAnnotation.EndPoint = new Point(pos.X, end.Y);
                break;
            case "SW":
                _selectedAnnotation.StartPoint = new Point(pos.X, start.Y);
                _selectedAnnotation.EndPoint = new Point(end.X, pos.Y);
                break;
            case "SE":
                _selectedAnnotation.EndPoint = new Point(pos.X, pos.Y);
                break;
        }

        if (_selectedAnnotation is BlurAnnotation blur)
        {
            UpdateBlurPreview(blur);
        }

        RedrawAnnotations();
    }

    private void CommitAnnotationMoveResize()
    {
        if (_selectedAnnotation == null) return;

        var newStart = _selectedAnnotation.StartPoint;
        var newEnd = _selectedAnnotation.EndPoint;
        List<Point>? newPoints = null;
        double newFontSize = 0;
        if (_selectedAnnotation is FreehandAnnotation fh)
            newPoints = new List<Point>(fh.Points);
        if (_selectedAnnotation is TextAnnotation txtCommit)
            newFontSize = txtCommit.FontSize;

        // Only commit if something actually changed
        bool fontChanged = _selectedAnnotation is TextAnnotation && newFontSize != _annotationOriginalFontSize;
        if (newStart != _annotationOriginalStart || newEnd != _annotationOriginalEnd || fontChanged)
        {
            // Restore original state first so the command can properly execute
            _selectedAnnotation.StartPoint = _annotationOriginalStart;
            _selectedAnnotation.EndPoint = _annotationOriginalEnd;
            if (_selectedAnnotation is FreehandAnnotation freehand && _annotationOriginalPoints != null)
            {
                freehand.Points.Clear();
                freehand.Points.AddRange(_annotationOriginalPoints);
            }
            if (_selectedAnnotation is TextAnnotation txtRestore)
                txtRestore.FontSize = _annotationOriginalFontSize;

            var cmd = new MoveAnnotationCommand(
                _selectedAnnotation,
                _annotationOriginalStart, _annotationOriginalEnd,
                newStart, newEnd,
                _annotationOriginalPoints, newPoints,
                _annotationOriginalFontSize, newFontSize);
            _commandHistory.Execute(cmd);
        }

        _annotationOriginalPoints = null;
        RedrawAnnotations();
    }

    private void DrawAnnotationHandles(Rect bounds, bool isLineType)
    {
        _annotationHandleRects.Clear();
        var half = AnnotationHandleSize / 2;

        Dictionary<string, Point> positions;
        if (isLineType && _selectedAnnotation != null)
        {
            // For arrows/lines, show handles at start and end points
            positions = new Dictionary<string, Point>
            {
                ["Start"] = _selectedAnnotation.StartPoint,
                ["End"] = _selectedAnnotation.EndPoint
            };
        }
        else
        {
            // For rect/ellipse/blur, show corner handles
            positions = new Dictionary<string, Point>
            {
                ["NW"] = new(bounds.Left, bounds.Top),
                ["NE"] = new(bounds.Right, bounds.Top),
                ["SW"] = new(bounds.Left, bounds.Bottom),
                ["SE"] = new(bounds.Right, bounds.Bottom)
            };
        }

        foreach (var (name, point) in positions)
        {
            var rect = new Rect(point.X - half, point.Y - half, AnnotationHandleSize, AnnotationHandleSize);
            _annotationHandleRects[name] = rect;

            var handle = new WpfRectangle
            {
                Width = AnnotationHandleSize,
                Height = AnnotationHandleSize,
                Fill = System.Windows.Media.Brushes.White,
                Stroke = new SolidColorBrush(Colors.Orange),
                StrokeThickness = 1.5,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(handle, rect.X);
            Canvas.SetTop(handle, rect.Y);
            AnnotationCanvas.Children.Add(handle);
        }
    }

    private void RedrawAnnotations(AnnotationBase? temporary = null)
    {
        AnnotationCanvas.Children.Clear();

        var drawingVisual = new DrawingVisual();
        using (var dc = drawingVisual.RenderOpen())
        {
            foreach (var annotation in _annotations)
            {
                annotation.Render(dc);
            }
            temporary?.Render(dc);

            // Draw selection highlight around selected annotation
            if (_selectedAnnotation != null && _annotations.Contains(_selectedAnnotation))
            {
                var selBounds = _selectedAnnotation is TextAnnotation txt
                    ? txt.GetTextBounds() : _selectedAnnotation.Bounds;
                selBounds.Inflate(3, 3);
                var dashPen = new Pen(new SolidColorBrush(Colors.Orange), 1.5)
                {
                    DashStyle = DashStyles.Dash
                };
                dashPen.Freeze();
                dc.DrawRectangle(null, dashPen, selBounds);
            }
        }

        var renderTarget = new RenderTargetBitmap(
            Math.Max(1, (int)Width), Math.Max(1, (int)Height), 96, 96, PixelFormats.Pbgra32);
        renderTarget.Render(drawingVisual);

        var image = new System.Windows.Controls.Image
        {
            Source = renderTarget,
            Width = Width,
            Height = Height,
            IsHitTestVisible = false
        };

        AnnotationCanvas.Children.Add(image);

        // Draw annotation resize handles on top (as Canvas elements, not in the bitmap)
        if (_selectedAnnotation != null && _annotations.Contains(_selectedAnnotation))
        {
            bool isLineType = _selectedAnnotation is ArrowAnnotation or LineAnnotation;
            var handleBounds = _selectedAnnotation is TextAnnotation txtAnn
                ? txtAnn.GetTextBounds() : _selectedAnnotation.Bounds;
            DrawAnnotationHandles(handleBounds, isLineType);
        }
    }

    #endregion

    #region Confirm / Cancel

    private void ConfirmCapture()
    {
        if (_state != CaptureState.Selected && _state != CaptureState.Annotating) return;

        CommitTextInput();

        var selBounds = _selection.Bounds;

        ClipboardService.CopyToClipboard(_screenshot, selBounds, _annotations, _dpiScale);

        Close();

        var toast = new ToastWindow();
        toast.ShowToast();
    }

    private void CancelCapture()
    {
        Close();
    }

    private void RunOcrAsync()
    {
        if (_state != CaptureState.Selected && _state != CaptureState.Annotating) return;
        StartOcrSelection();
    }

    private void StartOcrSelection()
    {
        _stateBeforeOcr = _state;
        _state = CaptureState.OcrSelecting;
        CommitTextInput();
        DeselectAnnotationSilent();
        Toolbar.Visibility = Visibility.Collapsed;
        Cursor = System.Windows.Input.Cursors.Cross;

        // Hint label
        var hint = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x00, 0x00, 0x00)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 4, 10, 4),
            IsHitTestVisible = false
        };
        var hintText = new TextBlock
        {
            Text = "Drag to select OCR region  ·  Esc to cancel",
            Foreground = Brushes.White,
            FontSize = 13,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
        };
        hint.Child = hintText;
        hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(hint, (Width - hint.DesiredSize.Width) / 2);
        Canvas.SetTop(hint, _selection.Bounds.Top - hint.DesiredSize.Height - 10);
        MainCanvas.Children.Add(hint);
        _ocrHintLabel = hint;
    }

    private void CancelOcrSelection()
    {
        CleanupOcrVisuals();
        _state = _stateBeforeOcr;
        Toolbar.Visibility = Visibility.Visible;
        UpdateToolbarPosition();
        Cursor = System.Windows.Input.Cursors.Arrow;
    }

    private async Task FinishOcrSelectionAsync(Rect ocrRegion)
    {
        CleanupOcrVisuals();
        _state = _stateBeforeOcr;
        Cursor = System.Windows.Input.Cursors.Arrow;

        // Clamp OCR region to selection bounds
        ocrRegion.Intersect(_selection.Bounds);
        if (ocrRegion.IsEmpty || ocrRegion.Width < 4 || ocrRegion.Height < 4)
        {
            Toolbar.Visibility = Visibility.Visible;
            UpdateToolbarPosition();
            return;
        }

        var text = await OcrService.RecognizeAsync(_screenshot, ocrRegion, _dpiScale);

        if (text == null)
        {
            Toolbar.Visibility = Visibility.Visible;
            UpdateToolbarPosition();
            Close();
            new ToastWindow("OCR unavailable — install a language pack in Windows Settings").ShowToast();
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Toolbar.Visibility = Visibility.Visible;
            UpdateToolbarPosition();
            new ToastWindow("No text found in selected region").ShowToast();
            return;
        }

        // Show the result panel — hide toolbar while panel is open
        ShowOcrResultPanel(text, ocrRegion);
    }

    private void ShowOcrResultPanel(string text, Rect ocrRegion)
    {
        OcrResultText.Text = text;
        OcrResultPanel.Visibility = Visibility.Visible;

        // Position the panel: prefer below the OCR region, fall back to above or center
        OcrResultPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var panelW = OcrResultPanel.DesiredSize.Width;
        var panelH = OcrResultPanel.DesiredSize.Height;

        var left = ocrRegion.X + (ocrRegion.Width - panelW) / 2;
        var top = ocrRegion.Bottom + 12;

        if (top + panelH > Height) top = ocrRegion.Y - panelH - 12;
        if (top < 4) top = (Height - panelH) / 2;
        left = Math.Max(4, Math.Min(left, Width - panelW - 4));

        Canvas.SetLeft(OcrResultPanel, left);
        Canvas.SetTop(OcrResultPanel, top);

        OcrResultText.Focus();
        OcrResultText.SelectAll();
    }

    private void OcrCopy_Click(object sender, RoutedEventArgs e)
    {
        var text = OcrResultText.Text;
        if (!string.IsNullOrWhiteSpace(text))
            Clipboard.SetText(text);

        OcrResultPanel.Visibility = Visibility.Collapsed;
        Toolbar.Visibility = Visibility.Visible;
        UpdateToolbarPosition();
        Close();
        new ToastWindow("Text copied to clipboard").ShowToast();
    }

    private void OcrClose_Click(object sender, RoutedEventArgs e)
    {
        OcrResultPanel.Visibility = Visibility.Collapsed;
        Toolbar.Visibility = Visibility.Visible;
        UpdateToolbarPosition();
        Keyboard.Focus(this);
    }

    // Allow the panel to be dragged
    private void OcrPanel_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingOcrPanel = true;
        var panelPos = new Point(Canvas.GetLeft(OcrResultPanel), Canvas.GetTop(OcrResultPanel));
        var mousePos = e.GetPosition(MainCanvas);
        _ocrPanelDragOffset = new Point(mousePos.X - panelPos.X, mousePos.Y - panelPos.Y);
        OcrResultPanel.CaptureMouse();
        e.Handled = true;
    }

    private void OcrPanel_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDraggingOcrPanel) return;
        var pos = e.GetPosition(MainCanvas);
        Canvas.SetLeft(OcrResultPanel, pos.X - _ocrPanelDragOffset.X);
        Canvas.SetTop(OcrResultPanel, pos.Y - _ocrPanelDragOffset.Y);
        e.Handled = true;
    }

    private void OcrPanel_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingOcrPanel = false;
        OcrResultPanel.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void CleanupOcrVisuals()
    {
        if (_ocrRectVisual != null)
        {
            MainCanvas.Children.Remove(_ocrRectVisual);
            _ocrRectVisual = null;
        }
        if (_ocrHintLabel != null)
        {
            MainCanvas.Children.Remove(_ocrHintLabel);
            _ocrHintLabel = null;
        }
    }

    // Variant that skips the Keyboard.Focus call (used during OCR selection cancel)
    private void DeselectAnnotationSilent()
    {
        if (_selectedAnnotation == null) return;
        _selectedAnnotation = null;
        _annotationHandleRects.Clear();
        RedrawAnnotations();
    }

    #endregion
}
