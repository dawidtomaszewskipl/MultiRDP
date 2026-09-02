using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MultiRDP.Services;
using MultiRDP.ViewModels;

namespace MultiRDP.Views;

/// <summary>
/// Floating panel wycentrowany i przyklejony do górnej krawędzi monitora głównego.
/// Obsługuje tryb auto-chowaj (odpięty) — pojawia się po najechaniu na 3px pasek u góry.
///
/// Floating panel centered and stuck to the top edge of the primary monitor.
/// Supports auto-hide (unpinned) — appears when hovering over the 3px strip at the top.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel    _viewModel;
    private readonly List<Button>     _tabButtons = new();

    // Przypinanie i auto-hide / Pinning and auto-hide
    private bool             _isPinned  = true;
    private double           _visibleTop;
    private double           _hiddenTop;
    private DispatcherTimer? _hideTimer;

    // Win32
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(
        IntPtr h, IntPtr ins, int X, int Y, int cx, int cy, uint f);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const uint SWP_NOSIZE     = 0x0001;
    private const uint SWP_NOZORDER   = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel  = viewModel;
        DataContext = viewModel;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        Loaded   += OnLoaded;

        // Popup zamknięty → sprawdź czy schować / Popup closed → check if should hide
        MenuPopup.Closed += (_, _) =>
        {
            if (!_isPinned && !IsMouseOver) ScheduleHide();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildTabButtons();
        UpdateTabStyles();
        UpdateStatusDot(_viewModel.ActiveSessionStatus);

        // Poczekaj na pełne wyrenderowanie przed pomiarem / Wait for full render before measuring
        Dispatcher.BeginInvoke(InitializePosition, DispatcherPriority.Render);
    }

    // ─────────────────────────────────────────────────────────────
    //  Pozycjonowanie / Positioning
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Wycentruj panel poziomo na monitorze głównym, przyklejony do górnej krawędzi.
    /// Center panel horizontally on primary monitor, flush to the top edge.
    /// </summary>
    public void InitializePosition()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        int idx = Math.Clamp(_viewModel.Settings.PrimaryMonitorIndex, 0, screens.Length - 1);
        var b = screens[idx].Bounds; // fizyczne piksele / physical pixels

        var src  = PresentationSource.FromVisual(this);
        double dpiX = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = src?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        // Logiczne granice monitora / Logical monitor bounds
        double monL = b.Left  / dpiX;
        double monT = b.Top   / dpiY;
        double monW = b.Width / dpiX;

        // Wycentruj poziomo / Center horizontally
        Left = monL + (monW - ActualWidth) / 2.0;

        // Widoczna pozycja = górna krawędź monitora / Visible = top edge of monitor
        _visibleTop = monT;

        // Ukryta pozycja = 3 fizyczne piksele widoczne u góry / Hidden = 3 physical pixels peek
        _hiddenTop = monT - ActualHeight + 3.0 / dpiY;

        // Ustaw pozycję zgodnie ze stanem / Set position per current state
        Top = _isPinned ? _visibleTop : _hiddenTop;

        UpdatePinButton();
    }

    // ─────────────────────────────────────────────────────────────
    //  Animacja / Animation
    // ─────────────────────────────────────────────────────────────

    private void AnimateTo(double targetTop)
    {
        var anim = new DoubleAnimation
        {
            To             = targetTop,
            Duration       = TimeSpan.FromMilliseconds(160),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(TopProperty, anim);
    }

    // ─────────────────────────────────────────────────────────────
    //  Przypinanie / Pinning
    // ─────────────────────────────────────────────────────────────

    private void TogglePin()
    {
        _isPinned = !_isPinned;
        UpdatePinButton();

        if (_isPinned)
        {
            _hideTimer?.Stop();
            AnimateTo(_visibleTop);
        }
        else
        {
            // Schowaj po chwili jeśli mysz poza panelem / Hide after delay if mouse is outside
            ScheduleHide();
        }
    }

    private void UpdatePinButton()
    {
        if (BtnPinMenuItem is null) return;
        BtnPinMenuItem.Content = _isPinned
            ? "📌  Odepnij (auto-chowaj)"
            : "📌  Przypnij (zawsze widoczny)";
    }

    // ─────────────────────────────────────────────────────────────
    //  Auto-hide — mysz / Auto-hide — mouse
    // ─────────────────────────────────────────────────────────────

    protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (!_isPinned)
        {
            _hideTimer?.Stop();
            AnimateTo(_visibleTop);
        }
    }

    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_isPinned) ScheduleHide();
    }

    private void ScheduleHide()
    {
        if (MenuPopup.IsOpen) return;

        _hideTimer?.Stop();
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer!.Stop();
            if (!IsMouseOver && !MenuPopup.IsOpen)
                AnimateTo(_hiddenTop);
        };
        _hideTimer.Start();
    }

    // ─────────────────────────────────────────────────────────────
    //  Zakładki hostów / Host tabs
    // ─────────────────────────────────────────────────────────────

    private void BuildTabButtons()
    {
        HostTabsPanel.Items.Clear();
        _tabButtons.Clear();

        for (int i = 0; i < _viewModel.Hosts.Count; i++)
        {
            int idx  = i;
            var host = _viewModel.Hosts[i];
            var btn  = new Button
            {
                Content = host.Name,
                Tag     = idx,
                Margin  = new Thickness(2, 0, 2, 0),
                Style   = idx == _viewModel.SelectedHostIndex
                              ? (Style)Resources["HostTabActiveStyle"]
                              : (Style)Resources["HostTabStyle"]
            };
            btn.Click += (_, _) => SelectHost(idx);
            _tabButtons.Add(btn);
            HostTabsPanel.Items.Add(btn);
        }
    }

    private void SelectHost(int index)
    {
        if (index == _viewModel.SelectedHostIndex) return;
        _viewModel.SelectedHostIndex = index;
        UpdateTabStyles();
    }

    private void UpdateTabStyles()
    {
        for (int i = 0; i < _tabButtons.Count; i++)
        {
            _tabButtons[i].Style = i == _viewModel.SelectedHostIndex
                ? (Style)Resources["HostTabActiveStyle"]
                : (Style)Resources["HostTabStyle"];
        }
    }

    private void UpdateStatusDot(string status)
    {
        bool connected  = status == "Połączono";
        bool connecting = status.StartsWith("Łącz");

        StatusDot.Fill = connecting
            ? new SolidColorBrush(Colors.Orange)
            : connected
                ? new SolidColorBrush(WpfColor.FromRgb(76, 175, 80))
                : new SolidColorBrush(WpfColor.FromRgb(70, 70, 70));

        StatusDot.ToolTip = status;
    }

    private void ViewModel_PropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.ActiveSessionStatus):
                Dispatcher.Invoke(() => UpdateStatusDot(_viewModel.ActiveSessionStatus));
                break;
            case nameof(MainViewModel.Hosts):
                Dispatcher.Invoke(() => { BuildTabButtons(); UpdateTabStyles(); });
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Obsługa przycisków / Button handlers
    // ─────────────────────────────────────────────────────────────

    private void BtnMenu_Click(object sender, RoutedEventArgs e)
        => MenuPopup.IsOpen = !MenuPopup.IsOpen;

    private void BtnPin_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        TogglePin();
    }

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        _viewModel.ConnectCommand.Execute(null);
    }

    private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        _viewModel.DisconnectCommand.Execute(null);
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        OpenSettings();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        Application.Current.Shutdown();
    }

    private void OpenSettings()
    {
        var win = new SettingsWindow(_viewModel.Settings) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _viewModel.ApplySettings(win.Result!);
            BuildTabButtons();
            UpdateTabStyles();
            Dispatcher.BeginInvoke(InitializePosition, DispatcherPriority.Render);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Przeciąganie / Drag
    // ─────────────────────────────────────────────────────────────

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }

    // ─────────────────────────────────────────────────────────────
    //  Skróty / Shortcuts
    // ─────────────────────────────────────────────────────────────

    protected override void OnKeyDown(WpfKeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control)
        { OpenSettings(); return; }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            int? tab = e.Key switch
            {
                Key.D1 => 0, Key.D2 => 1, Key.D3 => 2, Key.D4 => 3, _ => null
            };
            if (tab.HasValue && tab.Value < _viewModel.Hosts.Count)
                SelectHost(tab.Value);
        }
    }
}
