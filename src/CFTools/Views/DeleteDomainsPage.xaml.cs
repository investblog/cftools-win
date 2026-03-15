using CFTools.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace CFTools.Views;

public sealed partial class DeleteDomainsPage : Page
{
    public DeleteDomainsViewModel ViewModel { get; } = new();

    public DeleteDomainsPage()
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
        DispatcherQueue.TryEnqueue(() => ViewModel.UpdateCommandStates());
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var count = ViewModel.VisibleZones.Count(z => z.IsSelected);
        if (count == 0)
        {
            return;
        }

        var accountName = App.CurrentAccountName ?? "the selected account";
        var warning =
            count > 50
                ? "\n\nWarning: this is a large delete request and may remove many zones at once."
                : string.Empty;

        var dialog = new ContentDialog
        {
            Title = "Delete zones",
            Content =
                $"Permanently delete {count} zone(s) from {accountName}?\nThis cannot be undone.{warning}",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
            RequestedTheme = this.ActualTheme,
            Resources =
            {
                ["AccentButtonBackground"] = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C)
                ),
                ["AccentButtonBackgroundPointerOver"] = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(0xFF, 0xA1, 0x23, 0x16)
                ),
                ["AccentButtonBackgroundPressed"] = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(0xFF, 0x86, 0x1D, 0x12)
                ),
                ["AccentButtonForeground"] = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)
                ),
                ["AccentButtonForegroundPointerOver"] = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)
                ),
                ["AccentButtonForegroundPressed"] = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)
                ),
                ["AccentButtonBorderBrush"] = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C)
                ),
                ["AccentButtonBorderBrushPointerOver"] = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(0xFF, 0xA1, 0x23, 0x16)
                ),
                ["AccentButtonBorderBrushPressed"] = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(0xFF, 0x86, 0x1D, 0x12)
                ),
            },
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteSelectedCommand.ExecuteAsync(null);
        }
    }
}
