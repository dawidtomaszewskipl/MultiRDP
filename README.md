# MultiRDP

Dwie równoległe sesje RDP na dwóch monitorach, przełączane zakładkami. WPF / .NET 8.

## Problem

Aplikacja kliencka używana w centrum monitorowania nie uruchamia się w dwóch instancjach
z różnymi konfiguracjami na jednym komputerze. Obejściem były dwie maszyny wirtualne
z osobnymi instancjami Windows i osobną konfiguracją aplikacji w każdej z nich.

To rozwiązało jeden problem i stworzyło drugi: operator ma stanowisko z trzema ekranami
i musi widzieć obie maszyny **jednocześnie**, a nie przełączać się między dwoma oknami
`mstsc.exe` rozjeżdżającymi się po pulpicie.

MultiRDP utrzymuje równolegle sesje RDP z 1–4 maszynami, rozkłada każdą na tę samą parę
wskazanych ekranów i pozwala przełączać aktywnego hosta zakładkami — także z klawiatury,
bez rozłączania pozostałych sesji.

Aplikacja działa na produkcji.

## Jak to działa

- Każdy host ma własne bezramkowe okno z kontrolką ActiveX RDP, rozpięte na **parze monitorów**
  wybranej w ustawieniach (`UseMultimon` + `selectedmonitors`). Trzeci monitor zostaje wolny.
- Wszystkie okna sesji leżą na sobie; zakładka wybiera, które jest na wierzchu (kolejność Z).
  **Przełączenie zakładki nie rozłącza sesji** — wszystkie są połączone cały czas.
- Pływający pasek zakładek (zawsze na wierzchu) jest wyśrodkowany u góry pierwszego monitora.
  Można go przypiąć albo ustawić auto-chowanie (zostaje 3-pikselowy pasek do najechania myszą).

## Funkcjonalność

- **1–4 hosty RDP** jako zakładki — dodawane, usuwane i przestawiane w ustawieniach
- **Status każdej sesji** na zakładce (zielony — połączono, pomarańczowy — łączenie/wznawianie,
  czerwony — błąd, szary — rozłączono); szczegóły w podpowiedzi po najechaniu
- **Automatyczne ponowne łączenie** po zerwaniu sesji (5 s → 10 s → 30 s → co 60 s), wyłączalne
  w ustawieniach; nie działa po ręcznym „Rozłącz” ani gdy sesję przejęło inne stanowisko
- **Zmiana ustawień łączy ponownie tylko te sesje, których dotyczy** (zmiana nazwy czy kolejności
  zakładek nie zrywa połączeń)
- **Skróty klawiszowe**:
  - `Ctrl+Alt+1..4` — globalnie, także z fokusem w sesji RDP (patrz niżej)
  - `Ctrl+1..4`, `Ctrl+,` (ustawienia) — gdy fokus jest na pasku zakładek
- Hasła szyfrowane przez DPAPI
- Log zdarzeń połączeń w `%LocalAppData%\MultiRDP\logs\`
- Jedna instancja aplikacji naraz; okna dopasowują się do zmian konfiguracji ekranów
- Ciemny motyw interfejsu

## Wymagania

- Windows 10 / 11 (64-bit)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
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

Przy pierwszym uruchomieniu otwórz menu **⋯ → Ustawienia** (lub `Ctrl+,`) i uzupełnij hosty,
dane logowania oraz parę monitorów. Ustawienia trafiają do `appsettings.json` obok pliku
wykonywalnego — plik w repozytorium zawiera wyłącznie wartości przykładowe.

## Struktura

```
MultiRDP/
├── Controls/
│   └── RdpHostControl.cs       # WinForms UserControl z kontrolką ActiveX RDP
├── Interop/
│   └── NativeMethods.cs        # deklaracje Win32 (SetWindowPos, RegisterHotKey)
├── Models/
│   ├── AppSettings.cs
│   ├── HostConfig.cs
│   └── SessionState.cs
├── Sessions/
│   └── RdpSession.cs           # jedna sesja: stan, okno, ponowne łączenie
├── Services/
│   ├── HotkeyService.cs        # globalne skróty Ctrl+Alt+1..4
│   ├── Log.cs                  # log plikowy
│   ├── MonitorService.cs       # wykrywanie monitorów, pozycjonowanie okien
│   ├── SessionManager.cs       # zestaw sesji, kolejność Z, stosowanie ustawień
│   └── SettingsService.cs      # odczyt/zapis ustawień + szyfrowanie DPAPI
├── ViewModels/
│   ├── MainViewModel.cs        # pasek zakładek (MVVM)
│   ├── SettingsViewModel.cs    # okno ustawień
│   └── HostEditorViewModel.cs  # edycja jednego hosta
└── Views/
    ├── MainWindow.xaml         # pływający pasek zakładek
    ├── SessionWindow.xaml      # okno jednej sesji RDP (para monitorów)
    └── SettingsWindow.xaml
```

## Zależności

- [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm/)
- **MSTSCLib** (COM) — Microsoft Terminal Services Client ActiveX, `C:\Windows\System32\mstscax.dll`

## Uwagi techniczne

- **Hasła** są zawsze zapisywane zaszyfrowane DPAPI (zakres: bieżący użytkownik Windows).
  Jawne hasła z starszego pliku są szyfrowane przy pierwszym uruchomieniu. Plik ustawień
  przeniesiony na inne konto lub komputer nie odszyfruje haseł — trzeba je wpisać ponownie.
- **Skróty globalne a klawiatura w sesji.** Domyślnie kombinacje Windows (Alt+Tab, Win…) działają
  lokalnie — klient RDP nie przechwytuje wtedy klawiatury i `Ctrl+Alt+1..4` przełączają zakładki
  także z fokusem w sesji. Opcja *„Wysyłaj kombinacje Windows do sesji zdalnej”* przywraca
  przechwytywanie (`KeyboardHookMode = 2`), ale wtedy skróty globalne nie działają z fokusem w sesji.
- **Numeracja monitorów.** `selectedmonitors` przyjmuje identyfikatory monitorów klienta RDP
  (te z `mstsc /l`), a aplikacja przekazuje indeksy z `Screen.AllScreens`. Na stanowisku
  produkcyjnym są zgodne; na innym sprzęcie warto to sprawdzić przyciskiem „Identyfikuj”.
- Pozycjonowanie okien odbywa się w fizycznych pikselach (Win32), więc uwzględnia skalowanie DPI.
- Uszkodzony `appsettings.json` nie jest nadpisywany — zostaje skopiowany jako
  `appsettings.json.bad-<data>`, a aplikacja startuje z ustawieniami domyślnymi.

## Licencja

MIT — patrz [LICENSE](LICENSE).
