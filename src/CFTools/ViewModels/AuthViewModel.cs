using System.Collections.ObjectModel;
using CFTools.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;

namespace CFTools.ViewModels;

public partial class AuthViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditCredentials))]
    public partial string ApiKey { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnectAction))]
    [NotifyPropertyChangedFor(nameof(ShowDisconnectAction))]
    [NotifyPropertyChangedFor(nameof(ShowForgetAction))]
    [NotifyPropertyChangedFor(nameof(ShowSwitchAccountAction))]
    [NotifyPropertyChangedFor(nameof(CanEditCredentials))]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnectAction))]
    [NotifyPropertyChangedFor(nameof(ShowDisconnectAction))]
    [NotifyPropertyChangedFor(nameof(ShowForgetAction))]
    [NotifyPropertyChangedFor(nameof(ShowSwitchAccountAction))]
    [NotifyPropertyChangedFor(nameof(CanEditCredentials))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsStatusOpen { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity InfoBarSeverity { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnectAction))]
    [NotifyPropertyChangedFor(nameof(ShowDisconnectAction))]
    [NotifyPropertyChangedFor(nameof(ShowForgetAction))]
    [NotifyPropertyChangedFor(nameof(ShowSwitchAccountAction))]
    [NotifyPropertyChangedFor(nameof(CanEditCredentials))]
    public partial bool ShowAccountPicker { get; set; }

    [ObservableProperty]
    public partial CfAccount? SelectedAccount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowForgetAction))]
    public partial bool HasStoredCredentials { get; set; }

    public ObservableCollection<CfAccount> Accounts { get; } = new();

    public bool ShowConnectAction => !IsBusy && !IsConnected && !ShowAccountPicker;

    public bool ShowDisconnectAction => !IsBusy && (IsConnected || ShowAccountPicker);

    public bool ShowForgetAction =>
        !IsBusy && HasStoredCredentials && !IsConnected && !ShowAccountPicker;

    public bool ShowSwitchAccountAction =>
        !IsBusy && IsConnected && !ShowAccountPicker && Accounts.Count > 1;

    public bool CanEditCredentials => !IsBusy && !ShowAccountPicker && !IsConnected;

    public AuthViewModel()
    {
        Accounts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowSwitchAccountAction));

        var saved = App.Credentials.Load();
        if (saved is not null)
        {
            Email = saved.Value.Email;
            ApiKey = saved.Value.ApiKey;
            HasStoredCredentials = true;
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
        IsConnected = false;
        ShowAccountPicker = false;
        Accounts.Clear();
        SelectedAccount = null;
        App.ClearAuthSession();

        try
        {
            App.Api.SetCredentials(Email.Trim(), ApiKey.Trim());

            var user = await App.Api.VerifyCredentials();
            var accounts = await App.Api.GetAccounts();
            if (accounts.Count == 0)
            {
                ShowStatus("No accounts found for this user", InfoBarSeverity.Error);
                App.ClearAuthSession();
                return;
            }

            App.CurrentEmail = user.Email;

            if (accounts.Count == 1)
            {
                Accounts.Add(accounts[0]);
                SelectAccount(accounts[0]);
            }
            else
            {
                foreach (var account in accounts)
                {
                    Accounts.Add(account);
                }

                ShowAccountPicker = true;
                App.NotifyAuthChanged();
                ShowStatus(
                    $"Authenticated as {user.Email}. Select an account.",
                    InfoBarSeverity.Informational
                );
            }
        }
        catch (CfApiException ex)
        {
            App.ClearAuthSession();
            ShowStatus(
                $"Auth failed: {ex.Normalized.Message} - {ex.Normalized.Recommendation}",
                InfoBarSeverity.Error
            );
        }
        catch (Exception ex)
        {
            App.ClearAuthSession();
            ShowStatus($"Connection error: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedAccountChanged(CfAccount? value)
    {
        if (value is not null)
        {
            SelectAccount(value);
        }
    }

    private void SelectAccount(CfAccount account)
    {
        App.CurrentAccountId = account.Id;
        App.CurrentAccountName = account.Name;
        App.Credentials.Save(Email.Trim(), ApiKey.Trim());
        HasStoredCredentials = true;
        IsConnected = true;
        ShowAccountPicker = false;
        ShowStatus($"Connected as {App.CurrentEmail} ({account.Name})", InfoBarSeverity.Success);
        App.NotifyAuthChanged();
    }

    [RelayCommand]
    private void Disconnect()
    {
        IsConnected = false;
        ShowAccountPicker = false;
        Accounts.Clear();
        SelectedAccount = null;
        IsStatusOpen = false;
        App.ClearAuthSession();
    }

    [RelayCommand]
    private void Forget()
    {
        IsConnected = false;
        ShowAccountPicker = false;
        Accounts.Clear();
        SelectedAccount = null;
        HasStoredCredentials = false;
        Email = string.Empty;
        ApiKey = string.Empty;
        StatusMessage = string.Empty;
        IsStatusOpen = false;
        App.ClearAuthSession(clearStoredCredentials: true);
    }

    [RelayCommand]
    private void SwitchAccount()
    {
        if (Accounts.Count <= 1 || App.CurrentEmail is null)
        {
            return;
        }

        IsConnected = false;
        ShowAccountPicker = true;
        SelectedAccount = null;
        App.CurrentAccountId = null;
        App.CurrentAccountName = null;
        ShowStatus(
            $"Authenticated as {App.CurrentEmail}. Select an account.",
            InfoBarSeverity.Informational
        );
        App.NotifyAuthChanged();
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusMessage = message;
        InfoBarSeverity = severity;
        IsStatusOpen = true;
    }
}
