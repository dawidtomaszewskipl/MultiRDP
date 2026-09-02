using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MultiRDP.ViewModels;
using MultiRDP.Views;

namespace MultiRDP;

/// <summary>
/// Punkt wejścia aplikacji.
/// Tworzy dwa okna RDP (jedno per host) rozpięte na obu monitorach oraz pasek zakładek.
/// Zmiana zakładki zmienia kolejność Z okien (bez rozłączania sesji).
///
/// Application entry point.
/// Creates two RDP windows (one per host) spanning both monitors and a tab bar.
/// Tab switching changes the Z-order of windows (without disconnecting sessions).
/// </summary>
public partial class App : Application
{
    // --- Win32 API do zarządzania kolejnością Z bez utraty fokusa ---
    // --- Win32 API for Z-order management without losing focus ---
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_BOTTOM    = new(1);
    private static readonly IntPtr HWND_TOP       = new(0);
    private const uint SWP_NOMOVE      = 0x0002;
    private const uint SWP_NOSIZE      = 0x0001;
    private const uint SWP_NOACTIVATE  = 0x0010; // Nie kradnij fokusa / Don't steal focus

    private MainWindow?      _tabBar;
    private SecondaryWindow? _rdpWindow0;
    private SecondaryWindow? _rdpWindow1;
    private MainViewModel?   _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _viewModel = new MainViewModel();
        var settings = _viewModel.Settings;

        int primary   = settings.PrimaryMonitorIndex;
        int secondary = settings.SecondaryMonitorIndex;

        // Utwórz dwa okna RDP (jedno na hosta) / Create two RDP windows (one per host)
        _rdpWindow0 = new SecondaryWindow(primary, secondary);
        _rdpWindow1 = new SecondaryWindow(primary, secondary);

        // Cienki pasek zakładek na górze monitora głównego / Thin tab bar at top of primary monitor
        _tabBar = new MainWindow(_viewModel);

        // Pokaż okna RDP (oba w tle) / Show RDP windows (both in background)
        _rdpWindow0.Show();
        _rdpWindow1.Show();

        // Pokaż pasek zakładek na wierzchu (Topmost=true w XAML) / Show tab bar on top
        _tabBar.Show();

        // Ustaw Z-order zgodnie z zapisanym aktywnym hostem / Set Z-order per saved active host
        var frontWindow = _viewModel.SelectedHostIndex == 0 ? _rdpWindow0 : _rdpWindow1;
        var backWindow  = _viewModel.SelectedHostIndex == 0 ? _rdpWindow1 : _rdpWindow0;
        SendWindowToBack(backWindow);
        BringRdpWindowToFront(frontWindow);

        // Zarejestruj kontrolki RDP i nawiąż połączenia z oboma hostami
        // Register RDP controls and establish connections to both hosts
        _viewModel.InitializeSession(
            _rdpWindow0.RdpControl,
            _rdpWindow1.RdpControl);

        // Reaguj na zmianę zakładki / React to tab changes
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Obsługa zamknięcia / Shutdown handling
        _tabBar.Closed += (_, _) =>
        {
            _viewModel.SaveSettings();
            _rdpWindow0.Close();
            _rdpWindow1.Close();
            Shutdown();
        };

        _rdpWindow0.Closed += (_, _) => { if (_tabBar.IsLoaded) _tabBar.Close(); };
        _rdpWindow1.Closed += (_, _) => { if (_tabBar.IsLoaded) _tabBar.Close(); };
    }

    private void OnViewModelPropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.SelectedHostIndex)) return;

        // Zmiana zakładki = zmiana Z-order, BEZ rozłączania sesji
        // Tab change = Z-order change, WITHOUT disconnecting sessions
        Dispatcher.Invoke(() =>
        {
            var activeWindow   = _viewModel!.SelectedHostIndex == 0 ? _rdpWindow0! : _rdpWindow1!;
            var inactiveWindow = _viewModel!.SelectedHostIndex == 0 ? _rdpWindow1! : _rdpWindow0!;

            // Przesuń nieaktywne okno na spód / Push inactive window to bottom
            SendWindowToBack(inactiveWindow);

            // Wysuń aktywne okno na wierzch (pod pasek zakładek) / Bring active window to front (below tab bar)
            BringRdpWindowToFront(activeWindow);
        });
    }

    /// <summary>
    /// Wysuwa okno RDP na wierzch (pod topmost pasek zakładek), bez kradzieży fokusa.
    /// Brings RDP window to front (below topmost tab bar), without stealing focus.
    /// </summary>
    private void BringRdpWindowToFront(SecondaryWindow window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>
    /// Przesuwa okno RDP na spód kolejności Z.
    /// Sends RDP window to the bottom of Z-order.
    /// </summary>
    private void SendWindowToBack(SecondaryWindow window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }
}
