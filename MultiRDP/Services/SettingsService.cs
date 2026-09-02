using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MultiRDP.Models;

namespace MultiRDP.Services;

/// <summary>
/// Serwis do zapisu i odczytu ustawień aplikacji z pliku JSON
/// Service for saving and loading application settings from JSON file
/// </summary>
public class SettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(
        AppContext.BaseDirectory, "appsettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>Wczytuje ustawienia z pliku JSON / Loads settings from JSON file</summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return new AppSettings();

            string json = File.ReadAllText(SettingsFilePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>Zapisuje ustawienia do pliku JSON / Saves settings to JSON file</summary>
    public void Save(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
    }

    /// <summary>
    /// Szyfruje hasło przy użyciu DPAPI (Windows Data Protection API)
    /// Encrypts password using DPAPI (Windows Data Protection API)
    /// </summary>
    public static string EncryptPassword(string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword)) return string.Empty;
        byte[] encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plainPassword),
            null,
            DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Deszyfruje hasło przy użyciu DPAPI / Decrypts password using DPAPI
    /// </summary>
    public static string DecryptPassword(string encryptedPassword)
    {
        if (string.IsNullOrEmpty(encryptedPassword)) return string.Empty;
        try
        {
            byte[] decrypted = ProtectedData.Unprotect(
                Convert.FromBase64String(encryptedPassword),
                null,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Zwraca odszyfrowane hasło dla danej konfiguracji hosta
    /// Returns decrypted password for the given host configuration
    /// </summary>
    public static string GetPassword(HostConfig host)
    {
        if (host.IsPasswordEncrypted)
            return DecryptPassword(host.Password);
        return host.Password;
    }
}
