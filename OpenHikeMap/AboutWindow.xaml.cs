using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
namespace OpenHikeMap;
public partial class AboutWindow : Window
{
    public AboutWindow(string languageCode = "it-IT")
    {
        InitializeComponent();
        DataContext = new AboutWindowViewModel(languageCode);
    }
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
public sealed class AboutWindowViewModel
{
    public string WindowTitle { get; }
    public string DescriptionLine1 { get; }
    public string DescriptionLine2 { get; }
    public string CreditsLine { get; }
    public string CloseButtonText { get; }
    public string GitHubUrl { get; } = "https://github.com/Rallo00/";
    public AboutWindowViewModel(string languageCode)
    {
        var isEnglish = languageCode == "en-US";
        WindowTitle = isEnglish ? "About" : "Informazioni su";
        DescriptionLine1 = isEnglish
            ? "OpenHikeMap is an open-source project dedicated to hiking enthusiasts."
            : "OpenHikeMap è un progetto open source dedicato agli appassionati di escursionismo.";
        DescriptionLine2 = isEnglish
            ? "This project was developed with the help of AI and aims to replace several platforms that offer similar paid services."
            : "Questo progetto è stato sviluppato con l'aiuto dell'AI e punta a sostituire diverse piattaforme che offrono servizi simili a pagamento.";
        CreditsLine = isEnglish
            ? "This project has been created by Rallo and posted on GitHub."
            : "Questo progetto è stato creato da Rallo e pubblicato su GitHub.";
        CloseButtonText = isEnglish ? "Close" : "Chiudi";
    }
}
