using CFTools.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CFTools;

public sealed partial class MainWindow : Window
{
    private const int MinWidth = 500;
    private const int MinHeight = 400;

    public MainWindow()
    {
        this.InitializeComponent();

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(900, 650));
        appWindow.SetIcon("Assets/app.ico");
        appWindow.Changed += AppWindow_Changed;

        ContentFrame.Navigate(typeof(AuthPage));
        NavView.SelectedItem = AuthNavItem;

        App.AuthStateChanged += OnAuthStateChanged;
        App.NavigateToAuthRequested += () =>
            DispatcherQueue.TryEnqueue(() =>
            {
                ContentFrame.Navigate(typeof(AuthPage));
                NavView.SelectedItem = AuthNavItem;
            });
        OnAuthStateChanged();
    }

    private void OnAuthStateChanged()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (App.CurrentEmail is null)
            {
                AuthIcon.ClearValue(IconElement.ForegroundProperty);
                AuthNavItem.Content = "Not connected";
                ToolTipService.SetToolTip(AuthNavItem, "Not connected");
                return;
            }

            if (App.CurrentAccountName is not null)
            {
                AuthIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green);
                var label = App.CurrentEmail ?? App.CurrentAccountName;
                AuthNavItem.Content = label.Length > 20 ? label[..17] + "..." : label;
                ToolTipService.SetToolTip(
                    AuthNavItem,
                    $"{App.CurrentEmail}\n{App.CurrentAccountName}"
                );
            }
            else
            {
                AuthIcon.ClearValue(IconElement.ForegroundProperty);
                AuthNavItem.Content = "Select account";
                ToolTipService.SetToolTip(
                    AuthNavItem,
                    $"Authenticated as {App.CurrentEmail}. Select an account to continue."
                );
            }
        });
    }

    private static void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange)
        {
            return;
        }

        var size = sender.Size;
        if (size.Width < MinWidth || size.Height < MinHeight)
        {
            sender.Resize(
                new Windows.Graphics.SizeInt32(
                    Math.Max(size.Width, MinWidth),
                    Math.Max(size.Height, MinHeight)
                )
            );
        }
    }

    private void NavView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args
    )
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        var tag = item.Tag as string;
        switch (tag)
        {
            case "AddDomains":
                ContentFrame.Navigate(typeof(AddDomainsPage));
                break;
            case "PurgeCache":
                ContentFrame.Navigate(typeof(PurgeCachePage));
                break;
            case "DeleteDomains":
                ContentFrame.Navigate(typeof(DeleteDomainsPage));
                break;
            case "Auth":
                ContentFrame.Navigate(typeof(AuthPage));
                break;
        }
    }
}
