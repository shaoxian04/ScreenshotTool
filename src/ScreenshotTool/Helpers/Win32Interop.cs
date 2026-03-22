using System.Runtime.InteropServices;

namespace ScreenshotTool.Helpers;

public static class Win32Interop
{
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForSystem();

    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint VK_S = 0x53;

    /// <summary>
    /// Returns the system DPI scale factor (e.g., 1.0 for 100%, 1.5 for 150%).
    /// </summary>
    public static double GetDpiScale()
    {
        var dpi = GetDpiForSystem();
        return dpi / 96.0;
    }
}
