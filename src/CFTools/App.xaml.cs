using CFTools.Services;
using Microsoft.UI.Xaml;

namespace CFTools;

public partial class App : Application
{
    private Window? _window;

    // Shared services — simple singleton access for MVP
    public static AppSettings Settings { get; } = AppSettings.Load();
    public static CloudflareApi Api { get; } = new();
    public static CredentialStore Credentials { get; } = new();
    public static RequestPool Pool { get; } =
        new(maxConcurrency: Settings.MaxConcurrency, maxRetries: Settings.MaxRetries);

    /// <summary>
    /// Fired when auth state changes so all pages can react.
    /// </summary>
    public static event Action? AuthStateChanged;

    public static string? CurrentAccountId { get; set; }
    public static string? CurrentAccountName { get; set; }
    public static string? CurrentEmail { get; set; }

    public static void ClearAuthSession(bool clearStoredCredentials = false)
    {
        Api.ClearCredentials();

        if (clearStoredCredentials)
        {
            Credentials.Delete();
        }

        CurrentAccountId = null;
        CurrentAccountName = null;
        CurrentEmail = null;
        NotifyAuthChanged();
    }

    public static void NotifyAuthChanged()
    {
        AuthStateChanged?.Invoke();
    }

    public static void ApplyTheme(int themeIndex)
    {
        if (Current is App app && app._window?.Content is FrameworkElement root)
        {
            root.RequestedTheme = themeIndex switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
        }
    }

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
        ApplyTheme(Settings.ThemeIndex);
    }
}
