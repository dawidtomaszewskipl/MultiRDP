using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiRDP.Controls;
using MultiRDP.Models;
using MultiRDP.Services;

namespace MultiRDP.ViewModels;

/// <summary>
/// ViewModel główny. Utrzymuje dwie równoległe sesje RDP (po jednej na hosta).
/// Przełączanie zakładek NIE rozłącza sesji — zmienia tylko aktywny indeks
/// (App.xaml.cs obsługuje zmianę kolejności Z okien).
///
/// Main ViewModel. Maintains two parallel RDP sessions (one per host).
/// Tab switching does NOT disconnect sessions — it only changes the active index
/// (App.xaml.cs handles window Z-order changes).
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly SettingsService    _settingsService;
    private readonly RdpSessionManager  _sessionManager;
    private AppSettings _settings;

    [ObservableProperty]
    private ObservableCollection<HostConfig> _hosts = new();

    /// <summary>
    /// Zmiana tego indeksu NIE rozłącza sesji — powiadamia App do zmiany Z-order.
    /// Changing this index does NOT disconnect sessions — notifies App for Z-order change.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveHost))]
    private int _selectedHostIndex = 0;

    [ObservableProperty] private string _session0Status = "Rozłączono";
    [ObservableProperty] private string _session1Status = "Rozłączono";

    /// <summary>Aktywna konfiguracja hosta / Active host configuration</summary>
    public HostConfig? ActiveHost =>
        SelectedHostIndex >= 0 && SelectedHostIndex < Hosts.Count
            ? Hosts[SelectedHostIndex]
            : null;

    /// <summary>Status aktywnej sesji (do wyświetlenia w pasku) / Active session status (for display)</summary>
    public string ActiveSessionStatus =>
        SelectedHostIndex == 0 ? Session0Status : Session1Status;

    public AppSettings Settings => _settings;

    public MainViewModel()
    {
        _settingsService = new SettingsService();
        _sessionManager  = new RdpSessionManager();
        _settings        = _settingsService.Load();

        foreach (var host in _settings.Hosts)
            Hosts.Add(host);

        SelectedHostIndex = Math.Clamp(
            _settings.ActiveHostIndex, 0, Math.Max(0, Hosts.Count - 1));

        _sessionManager.ConnectionStateChanged += OnConnectionStateChanged;
        _sessionManager.ConnectionError        += OnConnectionError;
    }

    /// <summary>
    /// Rejestruje kontrolki RDP z obu okien i łączy z obu hostami jednocześnie.
    /// Registers RDP controls from both windows and connects to both hosts simultaneously.
    /// </summary>
    public void InitializeSession(RdpHostControl control0, RdpHostControl control1)
    {
        _sessionManager.RegisterControls(control0, control1);
        _sessionManager.SetMonitorIndices(
            _settings.PrimaryMonitorIndex,
            _settings.SecondaryMonitorIndex);
        ConnectAll();
    }

    /// <summary>
    /// Łączy z OBOMA hostami jednocześnie.
    /// Connects to BOTH hosts simultaneously.
    /// </summary>
    private void ConnectAll()
    {
        if (Hosts.Count > 0) _sessionManager.Connect(0, Hosts[0]);
        if (Hosts.Count > 1) _sessionManager.Connect(1, Hosts[1]);
    }

    /// <summary>
    /// Zmiana zakładki — tylko zmiana indeksu, BRAK rozłączania.
    /// Tab change — only index change, NO disconnecting.
    /// </summary>
    partial void OnSelectedHostIndexChanged(int value)
    {
        if (value < 0 || value >= Hosts.Count) return;
        _settings.ActiveHostIndex = value;
        // Powiadomienie właściwości zależnych / Notify dependent properties
        OnPropertyChanged(nameof(ActiveSessionStatus));
    }

    partial void OnSession0StatusChanged(string value) => OnPropertyChanged(nameof(ActiveSessionStatus));
    partial void OnSession1StatusChanged(string value) => OnPropertyChanged(nameof(ActiveSessionStatus));

    // --- Komendy / Commands ---

    /// <summary>Ponowne połączenie z aktywnym hostem / Reconnect to active host</summary>
    [RelayCommand]
    public void Connect()
    {
        if (SelectedHostIndex >= 0 && SelectedHostIndex < Hosts.Count)
            _sessionManager.Connect(SelectedHostIndex, Hosts[SelectedHostIndex]);
    }

    /// <summary>Rozłącza aktywną sesję / Disconnects the active session</summary>
    [RelayCommand]
    public void Disconnect()
        => _sessionManager.Disconnect(SelectedHostIndex);

    /// <summary>Rozłącza obie sesje / Disconnects both sessions</summary>
    [RelayCommand]
    public void DisconnectAll()
        => _sessionManager.DisconnectAll();

    /// <summary>
    /// Stosuje nowe ustawienia i ponownie łączy z obu hostami.
    /// Applies new settings and reconnects to both hosts.
    /// </summary>
    public void ApplySettings(AppSettings newSettings)
    {
        _settings = newSettings;
        _settingsService.Save(_settings);

        _sessionManager.SetMonitorIndices(
            _settings.PrimaryMonitorIndex,
            _settings.SecondaryMonitorIndex);

        Hosts.Clear();
        foreach (var host in _settings.Hosts)
            Hosts.Add(host);

        SelectedHostIndex = Math.Clamp(
            _settings.ActiveHostIndex, 0, Math.Max(0, Hosts.Count - 1));

        // Ponowne połączenie z nowymi konfiguracjami / Reconnect with new configurations
        ConnectAll();
    }

    public void SaveSettings() => _settingsService.Save(_settings);

    // --- Obsługa zdarzeń sesji / Session event handlers ---

    private void OnConnectionStateChanged(object? sender, (int SessionIndex, bool IsConnected) e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            string status = e.IsConnected ? "Połączono" : "Rozłączono";
            if (e.SessionIndex == 0) Session0Status = status;
            else                     Session1Status = status;
        });
    }

    private void OnConnectionError(object? sender, (int SessionIndex, string Message) e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            string status = $"Błąd: {e.Message}";
            if (e.SessionIndex == 0) Session0Status = status;
            else                     Session1Status = status;
        });
    }
}
