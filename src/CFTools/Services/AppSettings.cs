using System.Text.Json;

namespace CFTools.Services;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CFTools"
    );
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    public int MaxConcurrency { get; set; } = 4;
    public int MaxRetries { get; set; } = 3;
    public int ThemeIndex { get; set; } // 0=System, 1=Light, 2=Dark

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return new AppSettings();

            var json = File.ReadAllText(SettingsFile);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Normalize();
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Silently fail - non-critical
        }
    }

    public void Normalize()
    {
        MaxConcurrency = Math.Clamp(MaxConcurrency, 1, 8);
        MaxRetries = Math.Clamp(MaxRetries, 0, 5);
        ThemeIndex = Math.Clamp(ThemeIndex, 0, 2);
    }
}
