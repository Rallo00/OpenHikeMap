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
    private readonly Border _loading = new();
    private WebApplication? _server;
    private Task? _startup;
    private Task? _serverStartup;
    private bool _closed;
    internal bool IsStopping { get; private set; }

    internal DesktopWindow()
    {
        Title = ApplicationInfo.WindowTitle; Width = 1400; Height = 900; MinWidth = 850; MinHeight = 600; WindowState = WindowState.Maximized;
        Icon = BitmapDecoder.Create(new Uri("pack://application:,,,/Assets/OpenHikeMap.ico"), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames.MaxBy(frame => frame.PixelWidth)!;
        Background = new SolidColorBrush(Color.FromRgb(248, 249, 245));
        var brand = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new Image { Source = Icon, Width = 96, Height = 96, Margin = new Thickness(0, 0, 0, 18) });
        brand.Children.Add(new TextBlock { Text = "OpenHikeMap", FontSize = 30, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(49, 92, 67)), HorizontalAlignment = HorizontalAlignment.Center });
        brand.Children.Add(new TextBlock { Text = $"v{ApplicationInfo.Version}", FontSize = 15, Foreground = new SolidColorBrush(Color.FromRgb(89, 99, 86)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) });
        _loading.Background = Background; _loading.Child = brand;
        var layout = new Grid(); layout.Children.Add(_browser); layout.Children.Add(_loading); Content = layout;
    }

    internal Task StartAsync() => _startup ??= InitializeAsync();
    private async Task InitializeAsync()
    {
        var token = _lifetime.Token;
        var watch = Stopwatch.StartNew();
        _serverStartup = Task.Run(async () =>
        {
            var assets = WebAssets.Prepare(); token.ThrowIfCancellationRequested();
            _server = WebApplicationHost.Create(assets);
            await _server.StartAsync(token);
        }, token);
        var environmentStartup = CoreWebView2Environment.CreateAsync(null, Path.Combine(DesktopApplication.DataDirectory, "Profile"));
        await Task.WhenAll(_serverStartup, environmentStartup).WaitAsync(token);
        var environment = await environmentStartup;
        token.ThrowIfCancellationRequested();
        await _browser.EnsureCoreWebView2Async(environment).WaitAsync(token); token.ThrowIfCancellationRequested();
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
        base.OnClosing(e);
        if (_closed || e.Cancel) return;
        e.Cancel = true; if (IsStopping) return;
        var watch = Stopwatch.StartNew();
        IsStopping = true; IsEnabled = false; _lifetime.Cancel();
        try
        {
            // Only wait for the local host; WebView initialization may still be pending when the user closes.
            try { if (_serverStartup is not null) await _serverStartup; } catch (OperationCanceledException) { }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            // Host lifetime callbacks must run off the WPF dispatcher to avoid a synchronous shutdown stall.
            var stopping = _server is { } server ? Task.Run(() => server.StopAsync(timeout.Token)) : Task.CompletedTask;
            StartupDiagnostics.Write($"Desktop shutdown: local host stop requested at {watch.ElapsedMilliseconds} ms.");
            _browser.Dispose();
            StartupDiagnostics.Write($"Desktop shutdown: browser disposed at {watch.ElapsedMilliseconds} ms.");
            await stopping;
            StartupDiagnostics.Write($"Desktop shutdown: local host stopped at {watch.ElapsedMilliseconds} ms.");
        }
        catch (OperationCanceledException) { StartupDiagnostics.Write("Local host graceful shutdown deadline reached."); }
        catch (Exception ex) { StartupDiagnostics.Write("Local host shutdown failed.", ex); }
        finally
        {
            _browser.Dispose();
            try { if (_server is { } server) await Task.Run(async () => await server.DisposeAsync()); }
            catch (Exception ex) { StartupDiagnostics.Write("Local host disposal failed.", ex); }
            _lifetime.Dispose(); _closed = true;
            StartupDiagnostics.Write($"Desktop shutdown completed in {watch.ElapsedMilliseconds} ms.");
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
    }
}
