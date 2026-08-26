using System.Net.Http;
using System.Windows;
namespace OpenHikeMap;
public partial class SettingsWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly SearchService _searchService = new(new HttpClient());
    private SearchResult? _lastDefaultCenterResult;
    public SettingsWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        ApplyUnitPreset(_viewModel.Settings.UnitPreset);
    }
    private async void SearchDefaultCenter_Click(object sender, RoutedEventArgs e)
    {
        var query = DefaultCenterSearchTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }
        try
        {
            var results = await _searchService.SearchAsync(query, _viewModel.Settings.DefaultLatitude, _viewModel.Settings.DefaultLongitude);
            var first = results.FirstOrDefault();
            if (first is null)
            {
                return;
            }
            _lastDefaultCenterResult = first;
            DefaultCenterPreviewTextBlock.Text = first.DisplayName;
        }
        catch
        {
        }
    }
    private async void SetDefaultCenterHome_Click(object sender, RoutedEventArgs e)
    {
        if (_lastDefaultCenterResult is null)
        {
            SearchDefaultCenter_Click(sender, e);
            if (_lastDefaultCenterResult is null)
            {
                return;
            }
        }
        _viewModel.Settings.DefaultLatitude = _lastDefaultCenterResult.Latitude;
        _viewModel.Settings.DefaultLongitude = _lastDefaultCenterResult.Longitude;
        await Dispatcher.InvokeAsync(() => DefaultCenterPreviewTextBlock.Text = _lastDefaultCenterResult.DisplayName);
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
    private void UnitPreset_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ApplyUnitPreset(_viewModel.Settings.UnitPreset);
    }
    private void ApplyUnitPreset(string? preset)
    {
        switch (preset)
        {
            case "Metric":
                _viewModel.Settings.IsMetric = true;
                _viewModel.Settings.DistanceUnit = "km";
                _viewModel.Settings.AltitudeUnit = "m";
                _viewModel.Settings.SpeedUnit = "km/h";
                _viewModel.Settings.TravelSpeed = 2.0;
                _viewModel.Settings.PressureUnit = "hPa";
                _viewModel.Settings.TemperatureUnit = "C";
                break;
            case "Imperial":
                _viewModel.Settings.IsMetric = false;
                _viewModel.Settings.DistanceUnit = "mi";
                _viewModel.Settings.AltitudeUnit = "ft";
                _viewModel.Settings.SpeedUnit = "mph";
                _viewModel.Settings.TravelSpeed = 1.2;
                _viewModel.Settings.PressureUnit = "inHg";
                _viewModel.Settings.TemperatureUnit = "F";
                break;
            case "Custom":
                break;
            default:
                _viewModel.Settings.UnitPreset = "Metric";
                ApplyUnitPreset("Metric");
                break;
        }
    }
}
