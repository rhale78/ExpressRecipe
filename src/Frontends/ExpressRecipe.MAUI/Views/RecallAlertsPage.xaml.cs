using ExpressRecipe.MAUI.ViewModels;

namespace ExpressRecipe.MAUI.Views;

public partial class RecallAlertsPage : ContentPage
{
    public RecallAlertsPage(RecallAlertsPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
