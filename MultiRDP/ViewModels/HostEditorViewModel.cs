using CommunityToolkit.Mvvm.ComponentModel;
using MultiRDP.Models;
using MultiRDP.Services;

namespace MultiRDP.ViewModels;

/// <summary>
/// Edytowalna kopia jednego hosta w oknie ustawień. Hasło trzymane jawnie tylko na czas edycji.
/// Editable copy of one host in the settings window. Password held in plain text only while editing.
/// </summary>
public partial class HostEditorViewModel : ObservableObject
{
    /// <summary>Tożsamość hosta (dopasowanie do działającej sesji) / Host identity (matches the running session)</summary>
    public Guid Id { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Header))]
    private int _index;

    [ObservableProperty] private bool _canMoveUp;
    [ObservableProperty] private bool _canMoveDown;

    [ObservableProperty] private string _name     = string.Empty;
    [ObservableProperty] private string _address  = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _domain   = string.Empty;
    [ObservableProperty] private string _port     = "3389";

    /// <summary>Hasło jawne — ustawiane z PasswordBox w code-behind / Plain password — set from PasswordBox in code-behind</summary>
    public string Password { get; set; } = string.Empty;

    public string Header => $"▸ HOST {Index + 1}   ·   Ctrl+Alt+{Index + 1}";

    public HostEditorViewModel(int index, HostConfig? source = null)
    {
        Index = index;
        Id    = source?.Id ?? Guid.NewGuid();

        if (source is null)
        {
            Name = $"Host {index + 1}";
            return;
        }

        Name     = source.Name;
        Address  = source.Address;
        Username = source.Username;
        Domain   = source.Domain;
        Port     = source.Port.ToString();
        Password = SettingsService.GetPassword(source);
    }

    /// <summary>Komunikat walidacji lub null / Validation message or null</summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Address))
            return $"Podaj adres IP / hostname dla hosta {Index + 1}.";
        if (!int.TryParse(Port.Trim(), out int port) || port is < 1 or > 65535)
            return $"Port hosta {Index + 1} musi być liczbą 1–65535.";
        return null;
    }

    /// <summary>Hasło jawne — zaszyfruje je SettingsService przy zapisie / Plain password — SettingsService encrypts it on save</summary>
    public HostConfig ToHostConfig() => new()
    {
        Id                  = Id,
        Name                = Name.Trim().Length > 0 ? Name.Trim() : $"Host {Index + 1}",
        Address             = Address.Trim(),
        Username            = Username.Trim(),
        Domain              = Domain.Trim(),
        Port                = int.Parse(Port.Trim()),
        Password            = Password,
        IsPasswordEncrypted = false
    };
}
