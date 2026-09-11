using System.Collections.ObjectModel;
using CFTools.Models;
using CFTools.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;

namespace CFTools.ViewModels;

public partial class AuthViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    /// <summary>
    /// The secret: an API token (cfut_ / cfat_) or a Global API Key.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditCredentials))]
    public partial string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional account ID for account-owned tokens that cannot list their own account.
    /// </summary>
    [ObservableProperty]
    public partial string AccountIdInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CredentialHint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsEmailRequired { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowAccountIdField { get; set; }

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

    private CfCredential? _activeCredential;

    public bool ShowTips => App.Settings.Show301Tips;

    public Uri PromoUrl { get; } = PromoLinks.Uri(PromoLinks.AuthCampaign);

    public AuthViewModel()
    {
        Accounts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowSwitchAccountAction));
        App.TipsSettingChanged += () => OnPropertyChanged(nameof(ShowTips));

        var saved = App.Credentials.Load();
        if (saved is not null)
        {
            Email = saved.Email ?? string.Empty;
            AccountIdInput = saved.AccountId ?? string.Empty;
            ApiKey = saved.Secret;
            HasStoredCredentials = true;
        }

        UpdateCredentialHint();
    }

    partial void OnApiKeyChanged(string value) => UpdateCredentialHint();

    private void UpdateCredentialHint()
    {
        var kind = CredentialDetector.Detect(ApiKey);

        IsEmailRequired = kind is null or CredentialKind.GlobalKey;
        ShowAccountIdField = kind == CredentialKind.AccountToken;
        CredentialHint = kind switch
        {
            CredentialKind.GlobalKey =>
                "Detected: Global API Key. Enter the account e-mail as well.",
            CredentialKind.UserToken => "Detected: user API token (cfut_). No e-mail needed.",
            CredentialKind.AccountToken =>
                "Detected: account-owned token (cfat_), scoped to one account. Enter the Account ID if the token cannot list its own account.",
            _ when string.IsNullOrWhiteSpace(ApiKey) => string.Empty,
            _ =>
                "Format not recognized: with an e-mail it is treated as a Global API Key, without one as an API token.",
        };
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        var secret = ApiKey.Trim();
        var email = Email.Trim();

        if (string.IsNullOrWhiteSpace(secret))
        {
            ShowStatus("Enter your API token or Global API Key", InfoBarSeverity.Warning);
            return;
        }

        var kind = CredentialDetector.Resolve(secret, email);
        if (kind == CredentialKind.GlobalKey && string.IsNullOrWhiteSpace(email))
        {
            ShowStatus("Global API Key requires the account e-mail", InfoBarSeverity.Warning);
            return;
        }

        var accountId = AccountIdInput.Trim();
        var credential = new CfCredential(
            kind,
            secret,
            Email: kind == CredentialKind.GlobalKey ? email : null,
            AccountId: kind == CredentialKind.AccountToken && accountId.Length > 0
                ? accountId
                : null
        );

        IsBusy = true;
        IsStatusOpen = false;
        IsConnected = false;
        ShowAccountPicker = false;
        Accounts.Clear();
        SelectedAccount = null;
        _activeCredential = null;
        App.ClearAuthSession();

        try
        {
            App.Api.SetCredentials(credential);

            var identity = await App.Api.VerifyCredentials();

            List<CfAccount> accounts;
            try
            {
                accounts = await App.Api.GetAccounts();
            }
            catch (CfApiException) when (credential.AccountId is not null)
            {
                // Account-owned token without "Account Settings: Read" — use the given ID.
                accounts = new List<CfAccount>();
            }

            if (accounts.Count == 0 && credential.AccountId is not null)
            {
                var id8 = credential.AccountId[..Math.Min(8, credential.AccountId.Length)];
                accounts.Add(new CfAccount(credential.AccountId, $"Account {id8}"));
            }

            if (accounts.Count == 0)
            {
                ShowStatus(
                    credential.IsToken
                        ? "No accounts visible to this token. Grant it \"Account Settings: Read\" or enter the Account ID."
                        : "No accounts found for this user",
                    InfoBarSeverity.Error
                );
                App.ClearAuthSession();
                return;
            }

            _activeCredential = credential;
            App.CurrentEmail = identity.Label;
            App.AvailableAccounts = accounts;

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
                    $"Authenticated as {identity.Label}. Select an account.",
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
        if (_activeCredential is not null)
        {
            App.Credentials.Save(_activeCredential);
            HasStoredCredentials = true;
        }
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
        _activeCredential = null;
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
        AccountIdInput = string.Empty;
        StatusMessage = string.Empty;
        IsStatusOpen = false;
        _activeCredential = null;
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
