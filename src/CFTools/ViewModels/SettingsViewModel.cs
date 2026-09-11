using CommunityToolkit.Mvvm.ComponentModel;

namespace CFTools.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private bool _isInitializing;

    [ObservableProperty]
    public partial double MaxConcurrency { get; set; }

    [ObservableProperty]
    public partial double MaxRetries { get; set; }

    [ObservableProperty]
    public partial int SelectedThemeIndex { get; set; }

    [ObservableProperty]
    public partial bool Show301Tips { get; set; }

    public SettingsViewModel()
    {
        _isInitializing = true;

        var settings = App.Settings;
        settings.Normalize();

        MaxConcurrency = settings.MaxConcurrency;
        MaxRetries = settings.MaxRetries;
        SelectedThemeIndex = settings.ThemeIndex;
        Show301Tips = settings.Show301Tips;

        _isInitializing = false;
    }

    partial void OnMaxConcurrencyChanged(double value)
    {
        var intValue = Math.Clamp((int)Math.Round(value), 1, 8);
        if (Math.Abs(MaxConcurrency - intValue) > double.Epsilon)
        {
            MaxConcurrency = intValue;
            return;
        }

        App.Pool.UpdateConcurrency(intValue);
        if (!_isInitializing)
            SaveSettings();
    }

    partial void OnMaxRetriesChanged(double value)
    {
        var intValue = Math.Clamp((int)Math.Round(value), 0, 5);
        if (Math.Abs(MaxRetries - intValue) > double.Epsilon)
        {
            MaxRetries = intValue;
            return;
        }

        App.Pool.UpdateRetries(intValue);
        if (!_isInitializing)
            SaveSettings();
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        var normalized = Math.Clamp(value, 0, 2);
        if (SelectedThemeIndex != normalized)
        {
            SelectedThemeIndex = normalized;
            return;
        }

        if (!_isInitializing)
            SaveSettings();
        App.ApplyTheme(normalized);
    }

    partial void OnShow301TipsChanged(bool value)
    {
        if (_isInitializing)
            return;

        var settings = App.Settings;
        settings.Show301Tips = value;
        if (value)
            settings.AfterCreateTipDismissed = false; // re-enabling brings the tips back
        settings.Save();
        App.NotifyTipsSettingChanged();
    }

    private void SaveSettings()
    {
        var settings = App.Settings;
        settings.MaxConcurrency = Math.Clamp((int)Math.Round(MaxConcurrency), 1, 8);
        settings.MaxRetries = Math.Clamp((int)Math.Round(MaxRetries), 0, 5);
        settings.ThemeIndex = Math.Clamp(SelectedThemeIndex, 0, 2);
        settings.Save();
    }
}
