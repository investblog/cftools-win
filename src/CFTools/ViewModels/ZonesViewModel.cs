using System.Collections.ObjectModel;
using CFTools.Models;
using CFTools.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace CFTools.ViewModels;

/// <summary>
/// Read-only zone row for the Zones page.
/// </summary>
public partial class ZoneRow : ObservableObject
{
    public CfZone Zone { get; }

    public string ZoneName => Zone.Name;

    public string ZoneStatus => Zone.Status;

    public string PlanName => string.IsNullOrEmpty(Zone.Plan?.Name) ? "free" : Zone.Plan.Name;

    public string NameServersText =>
        Zone.NameServers is { Length: > 0 } ? string.Join(", ", Zone.NameServers) : "-";

    public ZoneRow(CfZone zone) => Zone = zone;

    public void RefreshThemeBindings() => OnPropertyChanged(nameof(ZoneStatus));
}

public partial class ZonesViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanLoadZones { get; set; } = true;

    [ObservableProperty]
    public partial bool CanExport { get; set; }

    [ObservableProperty]
    public partial bool CanExportAllAccounts { get; set; }

    [ObservableProperty]
    public partial bool ShowPendingHint { get; set; }

    [ObservableProperty]
    public partial string PendingZonesHint { get; set; } = string.Empty;

    public Uri ZonesPendingUrl { get; } = PromoLinks.Uri(PromoLinks.ZonesPendingCampaign);

    public ObservableCollection<ZoneRow> Zones { get; } = new();

    public ObservableCollection<ZoneRow> VisibleZones { get; } = new();

    public string AccountContextText =>
        App.CurrentAccountName is { Length: > 0 } name
            ? Loc.Format("Status_CurrentAccount", name)
            : string.Empty;

    public bool IsAccountMissing => App.CurrentAccountId is null;

    private readonly DispatcherQueue _dispatcher;
    private string? _loadedAccountId;
    private string? _observedAccountId;
    private bool _pendingAccountInvalidation;

    public ZonesViewModel()
    {
        _dispatcher =
            DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException(
                "ZonesViewModel must be created on the UI thread."
            );
        _observedAccountId = App.CurrentAccountId;
        App.AuthStateChanged += () => _dispatcher.TryEnqueue(HandleAuthStateChanged);
        App.ThemeChanged += () => _dispatcher.TryEnqueue(RefreshThemeBindings);
        App.TipsSettingChanged += () =>
            _dispatcher.TryEnqueue(() => UpdateCommandStates(keepStatus: true));
        App.ZoneListChanged += () =>
            _dispatcher.TryEnqueue(() =>
            {
                if (!IsBusy && _loadedAccountId is not null)
                {
                    ResetLoadedZones(Loc.Get("Status_ZoneListChangedRefresh"));
                }
            });
    }

    [RelayCommand]
    private async Task LoadZonesAsync()
    {
        if (!App.Api.IsConfigured || App.CurrentAccountId is null)
        {
            StatusText = Loc.Get("Status_ConnectFirst");
            return;
        }

        var accountId = App.CurrentAccountId;
        var accountName = App.CurrentAccountName ?? Loc.Get("Status_SelectedAccountFallback");

        IsBusy = true;
        UpdateCommandStates();
        ClearLoadedZones();
        StatusText = Loc.Format("Status_LoadingZones", accountName);

        try
        {
            var zones = await App.Api.ListAllZones(accountId);

            if (App.CurrentAccountId != accountId)
            {
                ResetLoadedZones(
                    Loc.Format(
                        "Status_AccountChangedRefresh",
                        App.CurrentAccountName ?? Loc.Get("Status_SelectedAccountFallback")
                    )
                );
                return;
            }

            foreach (var zone in zones.OrderBy(z => z.Name))
                Zones.Add(new ZoneRow(zone));

            _loadedAccountId = accountId;
            RefreshVisibleZones();
        }
        catch (CfApiException ex)
        {
            StatusText = Loc.Format("Status_Error", ex.Normalized.Message);
        }
        catch (Exception ex)
        {
            StatusText = Loc.Format("Status_Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
            ApplyPendingAccountInvalidationIfNeeded();
            UpdateCommandStates();
        }
    }

    /// <summary>
    /// Export the zones currently shown (respects the filter) for the active account.
    /// </summary>
    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (VisibleZones.Count == 0)
            return;

        var rows = VisibleZones.Select(z => z.Zone).ToList();
        var fileName =
            $"zones-{FileExporter.Slug(App.CurrentAccountName)}-{DateTime.Now:yyyy-MM-dd}.csv";

        if (await FileExporter.SaveCsvAsync(fileName, CsvBuilder.ZonesCsv(rows)))
            StatusText = Loc.Format("Zones_Exported", rows.Count, fileName);
    }

    /// <summary>
    /// Export every zone of every account visible to the current credential.
    /// </summary>
    [RelayCommand]
    private async Task ExportAllAccountsCsvAsync()
    {
        var accounts = App.AvailableAccounts;
        if (!App.Api.IsConfigured || accounts.Count == 0)
        {
            StatusText = Loc.Get("Zones_ConnectToExportAll");
            return;
        }

        var credential = App.Api.Credential; // instance identity = auth session identity
        IsBusy = true;
        UpdateCommandStates();

        try
        {
            var rows = new List<(CfZone Zone, string AccountName)>();
            var index = 0;

            foreach (var account in accounts)
            {
                index++;
                StatusText = Loc.Format(
                    "Zones_ExportingAccount",
                    account.Name,
                    index,
                    accounts.Count
                );

                var zones = await App.Api.ListAllZones(account.Id);

                if (!ReferenceEquals(App.Api.Credential, credential))
                {
                    StatusText = Loc.Get("Zones_CredentialsChanged");
                    return;
                }

                rows.AddRange(zones.OrderBy(z => z.Name).Select(z => (z, account.Name)));
            }

            var fileName = $"cloudflare-zones-all-{DateTime.Now:yyyy-MM-dd}.csv";
            StatusText = Loc.Format("Zones_ExportReady", rows.Count, accounts.Count);

            if (await FileExporter.SaveCsvAsync(fileName, CsvBuilder.ZonesCsv(rows)))
                StatusText = Loc.Format("Zones_ExportedAll", rows.Count, accounts.Count, fileName);
        }
        catch (CfApiException ex)
        {
            StatusText = Loc.Format("Zones_ExportFailed", ex.Normalized.Message);
        }
        catch (Exception ex)
        {
            StatusText = Loc.Format("Zones_ExportFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
            ApplyPendingAccountInvalidationIfNeeded();
            UpdateCommandStates(keepStatus: true);
        }
    }

    partial void OnFilterTextChanged(string value)
    {
        RefreshVisibleZones();
        UpdateCommandStates();
    }

    private void RefreshVisibleZones()
    {
        VisibleZones.Clear();

        foreach (var zone in GetFilteredZones())
            VisibleZones.Add(zone);
    }

    private IEnumerable<ZoneRow> GetFilteredZones()
    {
        if (string.IsNullOrWhiteSpace(FilterText))
            return Zones;

        return Zones.Where(z =>
            z.Zone.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
        );
    }

    private void UpdateCommandStates(bool keepStatus = false)
    {
        CanLoadZones = !IsBusy && App.CurrentAccountId is not null;
        CanExport =
            !IsBusy
            && _loadedAccountId is not null
            && _loadedAccountId == App.CurrentAccountId
            && VisibleZones.Count > 0;
        CanExportAllAccounts = !IsBusy && App.Api.IsConfigured && App.AvailableAccounts.Count > 0;

        var pending = _loadedAccountId is null ? 0 : Zones.Count(z => z.Zone.Status != "active");
        ShowPendingHint = pending > 0 && App.Settings.Show301Tips;
        PendingZonesHint = Loc.Format("Zones_PendingHint", pending);

        if (!keepStatus && !IsBusy && _loadedAccountId is not null)
        {
            var total = Zones.Count;
            var active = Zones.Count(z => z.Zone.Status == "active");
            StatusText = string.IsNullOrWhiteSpace(FilterText)
                ? Loc.Format("Zones_Loaded", total, active)
                : Loc.Format("Zones_FilterMatch", VisibleZones.Count, total);
        }
    }

    private void HandleAuthStateChanged()
    {
        var currentAccountId = App.CurrentAccountId;
        var accountChanged = _observedAccountId != currentAccountId;
        _observedAccountId = currentAccountId;

        OnPropertyChanged(nameof(AccountContextText));
        OnPropertyChanged(nameof(IsAccountMissing));
        UpdateCommandStates(keepStatus: true);

        if (!accountChanged)
        {
            return;
        }

        if (IsBusy)
        {
            _pendingAccountInvalidation = true;
            return;
        }

        if (_loadedAccountId is not null || Zones.Count > 0)
        {
            ResetLoadedZones(
                currentAccountId is null
                    ? Loc.Get("Status_ConnectFirst")
                    : Loc.Format(
                        "Status_AccountChangedRefresh",
                        App.CurrentAccountName ?? Loc.Get("Status_SelectedAccountFallback")
                    )
            );
        }
    }

    private bool ApplyPendingAccountInvalidationIfNeeded()
    {
        if (!_pendingAccountInvalidation || IsBusy)
        {
            return false;
        }

        _pendingAccountInvalidation = false;
        ResetLoadedZones(
            App.CurrentAccountId is null
                ? Loc.Get("Status_ConnectFirst")
                : Loc.Format(
                    "Status_AccountChangedRefresh",
                    App.CurrentAccountName ?? Loc.Get("Status_SelectedAccountFallback")
                )
        );
        return true;
    }

    private void ResetLoadedZones(string statusMessage)
    {
        ClearLoadedZones();
        StatusText = statusMessage;
        UpdateCommandStates(keepStatus: true);
    }

    private void ClearLoadedZones()
    {
        _loadedAccountId = null;
        Zones.Clear();
        VisibleZones.Clear();
        FilterText = string.Empty;
    }

    private void RefreshThemeBindings()
    {
        foreach (var zone in Zones)
        {
            zone.RefreshThemeBindings();
        }
    }
}
