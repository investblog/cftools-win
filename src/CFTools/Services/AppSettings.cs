using System.Text.Json;

namespace CFTools.Services;

public sealed class AppSettings
{
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
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
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
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(
                this,
                new JsonSerializerOptions { WriteIndented = true }
            );
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Silently fail — non-critical
        }
    }
}
