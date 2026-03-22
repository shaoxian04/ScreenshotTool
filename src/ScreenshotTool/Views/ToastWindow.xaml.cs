using System.Windows;
using System.Windows.Media.Animation;

namespace ScreenshotTool.Views;

public partial class ToastWindow : Window
{
    public ToastWindow(string message = "Screenshot copied to clipboard")
    {
        InitializeComponent();
        MessageText.Text = message;

        // Position near bottom-right of primary screen
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - 350;
        Top = workArea.Bottom - 80;
        Opacity = 0;
    }

    public void ShowToast()
    {
        Show();

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        fadeIn.Completed += async (_, _) =>
        {
            await Task.Delay(1500);
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        };
        BeginAnimation(OpacityProperty, fadeIn);
    }
}
