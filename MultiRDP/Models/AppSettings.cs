namespace MultiRDP.Models;

/// <summary>
/// Główne ustawienia aplikacji / Main application settings
/// </summary>
public class AppSettings
{
    /// <summary>Minimalna liczba hostów / Minimum number of hosts</summary>
    public const int MinHosts = 1;

    /// <summary>Maksymalna liczba hostów (zakładek) / Maximum number of hosts (tabs)</summary>
    public const int MaxHosts = 4;

    /// <summary>Lista skonfigurowanych hostów RDP (1–4) / List of configured RDP hosts (1–4)</summary>
    public List<HostConfig> Hosts { get; set; } = new()
    {
        new HostConfig { Name = "Host 1", Address = "192.168.1.10" },
        new HostConfig { Name = "Host 2", Address = "192.168.1.20" }
    };

    /// <summary>
    /// Indeks pierwszego monitora sesji (tu jest też pasek zakładek)
    /// Index of the first session monitor (also hosts the tab bar)
    /// </summary>
    public int PrimaryMonitorIndex { get; set; } = 0;

    /// <summary>Indeks drugiego monitora sesji / Index of the second session monitor</summary>
    public int SecondaryMonitorIndex { get; set; } = 1;

    /// <summary>Indeks aktywnego hosta (zakładki) / Currently active host tab index</summary>
    public int ActiveHostIndex { get; set; } = 0;

    /// <summary>
    /// Czy łączyć ponownie po zerwaniu sesji niezainicjowanym przez operatora
    /// Whether to reconnect after a disconnect not initiated by the operator
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// true: kombinacje Windows (Alt+Tab, Win, …) trafiają do sesji zdalnej, ale klient RDP
    /// przechwytuje wtedy klawiaturę i globalne skróty Ctrl+Alt+1..4 nie działają z fokusem w sesji.
    /// false (domyślnie): kombinacje Windows działają lokalnie, skróty zakładek działają zawsze.
    ///
    /// true: Windows key combinations (Alt+Tab, Win, …) go to the remote session, but the RDP client
    /// then hooks the keyboard and global Ctrl+Alt+1..4 shortcuts don't work while a session has focus.
    /// false (default): Windows combinations stay local, tab shortcuts always work.
    /// </summary>
    public bool RemoteWindowsKeys { get; set; } = false;

    /// <summary>
    /// true (domyślnie): certyfikat hosta akceptowany bez weryfikacji i bez okna ostrzeżenia
    /// (AuthenticationLevel = 0) — odcisk klucza serwera trafia do logu.
    /// false: okno ostrzeżenia kontrolki RDP (AuthenticationLevel = 2), wymaga potwierdzenia operatora.
    ///
    /// true (default): host certificate accepted without verification and without a warning dialog
    /// (AuthenticationLevel = 0) — the server key fingerprint is logged.
    /// false: RDP control warning dialog (AuthenticationLevel = 2), requires operator confirmation.
    /// </summary>
    public bool AcceptAnyCertificate { get; set; } = true;

    /// <summary>
    /// Doprowadza ustawienia do poprawnego stanu (1–4 hosty, indeksy w zakresie).
    /// Brings settings into a valid state (1–4 hosts, indices in range).
    /// </summary>
    public void Normalize()
    {
        Hosts ??= new List<HostConfig>();
        Hosts.RemoveAll(h => h is null);
        if (Hosts.Count > MaxHosts) Hosts.RemoveRange(MaxHosts, Hosts.Count - MaxHosts);
        while (Hosts.Count < MinHosts) Hosts.Add(new HostConfig { Name = $"Host {Hosts.Count + 1}" });

        PrimaryMonitorIndex   = Math.Max(0, PrimaryMonitorIndex);
        SecondaryMonitorIndex = Math.Max(0, SecondaryMonitorIndex);
        ActiveHostIndex       = Math.Clamp(ActiveHostIndex, 0, Hosts.Count - 1);
    }

    /// <summary>Głęboka kopia / Deep copy</summary>
    public AppSettings Clone() => new()
    {
        Hosts                 = Hosts.Select(h => h.Clone()).ToList(),
        PrimaryMonitorIndex   = PrimaryMonitorIndex,
        SecondaryMonitorIndex = SecondaryMonitorIndex,
        ActiveHostIndex       = ActiveHostIndex,
        AutoReconnect         = AutoReconnect,
        RemoteWindowsKeys     = RemoteWindowsKeys,
        AcceptAnyCertificate  = AcceptAnyCertificate
    };
}
