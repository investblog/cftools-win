using CFTools.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CFTools.Views;

public sealed partial class PurgeCachePage : Page
{
    public PurgeCacheViewModel ViewModel { get; } = new();

    public PurgeCachePage()
    {
        this.InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsBusy || ViewModel.Zones.Count > 0)
        {
            return;
        }

        await ViewModel.LoadZonesCommand.ExecuteAsync(null);
    }

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
                $"Purge cache for {count} zone(s) in {accountName}? This cannot be undone.{warning}",
            PrimaryButtonText = "Purge",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.PurgeSelectedCommand.ExecuteAsync(null);
        }
    }
}
