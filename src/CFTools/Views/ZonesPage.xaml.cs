using CFTools.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CFTools.Views;

public sealed partial class ZonesPage : Page
{
    public ZonesViewModel ViewModel { get; } = new();

    public ZonesPage()
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
}
