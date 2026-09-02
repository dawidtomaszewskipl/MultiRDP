using System.Windows.Forms;
using AxMSTSCLib;
using MSTSCLib;
using MultiRDP.Models;
using MultiRDP.Services;

namespace MultiRDP.Controls;

/// <summary>
/// Kontrolka WinForms hostująca ActiveX RDP (MsRdpClient11).
/// Używa UseMultimon=true + ExtSettings['selectedmonitors'] do ograniczenia do 2 monitorów.
///
/// WinForms control hosting ActiveX RDP (MsRdpClient11).
/// Uses UseMultimon=true + ExtSettings['selectedmonitors'] to limit to 2 monitors.
/// </summary>
public class RdpHostControl : UserControl
{
    private AxMsRdpClient11NotSafeForScripting _rdpClient;
    private bool _isConnected  = false;
    private bool _isConnecting = false;

    public event EventHandler?         Connected;
    public event EventHandler?         Disconnected;
    public event EventHandler<string>? ConnectionError;

    public bool IsConnected  => _isConnected;
    public bool IsConnecting => _isConnecting;

    public RdpHostControl()
    {
        _rdpClient = new AxMsRdpClient11NotSafeForScripting();
        InitializeRdpControl();
    }

    private void InitializeRdpControl()
    {
        ((System.ComponentModel.ISupportInitialize)_rdpClient).BeginInit();
        _rdpClient.Dock    = DockStyle.Fill;
        _rdpClient.Enabled = true;
        Controls.Add(_rdpClient);
        ((System.ComponentModel.ISupportInitialize)_rdpClient).EndInit();

        _rdpClient.OnConnected    += OnRdpConnected;
        _rdpClient.OnDisconnected += OnRdpDisconnected;
        _rdpClient.OnFatalError   += OnRdpFatalError;
        _rdpClient.OnLogonError   += OnRdpLogonError;

        Dock = DockStyle.Fill;
    }

    /// <summary>
    /// Łączy z hostem, ograniczając sesję do dwóch wskazanych monitorów.
    /// Connects to host, limiting the session to the two specified monitors.
    /// </summary>
    public void Connect(HostConfig host, int primaryMonitorIdx, int secondaryMonitorIdx)
    {
        if (_isConnecting || _isConnected) Disconnect();
        _isConnecting = true;

        try
        {
            _rdpClient.Server   = host.Address;
            _rdpClient.UserName = host.Username;
            _rdpClient.Domain   = host.Domain;

            _rdpClient.AdvancedSettings8.ClearTextPassword    = SettingsService.GetPassword(host);
            _rdpClient.AdvancedSettings8.RDPPort              = host.Port;
            _rdpClient.AdvancedSettings8.EnableCredSspSupport = true;
            _rdpClient.AdvancedSettings8.AuthenticationLevel  = 2;

            _rdpClient.FullScreen = true;
            _rdpClient.AdvancedSettings8.ContainerHandledFullScreen = 1;

            // Ukryj pasek połączenia RDP (pin bar u góry) / Hide RDP connection bar (pin bar at top)
            _rdpClient.AdvancedSettings2.DisplayConnectionBar = false;

            _rdpClient.AdvancedSettings8.RedirectDrives    = false;
            _rdpClient.AdvancedSettings8.RedirectPrinters  = false;
            _rdpClient.AdvancedSettings8.RedirectClipboard = true;
            _rdpClient.AdvancedSettings8.BitmapCacheSize   = 32768;

            ConfigureMonitors(primaryMonitorIdx, secondaryMonitorIdx);

            _rdpClient.Connect();
        }
        catch (Exception ex)
        {
            _isConnecting = false;
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

        // Krok 1: włącz UseMultimon / Step 1: enable UseMultimon
        if (ocx is IMsRdpClientNonScriptable5 ns5)
            ns5.UseMultimon = true;

        // Krok 2: ogranicz do 2 wybranych monitorów
        // Step 2: limit to 2 selected monitors
        // Metoda potwierdzona przez plik diagnostyczny — ExtSettings['selectedmonitors'] działa
        // Method confirmed by diagnostic log — ExtSettings['selectedmonitors'] works
        if (ocx is IMsRdpExtendedSettings ext)
        {
            try
            {
                object selectedMonitors = $"{primaryIdx},{secondaryIdx}";
                ext.set_Property("selectedmonitors", ref selectedMonitors);
            }
            catch { }
        }
    }

    /// <summary>Rozłącza sesję RDP / Disconnects the RDP session</summary>
    public void Disconnect()
    {
        if (!_isConnected && !_isConnecting) return;
        try { _rdpClient.Disconnect(); } catch { }
        _isConnected  = false;
        _isConnecting = false;
    }

    private void OnRdpConnected(object? sender, EventArgs e)
    {
        _isConnected  = true;
        _isConnecting = false;
        Connected?.Invoke(this, EventArgs.Empty);
    }

    private void OnRdpDisconnected(object sender, IMsTscAxEvents_OnDisconnectedEvent e)
    {
        _isConnected  = false;
        _isConnecting = false;
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    private void OnRdpFatalError(object sender, IMsTscAxEvents_OnFatalErrorEvent e)
    {
        _isConnected  = false;
        _isConnecting = false;
        ConnectionError?.Invoke(this, $"Błąd krytyczny RDP (kod: {e.errorCode})");
    }

    private void OnRdpLogonError(object sender, IMsTscAxEvents_OnLogonErrorEvent e)
    {
        _isConnecting = false;
        ConnectionError?.Invoke(this, $"Błąd logowania RDP (kod: {e.lError})");
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!_isConnected && !_isConnecting)
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
            _rdpClient.OnConnected    -= OnRdpConnected;
            _rdpClient.OnDisconnected -= OnRdpDisconnected;
            _rdpClient.OnFatalError   -= OnRdpFatalError;
            _rdpClient.OnLogonError   -= OnRdpLogonError;
            Disconnect();
            _rdpClient.Dispose();
        }
        base.Dispose(disposing);
    }
}
