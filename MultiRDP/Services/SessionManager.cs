using System.Collections.ObjectModel;
using Microsoft.Win32;
using MultiRDP.Models;
using MultiRDP.Sessions;

namespace MultiRDP.Services;

/// <summary>
/// Zarządza zestawem sesji (1–4): tworzy i usuwa okna, ustala aktywną sesję przez kolejność Z
/// i stosuje zmiany ustawień, łącząc ponownie tylko te sesje, których dotyczą.
///
/// Manages the set of sessions (1–4): creates and removes windows, selects the active session
/// via Z-order and applies settings changes, reconnecting only the sessions they affect.
/// </summary>
public sealed class SessionManager : IDisposable
{
    private int _primaryMonitor;
    private int _secondaryMonitor;

    public ObservableCollection<RdpSession> Sessions { get; } = new();

    public SessionManager(AppSettings settings)
    {
        _primaryMonitor   = settings.PrimaryMonitorIndex;
        _secondaryMonitor = settings.SecondaryMonitorIndex;

        for (int i = 0; i < settings.Hosts.Count; i++)
            Sessions.Add(new RdpSession(i, settings.Hosts[i], settings));

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>Pokazuje okna sesji / Shows session windows</summary>
    public void ShowAll()
    {
        foreach (var s in Sessions) s.Window.Show();
    }

    public void ConnectAll()
    {
        foreach (var s in Sessions) s.Connect();
    }

    /// <summary>
    /// Wysuwa okno wskazanej sesji na wierzch, pozostałe chowa pod spód. Sesje nie są rozłączane.
    /// Jeśli fokus klawiatury był w oknie sesji, przechodzi do nowej aktywnej sesji —
    /// inaczej operator pisałby do zasłoniętej sesji.
    ///
    /// Brings the given session window to front and sends the others back. Sessions stay connected.
    /// If keyboard focus was in a session window it moves to the new active session —
    /// otherwise the operator would keep typing into a hidden session.
    /// </summary>
    public void Activate(int index)
    {
        if (index < 0 || index >= Sessions.Count) return;

        bool sessionHadFocus = Sessions.Any(s => s.Window.IsActive);
        var  active          = Sessions[index];

        foreach (var s in Sessions.Where(s => s != active))
        {
            s.IsActive = false;
            s.Window.SendToBack();
        }

        active.IsActive = true;
        active.Window.BringToFront();

        if (sessionHadFocus) active.Window.Activate();
    }

    /// <summary>
    /// Stosuje nowe ustawienia. Sesje dopasowywane są po <see cref="HostConfig.Id"/>, więc zmiana
    /// kolejności lub nazwy nie zrywa połączeń. Ponowne łączenie tylko gdy zmieniły się parametry
    /// połączenia, monitory lub tryb klawiatury.
    ///
    /// Applies new settings. Sessions are matched by <see cref="HostConfig.Id"/>, so reordering or
    /// renaming doesn't drop connections. Reconnects only when connection parameters, monitors or
    /// keyboard mode changed.
    /// </summary>
    public void Apply(AppSettings settings)
    {
        bool monitorsChanged = settings.PrimaryMonitorIndex   != _primaryMonitor ||
                               settings.SecondaryMonitorIndex != _secondaryMonitor;
        _primaryMonitor   = settings.PrimaryMonitorIndex;
        _secondaryMonitor = settings.SecondaryMonitorIndex;

        var remaining = Sessions.ToDictionary(s => s.Host.Id);
        var ordered   = new List<RdpSession>();
        var toConnect = new List<RdpSession>();

        for (int i = 0; i < settings.Hosts.Count; i++)
        {
            var host = settings.Hosts[i];

            if (remaining.Remove(host.Id, out var session))
            {
                bool reconnect = monitorsChanged ||
                                 session.RemoteWindowsKeys    != settings.RemoteWindowsKeys ||
                                 session.AcceptAnyCertificate != settings.AcceptAnyCertificate ||
                                 !session.Host.ConnectionEquals(host);

                session.Index                = i;
                session.Host                 = host;
                session.AutoReconnect        = settings.AutoReconnect;
                session.RemoteWindowsKeys    = settings.RemoteWindowsKeys;
                session.AcceptAnyCertificate = settings.AcceptAnyCertificate;
                if (monitorsChanged) session.SetMonitors(_primaryMonitor, _secondaryMonitor);
                if (reconnect) toConnect.Add(session);
            }
            else
            {
                session = new RdpSession(i, host, settings);
                session.Window.Show();
                toConnect.Add(session);
            }

            ordered.Add(session);
        }

        foreach (var removed in remaining.Values)
        {
            Log.Info($"[{removed.Name}] Usunięto hosta z ustawień");
            removed.Dispose();
        }

        Sessions.Clear();
        foreach (var s in ordered) Sessions.Add(s);

        foreach (var s in toConnect) s.Connect();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Zdarzenie może przyjść z innego wątku / The event may arrive on another thread
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            Log.Info("Zmiana konfiguracji ekranów — repozycjonowanie okien sesji");
            foreach (var s in Sessions) s.Window.Reposition();
        });
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        foreach (var s in Sessions) s.Dispose();
        Sessions.Clear();
    }
}
