using CFTools.Models;
using CFTools.Services;
using Microsoft.UI.Xaml;

namespace CFTools;

public partial class App : Application
{
    private Window? _window;

    public static FrameworkElement? MainRoot =>
        (Current as App)?._window?.Content as FrameworkElement;

    // Shared services - simple singleton access for MVP
    public static AppSettings Settings { get; } = AppSettings.Load();
    public static CloudflareApi Api { get; } = new();
    public static CredentialStore Credentials { get; } = new();
    public static RequestPool Pool { get; } =
        new(maxConcurrency: Settings.MaxConcurrency, maxRetries: Settings.MaxRetries);

    /// <summary>
    /// Fired when auth state changes so all pages can react.
    /// </summary>
    public static event Action? AuthStateChanged;
    public static event Action? ThemeChanged;
    public static event Action? NavigateToAuthRequested;
    public static event Action? ZoneListChanged;
    public static event Action? TipsSettingChanged;

    public static string? CurrentAccountId { get; set; }
    public static string? CurrentAccountName { get; set; }

    /// <summary>Display label of the signed-in identity: e-mail for a Global API Key, "API token …" for tokens.</summary>
    public static string? CurrentEmail { get; set; }

    /// <summary>Accounts visible to the active credential (for cross-account export).</summary>
    public static IReadOnlyList<CfAccount> AvailableAccounts { get; set; } =
        Array.Empty<CfAccount>();

    /// <summary>HWND of the main window, needed to parent pickers and dialogs.</summary>
    public static IntPtr MainWindowHandle { get; private set; }

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
        AvailableAccounts = Array.Empty<CfAccount>();
        NotifyAuthChanged();
    }

    public static void NotifyAuthChanged()
    {
        AuthStateChanged?.Invoke();
    }

    public static void RequestNavigateToAuth()
    {
        NavigateToAuthRequested?.Invoke();
    }

    public static void NotifyZoneListChanged()
    {
        ZoneListChanged?.Invoke();
    }

    public static void NotifyTipsSettingChanged()
    {
        TipsSettingChanged?.Invoke();
    }

    public static void NotifyThemeChanged()
    {
        ThemeChanged?.Invoke();
    }

    public static ElementTheme ThemeFor(int themeIndex) =>
        themeIndex switch
        {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

    public static void ApplyTheme(int themeIndex)
    {
        if (Current is App app && app._window?.Content is FrameworkElement root)
        {
            root.RequestedTheme = ThemeFor(themeIndex);
        }
    }

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        MainWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        _window.Activate();
        AttachThemeObserver();
        ApplyTheme(Settings.ThemeIndex);
    }

    private void AttachThemeObserver()
    {
        if (_window?.Content is FrameworkElement root)
        {
            root.ActualThemeChanged += MainRoot_ActualThemeChanged;
        }
    }

    private static void MainRoot_ActualThemeChanged(FrameworkElement sender, object args)
    {
        NotifyThemeChanged();
    }
}
