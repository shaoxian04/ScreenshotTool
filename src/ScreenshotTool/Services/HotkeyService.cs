using System.Windows;
using System.Windows.Interop;
using ScreenshotTool.Helpers;

namespace ScreenshotTool.Services;

public class HotkeyService
{
    private const int HOTKEY_ID = 1;
    private HwndSource? _hwndSource;
    private Window? _hiddenWindow;

    public event EventHandler? HotkeyPressed;

    public void Register()
    {
        _hiddenWindow = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false
        };
        _hiddenWindow.Show();
        _hiddenWindow.Hide();

        var handle = new WindowInteropHelper(_hiddenWindow).Handle;
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);

        Win32Interop.RegisterHotKey(handle, HOTKEY_ID,
            Win32Interop.MOD_CONTROL | Win32Interop.MOD_SHIFT, Win32Interop.VK_S);
    }

    public void Unregister()
    {
        if (_hiddenWindow != null)
        {
            var handle = new WindowInteropHelper(_hiddenWindow).Handle;
            Win32Interop.UnregisterHotKey(handle, HOTKEY_ID);
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource?.Dispose();
            _hiddenWindow.Close();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Interop.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }
}
