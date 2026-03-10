using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CFTools.ViewModels;

namespace CFTools.Views;

public sealed partial class DeleteDomainsPage : Page
{
    public DeleteDomainsViewModel ViewModel { get; } = new();

    public DeleteDomainsPage()
    {
        this.InitializeComponent();
    }

    private void CheckBox_Changed(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => ViewModel.UpdateCanDelete());
    }
}
