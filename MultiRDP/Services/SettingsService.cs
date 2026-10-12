using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MultiRDP.Models;

namespace MultiRDP.Services;

/// <summary>
/// Serwis do zapisu i odczytu ustawień aplikacji z pliku JSON (obok pliku wykonywalnego).
/// Hasła są zawsze zapisywane zaszyfrowane DPAPI (zakres: bieżący użytkownik Windows).
///
/// Service for saving and loading application settings from a JSON file (next to the executable).
/// Passwords are always saved DPAPI-encrypted (scope: current Windows user).
/// </summary>
public class SettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(
        AppContext.BaseDirectory, "appsettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Wczytuje ustawienia. Jawne hasła są od razu szyfrowane i zapisywane (migracja).
    /// Uszkodzony plik jest archiwizowany, a <paramref name="warning"/> zawiera komunikat dla operatora.
    ///
    /// Loads settings. Plain-text passwords are encrypted and saved right away (migration).
    /// A corrupted file is archived and <paramref name="warning"/> holds a message for the operator.
    /// </summary>
    public AppSettings Load(out string? warning)
    {
        warning = null;
        AppSettings settings;

        if (!File.Exists(SettingsFilePath))
        {
            settings = new AppSettings();
        }
        else
        {
            try
            {
                string json = File.ReadAllText(SettingsFilePath, Encoding.UTF8);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                // Nie nadpisuj po cichu konfiguracji operatora — zachowaj kopię
                // Don't silently overwrite the operator's configuration — keep a copy
                string backup = $"{SettingsFilePath}.bad-{DateTime.Now:yyyyMMdd-HHmmss}";
                try { File.Copy(SettingsFilePath, backup, overwrite: true); } catch { }
                Log.Error($"Nie można wczytać {SettingsFilePath}, kopia: {backup}", ex);
                warning = $"Nie udało się wczytać pliku ustawień — użyto ustawień domyślnych.\n\n" +
                          $"Kopia uszkodzonego pliku: {backup}\n\n{ex.Message}";
                return Normalized(new AppSettings());
            }
        }

        settings = Normalized(settings);

        if (settings.Hosts.Any(h => !h.IsPasswordEncrypted && h.Password.Length > 0))
        {
            Log.Info("Migracja: szyfrowanie jawnych haseł w appsettings.json");
            if (!TrySave(settings, out string? error))
                warning = $"Nie udało się zaszyfrować haseł w pliku ustawień:\n{error}";
        }

        return settings;
    }

    /// <summary>
    /// Zapisuje ustawienia (hasła szyfrowane DPAPI, zapis atomowy).
    /// Saves settings (DPAPI-encrypted passwords, atomic write).
    /// </summary>
    public bool TrySave(AppSettings settings, out string? error)
    {
        error = null;
        try
        {
            EncryptPasswords(settings);
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            string tmp  = SettingsFilePath + ".tmp";
            File.WriteAllText(tmp, json, Encoding.UTF8);
            File.Move(tmp, SettingsFilePath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Zapis {SettingsFilePath} nie powiódł się", ex);
            error = ex.Message;
            return false;
        }
    }

    private static AppSettings Normalized(AppSettings settings)
    {
        settings.Normalize();
        return settings;
    }

    private static void EncryptPasswords(AppSettings settings)
    {
        foreach (var host in settings.Hosts.Where(h => !h.IsPasswordEncrypted))
        {
            host.Password            = EncryptPassword(host.Password);
            host.IsPasswordEncrypted = true;
        }
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
        catch (Exception ex)
        {
            // Np. plik skopiowany z innego konta / maszyny / E.g. file copied from another account / machine
            Log.Warn($"Nie można odszyfrować hasła (inne konto Windows?): {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Zwraca odszyfrowane hasło dla danej konfiguracji hosta
    /// Returns decrypted password for the given host configuration
    /// </summary>
    public static string GetPassword(HostConfig host)
        => host.IsPasswordEncrypted ? DecryptPassword(host.Password) : host.Password;
}
