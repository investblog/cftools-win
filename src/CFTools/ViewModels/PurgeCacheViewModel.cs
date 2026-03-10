using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CFTools.Models;

namespace CFTools.ViewModels;

public partial class PurgeCacheViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private bool _canPurge;

    [ObservableProperty]
    private string _filterText = string.Empty;

    public ObservableCollection<ZoneSelection> Zones { get; } = new();

    [RelayCommand]
    private async Task LoadZonesAsync()
    {
        if (!App.Api.IsConfigured || App.CurrentAccountId is null)
        {
            StatusText = "Connect to Cloudflare first";
            return;
        }

        IsBusy = true;
        Zones.Clear();
        StatusText = "Loading zones...";

        try
        {
            var zones = await App.Api.ListAllZones(App.CurrentAccountId);

            foreach (var zone in zones.OrderBy(z => z.Name))
            {
                Zones.Add(new ZoneSelection(zone));
            }

            StatusText = $"{zones.Count} zones loaded";
            UpdateCanPurge();
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
    private void SelectAll()
    {
        foreach (var zone in Zones)
            zone.IsSelected = true;
        UpdateCanPurge();
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var zone in Zones)
            zone.IsSelected = false;
        UpdateCanPurge();
    }

    [RelayCommand]
    private async Task PurgeSelectedAsync()
    {
        var selected = Zones.Where(z => z.IsSelected).ToList();
        if (selected.Count == 0) return;

        IsRunning = true;
        CanPurge = false;
        var total = selected.Count;
        var success = 0;
        var failed = 0;

        try
        {
            var tasks = new List<Task>();

            foreach (var zone in selected)
            {
                tasks.Add(App.Pool.Add(async ct =>
                {
                    await App.Api.PurgeCacheEverything(zone.Zone.Id, ct);

                    Interlocked.Increment(ref success);
                    zone.IsPurged = true;
                    UpdatePurgeProgress(success, failed, total);

                    return zone.Zone.Id;
                }));
            }

            await Task.WhenAll(tasks);
        }
        catch (CfApiException)
        {
            Interlocked.Increment(ref failed);
            UpdatePurgeProgress(success, failed, total);
        }
        catch (Exception)
        {
            Interlocked.Increment(ref failed);
            UpdatePurgeProgress(success, failed, total);
        }
        finally
        {
            IsRunning = false;
            ProgressText = $"Done: {success} purged, {failed} failed out of {total}";
        }
    }

    public void UpdateCanPurge()
    {
        CanPurge = Zones.Any(z => z.IsSelected) && !IsRunning;
    }

    private void UpdatePurgeProgress(int success, int failed, int total)
    {
        ProgressText = $"Progress: {success + failed}/{total} ({success} ok, {failed} failed)";
    }
}

/// <summary>
/// Wraps a CfZone with selection state for the UI.
/// </summary>
public partial class ZoneSelection : ObservableObject
{
    public CfZone Zone { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isPurged;

    public ZoneSelection(CfZone zone) => Zone = zone;
}
