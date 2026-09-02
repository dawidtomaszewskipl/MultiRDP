# MultiRDP

Dwie równoległe sesje RDP na dwóch monitorach, przełączane zakładkami. WPF / .NET 8.

## Problem

Aplikacja kliencka używana w centrum monitorowania nie uruchamia się w dwóch instancjach
z różnymi konfiguracjami na jednym komputerze. Obejściem były dwie maszyny wirtualne
z osobnymi instancjami Windows i osobną konfiguracją aplikacji w każdej z nich.

To rozwiązało jeden problem i stworzyło drugi: operator ma stanowisko z trzema ekranami
i musi widzieć obie maszyny **jednocześnie**, a nie przełączać się między dwoma oknami
`mstsc.exe` rozjeżdżającymi się po pulpicie.

MultiRDP łączy się z obiema maszynami naraz, rozkłada je na dwa wskazane ekrany i pozwala
przełączać aktywnego hosta zakładkami z klawiatury.

Aplikacja działa na produkcji.

## Funkcjonalność

- **Dwa okna pełnoekranowe** — każde na osobnym, wskazanym monitorze
- **Dwa hosty RDP** konfigurowane w ustawieniach
- **Pasek zakładek** w pierwszym oknie do przełączania aktywnego hosta
- **Skróty klawiszowe**: `Ctrl+1` / `Ctrl+2` (zakładki), `Enter` (połącz), `Esc` (rozłącz), `Ctrl+,` (ustawienia)
- Ciemny motyw interfejsu
- Hasła szyfrowane przez DPAPI

## Wymagania

- Windows 10 / 11 (64-bit)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (lub Rider / VS Code z rozszerzeniem C#)
- Usługa RDP włączona na hostach docelowych

## Uruchomienie

### 1. Otwórz `MultiRDP.sln`

### 2. Dodaj referencję COM do `mstscax.dll`

Interop do klienta terminala nie jest trzymany w repozytorium — generuje się go lokalnie
z biblioteki systemowej Windows.

W Visual Studio: prawy przycisk na projekcie `MultiRDP` → **Add** → **COM Reference** →
**Microsoft Terminal Services Client** → OK.

Albo w PowerShellu:

```powershell
aximp C:\Windows\System32\mstscax.dll
```

Wygenerowane `AxInterop.MSTSCLib.dll` i `MSTSCLib.dll` umieść w `MultiRDP/libs/`.

### 3. Zbuduj i uruchom

```powershell
dotnet restore
dotnet build -c Release
```

### 4. Skonfiguruj połączenia

Przy pierwszym uruchomieniu kliknij **⚙** i uzupełnij adresy hostów, dane logowania oraz
przypisanie okien do monitorów. Ustawienia trafiają do `appsettings.json` obok pliku
wykonywalnego — plik w repozytorium zawiera wyłącznie wartości przykładowe.

## Struktura

```
MultiRDP/
├── Controls/
│   └── RdpHostControl.cs       # WinForms UserControl z kontrolką ActiveX RDP
├── Models/
│   ├── AppSettings.cs
│   └── HostConfig.cs
├── Services/
│   ├── MonitorService.cs       # wykrywanie monitorów, pozycjonowanie okien
│   ├── RdpSessionManager.cs    # zarządzanie sesjami RDP
│   └── SettingsService.cs      # odczyt/zapis ustawień + szyfrowanie DPAPI
├── ViewModels/
│   └── MainViewModel.cs        # logika zakładek i przełączania hostów (MVVM)
└── Views/
    ├── MainWindow.xaml         # okno główne: pasek zakładek + sesja RDP
    ├── SecondaryWindow.xaml    # drugie okno: pełny ekran
    └── SettingsWindow.xaml
```

## Zależności

- [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm/)
- **MSTSCLib** (COM) — Microsoft Terminal Services Client ActiveX, `C:\Windows\System32\mstscax.dll`

## Uwagi techniczne

- Przy przełączaniu zakładki sesje są rozłączane i nawiązywane ponownie.
- Pozycjonowanie okien pełnoekranowych uwzględnia skalowanie DPI w Windows.
- Hasła mogą być szyfrowane DPAPI — flaga `IsPasswordEncrypted` w `appsettings.json`.

## Licencja

MIT — patrz [LICENSE](LICENSE).
