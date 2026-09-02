using MultiRDP.Controls;
using MultiRDP.Models;

namespace MultiRDP.Services;

/// <summary>
/// Zarządza dwoma równoległymi sesjami RDP (jedna na hosta).
/// Obie sesje są utrzymywane jednocześnie — przełączanie = tylko zmiana widoczności.
///
/// Manages two parallel RDP sessions (one per host).
/// Both sessions are maintained simultaneously — switching = only visibility change.
/// </summary>
public class RdpSessionManager
{
    private readonly RdpHostControl?[] _controls    = new RdpHostControl?[2];
    private readonly string[]          _statusCache = { "Rozłączono", "Rozłączono" };

    /// <summary>
    /// Zdarzenie: (indeks sesji 0/1, czy połączono)
    /// Event: (session index 0/1, whether connected)
    /// </summary>
    public event EventHandler<(int SessionIndex, bool IsConnected)>? ConnectionStateChanged;

    /// <summary>
    /// Zdarzenie: (indeks sesji 0/1, komunikat błędu)
    /// Event: (session index 0/1, error message)
    /// </summary>
    public event EventHandler<(int SessionIndex, string Message)>? ConnectionError;

    public bool IsConnected(int idx)  => idx >= 0 && idx < 2 && (_controls[idx]?.IsConnected  ?? false);
    public bool IsConnecting(int idx) => idx >= 0 && idx < 2 && (_controls[idx]?.IsConnecting ?? false);
    public string GetStatus(int idx) => _statusCache[idx >= 0 && idx < 2 ? idx : 0];

    /// <summary>
    /// Rejestruje kontrolki RDP z obu okien.
    /// Registers RDP controls from both windows.
    /// </summary>
    public void RegisterControls(RdpHostControl control0, RdpHostControl control1)
    {
        RegisterControl(0, control0);
        RegisterControl(1, control1);
    }

    private void RegisterControl(int idx, RdpHostControl control)
    {
        // Odepnij poprzednie zdarzenia jeśli istnieją / Detach previous events if any
        if (_controls[idx] is { } old)
        {
            old.Connected       -= MakeConnectedHandler(idx);
            old.Disconnected    -= MakeDisconnectedHandler(idx);
            old.ConnectionError -= MakeErrorHandler(idx);
        }

        _controls[idx] = control;
        control.Connected       += MakeConnectedHandler(idx);
        control.Disconnected    += MakeDisconnectedHandler(idx);
        control.ConnectionError += MakeErrorHandler(idx);
    }

    // --- Fabryki handlerów zdarzeniowych / Event handler factories ---

    private EventHandler MakeConnectedHandler(int idx)
        => (_, _) =>
        {
            _statusCache[idx] = "Połączono";
            ConnectionStateChanged?.Invoke(this, (idx, true));
        };

    private EventHandler MakeDisconnectedHandler(int idx)
        => (_, _) =>
        {
            _statusCache[idx] = "Rozłączono";
            ConnectionStateChanged?.Invoke(this, (idx, false));
        };

    private EventHandler<string> MakeErrorHandler(int idx)
        => (_, msg) =>
        {
            _statusCache[idx] = $"Błąd: {msg}";
            ConnectionError?.Invoke(this, (idx, msg));
        };

    // --- Publiczne metody zarządzania sesjami / Public session management methods ---

    private int _primaryMonitorIdx   = 0;
    private int _secondaryMonitorIdx = 1;

    /// <summary>Ustawia indeksy monitorów / Sets monitor indices</summary>
    public void SetMonitorIndices(int primary, int secondary)
    {
        _primaryMonitorIdx   = primary;
        _secondaryMonitorIdx = secondary;
    }

    /// <summary>Łączy z wybranym hostem / Connects to the specified host</summary>
    public void Connect(int idx, HostConfig host)
    {
        if (_controls[idx] is null) return;
        _statusCache[idx] = "Łączenie...";
        _controls[idx]!.Connect(host, _primaryMonitorIdx, _secondaryMonitorIdx);
    }

    /// <summary>Rozłącza wybraną sesję / Disconnects the specified session</summary>
    public void Disconnect(int idx) => _controls[idx]?.Disconnect();

    /// <summary>Rozłącza wszystkie sesje / Disconnects all sessions</summary>
    public void DisconnectAll()
    {
        _controls[0]?.Disconnect();
        _controls[1]?.Disconnect();
    }

    /// <summary>
    /// Ponownie łączy z hostem (np. po zmianie konfiguracji).
    /// Reconnects to host (e.g., after config change).
    /// </summary>
    public void Reconnect(int idx, HostConfig host) => Connect(idx, host);
}
