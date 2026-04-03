using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenshotTool.Models;

namespace ScreenshotTool.Views;

public partial class ToolbarControl : UserControl
{
    public event EventHandler<AnnotationTool>? ToolSelected;
    public event EventHandler<Color>? ColorChanged;
    public event EventHandler<double>? StrokeWidthChanged;
    public event EventHandler? ConfirmClicked;
    public event EventHandler? CancelClicked;
    public event EventHandler? OcrClicked;

    private readonly ToggleButton[] _toolButtons;
    private readonly DispatcherTimer _paletteCloseTimer;

    public ToolbarControl()
    {
        InitializeComponent();
        _toolButtons = new[] { ArrowBtn, RectBtn, EllipseBtn, LineBtn, TextBtn, BlurBtn, FreehandBtn };

        _paletteCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _paletteCloseTimer.Tick += (_, _) =>
        {
            _paletteCloseTimer.Stop();
            if (!ColorPickerBorder.IsMouseOver && !ColorPalette.IsMouseOver)
                ColorPalette.Visibility = Visibility.Collapsed;
        };
    }

    private void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton clicked)
        {
            foreach (var btn in _toolButtons)
            {
                if (btn != clicked) btn.IsChecked = false;
            }

            var tool = clicked.IsChecked == true
                ? (AnnotationTool)clicked.Tag
                : AnnotationTool.None;

            ToolSelected?.Invoke(this, tool);
        }
    }

    private void ColorPicker_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _paletteCloseTimer.Stop();
        ShowColorPalette();
    }

    private void ColorPicker_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _paletteCloseTimer.Start();
    }

    private void ColorPicker_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ColorPalette.Visibility == Visibility.Visible)
            ColorPalette.Visibility = Visibility.Collapsed;
        else
            ShowColorPalette();
        e.Handled = true;
    }

    private void ShowColorPalette()
    {
        ColorPalette.Visibility = Visibility.Visible;
        // Position the palette above the toolbar bar with no gap
        ColorPalette.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var paletteHeight = ColorPalette.DesiredSize.Height;
        Canvas.SetTop(ColorPalette, -paletteHeight);
        // Center horizontally over the color picker
        var pickerPos = ColorPickerBorder.TranslatePoint(new Point(0, 0), ToolbarCanvas);
        var paletteWidth = ColorPalette.DesiredSize.Width;
        var left = pickerPos.X + ColorPickerBorder.ActualWidth / 2 - paletteWidth / 2;
        Canvas.SetLeft(ColorPalette, left);
    }

    private void ColorPalette_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _paletteCloseTimer.Stop();
    }

    private void ColorPalette_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _paletteCloseTimer.Start();
    }

    private void ColorSelected(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string colorStr)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorStr);
            ColorBrush.Color = color;
            ColorPalette.Visibility = Visibility.Collapsed;
            ColorChanged?.Invoke(this, color);
        }
    }

    private void StrokeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        StrokeWidthChanged?.Invoke(this, e.NewValue);
    }

    private void Ocr_Click(object sender, RoutedEventArgs e)
    {
        OcrClicked?.Invoke(this, EventArgs.Empty);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        ConfirmClicked?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelClicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Returns the pixel size of the visible toolbar bar (excludes the floating color palette).</summary>
    public Size BarSize
    {
        get
        {
            ToolbarBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var w = ToolbarBar.ActualWidth > 0 ? ToolbarBar.ActualWidth : ToolbarBar.DesiredSize.Width;
            var h = ToolbarBar.ActualHeight > 0 ? ToolbarBar.ActualHeight : ToolbarBar.DesiredSize.Height;
            return new Size(w > 0 ? w : 480, h > 0 ? h : 44);
        }
    }

    public void ResetTools()
    {
        foreach (var btn in _toolButtons)
            btn.IsChecked = false;
    }

    // Prevent mouse events from bubbling up to the overlay window
    private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void Border_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void Border_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        e.Handled = true;
    }
}
