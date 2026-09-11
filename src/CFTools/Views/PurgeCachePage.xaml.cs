using CFTools.Services;
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

    private void GoToAuth_Click(object sender, RoutedEventArgs e) => App.RequestNavigateToAuth();

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

        var accountName = App.CurrentAccountName ?? Loc.Get("Status_SelectedAccountFallback");
        var warning = count > 50 ? Loc.Get("Purge_DialogWarning") : string.Empty;

        var dialog = new ContentDialog
        {
            Title = Loc.Get("Purge_DialogTitle"),
            Content = Loc.Format("Purge_DialogBody", count, accountName, warning),
            PrimaryButtonText = Loc.Get("Purge_DialogPrimary"),
            CloseButtonText = Loc.Get("Dialog_Cancel"),
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
