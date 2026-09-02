using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MultiRDP.Models;
using MultiRDP.Services;

namespace MultiRDP.Views;

/// <summary>
/// Okno ustawień — konfiguracja hostów RDP i przypisania monitorów
/// Settings window — RDP host configuration and monitor assignment
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _originalSettings;

    /// <summary>Wynik po zatwierdzeniu / Result after confirmation</summary>
    public AppSettings? Result { get; private set; }

    public SettingsWindow(AppSettings currentSettings)
    {
        InitializeComponent();

        // Głęboka kopia ustawień / Deep copy of settings
        _originalSettings = currentSettings;

        LoadMonitors();
        PopulateFields(currentSettings);

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Ustaw selekcję monitorów po załadowaniu / Set monitor selection after load
        CmbMonitor1.SelectedIndex = Math.Clamp(
            _originalSettings.PrimaryMonitorIndex, 0, CmbMonitor1.Items.Count - 1);
        CmbMonitor2.SelectedIndex = Math.Clamp(
            _originalSettings.SecondaryMonitorIndex, 0, CmbMonitor2.Items.Count - 1);
    }

    /// <summary>
    /// Ładuje listę dostępnych monitorów do ComboBox
    /// Loads list of available monitors to ComboBox
    /// </summary>
    private void LoadMonitors()
    {
        var monitors = MonitorService.GetMonitors();

        CmbMonitor1.Items.Clear();
        CmbMonitor2.Items.Clear();

        foreach (var monitor in monitors)
        {
            // Pokazuj pozycję X,Y żeby użytkownik mógł zidentyfikować monitor
            // Show X,Y position so user can identify the monitor
            string pos   = $"@ ({monitor.Bounds.X}, {monitor.Bounds.Y})";
            string size  = $"{monitor.Bounds.Width}×{monitor.Bounds.Height}";
            string label = monitor.IsPrimary
                ? $"Monitor {monitor.Index + 1} [{size}] {pos} (główny)"
                : $"Monitor {monitor.Index + 1} [{size}] {pos}";

            CmbMonitor1.Items.Add(label);
            CmbMonitor2.Items.Add(label);
        }

        // Domyślna selekcja gdy brak elementów / Default selection when no items
        if (CmbMonitor1.Items.Count > 0) CmbMonitor1.SelectedIndex = 0;
        if (CmbMonitor2.Items.Count > 1) CmbMonitor2.SelectedIndex = 1;
        else if (CmbMonitor2.Items.Count > 0) CmbMonitor2.SelectedIndex = 0;
    }

    /// <summary>
    /// Wypełnia pola formularza bieżącymi ustawieniami
    /// Fills form fields with current settings
    /// </summary>
    private void PopulateFields(AppSettings settings)
    {
        var hosts = settings.Hosts;

        // Host 1
        if (hosts.Count > 0)
        {
            TxtH1Name.Text = hosts[0].Name;
            TxtH1Address.Text = hosts[0].Address;
            TxtH1Username.Text = hosts[0].Username;
            TxtH1Domain.Text = hosts[0].Domain;
            TxtH1Port.Text = hosts[0].Port.ToString();
            PwdH1Password.Password = SettingsService.GetPassword(hosts[0]);
        }

        // Host 2
        if (hosts.Count > 1)
        {
            TxtH2Name.Text = hosts[1].Name;
            TxtH2Address.Text = hosts[1].Address;
            TxtH2Username.Text = hosts[1].Username;
            TxtH2Domain.Text = hosts[1].Domain;
            TxtH2Port.Text = hosts[1].Port.ToString();
            PwdH2Password.Password = SettingsService.GetPassword(hosts[1]);
        }
    }

    /// <summary>
    /// Odczytuje dane z formularza i tworzy nowy AppSettings
    /// Reads form data and creates new AppSettings
    /// </summary>
    private AppSettings BuildSettings()
    {
        var settings = new AppSettings
        {
            PrimaryMonitorIndex = Math.Max(0, CmbMonitor1.SelectedIndex),
            SecondaryMonitorIndex = Math.Max(0, CmbMonitor2.SelectedIndex),
            ActiveHostIndex = _originalSettings.ActiveHostIndex,
            Hosts = new List<HostConfig>
            {
                new()
                {
                    Name = TxtH1Name.Text.Trim().Length > 0 ? TxtH1Name.Text.Trim() : "Host 1",
                    Address = TxtH1Address.Text.Trim(),
                    Username = TxtH1Username.Text.Trim(),
                    Domain = TxtH1Domain.Text.Trim(),
                    Port = int.TryParse(TxtH1Port.Text, out int p1) ? p1 : 3389,
                    Password = PwdH1Password.Password,
                    IsPasswordEncrypted = false
                },
                new()
                {
                    Name = TxtH2Name.Text.Trim().Length > 0 ? TxtH2Name.Text.Trim() : "Host 2",
                    Address = TxtH2Address.Text.Trim(),
                    Username = TxtH2Username.Text.Trim(),
                    Domain = TxtH2Domain.Text.Trim(),
                    Port = int.TryParse(TxtH2Port.Text, out int p2) ? p2 : 3389,
                    Password = PwdH2Password.Password,
                    IsPasswordEncrypted = false
                }
            }
        };

        return settings;
    }

    private bool ValidateFields()
    {
        if (string.IsNullOrWhiteSpace(TxtH1Address.Text))
        {
            ShowError("Podaj adres IP / hostname dla Host 1.");
            TxtH1Address.Focus();
            return false;
        }
        if (string.IsNullOrWhiteSpace(TxtH2Address.Text))
        {
            ShowError("Podaj adres IP / hostname dla Host 2.");
            TxtH2Address.Focus();
            return false;
        }
        return true;
    }

    private void ShowError(string message)
    {
        MessageBox.Show(message, "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    // --- Identyfikacja monitorów / Monitor identification ---

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
    private const uint SWP_NOZORDER     = 0x0004;
    private const uint SWP_NOACTIVATE   = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private readonly List<Window> _flashWindows = new();

    private void BtnIdentify1_Click(object sender, RoutedEventArgs e)
        => FlashMonitor(CmbMonitor1.SelectedIndex);

    private void BtnIdentify2_Click(object sender, RoutedEventArgs e)
        => FlashMonitor(CmbMonitor2.SelectedIndex);

    private void BtnIdentifyAll_Click(object sender, RoutedEventArgs e)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
            FlashMonitor(i, closeOthers: false);
    }

    /// <summary>
    /// Wyświetla duże okno z numerem monitora na 2 sekundy.
    /// Displays a large window with the monitor number for 2 seconds.
    /// </summary>
    private void FlashMonitor(int monitorIndex, bool closeOthers = true)
    {
        if (closeOthers)
        {
            foreach (var w in _flashWindows) { try { w.Close(); } catch { } }
            _flashWindows.Clear();
        }

        var screens = System.Windows.Forms.Screen.AllScreens;
        if (monitorIndex < 0 || monitorIndex >= screens.Length) return;

        var b = screens[monitorIndex].Bounds;

        var flashWin = new Window
        {
            WindowStyle          = WindowStyle.None,
            ResizeMode           = ResizeMode.NoResize,
            ShowInTaskbar        = false,
            Topmost              = true,
            Background           = new System.Windows.Media.SolidColorBrush(
                                        System.Windows.Media.Color.FromArgb(200, 0, 30, 70)),
            Content = new System.Windows.Controls.TextBlock
            {
                Text                = $"Monitor {monitorIndex + 1}\n(indeks: {monitorIndex})",
                FontSize            = 72,
                FontWeight          = FontWeights.Bold,
                Foreground          = System.Windows.Media.Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment   = System.Windows.VerticalAlignment.Center,
                TextAlignment       = System.Windows.TextAlignment.Center,
                Effect              = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color     = System.Windows.Media.Colors.Black,
                    BlurRadius = 20,
                    ShadowDepth = 0
                }
            }
        };

        _flashWindows.Add(flashWin);
        flashWin.Show();

        // Pozycjonuj przez Win32 (fizyczne piksele, bez DPI) / Position via Win32 (physical px, no DPI)
        var hwnd = new WindowInteropHelper(flashWin).Handle;
        SetWindowPos(hwnd, IntPtr.Zero,
            b.X, b.Y, b.Width, b.Height,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

        // Zamknij po 2 sekundach / Close after 2 seconds
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                flashWin.Close();
                _flashWindows.Remove(flashWin);
            }
            catch { }
        };
        timer.Start();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateFields()) return;

        Result = BuildSettings();
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        CloseAllFlashWindows();
        DialogResult = false;
        Close();
    }

    private void CloseAllFlashWindows()
    {
        foreach (var w in _flashWindows) { try { w.Close(); } catch { } }
        _flashWindows.Clear();
    }

    protected override void OnClosed(EventArgs e)
    {
        CloseAllFlashWindows();
        base.OnClosed(e);
    }
}
