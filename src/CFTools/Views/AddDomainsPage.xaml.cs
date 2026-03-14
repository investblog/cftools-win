using System.ComponentModel;
using CFTools.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CFTools.Views;

public sealed partial class AddDomainsPage : Page
{
    private const double WideLayoutMinWidth = 720;

    public AddDomainsViewModel ViewModel { get; } = new();

    public AddDomainsPage()
    {
        this.InitializeComponent();
        TwoColumnGrid.SizeChanged += TwoColumnGrid_SizeChanged;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AddDomainsViewModel.IsNextStepCreate))
        {
            var accentStyle = (Style)Application.Current.Resources["AccentButtonStyle"];

            if (ViewModel.IsNextStepCreate)
            {
                CheckBtn.Style = null!;
                CreateBtn.Style = accentStyle;
            }
            else
            {
                CheckBtn.Style = accentStyle;
                CreateBtn.Style = null!;
            }
        }
    }

    private void GoToAuth_Click(object sender, RoutedEventArgs e) =>
        App.RequestNavigateToAuth();

    private void TwoColumnGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width >= WideLayoutMinWidth)
        {
            TwoColumnGrid.ColumnSpacing = 16;
            TwoColumnGrid.RowSpacing = 0;
            ResultsColumn.Width = new GridLength(1, GridUnitType.Star);
            ResultsRow.Height = new GridLength(0);
            Grid.SetRow(ResultsList, 0);
            Grid.SetColumn(ResultsList, 1);
        }
        else
        {
            TwoColumnGrid.ColumnSpacing = 0;
            TwoColumnGrid.RowSpacing = 12;
            ResultsColumn.Width = new GridLength(0);
            ResultsRow.Height = new GridLength(1, GridUnitType.Star);
            Grid.SetRow(ResultsList, 1);
            Grid.SetColumn(ResultsList, 0);
        }
    }
}