using CFTools.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace CFTools.Views;

public sealed partial class AddDomainsPage : Page
{
    public AddDomainsViewModel ViewModel { get; } = new();

    public AddDomainsPage()
    {
        this.InitializeComponent();
    }
}
