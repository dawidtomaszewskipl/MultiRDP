using System.Threading;
using System.Windows;
using System.Windows.Threading;
using MultiRDP.Models;
using MultiRDP.Services;
using MultiRDP.ViewModels;
using MultiRDP.Views;

namespace MultiRDP;

/// <summary>
/// Punkt wejścia aplikacji.
/// Tworzy okno sesji RDP dla każdego hosta (1–4), wszystkie rozpięte na tej samej parze
/// monitorów, oraz pływający pasek zakładek. Zmiana zakładki zmienia kolejność Z okien
/// (bez rozłączania sesji).
///
/// Application entry point.
/// Creates an RDP session window per host (1–4), all spanning the same monitor pair, and a
/// floating tab bar. Tab switching changes the windows' Z-order (without disconnecting sessions).
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\MultiRDP.SingleInstance";

    private Mutex?          _instanceMutex;
    private SessionManager? _sessionManager;
    private MainViewModel?  _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException               += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("Nieobsłużony wyjątek", args.ExceptionObject as Exception);

        // Druga instancja zdublowałaby wszystkie sesje / A second instance would duplicate every session
        _instanceMutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            _instanceMutex.Dispose();
            _instanceMutex = null;
            MessageBox.Show("MultiRDP jest już uruchomiony.", "MultiRDP",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Log.Info("Start aplikacji");

        var settingsService = new SettingsService();
        AppSettings settings = settingsService.Load(out string? warning);

        _sessionManager = new SessionManager(settings);
        _viewModel      = new MainViewModel(settingsService, settings, _sessionManager);

        // Okna sesji pod spodem, pasek zakładek (Topmost) na wierzchu
        // Session windows underneath, tab bar (Topmost) on top
        _sessionManager.ShowAll();

        var tabBar = new MainWindow(_viewModel);
        MainWindow   = tabBar;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        tabBar.Show();

        _viewModel.SelectHost(settings.ActiveHostIndex);

        if (warning is not null)
            MessageBox.Show(tabBar, warning, "MultiRDP — ustawienia", MessageBoxButton.OK, MessageBoxImage.Warning);

        _sessionManager.ConnectAll();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _viewModel?.SaveSettings();
            _sessionManager?.Dispose();
            Log.Info("Zamknięcie aplikacji");
        }
        finally
        {
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
            base.OnExit(e);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Nieobsłużony wyjątek w wątku UI", e.Exception);
        MessageBox.Show($"Wystąpił nieoczekiwany błąd:\n{e.Exception.Message}\n\nSzczegóły w logu: {Log.Directory}",
            "MultiRDP", MessageBoxButton.OK, MessageBoxImage.Error);

        // Nie zamykaj sesji operatora z powodu pojedynczego błędu / Don't kill operator sessions over a single error
        e.Handled = true;
    }
}
