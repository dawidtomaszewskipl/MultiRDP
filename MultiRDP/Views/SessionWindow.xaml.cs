using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using MultiRDP.Controls;
using MultiRDP.Interop;
using MultiRDP.Services;

namespace MultiRDP.Views;

/// <summary>
/// Okno rozciągnięte na parę monitorów — jedna sesja RDP z UseMultimon=true.
/// Istnieje po jednym takim oknie na hosta; wszystkie leżą na sobie, a aktywną zakładkę
/// wybiera kolejność Z.
///
/// Window spanning the monitor pair — one RDP session with UseMultimon=true.
/// There is one such window per host; they are stacked and the active tab is chosen by Z-order.
/// </summary>
public partial class SessionWindow : Window
{
    private int _primaryMonitor;
    private int _secondaryMonitor;

    /// <summary>
    /// Okno zamyka tylko aplikacja (np. usunięcie hosta) — Alt+F4 nie może zgubić sesji.
    /// Only the app closes the window (e.g. host removed) — Alt+F4 must not lose a session.
    /// </summary>
    public bool AllowClose { get; set; }

    public bool IsClosed { get; private set; }

    /// <summary>Kontrolka RDP tego okna / This window's RDP control</summary>
    public RdpHostControl RdpControl { get; }

    public SessionWindow(int primaryMonitorIndex, int secondaryMonitorIndex)
    {
        InitializeComponent();

        _primaryMonitor   = primaryMonitorIndex;
        _secondaryMonitor = secondaryMonitorIndex;

        RdpControl    = new RdpHostControl();
        RdpHost.Child = RdpControl;

        Loaded += (_, _) => Reposition();
    }

    /// <summary>Zmienia parę monitorów / Changes the monitor pair</summary>
    public void SetMonitors(int primaryMonitorIndex, int secondaryMonitorIndex)
    {
        _primaryMonitor   = primaryMonitorIndex;
        _secondaryMonitor = secondaryMonitorIndex;
        if (IsLoaded) Reposition();
    }

    /// <summary>Dopasowuje okno do bieżącego układu monitorów / Fits the window to the current monitor layout</summary>
    public void Reposition()
        => MonitorService.SetWindowSpanning(this, _primaryMonitor, _secondaryMonitor);

    /// <summary>Na wierzch (pod pasek zakładek), bez kradzieży fokusa / To front (below tab bar), without stealing focus</summary>
    public void BringToFront() => SetZOrder(NativeMethods.HWND_TOP);

    /// <summary>Na spód kolejności Z / To the bottom of the Z-order</summary>
    public void SendToBack() => SetZOrder(NativeMethods.HWND_BOTTOM);

    private void SetZOrder(IntPtr insertAfter)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        NativeMethods.SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Przy Application.Shutdown anulowanie jest ignorowane / Cancellation is ignored during Application.Shutdown
        if (!AllowClose) e.Cancel = true;
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        IsClosed = true;
        base.OnClosed(e);
    }
}
