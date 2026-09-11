using Microsoft.UI.Xaml.Controls;

namespace CFTools.Views.Controls;

/// <summary>
/// WinUI resolves ThemeResource values inside VisualState setters when the state is entered
/// and does not refresh them on a theme change, so an InfoBar keeps the old theme's severity
/// background. Re-entering the severity state re-resolves the brushes.
/// </summary>
public static class InfoBarThemeFix
{
    public static void Reapply(InfoBar bar)
    {
        var severity = bar.Severity;
        bar.Severity =
            severity == InfoBarSeverity.Informational
                ? InfoBarSeverity.Warning
                : InfoBarSeverity.Informational;
        bar.Severity = severity;
    }
}
