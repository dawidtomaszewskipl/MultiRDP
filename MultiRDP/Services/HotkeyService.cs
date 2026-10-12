using System.Windows;
using System.Windows.Interop;
using MultiRDP.Interop;

namespace MultiRDP.Services;

/// <summary>
/// Globalne skróty Ctrl+Alt+1..N (RegisterHotKey) przełączające zakładki — działają także
/// z fokusem w sesji RDP, o ile kontrolka nie przechwytuje klawiatury (KeyboardHookMode = 0,
/// patrz <see cref="Models.AppSettings.RemoteWindowsKeys"/>).
///
/// Global Ctrl+Alt+1..N shortcuts (RegisterHotKey) switching tabs — they also work with focus in
/// an RDP session, as long as the control doesn't hook the keyboard (KeyboardHookMode = 0,
/// see <see cref="Models.AppSettings.RemoteWindowsKeys"/>).
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int  BaseId = 0x4D52; // "MR"
    private const uint VK_1   = 0x31;

    private readonly IntPtr     _hwnd;
    private readonly HwndSource _source;
    private int _registered;

    /// <summary>Indeks zakładki (0..N-1) / Tab index (0..N-1)</summary>
    public event Action<int>? Pressed;

    /// <summary>Wymaga okna z utworzonym HWND (po SourceInitialized) / Requires a window with an HWND (after SourceInitialized)</summary>
    public HotkeyService(Window window)
    {
        _hwnd   = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source.AddHook(WndProc);
    }

    /// <summary>Rejestruje Ctrl+Alt+1..count / Registers Ctrl+Alt+1..count</summary>
    public void Register(int count)
    {
        UnregisterAll();

        for (int i = 0; i < count; i++)
        {
            bool ok = NativeMethods.RegisterHotKey(_hwnd, BaseId + i,
                NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
                VK_1 + (uint)i);

            // Kombinacja zajęta przez inny program — nie blokuj aplikacji
            // Combination taken by another program — don't block the app
            if (!ok) Log.Warn($"Nie można zarejestrować skrótu Ctrl+Alt+{i + 1}");
        }
        _registered = count;
    }

    private void UnregisterAll()
    {
        for (int i = 0; i < _registered; i++)
            NativeMethods.UnregisterHotKey(_hwnd, BaseId + i);
        _registered = 0;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int index = wParam.ToInt32() - BaseId;
            if (index >= 0 && index < _registered)
            {
                Pressed?.Invoke(index);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
    }
}
