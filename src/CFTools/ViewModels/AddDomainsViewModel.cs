using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CFTools.Models;
using CFTools.Services;

namespace CFTools.ViewModels;

public partial class AddDomainsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _domainInput = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canCreate;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _progressText = string.Empty;

    public ObservableCollection<PreflightEntry> PreflightResults { get; } = new();

    private readonly List<string> _domainsToCreate = new();

    [RelayCommand]
    private async Task CheckAsync()
    {
        if (string.IsNullOrWhiteSpace(DomainInput))
            return;

        if (!App.Api.IsConfigured)
        {
            StatusText = "Connect to Cloudflare first";
            return;
        }

        IsBusy = true;
        PreflightResults.Clear();
        _domainsToCreate.Clear();
        CanCreate = false;
        StatusText = "Parsing domains...";

        try
        {
            var parsed = DomainParser.Parse(DomainInput, rootOnly: true);

            if (parsed.Domains.Count == 0)
            {
                StatusText = "No valid domains found";
                return;
            }

            StatusText = $"Checking {parsed.Domains.Count} domains against Cloudflare...";

            var willCreate = 0;
            var exists = 0;

            foreach (var domain in parsed.Domains)
            {
                var (zoneExists, zoneId) = await App.Api.CheckZoneExists(domain);

                if (zoneExists)
                {
                    PreflightResults.Add(new PreflightEntry(domain, PreflightStatus.Exists, zoneId));
                    exists++;
                }
                else
                {
                    PreflightResults.Add(new PreflightEntry(domain, PreflightStatus.WillCreate));
                    _domainsToCreate.Add(domain);
                    willCreate++;
                }
            }

            // Add duplicates as info
            foreach (var dup in parsed.Duplicates)
                PreflightResults.Add(new PreflightEntry(dup, PreflightStatus.Duplicate));

            // Add invalid as info
            foreach (var inv in parsed.Invalid)
                PreflightResults.Add(new PreflightEntry(inv, PreflightStatus.Invalid));

            CanCreate = willCreate > 0;
            StatusText = $"{willCreate} to create, {exists} already exist, {parsed.Duplicates.Count} duplicates";
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
        }
    }

    [RelayCommand]
    private async Task CreateAllAsync()
    {
        if (_domainsToCreate.Count == 0 || App.CurrentAccountId is null)
            return;

        IsRunning = true;
        CanCreate = false;
        var accountId = App.CurrentAccountId;
        var total = _domainsToCreate.Count;
        var success = 0;
        var failed = 0;

        try
        {
            var tasks = new List<Task>();

            foreach (var domain in _domainsToCreate)
            {
                tasks.Add(App.Pool.Add(async ct =>
                {
                    await App.Api.CreateZone(domain, accountId, ct: ct);

                    Interlocked.Increment(ref success);
                    UpdateProgress(success, failed, total);

                    // Update preflight entry status
                    UpdatePreflightStatus(domain, PreflightStatus.Exists);

                    return domain;
                }));
            }

            await Task.WhenAll(tasks);
        }
        catch (CfApiException)
        {
            Interlocked.Increment(ref failed);
            UpdateProgress(success, failed, total);
        }
        catch (Exception)
        {
            Interlocked.Increment(ref failed);
            UpdateProgress(success, failed, total);
        }
        finally
        {
            IsRunning = false;
            ProgressText = $"Done: {success} created, {failed} failed out of {total}";
        }
    }

    private void UpdateProgress(int success, int failed, int total)
    {
        ProgressText = $"Progress: {success + failed}/{total} ({success} ok, {failed} failed)";
    }

    private void UpdatePreflightStatus(string domain, PreflightStatus newStatus)
    {
        for (int i = 0; i < PreflightResults.Count; i++)
        {
            if (PreflightResults[i].Domain == domain)
            {
                PreflightResults[i] = PreflightResults[i] with { Status = newStatus };
                break;
            }
        }
    }
}
