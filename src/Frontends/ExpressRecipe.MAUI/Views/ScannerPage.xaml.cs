using ExpressRecipe.MAUI.ViewModels;
using ZXing.Net.Maui;

namespace ExpressRecipe.MAUI.Views;

public partial class ScannerPage : ContentPage
{
    private readonly ScannerViewModel _viewModel;

    public ScannerPage(ScannerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Reset scanner when page appears
        _viewModel.ResetScannerCommand.Execute(null);
    }

    private async void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        await _viewModel.OnBarcodeDetectedAsync(e);
    }

    private async void OnManualSearchClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//search");
    }
}
