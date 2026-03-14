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

    public string AccountContextText =>
        App.CurrentAccountName is { Length: > 0 } name
            ? $"Current account: {name}"
            : "Current account: not selected";

    private readonly List<string> _domainsToCreate = new();
    private readonly DispatcherQueue _dispatcher;
    private CancellationTokenSource? _batchCts;

    public AddDomainsViewModel()
    {
        _dispatcher =
            DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException(
                "AddDomainsViewModel must be created on the UI thread."
            );
    }

    [RelayCommand]
    private async Task CheckAsync()
    {
        if (string.IsNullOrWhiteSpace(DomainInput))
            return;

        if (!App.Api.IsConfigured || App.CurrentAccountId is null)
        {
            StatusText = "Connect and select a Cloudflare account first";
            return;
        }

        IsBusy = true;
        IsRunning = false;
        CanCreate = false;
        CanCancel = false;
        CanCheck = false;
        ShowProgress = false;
        ProgressValue = 0;
        ProgressMaximum = 1;
        ProgressText = string.Empty;
        PreflightResults.Clear();
        _domainsToCreate.Clear();
        StatusText = "Parsing domains...";

        try
        {
            var parsed = DomainParser.Parse(DomainInput, rootOnly: true);

            if (parsed.Domains.Count == 0)
            {
                StatusText = "No valid domains found";
                return;
            }

            StatusText =
                $"Checking {parsed.Domains.Count} domains in {App.CurrentAccountName ?? "the selected account"}...";

            var willCreate = 0;
            var exists = 0;

            foreach (var domain in parsed.Domains)
            {
                var (zoneExists, zoneId) = await App.Api.CheckZoneExists(
                    domain,
                    App.CurrentAccountId
                );

                if (zoneExists)
                {
                    PreflightResults.Add(
                        new PreflightEntry(
                            domain,
                            PreflightStatus.Exists,
                            zoneId,
                            "Already exists in Cloudflare"
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
                            Message: "Ready to create"
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
                        Message: "Duplicate in input"
                    )
                );

            foreach (var inv in parsed.Invalid)
                PreflightResults.Add(
                    new PreflightEntry(
                        inv,
                        PreflightStatus.Invalid,
                        Message: "Input is not a valid root domain"
                    )
                );

            StatusText =
                $"{willCreate} ready, {exists} already exist, {parsed.Duplicates.Count} duplicates, {parsed.Invalid.Count} invalid";
            CanCreate = willCreate > 0;
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
            UpdateCommandStates();
        }
    }

    [RelayCommand]
    private async Task CreateAllAsync()
    {
        if (_domainsToCreate.Count == 0 || App.CurrentAccountId is null)
            return;

        _batchCts?.Dispose();
        _batchCts = new CancellationTokenSource();

        IsRunning = true;
        IsBusy = false;
        ShowProgress = true;
        ProgressValue = 0;
        ProgressMaximum = _domainsToCreate.Count;
        StatusText = $"Creating zones in {App.CurrentAccountName ?? "the selected account"}...";
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
                                "Creating zone..."
                            )
                        );

                        try
                        {
                            await App.Api.CreateZone(domain, accountId, ct: ct);

                            var successCount = Interlocked.Increment(ref succeeded);
                            var processedCount = Interlocked.Increment(ref processed);

                            await RunOnUiThreadAsync(() =>
                            {
                                UpdatePreflightStatus(
                                    domain,
                                    PreflightStatus.Created,
                                    "Zone created"
                                );
                                UpdateProgress(processedCount, successCount, failed, total);
                            });
                        }
                        catch (OperationCanceledException)
                        {
                            Interlocked.Exchange(ref wasCancelled, 1);
                            var failureCount = Interlocked.Increment(ref failed);
                            var processedCount = Interlocked.Increment(ref processed);

                            await RunOnUiThreadAsync(() =>
                            {
                                UpdatePreflightStatus(
                                    domain,
                                    PreflightStatus.Cancelled,
                                    "Cancelled"
                                );
                                UpdateProgress(processedCount, succeeded, failureCount, total);
                            });
                        }
                        catch (CfApiException ex)
                        {
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
        finally
        {
            IsRunning = false;
            CanCancel = false;
            UpdateCommandStates();

            ProgressText =
                wasCancelled == 1
                    ? $"Cancelled: {succeeded} created, {failed} not completed out of {total}"
                    : $"Done: {succeeded} created, {failed} failed out of {total}";

            StatusText = wasCancelled == 1 ? "Batch cancelled" : "Batch finished";
        }
    }

    [RelayCommand]
    private void CancelBatch()
    {
        if (!IsRunning)
            return;

        StatusText = "Cancelling batch...";
        CanCancel = false;
        _batchCts?.Cancel();
        App.Pool.Cancel();
    }

    private void UpdateProgress(int processed, int success, int failed, int total)
    {
        ProgressMaximum = total;
        ProgressValue = processed;
        ProgressText = $"{processed}/{total} processed - {success} created, {failed} failed";
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
        CanCreate = !IsBusy && !IsRunning && _domainsToCreate.Count > 0;
        CanCancel = IsRunning;
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
