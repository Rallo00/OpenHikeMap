using System.IO;
using System.Windows;

namespace OpenHikeMap;

internal sealed class DesktopApplication : Application
{
    // Preserve the existing WebView profile and browser storage across the application rename.
    internal static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenHikeMapWeb");
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.FirstOrDefault() == "--apply-update")
        {
            var result = Services.ApplicationUpdateInstaller.RunHelperAsync(args).GetAwaiter().GetResult();
            if (!result.Success)
            {
                var text = new Models.LocalizationService(); text.SetLanguage(result.Language);
                UpdateWindow.Message(null, text, text["UpdateFailed"]); Environment.ExitCode = 1;
            }
            return;
        }
        using var instance = new Mutex(true, @"Local\OpenHikeMapWeb.Desktop", out var first);
        if (!first) { MessageBox.Show("OpenHikeMap è già in esecuzione.", "OpenHikeMap", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var app = new DesktopApplication { ShutdownMode = ShutdownMode.OnMainWindowClose, _arguments = args };
        app.Run();
    }
    private string[] _arguments = [];
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new DesktopWindow(); MainWindow = window; window.Show();
        try
        {
            await window.StartAsync();
            Services.ApplicationUpdateInstaller.CompleteRestart(_arguments);
            if (!_arguments.Contains("--skip-update-check") && !window.IsStopping) window.BeginUpdateCheck();
        }
        catch (OperationCanceledException) when (window.IsStopping) { }
        catch (Exception ex) when (window.IsStopping) { Services.StartupDiagnostics.Write("Startup interrupted by application shutdown.", ex); }
        catch (Exception ex)
        {
            Directory.CreateDirectory(DataDirectory);
            File.AppendAllText(Path.Combine(DataDirectory, "startup.log"), $"{DateTimeOffset.Now:O} {ex}\n");
            MessageBox.Show(window, $"Impossibile avviare OpenHikeMap.\n{ex.Message}\n\nDettagli: {Path.Combine(DataDirectory, "startup.log")}", "OpenHikeMap", MessageBoxButton.OK, MessageBoxImage.Error);
            window.Close();
        }
    }
}
