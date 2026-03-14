using System.Collections.ObjectModel;
using CFTools.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace CFTools.ViewModels;

public partial class DeleteDomainsViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanDelete { get; set; }

    [ObservableProperty]
    public partial bool CanCancel { get; set; }

    [ObservableProperty]
    public partial bool CanLoadZones { get; set; } = true;

    [ObservableProperty]
    public partial bool CanChangeSelection { get; set; } = true;

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial double ProgressMaximum { get; set; } = 1;

    [ObservableProperty]
    public partial bool ShowProgress { get; set; }

    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    public ObservableCollection<ZoneSelection> Zones { get; } = new();

    public ObservableCollection<ZoneSelection> VisibleZones { get; } = new();

    public string AccountContextText =>
        App.CurrentAccountName is { Length: > 0 } name
            ? $"Current account: {name}"
            : string.Empty;

    public bool IsAccountMissing => App.CurrentAccountId is null;

    private readonly DispatcherQueue _dispatcher;
    private CancellationTokenSource? _batchCts;
    private string? _loadedAccountId;
    private string? _observedAccountId;
    private bool _pendingAccountInvalidation;

    public DeleteDomainsViewModel()
    {
        _dispatcher =
            DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException(
                "DeleteDomainsViewModel must be created on the UI thread."
            );
        _observedAccountId = App.CurrentAccountId;
        App.AuthStateChanged += () => _dispatcher.TryEnqueue(HandleAuthStateChanged);
        App.ZoneListChanged += () =>
            _dispatcher.TryEnqueue(() =>
            {
                if (!IsBusy && !IsRunning && _loadedAccountId is not null)
                {
                    ResetLoadedZones("Zone list changed. Press Load Zones to refresh.");
                }
            });
    }

    [RelayCommand]
    private async Task LoadZonesAsync()
    {
        if (!App.Api.IsConfigured || App.CurrentAccountId is null)
        {
            StatusText = "Connect and select a Cloudflare account first";
            return;
        }

        var accountId = App.CurrentAccountId;
        var accountName = App.CurrentAccountName ?? "the selected account";

        IsBusy = true;
        ProgressText = string.Empty;
        ShowProgress = false;
        ProgressValue = 0;
        ProgressMaximum = 1;
        ClearLoadedZones();
        StatusText = $"Loading zones for {accountName}...";
        UpdateCommandStates();

        try
        {
            var zones = await App.Api.ListAllZones(accountId);

            if (App.CurrentAccountId != accountId)
            {
                ResetLoadedZones(
                    $"Account changed to {App.CurrentAccountName ?? "the selected account"}. Load zones again to continue."
                );
                return;
            }

            foreach (var zone in zones.OrderBy(z => z.Name))
            {
                Zones.Add(new ZoneSelection(zone));
            }

            _loadedAccountId = accountId;
            RefreshVisibleZones();
            StatusText = $"{zones.Count} zones loaded";
        }
        catch (CfApiException ex)
        {
            StatusText = $"Error: {ex.Normalized.Message}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ApplyPendingAccountInvalidationIfNeeded();
            UpdateCommandStates();
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        if (IsBusy || IsRunning)
        {
            return;
        }

        foreach (var zone in VisibleZones)
        {
            zone.IsSelected = true;
        }

        UpdateCommandStates();
    }

    [RelayCommand]
    private void SelectNone()
    {
        if (IsBusy || IsRunning)
        {
            return;
        }

        foreach (var zone in VisibleZones)
        {
            zone.IsSelected = false;
        }

        UpdateCommandStates();
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selected = VisibleZones.Where(z => z.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        if (_loadedAccountId is null || _loadedAccountId != App.CurrentAccountId)
        {
            ResetLoadedZones(
                App.CurrentAccountId is null
                    ? "Connect and select a Cloudflare account first"
                    : $"Account changed to {App.CurrentAccountName ?? "the selected account"}. Load zones again to continue."
            );
            return;
        }

        _batchCts?.Dispose();
        _batchCts = new CancellationTokenSource();

        IsRunning = true;
        ShowProgress = true;
        ProgressValue = 0;
        ProgressMaximum = selected.Count;
        StatusText =
            $"Deleting {selected.Count} zone(s) from {App.CurrentAccountName ?? "the selected account"}...";
        ProgressText = string.Empty;
        foreach (var zone in selected)
        {
            zone.StatusText = "Queued";
        }
        UpdateCommandStates();

        var total = selected.Count;
        var succeeded = 0;
        var failed = 0;
        var processed = 0;
        var wasCancelled = 0;

        var tasks = selected
            .Select(zone =>
                App.Pool.Add(
                    async ct =>
                    {
                        await RunOnUiThreadAsync(() => zone.StatusText = "Deleting...");

                        try
                        {
                            await App.Api.DeleteZone(zone.Zone.Id, ct);

                            var successCount = Interlocked.Increment(ref succeeded);
                            var processedCount = Interlocked.Increment(ref processed);

                            await RunOnUiThreadAsync(() =>
                            {
                                zone.IsDeleted = true;
                                zone.StatusText = "Deleted";
                                UpdateDeleteProgress(processedCount, successCount, failed, total);
                            });
                        }
                        catch (OperationCanceledException)
                        {
                            Interlocked.Exchange(ref wasCancelled, 1);
                            var failureCount = Interlocked.Increment(ref failed);
                            var processedCount = Interlocked.Increment(ref processed);

                            await RunOnUiThreadAsync(() =>
                            {
                                zone.StatusText = "Cancelled";
                                UpdateDeleteProgress(processedCount, succeeded, failureCount, total);
                            });
                        }
                        catch (CfApiException ex)
                        {
                            var failureCount = Interlocked.Increment(ref failed);
                            var processedCount = Interlocked.Increment(ref processed);

                            await RunOnUiThreadAsync(() =>
                            {
                                zone.StatusText = $"Failed: {ex.Normalized.Message}";
                                UpdateDeleteProgress(processedCount, succeeded, failureCount, total);
                            });
                        }
                        catch (Exception ex)
                        {
                            var failureCount = Interlocked.Increment(ref failed);
                            var processedCount = Interlocked.Increment(ref processed);

                            await RunOnUiThreadAsync(() =>
                            {
                                zone.StatusText = $"Failed: {ex.Message}";
                                UpdateDeleteProgress(processedCount, succeeded, failureCount, total);
                            });
                        }

                        return zone.Zone.Id;
                    },
                    _batchCts.Token
                )
            )
            .ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        finally
        {
            IsRunning = false;
            var invalidated = ApplyPendingAccountInvalidationIfNeeded();
            UpdateCommandStates();

            if (!invalidated)
            {
                StatusText =
                    wasCancelled == 1
                        ? $"Cancelled: {succeeded} deleted, {failed} not completed out of {total}"
                        : $"Done: {succeeded} deleted, {failed} failed out of {total}";

                await LoadZonesAsync();
                App.NotifyZoneListChanged();
            }
        }
    }

    [RelayCommand]
    private void CancelBatch()
    {
        if (!IsRunning)
        {
            return;
        }

        StatusText = "Cancelling batch...";
        CanCancel = false;
        _batchCts?.Cancel();
        App.Pool.Cancel();
    }

    public void UpdateCommandStates()
    {
        CanLoadZones = !IsBusy && !IsRunning && App.CurrentAccountId is not null;
        CanChangeSelection = !IsBusy && !IsRunning;
        CanCancel = IsRunning;
        CanDelete =
            !IsBusy
            && !IsRunning
            && _loadedAccountId is not null
            && _loadedAccountId == App.CurrentAccountId
            && VisibleZones.Any(z => z.IsSelected);
    }

    partial void OnFilterTextChanged(string value)
    {
        DeselectHiddenSelections(value);
        RefreshVisibleZones();
        UpdateCommandStates();
    }

    private void RefreshVisibleZones()
    {
        VisibleZones.Clear();

        foreach (var zone in GetFilteredZones())
        {
            VisibleZones.Add(zone);
        }
    }

    private IEnumerable<ZoneSelection> GetFilteredZones()
    {
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            return Zones;
        }

        return Zones.Where(z => MatchesFilter(z, FilterText));
    }

    private void DeselectHiddenSelections(string filterText)
    {
        foreach (var zone in Zones)
        {
            if (!MatchesFilter(zone, filterText))
            {
                zone.IsSelected = false;
            }
        }
    }

    private static bool MatchesFilter(ZoneSelection zone, string filterText)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            return true;
        }

        return zone.Zone.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    private void HandleAuthStateChanged()
    {
        var currentAccountId = App.CurrentAccountId;
        var accountChanged = _observedAccountId != currentAccountId;
        _observedAccountId = currentAccountId;

        OnPropertyChanged(nameof(AccountContextText));
        OnPropertyChanged(nameof(IsAccountMissing));

        if (!accountChanged)
        {
            return;
        }

        if (IsBusy || IsRunning)
        {
            _pendingAccountInvalidation = true;
            return;
        }

        if (_loadedAccountId is not null || Zones.Count > 0)
        {
            ResetLoadedZones(
                currentAccountId is null
                    ? "Connect and select a Cloudflare account first"
                    : $"Account changed to {App.CurrentAccountName ?? "the selected account"}. Load zones again to continue."
            );
        }
    }

    private bool ApplyPendingAccountInvalidationIfNeeded()
    {
        if (!_pendingAccountInvalidation || IsBusy || IsRunning)
        {
            return false;
        }

        _pendingAccountInvalidation = false;
        ResetLoadedZones(
            App.CurrentAccountId is null
                ? "Connect and select a Cloudflare account first"
                : $"Account changed to {App.CurrentAccountName ?? "the selected account"}. Load zones again to continue."
        );
        return true;
    }

    private void ResetLoadedZones(string statusMessage)
    {
        ClearLoadedZones();
        StatusText = statusMessage;
        UpdateCommandStates();
    }

    private void ClearLoadedZones()
    {
        _loadedAccountId = null;
        Zones.Clear();
        VisibleZones.Clear();
        FilterText = string.Empty;
        ProgressText = string.Empty;
        ShowProgress = false;
        ProgressValue = 0;
        ProgressMaximum = 1;
    }

    private void UpdateDeleteProgress(int processed, int success, int failed, int total)
    {
        ProgressMaximum = total;
        ProgressValue = processed;
        ProgressText = $"{processed}/{total} processed - {success} deleted, {failed} failed";
    }

    private Task RunOnUiThreadAsync(Action action)
    {
        if (_dispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatcher.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }
}
