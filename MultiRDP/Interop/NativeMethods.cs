using System.Runtime.InteropServices;

namespace MultiRDP.Interop;

/// <summary>
/// Wspólne deklaracje Win32 używane przez aplikację.
/// Shared Win32 declarations used by the application.
/// </summary>
internal static class NativeMethods
{
    // --- SetWindowPos ---
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    public static readonly IntPtr HWND_TOP    = new(0);
    public static readonly IntPtr HWND_BOTTOM = new(1);

    public const uint SWP_NOSIZE       = 0x0001;
    public const uint SWP_NOMOVE       = 0x0002;
    public const uint SWP_NOZORDER     = 0x0004;
    public const uint SWP_NOACTIVATE   = 0x0010; // Nie kradnij fokusa / Don't steal focus
    public const uint SWP_FRAMECHANGED = 0x0020;

    // --- Globalne skróty / Global hotkeys ---
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int  WM_HOTKEY    = 0x0312;
    public const uint MOD_ALT      = 0x0001;
    public const uint MOD_CONTROL  = 0x0002;
    public const uint MOD_NOREPEAT = 0x4000;
}
