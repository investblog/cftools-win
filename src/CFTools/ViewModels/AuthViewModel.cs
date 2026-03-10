using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CFTools.Models;
using Microsoft.UI.Xaml.Controls;

namespace CFTools.ViewModels;

public partial class AuthViewModel : ObservableObject
{
    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusOpen;

    [ObservableProperty]
    private InfoBarSeverity _infoBarSeverity;

    public AuthViewModel()
    {
        // Try to restore saved credentials
        var saved = App.Credentials.Load();
        if (saved is not null)
        {
            Email = saved.Value.Email;
            ApiKey = saved.Value.ApiKey;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(ApiKey))
        {
            ShowStatus("Enter both email and API key", InfoBarSeverity.Warning);
            return;
        }

        IsBusy = true;
        IsStatusOpen = false;

        try
        {
            App.Api.SetCredentials(Email.Trim(), ApiKey.Trim());

            var user = await App.Api.VerifyCredentials();

            // Get accounts to find the default account ID
            var accounts = await App.Api.GetAccounts();
            if (accounts.Count == 0)
            {
                ShowStatus("No accounts found for this user", InfoBarSeverity.Error);
                App.Api.ClearCredentials();
                return;
            }

            App.CurrentAccountId = accounts[0].Id;
            App.CurrentEmail = user.Email;

            // Save credentials
            App.Credentials.Save(Email.Trim(), ApiKey.Trim());

            IsConnected = true;
            ShowStatus($"Connected as {user.Email} ({accounts[0].Name})", InfoBarSeverity.Success);

            App.NotifyAuthChanged(true);
        }
        catch (CfApiException ex)
        {
            App.Api.ClearCredentials();
            ShowStatus($"Auth failed: {ex.Normalized.Message} — {ex.Normalized.Recommendation}", InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            App.Api.ClearCredentials();
            ShowStatus($"Connection error: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        App.Api.ClearCredentials();
        App.Credentials.Delete();
        App.CurrentAccountId = null;
        App.CurrentEmail = null;

        IsConnected = false;
        ApiKey = string.Empty;
        IsStatusOpen = false;

        App.NotifyAuthChanged(false);
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusMessage = message;
        InfoBarSeverity = severity;
        IsStatusOpen = true;
    }
}
