using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ScreenshotTool.Models;

namespace ScreenshotTool.Views;

public partial class ToolbarControl : UserControl
{
    public event EventHandler<AnnotationTool>? ToolSelected;
    public event EventHandler<Color>? ColorChanged;
    public event EventHandler<double>? StrokeWidthChanged;
    public event EventHandler? ConfirmClicked;
    public event EventHandler? CancelClicked;

    private readonly ToggleButton[] _toolButtons;

    public ToolbarControl()
    {
        InitializeComponent();
        _toolButtons = new[] { ArrowBtn, RectBtn, EllipseBtn, LineBtn, TextBtn, BlurBtn, FreehandBtn };
    }

    private void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton clicked)
        {
            // Uncheck all other buttons
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
        ColorPopup.IsOpen = true;
    }

    private void ColorPicker_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ColorPopup.IsOpen = !ColorPopup.IsOpen;
    }

    private void ColorPopup_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Small delay to allow moving back — close only if mouse truly left
        var popup = ColorPopup;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            if (!ColorPickerBorder.IsMouseOver && popup.IsOpen)
            {
                // Check if mouse is over the popup content
                var popupChild = popup.Child as Border;
                if (popupChild != null && !popupChild.IsMouseOver)
                    popup.IsOpen = false;
            }
        });
    }

    private void ColorSelected(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string colorStr)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorStr);
            ColorBrush.Color = color;
            ColorPopup.IsOpen = false;
            ColorChanged?.Invoke(this, color);
        }
    }

    private void StrokeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        StrokeWidthChanged?.Invoke(this, e.NewValue);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        ConfirmClicked?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelClicked?.Invoke(this, EventArgs.Empty);
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
