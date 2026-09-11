using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace CFTools.Services;

/// <summary>
/// Localized strings for code (XAML uses x:Uid). Keys live in i18n/en.txt and are
/// compiled from Strings/&lt;culture&gt;/Resources.resw; a missing key returns the key
/// itself so a typo is visible instead of silent.
/// </summary>
public static class Loc
{
    private static readonly ResourceLoader Loader = new();

    public static string Get(string key)
    {
        try
        {
            var value = Loader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch (Exception)
        {
            return key;
        }
    }

    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>
    /// Languages the app ships (same set as the Store listings). Tag = BCP-47 for
    /// PrimaryLanguageOverride; Name is the native name (never translated).
    /// </summary>
    public static readonly (string Tag, string Name)[] Languages =
    {
        ("en-US", "English"),
        ("ru", "Русский"),
        ("de", "Deutsch"),
        ("fr", "Français"),
        ("es", "Español"),
        ("it", "Italiano"),
        ("pt-BR", "Português (Brasil)"),
        ("ja", "日本語"),
        ("ko", "한국어"),
        ("zh-Hans", "简体中文"),
        ("pl", "Polski"),
        ("tr", "Türkçe"),
    };
}
