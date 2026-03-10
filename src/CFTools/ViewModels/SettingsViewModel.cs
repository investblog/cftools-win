using CommunityToolkit.Mvvm.ComponentModel;

namespace CFTools.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    public partial double MaxConcurrency { get; set; }

    [ObservableProperty]
    public partial double MaxRetries { get; set; }

    [ObservableProperty]
    public partial int SelectedThemeIndex { get; set; }

    public string Version { get; } =
        $"v{typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.1.0"}";

    public SettingsViewModel()
    {
        var settings = App.Settings;
        MaxConcurrency = settings.MaxConcurrency;
        MaxRetries = settings.MaxRetries;
        SelectedThemeIndex = settings.ThemeIndex;
    }

    partial void OnMaxConcurrencyChanged(double value)
    {
        var intValue = Math.Clamp((int)value, 1, 8);
        App.Pool.UpdateConcurrency(intValue);
        SaveSettings();
    }

    partial void OnMaxRetriesChanged(double value)
    {
        var intValue = Math.Clamp((int)value, 0, 5);
        App.Pool.UpdateRetries(intValue);
        SaveSettings();
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        SaveSettings();
        App.ApplyTheme(value);
    }

    private void SaveSettings()
    {
        var settings = App.Settings;
        settings.MaxConcurrency = (int)MaxConcurrency;
        settings.MaxRetries = (int)MaxRetries;
        settings.ThemeIndex = SelectedThemeIndex;
        settings.Save();
    }
}
