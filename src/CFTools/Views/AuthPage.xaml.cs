using CFTools.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CFTools.Views;

public sealed partial class AuthPage : Page
{
    public AuthViewModel ViewModel { get; } = new();

    public AuthPage()
    {
        this.InitializeComponent();

        // Restore saved API key to PasswordBox if available
        if (!string.IsNullOrEmpty(ViewModel.ApiKey))
            ApiKeyBox.Password = ViewModel.ApiKey;
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.ApiKey = ApiKeyBox.Password;
    }
}
