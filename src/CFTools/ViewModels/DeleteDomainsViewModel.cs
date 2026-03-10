using System.Collections.ObjectModel;
using CFTools.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
            UpdateCanDelete();
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
        UpdateCanDelete();
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var zone in Zones)
            zone.IsSelected = false;
        UpdateCanDelete();
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selected = Zones.Where(z => z.IsSelected).ToList();
        if (selected.Count == 0)
            return;

        IsRunning = true;
        CanDelete = false;
        var total = selected.Count;
        var success = 0;
        var failed = 0;

        try
        {
            var tasks = new List<Task>();

            foreach (var zone in selected)
            {
                tasks.Add(
                    App.Pool.Add(async ct =>
                    {
                        await App.Api.DeleteZone(zone.Zone.Id, ct);

                        Interlocked.Increment(ref success);
                        zone.IsDeleted = true;
                        UpdateDeleteProgress(success, failed, total);

                        return zone.Zone.Id;
                    })
                );
            }

            await Task.WhenAll(tasks);
        }
        catch (CfApiException)
        {
            Interlocked.Increment(ref failed);
            UpdateDeleteProgress(success, failed, total);
        }
        catch (Exception)
        {
            Interlocked.Increment(ref failed);
            UpdateDeleteProgress(success, failed, total);
        }
        finally
        {
            IsRunning = false;
            ProgressText = $"Done: {success} deleted, {failed} failed out of {total}";
        }
    }

    public void UpdateCanDelete()
    {
        CanDelete = Zones.Any(z => z.IsSelected) && !IsRunning;
    }

    private void UpdateDeleteProgress(int success, int failed, int total)
    {
        ProgressText = $"Progress: {success + failed}/{total} ({success} ok, {failed} failed)";
    }
}
