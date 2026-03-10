using Microsoft.UI.Xaml.Controls;
using CFTools.ViewModels;

namespace CFTools.Views;

public sealed partial class AddDomainsPage : Page
{
    public AddDomainsViewModel ViewModel { get; } = new();

    public AddDomainsPage()
    {
        this.InitializeComponent();
    }
}
