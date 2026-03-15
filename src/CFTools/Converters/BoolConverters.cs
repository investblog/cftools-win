using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace CFTools.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Visible;
}

public class InvertBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Collapsed;
}

public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? false : true;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is true ? false : true;
}

public class PreflightStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is Models.PreflightStatus status
            ? status switch
            {
                Models.PreflightStatus.WillCreate => "Ready",
                Models.PreflightStatus.Exists => "Exists",
                Models.PreflightStatus.Invalid => "Invalid",
                Models.PreflightStatus.Duplicate => "Duplicate",
                Models.PreflightStatus.Creating => "Creating\u2026",
                Models.PreflightStatus.Created => "Created",
                Models.PreflightStatus.Failed => "Failed",
                Models.PreflightStatus.Cancelled => "Cancelled",
                _ => status.ToString(),
            }
            : "";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}

public class PreflightStatusBrushConverter : IValueConverter
{
    public bool IsBackground { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var dark = ThemeHelper.IsDarkTheme();
        var (fg, bg) = value is Models.PreflightStatus status
            ? status switch
            {
                Models.PreflightStatus.WillCreate or Models.PreflightStatus.Created =>
                    BadgeColors.Success(dark),
                Models.PreflightStatus.Creating => BadgeColors.Info(dark),
                Models.PreflightStatus.Exists => BadgeColors.Warning(dark),
                Models.PreflightStatus.Failed or Models.PreflightStatus.Invalid =>
                    BadgeColors.Danger(dark),
                _ => BadgeColors.Neutral(dark),
            }
            : BadgeColors.Neutral(dark);
        return new SolidColorBrush(IsBackground ? bg : fg);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}

public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? 1.0 : 0.5;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}

public class PurgeTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var status = value as string ?? "";
        return status == "active" ? (object)null! : $"Zone is {status} — only active zones can be purged";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}

public class PunycodeTooltipConverter : IValueConverter
{
    private static readonly System.Globalization.IdnMapping Idn = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string domain || !domain.Contains("xn--"))
            return (object)null!;

        try
        {
            var unicode = Idn.GetUnicode(domain);
            return unicode != domain ? unicode : (object)null!;
        }
        catch
        {
            return (object)null!;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}

public class WillCreateToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is Models.PreflightStatus.WillCreate ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}

public class StatusToBadgeBrushConverter : IValueConverter
{
    public bool IsBackground { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var status = (value as string ?? "").ToLowerInvariant();
        var dark = ThemeHelper.IsDarkTheme();
        var (fg, bg) = status switch
        {
            "active" => BadgeColors.Success(dark),
            "moved" => BadgeColors.Warning(dark),
            "pending" or "initializing" => BadgeColors.Info(dark),
            _ => BadgeColors.Neutral(dark),
        };
        return new SolidColorBrush(IsBackground ? bg : fg);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}

/// <summary>
/// Badge color pairs from 301-ui design system. WCAG AA compliant on both themes.
/// </summary>
internal static class BadgeColors
{
    // Dark: #18C27A on rgba(37,208,164,0.12) — Light: #0E7B4C on rgba(14,123,76,0.16)
    public static (Windows.UI.Color Fg, Windows.UI.Color Bg) Success(bool dark) =>
        dark
            ? (C(0x18, 0xC2, 0x7A), C(30, 0x25, 0xD0, 0xA4))
            : (C(0x0E, 0x7B, 0x4C), C(40, 0x0E, 0x7B, 0x4C));

    // Dark: #FF4F6E on rgba(255,107,107,0.12) — Light: #D9264A on rgba(217,38,74,0.16)
    public static (Windows.UI.Color Fg, Windows.UI.Color Bg) Danger(bool dark) =>
        dark
            ? (C(0xFF, 0x4F, 0x6E), C(30, 0xFF, 0x6B, 0x6B))
            : (C(0xD9, 0x26, 0x4A), C(40, 0xD9, 0x26, 0x4A));

    // Dark: #FFB347 on rgba(255,179,71,0.12) — Light: #9A6200 on rgba(154,98,0,0.16)
    public static (Windows.UI.Color Fg, Windows.UI.Color Bg) Warning(bool dark) =>
        dark
            ? (C(0xFF, 0xB3, 0x47), C(30, 0xFF, 0xB3, 0x47))
            : (C(0x9A, 0x62, 0x00), C(40, 0x9A, 0x62, 0x00));

    // Dark: #4DA3FF on rgba(77,163,255,0.10) — Light: #0055DC on rgba(0,85,220,0.12)
    public static (Windows.UI.Color Fg, Windows.UI.Color Bg) Info(bool dark) =>
        dark
            ? (C(0x4D, 0xA3, 0xFF), C(25, 0x4D, 0xA3, 0xFF))
            : (C(0x00, 0x55, 0xDC), C(30, 0x00, 0x55, 0xDC));

    // Dark: #A0A4AF — Light: #666A73
    public static (Windows.UI.Color Fg, Windows.UI.Color Bg) Neutral(bool dark) =>
        dark
            ? (C(0xA0, 0xA4, 0xAF), C(20, 0xA0, 0xA4, 0xAF))
            : (C(0x66, 0x6A, 0x73), C(30, 0x66, 0x6A, 0x73));

    private static Windows.UI.Color C(byte r, byte g, byte b) =>
        Windows.UI.Color.FromArgb(255, r, g, b);

    private static Windows.UI.Color C(byte a, byte r, byte g, byte b) =>
        Windows.UI.Color.FromArgb(a, r, g, b);
}

internal static class ThemeHelper
{
    public static bool IsDarkTheme()
    {
        if (App.MainRoot is { } root)
            return root.ActualTheme == ElementTheme.Dark;
        return Application.Current.RequestedTheme == ApplicationTheme.Dark;
    }
}
