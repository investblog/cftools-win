using System.Collections.ObjectModel;
using CFTools.Models;
using CFTools.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace CFTools.ViewModels;

public partial class AddDomainsViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string DomainInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool CanCheck { get; set; } = true;

    [ObservableProperty]
    public partial bool CanCreate { get; set; }

    [ObservableProperty]
    public partial bool IsNextStepCreate { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial bool CanCancel { get; set; }

    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial double ProgressMaximum { get; set; } = 1;

    [ObservableProperty]
    public partial bool ShowProgress { get; set; }

    public ObservableCollection<PreflightEntry> PreflightResults { get; } = new();

    [ObservableProperty]
    public partial bool ShowAfterCreateTip { get; set; }

    [ObservableProperty]
    public partial string AfterCreateTipText { get; set; } = string.Empty;

    public Uri AfterCreateUrl { get; } = PromoLinks.Uri(PromoLinks.AfterCreateCampaign);

    [ObservableProperty]
    public partial bool HasBatchResults { get; set; }

    private readonly List<BatchResultRow> _batchResults = new();
    private readonly object _resultsLock = new();

    [RelayCommand]
    private async Task ExportResultsAsync()
    {
        List<BatchResultRow> rows;
        lock (_resultsLock)
            rows = _batchResults.ToList();
        if (rows.Count == 0)
            return;

        var fileName = $"cftools-create-results-{DateTime.Now:yyyy-MM-dd-HHmm}.csv";
        if (await FileExporter.SaveCsvAsync(fileName, CsvBuilder.BatchResultsCsv(rows)))
            StatusText = Loc.Format("Status_ExportedResults", rows.Count, fileName);
    }

    private void RecordResult(string domain, string status, string? error = null)
    {
        lock (_resultsLock)
            _batchResults.Add(new BatchResultRow(domain, status, error));
    }

    private void ClearBatchResults()
    {
        lock (_resultsLock)
            _batchResults.Clear();
        HasBatchResults = false;
    }

    /// <summary>
    /// Items the pool never started (cancelled while queued) get no callback, so after a
    /// cancelled batch they must be recorded explicitly. Returns the domains added.
    /// </summary>
    private List<string> MarkUnrecordedAsCancelled(IEnumerable<string> domains)
    {
        var added = new List<string>();
        lock (_resultsLock)
        {
            var recorded = _batchResults
                .Select(r => r.Domain)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var domain in domains)
            {
                if (recorded.Add(domain))
                {
                    _batchResults.Add(new BatchResultRow(domain, "cancelled", null));
                    added.Add(domain);
                }
            }
        }
        return added;
    }

    public string AccountContextText =>
        App.CurrentAccountName is { Length: > 0 } name
            ? Loc.Format("Status_CurrentAccount", name)
            : string.Empty;

    public bool IsAccountMissing => App.CurrentAccountId is null;

    private readonly List<string> _domainsToCreate = new();
    private readonly DispatcherQueue _dispatcher;
    private CancellationTokenSource? _batchCts;
    private string? _preflightAccountId;
    private string? _observedAccountId;
    private bool _pendingAccountInvalidation;

    public AddDomainsViewModel()
    {
        _dispatcher =
            DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException(
                "AddDomainsViewModel must be created on the UI thread."
            );
        _observedAccountId = App.CurrentAccountId;
        App.AuthStateChanged += () => _dispatcher.TryEnqueue(HandleAuthStateChanged);
        App.ThemeChanged += () => _dispatcher.TryEnqueue(RefreshThemeBindings);
        App.TipsSettingChanged += () =>
            _dispatcher.TryEnqueue(() =>
            {
                if (!App.Settings.Show301Tips)
                    ShowAfterCreateTip = false;
            });
    }

    [RelayCommand]
    private async Task CheckAsync()
    {
        if (string.IsNullOrWhiteSpace(DomainInput))
            return;

        if (!App.Api.IsConfigured || App.CurrentAccountId is null)
        {
            StatusText = Loc.Get("Status_ConnectFirst");
            return;
        }

        var accountId = App.CurrentAccountId;
        var accountName = App.CurrentAccountName ?? Loc.Get("Status_SelectedAccountFallback");

        IsBusy = true;
        IsRunning = false;
        CanCreate = false;
        CanCancel = false;
        CanCheck = false;
        IsNextStepCreate = false;
        ShowProgress = false;
        ProgressValue = 0;
        ProgressMaximum = 1;
        ProgressText = string.Empty;
        PreflightResults.Clear();
        _domainsToCreate.Clear();
        _preflightAccountId = null;
        ClearBatchResults();
        ShowAfterCreateTip = false;
        StatusText = Loc.Get("Add_Parsing");

        try
        {
            var parsed = DomainParser.Parse(DomainInput, rootOnly: true);

            if (parsed.Domains.Count == 0)
            {
                StatusText = Loc.Get("Add_NoValidDomains");
                return;
            }

            StatusText = Loc.Format("Add_Checking", parsed.Domains.Count, accountName);

            var willCreate = 0;
            var exists = 0;

            foreach (var domain in parsed.Domains)
            {
                var (zoneExists, zoneId) = await App.Api.CheckZoneExists(domain, accountId);

                if (zoneExists)
                {
                    PreflightResults.Add(
                        new PreflightEntry(
                            domain,
                            PreflightStatus.Exists,
                            zoneId,
                            Loc.Get("Add_Exists")
                        )
                    );
                    exists++;
                }
                else
                {
                    PreflightResults.Add(
                        new PreflightEntry(
                            domain,
                            PreflightStatus.WillCreate,
                            Message: Loc.Get("Add_Ready")
                        )
                    );
                    _domainsToCreate.Add(domain);
                    willCreate++;
                }
            }

            foreach (var dup in parsed.Duplicates)
                PreflightResults.Add(
                    new PreflightEntry(
                        dup,
                        PreflightStatus.Duplicate,
                        Message: Loc.Get("Add_Duplicate")
                    )
                );

            foreach (var inv in parsed.Invalid)
                PreflightResults.Add(
                    new PreflightEntry(
                        inv,
                        PreflightStatus.Invalid,
                        Message: Loc.Get("Add_InvalidRoot")
                    )
                );

            if (App.CurrentAccountId != accountId)
            {
                ResetPreflightState(
                    Loc.Format(
                        "Status_AccountChangedRecheck",
                        App.CurrentAccountName ?? Loc.Get("Status_SelectedAccountFallback")
                    )
                );
                return;
            }

            StatusText = Loc.Format(
                "Add_Summary",
                willCreate,
                exists,
                parsed.Duplicates.Count,
                parsed.Invalid.Count
            );
            CanCreate = willCreate > 0;
            IsNextStepCreate = willCreate > 0;
            _preflightAccountId = accountId;
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

    [RelayCommand]
    private async Task CreateAllAsync()
    {
        if (_domainsToCreate.Count == 0 || App.CurrentAccountId is null)
            return;

        if (_preflightAccountId != App.CurrentAccountId)
        {
            ResetPreflightState(
                Loc.Format(
                    "Status_AccountChangedRecheck",
                    App.CurrentAccountName ?? Loc.Get("Status_SelectedAccountFallback")
                )
            );
            return;
        }

        _batchCts?.Dispose();
        _batchCts = new CancellationTokenSource();
        ClearBatchResults();

        IsRunning = true;
        IsBusy = false;
        ShowProgress = true;
        ProgressValue = 0;
        ProgressMaximum = _domainsToCreate.Count;
        StatusText = Loc.Format(
            "Add_CreatingIn",
            App.CurrentAccountName ?? Loc.Get("Status_SelectedAccountFallback")
        );
        UpdateCommandStates();

        var accountId = App.CurrentAccountId;
        var total = _domainsToCreate.Count;
        var succeeded = 0;
        var failed = 0;
        var processed = 0;
        var wasCancelled = 0;

        var tasks = _domainsToCreate
            .Select(domain =>
                App.Pool.Add(
                    async ct =>
                    {
                        await RunOnUiThreadAsync(() =>
                            UpdatePreflightStatus(
                                domain,
                                PreflightStatus.Creating,
                                Loc.Get("Add_CreatingZone")
                            )
                        );

                        try
                        {
                            await App.Api.CreateZone(domain, accountId, ct: ct);
                            RecordResult(domain, "created");

                            var successCount = Interlocked.Increment(ref succeeded);
                            var processedCount = Interlocked.Increment(ref processed);

                            await RunOnUiThreadAsync(() =>
                            {
                                UpdatePreflightStatus(
                                    domain,
                                    PreflightStatus.Created,
                                    Loc.Get("Add_ZoneCreated")
                                );
                                UpdateProgress(processedCount, successCount, failed, total);
                            });
                        }
                        catch (OperationCanceledException)
                        {
                            Interlocked.Exchange(ref wasCancelled, 1);
                            RecordResult(domain, "cancelled");
                            var failureCount = Interlocked.Increment(ref failed);
                            var processedCount = Interlocked.Increment(ref processed);

                            await RunOnUiThreadAsync(() =>
                            {
                                UpdatePreflightStatus(
                                    domain,
                                    PreflightStatus.Cancelled,
                                    Loc.Get("Item_Cancelled")
                                );
                                UpdateProgress(processedCount, succeeded, failureCount, total);
                            });
                        }
                        catch (CfApiException ex)
                        {
                            RecordResult(domain, "failed", ex.Normalized.Message);
                            var failureCount = Interlocked.Increment(ref failed);
                            var processedCount = Interlocked.Increment(ref processed);

                            await RunOnUiThreadAsync(() =>
                            {
                                UpdatePreflightStatus(
                                    domain,
                                    PreflightStatus.Failed,
                                    ex.Normalized.Message
                                );
                                UpdateProgress(processedCount, succeeded, failureCount, total);
                            });
                        }
                        catch (Exception ex)
                        {
                            RecordResult(domain, "failed", ex.Message);
                            var failureCount = Interlocked.Increment(ref failed);
                            var processedCount = Interlocked.Increment(ref processed);

                            await RunOnUiThreadAsync(() =>
                            {
                                UpdatePreflightStatus(domain, PreflightStatus.Failed, ex.Message);
                                UpdateProgress(processedCount, succeeded, failureCount, total);
                            });
                        }

                        return domain;
                    },
                    _batchCts.Token
                )
            )
            .ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Queued items that never started are cancelled by the pool without a callback.
            Interlocked.Exchange(ref wasCancelled, 1);
        }
        finally
        {
            IsRunning = false;
            CanCancel = false;
            IsNextStepCreate = false;
            var neverStarted = MarkUnrecordedAsCancelled(_domainsToCreate);
            foreach (var domain in neverStarted)
                UpdatePreflightStatus(domain, PreflightStatus.Cancelled, Loc.Get("Item_Cancelled"));
            if (neverStarted.Count > 0)
            {
                failed += neverStarted.Count;
                processed += neverStarted.Count;
                UpdateProgress(processed, succeeded, failed, total);
            }
            lock (_resultsLock)
                HasBatchResults = _batchResults.Count > 0;
            var invalidated = ApplyPendingAccountInvalidationIfNeeded();
            UpdateCommandStates();

            if (!invalidated)
            {
                ProgressText =
                    wasCancelled == 1
                        ? Loc.Format("Add_DoneCancelled", succeeded, failed, total)
                        : Loc.Format("Add_Done", succeeded, failed, total);

                StatusText =
                    wasCancelled == 1
                        ? Loc.Get("Status_BatchCancelled")
                        : Loc.Get("Status_BatchFinished");

                if (succeeded > 0)
                {
                    App.NotifyZoneListChanged();
                    ShowAfterCreateTipIfEnabled(succeeded);
                }
            }
        }
    }

    [RelayCommand]
    private void CancelBatch()
    {
        if (!IsRunning)
            return;

        StatusText = Loc.Get("Status_Cancelling");
        CanCancel = false;
        _batchCts?.Cancel();
        App.Pool.Cancel();
    }

    public void RemoveDomain(string domain)
    {
        if (IsBusy || IsRunning)
            return;

        _domainsToCreate.Remove(domain);

        for (int i = PreflightResults.Count - 1; i >= 0; i--)
        {
            if (
                string.Equals(
                    PreflightResults[i].Domain,
                    domain,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                PreflightResults.RemoveAt(i);
                break;
            }
        }

        var ready = _domainsToCreate.Count;
        var exists = PreflightResults.Count(e => e.Status == Models.PreflightStatus.Exists);
        var dups = PreflightResults.Count(e => e.Status == Models.PreflightStatus.Duplicate);
        var inv = PreflightResults.Count(e => e.Status == Models.PreflightStatus.Invalid);
        StatusText = Loc.Format("Add_Summary", ready, exists, dups, inv);

        IsNextStepCreate = ready > 0;
        UpdateCommandStates();
    }

    private void ShowAfterCreateTipIfEnabled(int created)
    {
        var settings = App.Settings;
        if (!settings.Show301Tips || settings.AfterCreateTipDismissed)
            return;

        AfterCreateTipText = Loc.Format("Add_Tip", created);
        ShowAfterCreateTip = true;
    }

    /// <summary>User closed the tip: remember it so it is not shown after every batch.</summary>
    public void DismissAfterCreateTip()
    {
        ShowAfterCreateTip = false;
        App.Settings.AfterCreateTipDismissed = true;
        App.Settings.Save();
    }

    private void UpdateProgress(int processed, int success, int failed, int total)
    {
        ProgressMaximum = total;
        ProgressValue = processed;
        ProgressText = Loc.Format("Add_Progress", processed, total, success, failed);
    }

    private void UpdatePreflightStatus(string domain, PreflightStatus newStatus, string? message)
    {
        for (int i = 0; i < PreflightResults.Count; i++)
        {
            if (PreflightResults[i].Domain == domain)
            {
                PreflightResults[i] = PreflightResults[i] with
                {
                    Status = newStatus,
                    Message = message,
                };
                break;
            }
        }
    }

    private void UpdateCommandStates()
    {
        CanCheck = !IsBusy && !IsRunning;
        CanCreate =
            !IsBusy
            && !IsRunning
            && _domainsToCreate.Count > 0
            && _preflightAccountId is not null
            && _preflightAccountId == App.CurrentAccountId;
        CanCancel = IsRunning;
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

        if (PreflightResults.Count > 0 || _domainsToCreate.Count > 0)
        {
            ResetPreflightState(
                currentAccountId is null
                    ? Loc.Get("Status_ConnectFirst")
                    : Loc.Format(
                        "Status_AccountChangedRecheck",
                        App.CurrentAccountName ?? Loc.Get("Status_SelectedAccountFallback")
                    )
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
        ResetPreflightState(
            App.CurrentAccountId is null
                ? Loc.Get("Status_ConnectFirst")
                : Loc.Format(
                    "Status_AccountChangedRecheck",
                    App.CurrentAccountName ?? Loc.Get("Status_SelectedAccountFallback")
                )
        );
        return true;
    }

    private void ResetPreflightState(string statusMessage)
    {
        _domainsToCreate.Clear();
        _preflightAccountId = null;
        PreflightResults.Clear();
        ClearBatchResults();
        CanCreate = false;
        CanCancel = false;
        IsNextStepCreate = false;
        ShowProgress = false;
        ProgressValue = 0;
        ProgressMaximum = 1;
        ProgressText = string.Empty;
        StatusText = statusMessage;
    }

    private void RefreshThemeBindings()
    {
        for (var i = 0; i < PreflightResults.Count; i++)
        {
            PreflightResults[i] = PreflightResults[i] with { };
        }
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
