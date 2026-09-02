using System.Windows;
using MultiRDP.Controls;
using MultiRDP.Services;

namespace MultiRDP.Views;

/// <summary>
/// Okno rozciągnięte na oba monitory — jedna sesja RDP z UseMultimon=true.
/// Dwa takie okna istnieją jednocześnie (po jednym na hosta).
///
/// Window spanning both monitors — one RDP session with UseMultimon=true.
/// Two such windows exist simultaneously (one per host).
/// </summary>
public partial class SecondaryWindow : Window
{
    private readonly RdpHostControl _rdpControl;

    public SecondaryWindow(int primaryMonitorIndex, int secondaryMonitorIndex)
    {
        InitializeComponent();

        _rdpControl   = new RdpHostControl();
        RdpHost.Child = _rdpControl;

        Loaded += (_, _) =>
            MonitorService.SetWindowSpanning(this, primaryMonitorIndex, secondaryMonitorIndex);
    }

    /// <summary>Kontrolka RDP do przekazania do RdpSessionManager / RDP control for RdpSessionManager</summary>
    public RdpHostControl RdpControl => _rdpControl;
}
