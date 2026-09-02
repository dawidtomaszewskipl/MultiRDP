using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WinForms = System.Windows.Forms;

namespace MultiRDP.Services;

/// <summary>
/// Serwis do wykrywania monitorów i pozycjonowania okien.
/// Używa Win32 SetWindowPos (fizyczne piksele) zamiast WPF Left/Width (logiczne piksele)
/// by uniknąć problemów z DPI scaling.
///
/// Service for monitor detection and window positioning.
/// Uses Win32 SetWindowPos (physical pixels) instead of WPF Left/Width (logical pixels)
/// to avoid DPI scaling issues.
/// </summary>
public static class MonitorService
{
    // --- Win32 P/Invoke ---
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy,
        uint uFlags);

    private const uint SWP_NOZORDER     = 0x0004;
    private const uint SWP_NOACTIVATE   = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_FLAGS        = SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED;

    // --- Publiczne API ---

    /// <summary>
    /// Zwraca listę dostępnych monitorów.
    /// Returns list of available monitors.
    /// </summary>
    public static List<MonitorInfo> GetMonitors()
    {
        var result  = new List<MonitorInfo>();
        var screens = WinForms.Screen.AllScreens;

        for (int i = 0; i < screens.Length; i++)
        {
            result.Add(new MonitorInfo
            {
                Index     = i,
                Name      = $"Monitor {i + 1}: {screens[i].DeviceName.Replace("\\\\.\\", "")}",
                Bounds    = screens[i].Bounds,
                IsPrimary = screens[i].Primary
            });
        }
        return result;
    }

    /// <summary>
    /// Pozycjonuje okno na jednym monitorze pełnoekranowo (fizyczne piksele przez Win32).
    /// Positions a window fullscreen on one monitor (physical pixels via Win32).
    /// </summary>
    public static void SetWindowFullscreen(Window window, int monitorIndex)
    {
        var screens = WinForms.Screen.AllScreens;
        if (monitorIndex < 0 || monitorIndex >= screens.Length) monitorIndex = 0;

        var b = screens[monitorIndex].Bounds;
        window.WindowState = WindowState.Normal;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
            SetWindowPos(hwnd, IntPtr.Zero, b.X, b.Y, b.Width, b.Height, SWP_FLAGS);
        else
            SetWindowLogical(window, b.X, b.Y, b.Width, b.Height, monitorIndex);
    }

    /// <summary>
    /// Pozycjonuje okno rozpiętym na dwóch monitorach (fizyczne piksele przez Win32).
    /// Positions a window spanning two monitors (physical pixels via Win32).
    /// </summary>
    public static void SetWindowSpanning(Window window, int primaryIndex, int secondaryIndex)
    {
        var bounds = GetSpanningBounds(primaryIndex, secondaryIndex);
        window.WindowState = WindowState.Normal;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, IntPtr.Zero,
                bounds.X, bounds.Y, bounds.Width, bounds.Height,
                SWP_FLAGS);
        }
        else
        {
            // HWND jeszcze nie istnieje (przed Show()) — użyj przybliżonego WPF
            // HWND doesn't exist yet (before Show()) — use approximate WPF
            SetWindowLogical(window, bounds.X, bounds.Y, bounds.Width, bounds.Height, primaryIndex);
        }
    }

    /// <summary>
    /// Oblicza prostokąt obejmujący dokładnie dwa wskazane monitory.
    /// Calculates the bounding rectangle covering exactly two specified monitors.
    /// </summary>
    public static System.Drawing.Rectangle GetSpanningBounds(int primaryIndex, int secondaryIndex)
    {
        var screens = WinForms.Screen.AllScreens;
        if (primaryIndex   < 0 || primaryIndex   >= screens.Length) primaryIndex   = 0;
        if (secondaryIndex < 0 || secondaryIndex >= screens.Length) secondaryIndex = 0;

        var s1 = screens[primaryIndex].Bounds;
        var s2 = screens[secondaryIndex].Bounds;

        int left   = Math.Min(s1.Left,   s2.Left);
        int top    = Math.Min(s1.Top,    s2.Top);
        int right  = Math.Max(s1.Right,  s2.Right);
        int bottom = Math.Max(s1.Bottom, s2.Bottom);

        return new System.Drawing.Rectangle(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Zwraca fizyczne wymiary monitora.
    /// Returns physical dimensions of a monitor.
    /// </summary>
    public static (int Width, int Height) GetMonitorPhysicalSize(int monitorIndex)
    {
        var screens = WinForms.Screen.AllScreens;
        if (monitorIndex < 0 || monitorIndex >= screens.Length) monitorIndex = 0;
        var b = screens[monitorIndex].Bounds;
        return (b.Width, b.Height);
    }

    // --- Helpers ---

    /// <summary>
    /// Fallback: ustawia pozycję okna przez WPF z korekcją DPI.
    /// Fallback: sets window position via WPF with DPI correction.
    /// </summary>
    private static void SetWindowLogical(Window window, int x, int y, int w, int h, int monitorIdx)
    {
        var source = PresentationSource.FromVisual(window);
        double dpiX = 1.0, dpiY = 1.0;

        if (source?.CompositionTarget != null)
        {
            dpiX = source.CompositionTarget.TransformToDevice.M11;
            dpiY = source.CompositionTarget.TransformToDevice.M22;
        }
        else
        {
            // Próbuj uzyskać DPI z innego źródła / Try getting DPI from another source
            using var gfx = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            dpiX = dpiY = gfx.DpiX / 96.0;
        }

        window.Left   = x / dpiX;
        window.Top    = y / dpiY;
        window.Width  = w / dpiX;
        window.Height = h / dpiY;
    }
}

/// <summary>Informacje o monitorze / Monitor information</summary>
public class MonitorInfo
{
    public int                        Index     { get; set; }
    public string                     Name      { get; set; } = string.Empty;
    public System.Drawing.Rectangle   Bounds    { get; set; }
    public bool                       IsPrimary { get; set; }

    public override string ToString() => IsPrimary ? $"{Name} (główny)" : Name;
}
