using System.Text.RegularExpressions;

namespace ScannerDisabler;

internal sealed class AppSettings
{
    public string? SelectedInterfaceId { get; set; }
}

internal static class SettingsStore
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PenguinAntiScan");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.txt");
    private static readonly string[] LegacyPaths =
    {
        Path.Combine(SettingsDirectory, "settings.json"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControlWifi", "settings.json")
    };

    public static AppSettings Load()
    {
        try
        {
            var path = File.Exists(SettingsPath) ? SettingsPath : LegacyPaths.FirstOrDefault(File.Exists);
            if (path is null) return new AppSettings();

            var content = File.ReadAllText(path).Trim();
            var match = Regex.Match(content, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            var settings = new AppSettings { SelectedInterfaceId = match.Success ? match.Value : null };
            if (path != SettingsPath) Save(settings);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsPath, settings.SelectedInterfaceId ?? string.Empty);
        }
        catch { }
    }
}
