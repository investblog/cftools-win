using CFTools.Services;
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

    [ObservableProperty]
    public partial int SelectedLanguageIndex { get; set; }

    [ObservableProperty]
    public partial bool ShowRestartNote { get; set; }

    /// <summary>Index 0 = follow Windows; then Loc.Languages in order.</summary>
    public IReadOnlyList<string> LanguageNames { get; }

    public SettingsViewModel()
    {
        _isInitializing = true;

        var settings = App.Settings;
        settings.Normalize();

        MaxConcurrency = settings.MaxConcurrency;
        MaxRetries = settings.MaxRetries;
        SelectedThemeIndex = settings.ThemeIndex;
        Show301Tips = settings.Show301Tips;

        LanguageNames = new[] { Loc.Get("Settings_LanguageSystem") }
            .Concat(Loc.Languages.Select(l => l.Name))
            .ToList();
        var langIndex = Array.FindIndex(
            Loc.Languages,
            l => string.Equals(l.Tag, settings.Language, StringComparison.OrdinalIgnoreCase)
        );
        SelectedLanguageIndex = langIndex < 0 ? 0 : langIndex + 1;

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

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        if (_isInitializing)
            return;

        var tag =
            value <= 0
                ? string.Empty
                : Loc.Languages[Math.Min(value, Loc.Languages.Length) - 1].Tag;
        if (App.Settings.Language == tag)
            return;

        App.Settings.Language = tag;
        App.Settings.Save();
        ShowRestartNote = true;
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
