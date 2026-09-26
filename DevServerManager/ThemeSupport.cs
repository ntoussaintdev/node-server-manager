using Microsoft.Win32;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Windows;
using Application = System.Windows.Application;

namespace DevServerManager;

public enum ThemeMode { Device, Light, Dark }

public sealed class UiSettingsStore
{
    public ThemeMode LoadTheme()
    {
        using var connection = PortableDatabase.OpenConnection();
        using var command = connection.CreateCommand(); command.CommandText = "SELECT value FROM settings WHERE key = 'theme'";
        var value = command.ExecuteScalar()?.ToString();
        return Enum.TryParse<ThemeMode>(value, ignoreCase: true, out var theme) ? theme : ThemeMode.Device;
    }
    public void SaveTheme(ThemeMode theme)
    {
        using var connection = PortableDatabase.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO settings (key, value) VALUES ('theme', $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value";
        command.Parameters.AddWithValue("$value", theme.ToString()); command.ExecuteNonQuery();
    }
    public Dictionary<string, bool> LoadServerGroupExpansion()
    {
        using var connection = PortableDatabase.OpenConnection();
        using var command = connection.CreateCommand(); command.CommandText = "SELECT value FROM settings WHERE key = 'server_group_expansion'";
        var value = command.ExecuteScalar()?.ToString();
        try { return value is null ? new(StringComparer.OrdinalIgnoreCase) : new(JsonSerializer.Deserialize<Dictionary<string, bool>>(value) ?? [], StringComparer.OrdinalIgnoreCase); }
        catch (JsonException) { return new(StringComparer.OrdinalIgnoreCase); }
    }
    public void SaveServerGroupExpansion(IReadOnlyDictionary<string, bool> expansion)
    {
        using var connection = PortableDatabase.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO settings (key, value) VALUES ('server_group_expansion', $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value";
        command.Parameters.AddWithValue("$value", JsonSerializer.Serialize(expansion)); command.ExecuteNonQuery();
    }
}

public static class ThemeManager
{
    public static void Apply(ThemeMode mode)
    {
        var dark = mode == ThemeMode.Dark || (mode == ThemeMode.Device && DeviceUsesDarkTheme());
        var resources = Application.Current.Resources.MergedDictionaries;
        var oldTheme = resources.FirstOrDefault(x => x.Source?.OriginalString.Contains("Themes/Light.xaml") == true || x.Source?.OriginalString.Contains("Themes/Dark.xaml") == true);
        if (oldTheme is not null) resources.Remove(oldTheme);
        resources.Insert(0, new ResourceDictionary { Source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative) });
    }

    private static bool DeviceUsesDarkTheme()
    {
        try { return (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) as int? ?? 1) == 0; }
        catch { return false; }
    }
}
