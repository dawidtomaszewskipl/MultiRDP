using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MultiRDP.Controls;
using MultiRDP.Models;
using MultiRDP.Services;
using MultiRDP.Views;

namespace MultiRDP.Sessions;

/// <summary>
/// Jedna sesja RDP = jeden host = jedno okno = jedna zakładka.
/// Utrzymuje stan sesji i ponawia połączenie po zerwaniu, którego nie zlecił operator.
///
/// One RDP session = one host = one window = one tab.
/// Keeps the session state and reconnects after a disconnect the operator didn't request.
/// </summary>
public sealed partial class RdpSession : ObservableObject, IDisposable
{
    /// <summary>Odstępy kolejnych prób (s) / Delays between retries (s)</summary>
    private static readonly int[] RetryDelaysSeconds = { 5, 10, 30, 60 };

    private readonly DispatcherTimer _retryTimer;
    private int  _retryCount;
    private bool _userDisconnected;  // operator kliknął „Rozłącz” / operator clicked "Disconnect"
    private bool _connectPending;    // połącz po zakończeniu rozłączania / connect once disconnect completes
    private bool _disposed;
    private int  _primaryMonitor;
    private int  _secondaryMonitor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Shortcut), nameof(ToolTip))]
    private int _index;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name), nameof(ToolTip))]
    private HostConfig _host;

    [ObservableProperty] private bool _isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTip))]
    private SessionState _state = SessionState.Disconnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTip))]
    private string _statusText = "Rozłączono";

    private bool _autoReconnect;

    /// <summary>Wyłączenie anuluje zaplanowaną próbę / Disabling cancels a scheduled retry</summary>
    public bool AutoReconnect
    {
        get => _autoReconnect;
        set
        {
            _autoReconnect = value;
            if (!value && _retryTimer.IsEnabled)
            {
                _retryTimer.Stop();
                SetState(SessionState.Error, StatusText);
            }
        }
    }

    public bool RemoteWindowsKeys { get; set; }

    public SessionWindow Window { get; }

    public string Name     => Host.Name;
    public string Shortcut => $"Ctrl+Alt+{Index + 1}";
    public string ToolTip  => $"{Name} ({Host.Address})\n{StatusText}\n{Shortcut}";

    public RdpSession(int index, HostConfig host, AppSettings settings)
    {
        _retryTimer = new DispatcherTimer();
        _retryTimer.Tick += (_, _) =>
        {
            _retryTimer.Stop();
            StartConnect();
        };

        Index             = index;
        Host              = host;
        _primaryMonitor   = settings.PrimaryMonitorIndex;
        _secondaryMonitor = settings.SecondaryMonitorIndex;
        AutoReconnect     = settings.AutoReconnect;
        RemoteWindowsKeys = settings.RemoteWindowsKeys;

        Window = new SessionWindow(_primaryMonitor, _secondaryMonitor);

        var control = Window.RdpControl;
        control.Connected        += OnConnected;
        control.Disconnected     += OnDisconnected;
        control.ConnectionError  += OnConnectionError;
        control.AutoReconnecting += OnAutoReconnecting;
        control.AutoReconnected  += OnConnected;
    }

    private RdpHostControl Control => Window.RdpControl;

    /// <summary>Zmienia parę monitorów (zadziała przy następnym połączeniu) / Changes monitor pair (applies on next connect)</summary>
    public void SetMonitors(int primary, int secondary)
    {
        _primaryMonitor   = primary;
        _secondaryMonitor = secondary;
        Window.SetMonitors(primary, secondary);
    }

    /// <summary>
    /// Łączy (lub łączy ponownie) na żądanie operatora albo po zmianie ustawień.
    /// Connects (or reconnects) on operator request or after a settings change.
    /// </summary>
    public void Connect()
    {
        _userDisconnected = false;
        _retryCount       = 0;
        StartConnect();
    }

    /// <summary>Rozłącza na żądanie operatora — bez ponawiania / Disconnects on operator request — no retry</summary>
    public void Disconnect()
    {
        _userDisconnected = true;
        _connectPending   = false;
        _retryTimer.Stop();

        if (Control.IsBusy)
            Control.Disconnect();
        else
            SetState(SessionState.Disconnected, "Rozłączono");
    }

    private void StartConnect()
    {
        if (_disposed) return;
        _retryTimer.Stop();

        // Kontrolka ActiveX nie przyjmie nowych parametrów w trakcie sesji —
        // najpierw rozłącz, a połącz w OnDisconnected.
        // The ActiveX control won't accept new parameters during a session —
        // disconnect first and connect in OnDisconnected.
        if (Control.IsBusy)
        {
            _connectPending = true;
            Control.Disconnect();
            return;
        }

        if (string.IsNullOrWhiteSpace(Host.Address))
        {
            SetState(SessionState.Error, "Brak adresu hosta — uzupełnij ustawienia");
            return;
        }

        if (State != SessionState.Reconnecting)
            SetState(SessionState.Connecting, "Łączenie…");

        Log.Info($"[{Name}] Łączenie z {Host.Address}:{Host.Port}");
        Control.Connect(Host, _primaryMonitor, _secondaryMonitor, RemoteWindowsKeys);
    }

    private void OnConnected(object? sender, EventArgs e)
    {
        _retryCount = 0;
        SetState(SessionState.Connected, "Połączono");
        Log.Info($"[{Name}] Połączono");
    }

    private void OnAutoReconnecting(object? sender, int attempt)
    {
        SetState(SessionState.Reconnecting, $"Wznawianie sesji (próba {attempt})…");
        Log.Warn($"[{Name}] Wbudowane wznawianie, próba {attempt}");
    }

    private void OnDisconnected(object? sender, RdpDisconnectInfo info)
    {
        if (_disposed) return;

        if (_connectPending)
        {
            _connectPending = false;
            StartConnect();
            return;
        }

        // Po błędzie krytycznym ponowienie jest już zaplanowane / After a fatal error a retry is already scheduled
        if (_retryTimer.IsEnabled) return;

        string reason = string.IsNullOrWhiteSpace(info.Description)
            ? $"kod {info.Reason}/{info.ExtendedReason}"
            : info.Description.Trim();
        Log.Info($"[{Name}] Rozłączono ({info.Reason}/{info.ExtendedReason}): {reason}");

        if (_userDisconnected)
        {
            SetState(SessionState.Disconnected, "Rozłączono");
        }
        else if (info.ReplacedByOtherConnection)
        {
            // Nie walcz z innym stanowiskiem o tę samą sesję / Don't fight another workstation for the session
            SetState(SessionState.Error, $"Sesję przejęło inne połączenie — {reason}");
        }
        else
        {
            HandleFailure($"Rozłączono: {reason}");
        }
    }

    private void OnConnectionError(object? sender, string message)
    {
        if (_disposed) return;
        _connectPending = false;
        Log.Error($"[{Name}] {message}");
        HandleFailure(message);
    }

    private void HandleFailure(string message)
    {
        if (!AutoReconnect || _userDisconnected)
        {
            SetState(SessionState.Error, message);
            return;
        }

        int delay = RetryDelaysSeconds[Math.Min(_retryCount, RetryDelaysSeconds.Length - 1)];
        _retryCount++;
        SetState(SessionState.Reconnecting, $"{message}\nPonowna próba #{_retryCount} za {delay} s");

        _retryTimer.Interval = TimeSpan.FromSeconds(delay);
        _retryTimer.Start();
    }

    private void SetState(SessionState state, string text)
    {
        State      = state;
        StatusText = text;
    }

    /// <summary>Zamyka okno i sesję (usunięcie hosta / koniec aplikacji) / Closes window and session (host removed / app exit)</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _retryTimer.Stop();

        var control = Control;
        control.Connected        -= OnConnected;
        control.Disconnected     -= OnDisconnected;
        control.ConnectionError  -= OnConnectionError;
        control.AutoReconnecting -= OnAutoReconnecting;
        control.AutoReconnected  -= OnConnected;

        control.Disconnect();
        if (!Window.IsClosed)
        {
            Window.AllowClose = true;
            Window.Close();
        }
        control.Dispose();
    }
}
