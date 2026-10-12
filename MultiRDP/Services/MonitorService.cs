using System.Windows;
using System.Windows.Interop;
using MultiRDP.Interop;
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
    private const uint SWP_FLAGS =
        NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED;

    /// <summary>
    /// Zwraca listę dostępnych monitorów.
    /// Returns list of available monitors.
    /// </summary>
    public static List<MonitorInfo> GetMonitors()
    {
        var screens = WinForms.Screen.AllScreens;
        return screens.Select((s, i) => new MonitorInfo
        {
            Index     = i,
            Bounds    = s.Bounds,
            IsPrimary = s.Primary
        }).ToList();
    }

    /// <summary>
    /// Granice monitora w fizycznych pikselach (indeks spoza zakresu → monitor 0).
    /// Monitor bounds in physical pixels (out-of-range index → monitor 0).
    /// </summary>
    public static System.Drawing.Rectangle GetBounds(int monitorIndex)
    {
        var screens = WinForms.Screen.AllScreens;
        if (monitorIndex < 0 || monitorIndex >= screens.Length) monitorIndex = 0;
        return screens[monitorIndex].Bounds;
    }

    /// <summary>
    /// Oblicza prostokąt obejmujący dokładnie dwa wskazane monitory.
    /// Calculates the bounding rectangle covering exactly two specified monitors.
    /// </summary>
    public static System.Drawing.Rectangle GetSpanningBounds(int primaryIndex, int secondaryIndex)
        => System.Drawing.Rectangle.Union(GetBounds(primaryIndex), GetBounds(secondaryIndex));

    /// <summary>
    /// Pozycjonuje okno rozpiętym na dwóch monitorach (fizyczne piksele przez Win32).
    /// Positions a window spanning two monitors (physical pixels via Win32).
    /// </summary>
    public static void SetWindowSpanning(Window window, int primaryIndex, int secondaryIndex)
        => PositionWindow(window, GetSpanningBounds(primaryIndex, secondaryIndex));

    /// <summary>
    /// Ustawia okno na prostokącie w fizycznych pikselach.
    /// Places a window on a rectangle given in physical pixels.
    /// </summary>
    public static void PositionWindow(Window window, System.Drawing.Rectangle bounds)
    {
        window.WindowState = WindowState.Normal;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero,
                bounds.X, bounds.Y, bounds.Width, bounds.Height, SWP_FLAGS);
        }
        else
        {
            // HWND jeszcze nie istnieje (przed Show()) — użyj przybliżonego WPF
            // HWND doesn't exist yet (before Show()) — use approximate WPF
            SetWindowLogical(window, bounds);
        }
    }

    /// <summary>
    /// Fallback: ustawia pozycję okna przez WPF z korekcją DPI.
    /// Fallback: sets window position via WPF with DPI correction.
    /// </summary>
    private static void SetWindowLogical(Window window, System.Drawing.Rectangle b)
    {
        var source = PresentationSource.FromVisual(window);
        double dpiX, dpiY;

        if (source?.CompositionTarget != null)
        {
            dpiX = source.CompositionTarget.TransformToDevice.M11;
            dpiY = source.CompositionTarget.TransformToDevice.M22;
        }
        else
        {
            using var gfx = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            dpiX = dpiY = gfx.DpiX / 96.0;
        }

        window.Left   = b.X      / dpiX;
        window.Top    = b.Y      / dpiY;
        window.Width  = b.Width  / dpiX;
        window.Height = b.Height / dpiY;
    }
}

/// <summary>Informacje o monitorze / Monitor information</summary>
public class MonitorInfo
{
    public int                      Index     { get; set; }
    public System.Drawing.Rectangle Bounds    { get; set; }
    public bool                     IsPrimary { get; set; }

    /// <summary>Etykieta z rozmiarem i pozycją do identyfikacji / Label with size and position</summary>
    public override string ToString()
    {
        string label = $"Monitor {Index + 1} [{Bounds.Width}×{Bounds.Height}] @ ({Bounds.X}, {Bounds.Y})";
        return IsPrimary ? $"{label} (główny)" : label;
    }
}
