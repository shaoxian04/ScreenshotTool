using System.Windows;

namespace ScreenshotTool;

public static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    public static void Main()
    {
        const string mutexName = "ScreenshotTool_SingleInstance_Mutex";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show("Screenshot Tool is already running.", "Screenshot Tool",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
