using System.Globalization;
using System.Windows;
namespace OpenHikeMap;
public partial class AddPinCoordinatesWindow : Window
{
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public AddPinCoordinatesWindow(double defaultLatitude, double defaultLongitude)
    {
        InitializeComponent();
        LatitudeTextBox.Text = defaultLatitude.ToString(CultureInfo.InvariantCulture);
        LongitudeTextBox.Text = defaultLongitude.ToString(CultureInfo.InvariantCulture);
    }
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(LatitudeTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            latitude is < -90 or > 90)
        {
            MessageBox.Show(this, "La latitudine deve essere un numero valido compreso tra -90 e 90.", "Coordinate non valide", MessageBoxButton.OK, MessageBoxImage.Warning);
            LatitudeTextBox.Focus();
            LatitudeTextBox.SelectAll();
            return;
        }
        if (!double.TryParse(LongitudeTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
            longitude is < -180 or > 180)
        {
            MessageBox.Show(this, "La longitudine deve essere un numero valido compreso tra -180 e 180.", "Coordinate non valide", MessageBoxButton.OK, MessageBoxImage.Warning);
            LongitudeTextBox.Focus();
            LongitudeTextBox.SelectAll();
            return;
        }
        Latitude = latitude;
        Longitude = longitude;
        DialogResult = true;
        Close();
    }
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
