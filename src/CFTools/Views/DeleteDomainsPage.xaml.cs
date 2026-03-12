using CFTools.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CFTools.Views;

public sealed partial class DeleteDomainsPage : Page
{
    public DeleteDomainsViewModel ViewModel { get; } = new();

    public DeleteDomainsPage()
    {
        this.InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadZonesCommand.ExecuteAsync(null);
    }

    private void CheckBox_Changed(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => ViewModel.UpdateCanDelete());
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var count = ViewModel.Zones.Count(z => z.IsSelected);
        if (count == 0)
            return;

        var dialog = new ContentDialog
        {
            Title = "Delete zones",
            Content =
                $"Permanently delete {count} zone(s) from Cloudflare?\nThis cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteSelectedCommand.ExecuteAsync(null);
        }
    }
}
