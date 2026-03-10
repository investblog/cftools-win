using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CFTools.Views;

namespace CFTools;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        // Set window size
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(900, 650));

        // Navigate to AddDomains page by default
        ContentFrame.Navigate(typeof(AddDomainsPage));
        NavView.SelectedItem = NavView.MenuItems[0];

        // Listen for auth changes
        App.AuthStateChanged += OnAuthStateChanged;
    }

    private void OnAuthStateChanged(bool isConnected)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AuthNavItem.Content = isConnected
                ? $"Connected: {App.CurrentEmail}"
                : "Not connected";
        });
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item)
            return;

        var tag = item.Tag as string;
        switch (tag)
        {
            case "AddDomains":
                ContentFrame.Navigate(typeof(AddDomainsPage));
                break;
            case "PurgeCache":
                ContentFrame.Navigate(typeof(PurgeCachePage));
                break;
            case "Auth":
                ContentFrame.Navigate(typeof(AuthPage));
                break;
        }
    }
}
