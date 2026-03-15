using Microsoft.UI.Xaml.Controls;

namespace CFTools.Views;

public sealed partial class AboutPage : Page
{
    public string Version { get; } =
        $"v{typeof(AboutPage).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";

    public AboutPage()
    {
        this.InitializeComponent();
    }
}
