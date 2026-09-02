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
    /// Hasło (opcjonalnie szyfrowane DPAPI) / Password (optionally DPAPI-encrypted)
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
}
