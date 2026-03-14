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
        appWindow.Changed += AppWindow_Changed;

        ContentFrame.Navigate(typeof(AddDomainsPage));
        NavView.SelectedItem = NavView.MenuItems[0];

        App.AuthStateChanged += OnAuthStateChanged;
    }

    private void OnAuthStateChanged(bool isConnected)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (isConnected)
            {
                AuthIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green);
                AuthNavItem.Content = App.CurrentEmail;
                ToolTipService.SetToolTip(AuthNavItem, $"Connected: {App.CurrentEmail}");
            }
            else
            {
                AuthIcon.ClearValue(IconElement.ForegroundProperty);
                AuthNavItem.Content = "Not connected";
                ToolTipService.SetToolTip(AuthNavItem, "Not connected");
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