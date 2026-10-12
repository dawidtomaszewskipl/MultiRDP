using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiRDP.Models;
using MultiRDP.Services;
using MultiRDP.Sessions;

namespace MultiRDP.ViewModels;

/// <summary>
/// ViewModel paska zakładek. Przełączanie zakładek NIE rozłącza sesji — zmienia tylko
/// aktywną sesję (kolejność Z okien, patrz <see cref="SessionManager.Activate"/>).
///
/// Tab bar ViewModel. Tab switching does NOT disconnect sessions — it only changes the
/// active session (window Z-order, see <see cref="SessionManager.Activate"/>).
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly SessionManager  _sessionManager;
    private AppSettings _settings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveSession))]
    private int _selectedHostIndex = -1;

    public ObservableCollection<RdpSession> Sessions => _sessionManager.Sessions;

    public RdpSession? ActiveSession =>
        SelectedHostIndex >= 0 && SelectedHostIndex < Sessions.Count ? Sessions[SelectedHostIndex] : null;

    /// <summary>Bieżące ustawienia (nie modyfikować — zmiany przez <see cref="ApplySettings"/>)</summary>
    public AppSettings Settings => _settings;

    public MainViewModel(SettingsService settingsService, AppSettings settings, SessionManager sessionManager)
    {
        _settingsService = settingsService;
        _sessionManager  = sessionManager;
        _settings        = settings;
    }

    partial void OnSelectedHostIndexChanged(int value)
    {
        _settings.ActiveHostIndex = value;
        _sessionManager.Activate(value);
    }

    // --- Komendy / Commands ---

    /// <summary>Kliknięcie zakładki / Tab click</summary>
    [RelayCommand]
    private void SelectSession(RdpSession? session)
    {
        if (session is not null) SelectHost(Sessions.IndexOf(session));
    }

    /// <summary>Wybór zakładki (pasek, Ctrl+1..4, Ctrl+Alt+1..4) / Tab selection</summary>
    public void SelectHost(int index)
    {
        if (index < 0 || index >= Sessions.Count) return;
        if (index == SelectedHostIndex)
            _sessionManager.Activate(index); // np. przywróć fokus / e.g. restore focus
        else
            SelectedHostIndex = index;
    }

    /// <summary>(Ponowne) połączenie z aktywnym hostem / (Re)connect the active host</summary>
    [RelayCommand]
    public void Connect() => ActiveSession?.Connect();

    /// <summary>Rozłącza aktywną sesję / Disconnects the active session</summary>
    [RelayCommand]
    public void Disconnect() => ActiveSession?.Disconnect();

    /// <summary>Łączy ponownie wszystkie sesje / Reconnects all sessions</summary>
    [RelayCommand]
    public void ConnectAll() => _sessionManager.ConnectAll();

    /// <summary>
    /// Zapisuje i stosuje nowe ustawienia. Zwraca komunikat błędu zapisu (ustawienia są
    /// stosowane mimo to) lub null.
    ///
    /// Saves and applies new settings. Returns a save error message (settings are applied
    /// anyway) or null.
    /// </summary>
    public string? ApplySettings(AppSettings newSettings)
    {
        newSettings.Normalize();
        _settingsService.TrySave(newSettings, out string? error);

        _settings = newSettings;
        _sessionManager.Apply(newSettings);

        // Odśwież Z-order także gdy indeks się nie zmienił (nowe okna pojawiają się na wierzchu)
        // Refresh Z-order even when the index didn't change (new windows appear on top)
        int index = Math.Clamp(newSettings.ActiveHostIndex, 0, Sessions.Count - 1);
        if (index == SelectedHostIndex)
        {
            _settings.ActiveHostIndex = index;
            _sessionManager.Activate(index);
        }
        else
        {
            SelectedHostIndex = index;
        }

        return error;
    }

    public void SaveSettings() => _settingsService.TrySave(_settings, out _);
}
