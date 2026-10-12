using System.Text.Json.Serialization;

namespace MultiRDP.Models;

/// <summary>
/// Konfiguracja jednego hosta RDP / RDP host configuration
/// </summary>
public class HostConfig
{
    /// <summary>Nazwa wyświetlana w zakładce / Display name shown in tab</summary>
    public string Name { get; set; } = "Host";

    /// <summary>Adres IP lub nazwa hosta / IP address or hostname</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Nazwa użytkownika / Username</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Hasło — po zapisie zawsze szyfrowane DPAPI / Password — always DPAPI-encrypted once saved
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Domena (opcjonalnie) / Domain (optional)</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Port RDP (domyślnie 3389) / RDP port (default 3389)</summary>
    public int Port { get; set; } = 3389;

    /// <summary>
    /// Czy hasło jest zaszyfrowane przez DPAPI / Whether password is DPAPI-encrypted
    /// </summary>
    public bool IsPasswordEncrypted { get; set; } = false;

    /// <summary>
    /// Tożsamość w czasie działania (niezapisywana) — pozwala dopasować sesję do hosta
    /// po zmianie kolejności lub usunięciu innych hostów w ustawieniach.
    ///
    /// Runtime identity (not persisted) — matches a session to its host after hosts
    /// are reordered or others removed in settings.
    /// </summary>
    [JsonIgnore]
    public Guid Id { get; init; } = Guid.NewGuid();

    public HostConfig Clone() => (HostConfig)MemberwiseClone();

    /// <summary>
    /// Czy parametry połączenia są takie same (zmiana samej nazwy nie wymaga ponownego łączenia).
    /// Whether connection parameters are equal (a name-only change needs no reconnect).
    /// Hasła porównywane po odszyfrowaniu / Passwords compared after decryption.
    /// </summary>
    public bool ConnectionEquals(HostConfig other) =>
        string.Equals(Address,  other.Address,  StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Username, other.Username, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Domain,   other.Domain,   StringComparison.OrdinalIgnoreCase) &&
        Port == other.Port &&
        Services.SettingsService.GetPassword(this) == Services.SettingsService.GetPassword(other);
}
