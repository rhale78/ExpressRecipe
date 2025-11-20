using ExpressRecipe.MAUI.ViewModels;

namespace ExpressRecipe.MAUI.Views;

public partial class MealPlanPage : ContentPage
{
    public MealPlanPage(MealPlanPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
