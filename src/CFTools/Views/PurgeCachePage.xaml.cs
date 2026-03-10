using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CFTools.ViewModels;

namespace CFTools.Views;

public sealed partial class PurgeCachePage : Page
{
    public PurgeCacheViewModel ViewModel { get; } = new();

    public PurgeCachePage()
    {
        this.InitializeComponent();
    }

    private void CheckBox_Changed(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => ViewModel.UpdateCanPurge());
    }
}
