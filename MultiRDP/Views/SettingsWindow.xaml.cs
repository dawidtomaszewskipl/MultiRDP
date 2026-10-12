using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MultiRDP.Models;
using MultiRDP.Services;
using MultiRDP.ViewModels;

namespace MultiRDP.Views;

/// <summary>
/// Okno ustawień — hosty RDP (1–4), przypisanie monitorów, opcje sesji.
/// Settings window — RDP hosts (1–4), monitor assignment, session options.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly List<Window>      _flashWindows = new();

    /// <summary>Wynik po zatwierdzeniu / Result after confirmation</summary>
    public AppSettings? Result { get; private set; }

    public SettingsWindow(AppSettings currentSettings)
    {
        InitializeComponent();
        _viewModel  = new SettingsViewModel(currentSettings);
        DataContext = _viewModel;
    }

    // --- Hasła (PasswordBox nie wspiera bindowania) / Passwords (PasswordBox doesn't support binding) ---

    private void PasswordBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox { DataContext: HostEditorViewModel host } box)
            box.Password = host.Password;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox { DataContext: HostEditorViewModel host } box)
            host.Password = box.Password;
    }

    // --- Zapis / Save ---

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Validate() is { } error)
        {
            (HostList.ItemContainerGenerator.ContainerFromIndex(error.HostIndex) as FrameworkElement)?.BringIntoView();
            MessageBox.Show(this, error.Message, "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_viewModel.PrimaryMonitorIndex == _viewModel.SecondaryMonitorIndex &&
            MessageBox.Show(this,
                "Monitor 1 i Monitor 2 to ten sam ekran — sesje zajmą tylko jeden monitor.\nKontynuować?",
                "Monitory", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        Result       = _viewModel.BuildSettings();
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    protected override void OnClosed(EventArgs e)
    {
        CloseAllFlashWindows();
        base.OnClosed(e);
    }

    // --- Identyfikacja monitorów / Monitor identification ---

    private void BtnIdentify1_Click(object sender, RoutedEventArgs e)
        => FlashMonitor(_viewModel.PrimaryMonitorIndex);

    private void BtnIdentify2_Click(object sender, RoutedEventArgs e)
        => FlashMonitor(_viewModel.SecondaryMonitorIndex);

    private void BtnIdentifyAll_Click(object sender, RoutedEventArgs e)
    {
        CloseAllFlashWindows();
        foreach (var monitor in _viewModel.Monitors)
            FlashMonitor(monitor.Index, closeOthers: false);
    }

    /// <summary>
    /// Wyświetla duże okno z numerem monitora na 2 sekundy.
    /// Displays a large window with the monitor number for 2 seconds.
    /// </summary>
    private void FlashMonitor(int monitorIndex, bool closeOthers = true)
    {
        if (closeOthers) CloseAllFlashWindows();
        if (monitorIndex < 0 || monitorIndex >= _viewModel.Monitors.Count) return;

        var flashWin = new Window
        {
            WindowStyle   = WindowStyle.None,
            ResizeMode    = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost       = true,
            Background    = new SolidColorBrush(WpfColor.FromArgb(200, 0, 30, 70)),
            Content = new TextBlock
            {
                Text                = $"Monitor {monitorIndex + 1}\n(indeks: {monitorIndex})",
                FontSize            = 72,
                FontWeight          = FontWeights.Bold,
                Foreground          = System.Windows.Media.Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment   = System.Windows.VerticalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                Effect              = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color       = WpfColors.Black,
                    BlurRadius  = 20,
                    ShadowDepth = 0
                }
            }
        };

        _flashWindows.Add(flashWin);
        flashWin.Show();
        MonitorService.PositionWindow(flashWin, MonitorService.GetBounds(monitorIndex));

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            flashWin.Close();
            _flashWindows.Remove(flashWin);
        };
        timer.Start();
    }

    private void CloseAllFlashWindows()
    {
        foreach (var w in _flashWindows) w.Close();
        _flashWindows.Clear();
    }
}
