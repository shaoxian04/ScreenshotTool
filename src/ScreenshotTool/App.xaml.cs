using System.Windows;
using ScreenshotTool.Services;
using ScreenshotTool.Views;

namespace ScreenshotTool;

public partial class App : Application
{
    private TrayIconManager? _trayIconManager;
    private HotkeyService? _hotkeyService;
    private OverlayWindow? _activeOverlay;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _hotkeyService.Register();

        _trayIconManager = new TrayIconManager();
        _trayIconManager.ExitRequested += (_, _) => Shutdown();
        _trayIconManager.CaptureRequested += (_, _) => StartCapture();
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        StartCapture();
    }

    private void StartCapture()
    {
        // Prevent duplicate overlays — ignore if one is already open
        if (_activeOverlay != null && _activeOverlay.IsVisible)
            return;

        var captureService = new ScreenCaptureService();
        var monitorService = new MonitorService();

        var monitors = monitorService.GetMonitors();

        var (screenshot, virtualBounds) = captureService.CaptureAllScreens();

        _activeOverlay = new OverlayWindow(screenshot, monitors, virtualBounds);
        _activeOverlay.Closed += (_, _) => _activeOverlay = null;
        _activeOverlay.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Unregister();
        _trayIconManager?.Dispose();
        base.OnExit(e);
    }
}
