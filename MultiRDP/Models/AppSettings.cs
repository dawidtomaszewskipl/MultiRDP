namespace MultiRDP.Models;

/// <summary>
/// Główne ustawienia aplikacji / Main application settings
/// </summary>
public class AppSettings
{
    /// <summary>Lista skonfigurowanych hostów RDP / List of configured RDP hosts</summary>
    public List<HostConfig> Hosts { get; set; } = new()
    {
        new HostConfig { Name = "Host 1", Address = "192.168.1.10" },
        new HostConfig { Name = "Host 2", Address = "192.168.1.20" }
    };

    /// <summary>
    /// Indeks monitora dla głównego okna (z zakładkami) / Monitor index for main window (with tabs)
    /// </summary>
    public int PrimaryMonitorIndex { get; set; } = 0;

    /// <summary>
    /// Indeks monitora dla drugiego okna / Monitor index for secondary window
    /// </summary>
    public int SecondaryMonitorIndex { get; set; } = 1;

    /// <summary>
    /// Indeks aktywnego hosta (zakładki) / Currently active host tab index
    /// </summary>
    public int ActiveHostIndex { get; set; } = 0;
}
