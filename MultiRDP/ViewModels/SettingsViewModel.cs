using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiRDP.Models;
using MultiRDP.Services;

namespace MultiRDP.ViewModels;

/// <summary>
/// ViewModel okna ustawień: lista 1–4 hostów, para monitorów, opcje sesji.
/// Settings window ViewModel: list of 1–4 hosts, monitor pair, session options.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _original;

    public ObservableCollection<HostEditorViewModel> Hosts { get; } = new();
    public List<MonitorInfo> Monitors { get; } = MonitorService.GetMonitors();

    [ObservableProperty] private int  _primaryMonitorIndex;
    [ObservableProperty] private int  _secondaryMonitorIndex;
    [ObservableProperty] private bool _autoReconnect;
    [ObservableProperty] private bool _remoteWindowsKeys;
    [ObservableProperty] private bool _acceptAnyCertificate;

    public SettingsViewModel(AppSettings current)
    {
        _original = current;

        for (int i = 0; i < current.Hosts.Count; i++)
            Hosts.Add(new HostEditorViewModel(i, current.Hosts[i]));

        int last = Math.Max(0, Monitors.Count - 1);
        PrimaryMonitorIndex   = Math.Clamp(current.PrimaryMonitorIndex,   0, last);
        SecondaryMonitorIndex = Math.Clamp(current.SecondaryMonitorIndex, 0, last);
        AutoReconnect         = current.AutoReconnect;
        RemoteWindowsKeys     = current.RemoteWindowsKeys;
        AcceptAnyCertificate  = current.AcceptAnyCertificate;

        Hosts.CollectionChanged += (_, _) => UpdateHostPositions();
        UpdateHostPositions();
    }

    /// <summary>Czy można usunąć hosta (min. 1) / Whether a host can be removed (min. 1)</summary>
    public bool CanRemoveHost => Hosts.Count > AppSettings.MinHosts;

    private void UpdateHostPositions()
    {
        for (int i = 0; i < Hosts.Count; i++)
        {
            Hosts[i].Index       = i;
            Hosts[i].CanMoveUp   = i > 0;
            Hosts[i].CanMoveDown = i < Hosts.Count - 1;
        }
        OnPropertyChanged(nameof(CanRemoveHost));
        AddHostCommand.NotifyCanExecuteChanged();
    }

    // --- Lista hostów / Host list ---

    [RelayCommand(CanExecute = nameof(CanAddHost))]
    private void AddHost() => Hosts.Add(new HostEditorViewModel(Hosts.Count));
    private bool CanAddHost() => Hosts.Count < AppSettings.MaxHosts;

    // Dostępność przycisków przez IsEnabled (CanMoveUp/CanMoveDown/CanRemoveHost), nie CanExecute —
    // WPF może odpytać CanExecute zanim zbinduje CommandParameter.
    // Button availability via IsEnabled (CanMoveUp/CanMoveDown/CanRemoveHost), not CanExecute —
    // WPF may query CanExecute before CommandParameter is bound.

    [RelayCommand]
    private void RemoveHost(HostEditorViewModel? host)
    {
        if (host is not null && CanRemoveHost) Hosts.Remove(host);
    }

    [RelayCommand]
    private void MoveUp(HostEditorViewModel? host)
    {
        int i = host is null ? -1 : Hosts.IndexOf(host);
        if (i > 0) Hosts.Move(i, i - 1);
    }

    [RelayCommand]
    private void MoveDown(HostEditorViewModel? host)
    {
        int i = host is null ? -1 : Hosts.IndexOf(host);
        if (i >= 0 && i < Hosts.Count - 1) Hosts.Move(i, i + 1);
    }

    // --- Wynik / Result ---

    /// <summary>Pierwszy błąd walidacji (z indeksem hosta) lub null / First validation error (with host index) or null</summary>
    public (string Message, int HostIndex)? Validate()
    {
        foreach (var host in Hosts)
        {
            if (host.Validate() is { } error) return (error, host.Index);
        }
        return null;
    }

    public AppSettings BuildSettings()
    {
        // Aktywna zakładka podąża za hostem, nie za pozycją / The active tab follows its host, not the position
        var activeId = _original.Hosts.ElementAtOrDefault(_original.ActiveHostIndex)?.Id;
        int active   = Math.Max(0, Hosts.ToList().FindIndex(h => h.Id == activeId));

        return new AppSettings
        {
            Hosts                 = Hosts.Select(h => h.ToHostConfig()).ToList(),
            PrimaryMonitorIndex   = PrimaryMonitorIndex,
            SecondaryMonitorIndex = SecondaryMonitorIndex,
            ActiveHostIndex       = active,
            AutoReconnect         = AutoReconnect,
            RemoteWindowsKeys     = RemoteWindowsKeys,
            AcceptAnyCertificate  = AcceptAnyCertificate
        };
    }
}
