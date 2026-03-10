using CFTools.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CFTools.ViewModels;

/// <summary>
/// Wraps a CfZone with selection and operation state for the UI.
/// </summary>
public partial class ZoneSelection : ObservableObject
{
    public CfZone Zone { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsPurged { get; set; }

    [ObservableProperty]
    public partial bool IsDeleted { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    public ZoneSelection(CfZone zone) => Zone = zone;
}
