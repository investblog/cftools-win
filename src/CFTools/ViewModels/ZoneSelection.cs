using CommunityToolkit.Mvvm.ComponentModel;
using CFTools.Models;

namespace CFTools.ViewModels;

/// <summary>
/// Wraps a CfZone with selection and operation state for the UI.
/// </summary>
public partial class ZoneSelection : ObservableObject
{
    public CfZone Zone { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isPurged;

    [ObservableProperty]
    private bool _isDeleted;

    public ZoneSelection(CfZone zone) => Zone = zone;
}
