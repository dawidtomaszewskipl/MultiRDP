using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using AxMSTSCLib;
using MSTSCLib;
using MultiRDP.Models;
using MultiRDP.Services;

namespace MultiRDP.Controls;

/// <summary>Szczegóły rozłączenia / Disconnect details</summary>
/// <param name="Reason">discReason z kontrolki / discReason from the control</param>
/// <param name="ExtendedReason">ExtendedDisconnectReasonCode</param>
/// <param name="Description">Opis z GetErrorDescription / Description from GetErrorDescription</param>
public sealed record RdpDisconnectInfo(int Reason, int ExtendedReason, string Description)
{
    /// <summary>exDiscReasonReplacedByOtherConnection — sesję przejął inny klient / another client took over</summary>
    public bool ReplacedByOtherConnection => ExtendedReason == 5;
}

/// <summary>Parametry połączenia niezależne od hosta / Host-independent connection parameters</summary>
/// <param name="PrimaryMonitor">Pierwszy monitor sesji / First session monitor</param>
/// <param name="SecondaryMonitor">Drugi monitor sesji / Second session monitor</param>
/// <param name="RemoteWindowsKeys">Kombinacje Windows do sesji zdalnej / Windows key combinations to the remote session</param>
/// <param name="AcceptAnyCertificate">Akceptuj certyfikat hosta bez weryfikacji / Accept host certificate without verification</param>
public sealed record RdpConnectOptions(
    int  PrimaryMonitor,
    int  SecondaryMonitor,
    bool RemoteWindowsKeys,
    bool AcceptAnyCertificate);

/// <summary>
/// Kontrolka WinForms hostująca ActiveX RDP (MsRdpClient11).
/// Używa UseMultimon=true + ExtSettings['selectedmonitors'] do ograniczenia do 2 monitorów.
/// Cienka nakładka — stan sesji i ponowne łączenie obsługuje <see cref="Sessions.RdpSession"/>.
///
/// WinForms control hosting ActiveX RDP (MsRdpClient11).
/// Uses UseMultimon=true + ExtSettings['selectedmonitors'] to limit to 2 monitors.
/// Thin wrapper — session state and reconnecting are handled by <see cref="Sessions.RdpSession"/>.
/// </summary>
public class RdpHostControl : UserControl
{
    private readonly AxMsRdpClient11NotSafeForScripting _rdpClient;

    public event EventHandler?                    Connected;
    public event EventHandler<RdpDisconnectInfo>? Disconnected;
    public event EventHandler<string>?            ConnectionError;
    public event EventHandler<int>?               AutoReconnecting;
    public event EventHandler?                    AutoReconnected;
    public event EventHandler?                    CertificatePromptShown;
    public event EventHandler?                    CertificatePromptDismissed;

    /// <summary>SHA-256 klucza publicznego serwera (hex) / Server public key SHA-256 (hex)</summary>
    public event EventHandler<string>?            ServerKeyReceived;

    /// <summary>Połączona lub w trakcie łączenia / Connected or connecting</summary>
    public bool IsBusy { get; private set; }

    public RdpHostControl()
    {
        _rdpClient = new AxMsRdpClient11NotSafeForScripting();

        ((System.ComponentModel.ISupportInitialize)_rdpClient).BeginInit();
        _rdpClient.Dock    = DockStyle.Fill;
        _rdpClient.Enabled = true;
        Controls.Add(_rdpClient);
        ((System.ComponentModel.ISupportInitialize)_rdpClient).EndInit();

        _rdpClient.OnConnected        += OnRdpConnected;
        _rdpClient.OnDisconnected     += OnRdpDisconnected;
        _rdpClient.OnFatalError       += OnRdpFatalError;
        _rdpClient.OnLogonError       += OnRdpLogonError;
        _rdpClient.OnAutoReconnecting += OnRdpAutoReconnecting;
        _rdpClient.OnAutoReconnected  += OnRdpAutoReconnected;
        _rdpClient.OnReceivedTSPublicKey            += OnRdpReceivedTSPublicKey;
        _rdpClient.OnAuthenticationWarningDisplayed += OnRdpAuthenticationWarningDisplayed;
        _rdpClient.OnAuthenticationWarningDismissed += OnRdpAuthenticationWarningDismissed;

        Dock = DockStyle.Fill;
    }

    /// <summary>
    /// Łączy z hostem, ograniczając sesję do dwóch wskazanych monitorów.
    /// Wymaga stanu rozłączonego (<see cref="IsBusy"/> == false).
    ///
    /// Connects to host, limiting the session to the two specified monitors.
    /// Requires the disconnected state (<see cref="IsBusy"/> == false).
    /// </summary>
    public void Connect(HostConfig host, RdpConnectOptions options)
    {
        if (IsBusy) throw new InvalidOperationException("Sesja jest już połączona.");

        try
        {
            _rdpClient.Server   = host.Address;
            _rdpClient.UserName = host.Username;
            _rdpClient.Domain   = host.Domain;

            var adv = _rdpClient.AdvancedSettings8;
            adv.ClearTextPassword    = SettingsService.GetPassword(host);
            adv.RDPPort              = host.Port;
            adv.EnableCredSspSupport = true;

            // 0 = łącz bez ostrzeżenia, gdy certyfikatu hosta nie da się zweryfikować (bez okna, które
            //     potrafi zawisnąć niewidoczne w pełnoekranowym oknie sesji); 2 = okno ostrzeżenia
            // 0 = connect without warning when the host certificate can't be verified (no dialog that
            //     can hang invisible in the full-screen session window); 2 = warning dialog
            adv.AuthenticationLevel = options.AcceptAnyCertificate ? 0u : 2u;

            _rdpClient.FullScreen          = true;
            adv.ContainerHandledFullScreen = 1;

            // Ukryj pasek połączenia RDP (pin bar u góry) / Hide RDP connection bar (pin bar at top)
            _rdpClient.AdvancedSettings2.DisplayConnectionBar = false;

            adv.RedirectDrives    = false;
            adv.RedirectPrinters  = false;
            adv.RedirectClipboard = true;
            adv.BitmapCacheSize   = 32768;

            // 0 = kombinacje Windows lokalnie (bez hooka klawiatury — działają globalne skróty), 2 = zdalnie w pełnym ekranie
            // 0 = Windows combinations local (no keyboard hook — global hotkeys work), 2 = remote in full screen
            try { _rdpClient.SecuredSettings2.KeyboardHookMode = options.RemoteWindowsKeys ? 2 : 0; }
            catch (Exception ex) { Log.Warn($"KeyboardHookMode nie ustawione: {ex.Message}"); }

            // Wbudowane wznawianie po krótkich zerwaniach sieci
            // Built-in resume after short network drops
            adv.EnableAutoReconnect  = true;
            adv.MaxReconnectAttempts = 20;

            ConfigureMonitors(options.PrimaryMonitor, options.SecondaryMonitor);

            IsBusy = true;
            _rdpClient.Connect();
        }
        catch (Exception ex)
        {
            IsBusy = false;
            ConnectionError?.Invoke(this, ex.Message);
        }
    }

    /// <summary>
    /// Konfiguruje wielomonitorowość:
    ///   1) IMsRdpClientNonScriptable5.UseMultimon = true
    ///   2) IMsRdpExtendedSettings['selectedmonitors'] = "primary,secondary"
    ///
    /// Configures multi-monitor:
    ///   1) IMsRdpClientNonScriptable5.UseMultimon = true
    ///   2) IMsRdpExtendedSettings['selectedmonitors'] = "primary,secondary"
    /// </summary>
    private void ConfigureMonitors(int primaryIdx, int secondaryIdx)
    {
        var ocx = _rdpClient.GetOcx();
        if (ocx == null) return;

        if (ocx is IMsRdpClientNonScriptable5 ns5)
            ns5.UseMultimon = true;

        // Metoda potwierdzona przez plik diagnostyczny — ExtSettings['selectedmonitors'] działa
        // Method confirmed by diagnostic log — ExtSettings['selectedmonitors'] works
        if (ocx is IMsRdpExtendedSettings ext)
        {
            try
            {
                object selectedMonitors = primaryIdx == secondaryIdx
                    ? $"{primaryIdx}"
                    : $"{primaryIdx},{secondaryIdx}";
                ext.set_Property("selectedmonitors", ref selectedMonitors);
            }
            catch (Exception ex)
            {
                Log.Warn($"selectedmonitors nie ustawione: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Rozpoczyna rozłączanie; zakończenie sygnalizuje zdarzenie <see cref="Disconnected"/>.
    /// Starts disconnecting; completion is signalled by the <see cref="Disconnected"/> event.
    /// </summary>
    public void Disconnect()
    {
        if (!IsBusy) return;
        try { _rdpClient.Disconnect(); }
        catch (Exception ex) { Log.Warn($"Disconnect: {ex.Message}"); }
    }

    private void OnRdpConnected(object? sender, EventArgs e)
        => Connected?.Invoke(this, EventArgs.Empty);

    private void OnRdpDisconnected(object sender, IMsTscAxEvents_OnDisconnectedEvent e)
    {
        IsBusy = false;

        int extended = 0;
        string description;
        try
        {
            extended    = (int)_rdpClient.ExtendedDisconnectReason;
            description = _rdpClient.GetErrorDescription((uint)e.discReason, (uint)extended);
        }
        catch
        {
            description = string.Empty;
        }

        Disconnected?.Invoke(this, new RdpDisconnectInfo(e.discReason, extended, description));
    }

    private void OnRdpFatalError(object sender, IMsTscAxEvents_OnFatalErrorEvent e)
    {
        IsBusy = false;
        ConnectionError?.Invoke(this, $"Błąd krytyczny RDP (kod: {e.errorCode})");
    }

    /// <summary>
    /// OnLogonError zgłasza też komunikaty informacyjne w trakcie logowania — tylko log, bez zmiany stanu.
    /// OnLogonError also reports informational codes during logon — log only, no state change.
    /// </summary>
    private void OnRdpLogonError(object sender, IMsTscAxEvents_OnLogonErrorEvent e)
        => Log.Warn($"{_rdpClient.Server}: OnLogonError {e.lError}");

    private void OnRdpAutoReconnecting(object sender, IMsTscAxEvents_OnAutoReconnectingEvent e)
        => AutoReconnecting?.Invoke(this, e.attemptCount);

    private void OnRdpAutoReconnected(object? sender, EventArgs e)
        => AutoReconnected?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Klucz publiczny serwera (CredSSP/NLA) — tylko do logu; logowanie zawsze kontynuowane.
    /// Server public key (CredSSP/NLA) — for the log only; logon always continues.
    /// </summary>
    private void OnRdpReceivedTSPublicKey(object sender, IMsTscAxEvents_OnReceivedTSPublicKeyEvent e)
    {
        e.pfContinueLogon = true;
        try
        {
            // BSTR z danymi binarnymi — surowe bajty bez konwersji kodowania
            // BSTR carrying binary data — raw bytes without encoding conversion
            byte[] hash = SHA256.HashData(MemoryMarshal.AsBytes((e.publicKey ?? string.Empty).AsSpan()));
            ServerKeyReceived?.Invoke(this, Convert.ToHexString(hash));
        }
        catch (Exception ex)
        {
            Log.Warn($"{_rdpClient.Server}: nie można obliczyć odcisku klucza: {ex.Message}");
        }
    }

    private void OnRdpAuthenticationWarningDisplayed(object? sender, EventArgs e)
        => CertificatePromptShown?.Invoke(this, EventArgs.Empty);

    private void OnRdpAuthenticationWarningDismissed(object? sender, EventArgs e)
        => CertificatePromptDismissed?.Invoke(this, EventArgs.Empty);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!IsBusy)
        {
            e.Graphics.Clear(System.Drawing.Color.FromArgb(18, 18, 18));
            using var font  = new System.Drawing.Font("Segoe UI", 13f);
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(80, 80, 80));
            const string text = "Brak połączenia RDP";
            var sz = e.Graphics.MeasureString(text, font);
            e.Graphics.DrawString(text, font, brush,
                (Width  - sz.Width)  / 2f,
                (Height - sz.Height) / 2f);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rdpClient.OnConnected        -= OnRdpConnected;
            _rdpClient.OnDisconnected     -= OnRdpDisconnected;
            _rdpClient.OnFatalError       -= OnRdpFatalError;
            _rdpClient.OnLogonError       -= OnRdpLogonError;
            _rdpClient.OnAutoReconnecting -= OnRdpAutoReconnecting;
            _rdpClient.OnAutoReconnected  -= OnRdpAutoReconnected;
            _rdpClient.OnReceivedTSPublicKey            -= OnRdpReceivedTSPublicKey;
            _rdpClient.OnAuthenticationWarningDisplayed -= OnRdpAuthenticationWarningDisplayed;
            _rdpClient.OnAuthenticationWarningDismissed -= OnRdpAuthenticationWarningDismissed;
            Disconnect();
            _rdpClient.Dispose();
        }
        base.Dispose(disposing);
    }
}
