using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using MultiRDP.Services;
using MultiRDP.ViewModels;

namespace MultiRDP.Views;

/// <summary>
/// Pływający pasek zakładek przyklejony do górnej krawędzi pierwszego monitora sesji.
/// Obsługuje tryb auto-chowaj (odpięty) — pojawia się po najechaniu na 3px pasek u góry.
///
/// Floating tab bar stuck to the top edge of the first session monitor.
/// Supports auto-hide (unpinned) — appears when hovering over the 3px strip at the top.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private HotkeyService? _hotkeys;

    // Przypinanie i auto-hide / Pinning and auto-hide
    private bool   _isPinned = true;
    private double _visibleTop;
    private double _hiddenTop;
    private System.Drawing.Rectangle _monitorBounds; // logiczne px / logical px
    private readonly DispatcherTimer _hideTimer;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel  = viewModel;
        DataContext = viewModel;

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (!IsMouseOver && !MenuPopup.IsOpen) AnimateTo(_hiddenTop);
        };

        Loaded += (_, _) => Dispatcher.BeginInvoke(InitializePosition, DispatcherPriority.Render);

        // Liczba zakładek zmienia szerokość paska → wyśrodkuj ponownie
        // Tab count changes the bar width → re-center
        SizeChanged += (_, e) => { if (e.WidthChanged && IsLoaded) InitializePosition(); };

        // Popup zamknięty → sprawdź czy schować / Popup closed → check if should hide
        MenuPopup.Closed += (_, _) =>
        {
            if (!_isPinned && !IsMouseOver) ScheduleHide();
        };

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hotkeys = new HotkeyService(this);
        _hotkeys.Pressed += index => _viewModel.SelectHost(index);
        _hotkeys.Register(_viewModel.Sessions.Count);
        _viewModel.Sessions.CollectionChanged += (_, _) => _hotkeys.Register(_viewModel.Sessions.Count);
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _hotkeys?.Dispose();
        base.OnClosed(e);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(InitializePosition, DispatcherPriority.Render);

    // ─────────────────────────────────────────────────────────────
    //  Pozycjonowanie / Positioning
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Wycentruj panel poziomo na pierwszym monitorze sesji, przyklejony do górnej krawędzi.
    /// Center panel horizontally on the first session monitor, flush to the top edge.
    /// </summary>
    public void InitializePosition()
    {
        var b = MonitorService.GetBounds(_viewModel.Settings.PrimaryMonitorIndex); // fizyczne px / physical px

        var src  = PresentationSource.FromVisual(this);
        double dpiX = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = src?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        _monitorBounds = new System.Drawing.Rectangle(
            (int)(b.Left / dpiX), (int)(b.Top / dpiY), (int)(b.Width / dpiX), (int)(b.Height / dpiY));

        Left        = _monitorBounds.Left + (_monitorBounds.Width - ActualWidth) / 2.0;
        _visibleTop = _monitorBounds.Top;

        // Ukryta pozycja = 3 fizyczne piksele widoczne u góry / Hidden = 3 physical pixels peek
        _hiddenTop = _monitorBounds.Top - ActualHeight + 3.0 / dpiY;

        SetTop(_isPinned ? _visibleTop : _hiddenTop);
        UpdatePinButton();
    }

    /// <summary>
    /// Ustawia Top bez animacji. Animacja trzyma wartość właściwości, więc trzeba ją najpierw zdjąć.
    /// Sets Top without animation. An animation holds the property value, so it must be removed first.
    /// </summary>
    private void SetTop(double top)
    {
        BeginAnimation(TopProperty, null);
        Top = top;
    }

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
    //  Przypinanie i auto-hide / Pinning and auto-hide
    // ─────────────────────────────────────────────────────────────

    private void TogglePin()
    {
        _isPinned = !_isPinned;
        UpdatePinButton();

        if (_isPinned)
        {
            _hideTimer.Stop();
            AnimateTo(_visibleTop);
        }
        else
        {
            ScheduleHide();
        }
    }

    private void UpdatePinButton()
    {
        BtnPinMenuItem.Content = _isPinned
            ? "📌  Odepnij (auto-chowaj)"
            : "📌  Przypnij (zawsze widoczny)";
    }

    protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (!_isPinned)
        {
            _hideTimer.Stop();
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
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    // ─────────────────────────────────────────────────────────────
    //  Przeciąganie — tylko w poziomie / Drag — horizontal only
    // ─────────────────────────────────────────────────────────────

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 1) return;

        SetTop(Top);
        DragMove(); // blokuje do puszczenia przycisku / blocks until the button is released

        // Zostań na krawędzi i w granicach monitora / Stay on the edge and within the monitor
        double maxLeft = _monitorBounds.Right - ActualWidth;
        Left = Math.Clamp(Left, _monitorBounds.Left, Math.Max(_monitorBounds.Left, maxLeft));
        SetTop(_visibleTop);
        if (!_isPinned && !IsMouseOver) ScheduleHide();
    }

    // ─────────────────────────────────────────────────────────────
    //  Menu
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
        _viewModel.Connect();
    }

    private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        _viewModel.Disconnect();
    }

    private void BtnConnectAll_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        _viewModel.ConnectAll();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        OpenSettings();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        Close();
    }

    private void OpenSettings()
    {
        var win = new SettingsWindow(_viewModel.Settings) { Owner = this };
        if (win.ShowDialog() != true) return;

        if (_viewModel.ApplySettings(win.Result!) is { } error)
        {
            MessageBox.Show(this,
                $"Ustawienia zastosowano, ale nie udało się ich zapisać:\n{error}",
                "MultiRDP", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        Dispatcher.BeginInvoke(InitializePosition, DispatcherPriority.Render);
    }

    // ─────────────────────────────────────────────────────────────
    //  Skróty lokalne (fokus na pasku) / Local shortcuts (tab bar focused)
    //  Globalne Ctrl+Alt+1..4 obsługuje HotkeyService / Global Ctrl+Alt+1..4 handled by HotkeyService
    // ─────────────────────────────────────────────────────────────

    protected override void OnKeyDown(WpfKeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        if (e.Key == Key.OemComma)
        {
            OpenSettings();
            e.Handled = true;
            return;
        }

        int index = e.Key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            _ => -1
        };
        if (index >= 0)
        {
            _viewModel.SelectHost(index);
            e.Handled = true;
        }
    }
}
