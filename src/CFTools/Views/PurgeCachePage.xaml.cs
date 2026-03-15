using CFTools.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CFTools.Views;

public sealed partial class PurgeCachePage : Page
{
    public PurgeCacheViewModel ViewModel { get; } = new();

    public PurgeCachePage()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (ViewModel.IsBusy || ViewModel.Zones.Count > 0 || App.CurrentAccountId is null)
        {
            return;
        }

        await ViewModel.LoadZonesCommand.ExecuteAsync(null);
    }

    private void GoToAuth_Click(object sender, RoutedEventArgs e) =>
        App.RequestNavigateToAuth();

    private void CheckBox_Changed(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => ViewModel.UpdateCanPurge());
    }

    private async void PurgeButton_Click(object sender, RoutedEventArgs e)
    {
        var count = ViewModel.VisibleZones.Count(z => z.IsSelected);
        if (count == 0)
        {
            return;
        }

        var accountName = App.CurrentAccountName ?? "the selected account";
        var warning =
            count > 50
                ? "\n\nWarning: this is a large purge request and may affect many sites at once."
                : string.Empty;

        var dialog = new ContentDialog
        {
            Title = "Purge cache",
            Content =
                $"Purge all cached files for {count} zone(s) in {accountName}? Cache will rebuild automatically.{warning}",
            PrimaryButtonText = "Purge",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
            RequestedTheme = this.ActualTheme,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.PurgeSelectedCommand.ExecuteAsync(null);
        }
    }
}
