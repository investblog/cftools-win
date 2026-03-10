using CFTools.Services;
using Microsoft.UI.Xaml;

namespace CFTools;

public partial class App : Application
{
    private Window? _window;

    // Shared services — simple singleton access for MVP
    public static CloudflareApi Api { get; } = new();
    public static CredentialStore Credentials { get; } = new();
    public static RequestPool Pool { get; } = new();

    /// <summary>
    /// Fired when auth state changes so all pages can react.
    /// </summary>
    public static event Action<bool>? AuthStateChanged;

    public static string? CurrentAccountId { get; set; }
    public static string? CurrentEmail { get; set; }

    public static void NotifyAuthChanged(bool isConnected)
    {
        AuthStateChanged?.Invoke(isConnected);
    }

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
