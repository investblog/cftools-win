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
        byte alpha = IsBackground ? (byte)38 : (byte)255;
        var color = value is Models.PreflightStatus status
            ? status switch
            {
                Models.PreflightStatus.WillCreate or Models.PreflightStatus.Created =>
                    Windows.UI.Color.FromArgb(alpha, 16, 124, 16),
                Models.PreflightStatus.Creating => Windows.UI.Color.FromArgb(alpha, 0, 120, 212),
                Models.PreflightStatus.Exists => Windows.UI.Color.FromArgb(alpha, 157, 93, 0),
                Models.PreflightStatus.Failed or Models.PreflightStatus.Invalid =>
                    Windows.UI.Color.FromArgb(alpha, 196, 43, 28),
                _ => Windows.UI.Color.FromArgb(alpha, 128, 128, 128),
            }
            : Windows.UI.Color.FromArgb(alpha, 128, 128, 128);
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}

public class StatusToBadgeBrushConverter : IValueConverter
{
    public bool IsBackground { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var status = (value as string ?? "").ToLowerInvariant();
        byte alpha = IsBackground ? (byte)38 : (byte)255;
        var color = status switch
        {
            "active" => Windows.UI.Color.FromArgb(alpha, 16, 124, 16),
            "moved" => Windows.UI.Color.FromArgb(alpha, 157, 93, 0),
            "pending" or "initializing" => Windows.UI.Color.FromArgb(alpha, 0, 120, 212),
            _ => Windows.UI.Color.FromArgb(alpha, 128, 128, 128),
        };
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
