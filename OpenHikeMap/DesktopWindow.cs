using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using OpenHikeMap.Services;

namespace OpenHikeMap;

internal sealed class DesktopWindow : Window
{
    private const string Address = "http://127.0.0.1:5186/";
    private readonly CancellationTokenSource _lifetime = new();
    private readonly WebView2 _browser = new();
    private readonly TextBlock _loading = new() { Text = "Avvio di OpenHikeMap...", FontSize = 18, Foreground = new SolidColorBrush(Color.FromRgb(49, 92, 67)), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private WebApplication? _server;
    private Task? _startup;
    private bool _closed;
    internal bool IsStopping { get; private set; }

    internal DesktopWindow()
    {
        Title = ApplicationInfo.WindowTitle; Width = 1400; Height = 900; MinWidth = 850; MinHeight = 600; WindowState = WindowState.Maximized;
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/OpenHikeMap.ico"));
        Background = new SolidColorBrush(Color.FromRgb(248, 249, 245));
        var layout = new Grid(); layout.Children.Add(_browser); layout.Children.Add(_loading); Content = layout;
    }

    internal Task StartAsync() => _startup ??= InitializeAsync();
    private async Task InitializeAsync()
    {
        var token = _lifetime.Token;
        var watch = Stopwatch.StartNew();
        var serverStartup = Task.Run(async () =>
        {
            var assets = WebAssets.Prepare(); token.ThrowIfCancellationRequested();
            _server = WebApplicationHost.Create(assets);
            await _server.StartAsync(token);
        }, token);
        var environmentStartup = CoreWebView2Environment.CreateAsync(null, Path.Combine(DesktopApplication.DataDirectory, "Profile"));
        await Task.WhenAll(serverStartup, environmentStartup);
        var environment = await environmentStartup;
        token.ThrowIfCancellationRequested();
        await _browser.EnsureCoreWebView2Async(environment); token.ThrowIfCancellationRequested();
        var core = _browser.CoreWebView2;
        core.ProcessFailed += (_, e) => StartupDiagnostics.Write($"WebView2 process failure: {e.ProcessFailedKind}.");
        core.Settings.IsStatusBarEnabled = false; core.Settings.AreDefaultContextMenusEnabled = false; core.Settings.AreDevToolsEnabled = false;
        core.NavigationStarting += (_, e) => { if (IsLocal(e.Uri)) return; e.Cancel = true; if (e.IsUserInitiated) OpenLink(e.Uri); };
        core.NewWindowRequested += (_, e) => { e.Handled = true; if (e.IsUserInitiated) OpenLink(e.Uri); };
        core.DownloadStarting += OnDownloadStarting;
        core.NavigationCompleted += (_, e) =>
        {
            _loading.Visibility = Visibility.Collapsed;
            if (!e.IsSuccess && !IsStopping) MessageBox.Show(this, $"Impossibile caricare l'interfaccia: {e.WebErrorStatus}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
        };
        _browser.Source = new Uri(Address);
        StartupDiagnostics.Write($"Desktop shell ready in {watch.ElapsedMilliseconds} ms; WebView2 {environment.BrowserVersionString}.");
    }
    private static bool IsLocal(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "http" && uri.Host == "127.0.0.1" && uri.Port == 5186;
    private static void OpenLink(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        using var deferral = e.GetDeferral(); e.Handled = true;
        var dialog = new SaveFileDialog { FileName = Path.GetFileName(e.ResultFilePath), Filter = "Tutti i file (*.*)|*.*", OverwritePrompt = true };
        if (dialog.ShowDialog(this) == true) e.ResultFilePath = dialog.FileName; else e.Cancel = true;
    }
    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_closed) { base.OnClosing(e); return; }
        e.Cancel = true; if (IsStopping) return;
        IsStopping = true; IsEnabled = false; _lifetime.Cancel();
        try { if (_startup is not null) await _startup; } catch { }
        _browser.Dispose();
        if (_server is not null)
        {
            try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await _server.StopAsync(timeout.Token); }
            catch (OperationCanceledException) { StartupDiagnostics.Write("Local host shutdown timed out."); }
            finally { await _server.DisposeAsync(); }
        }
        _lifetime.Dispose(); _closed = true;
        _ = Dispatcher.BeginInvoke(new Action(Close));
    }
}
