using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenHikeMap.Models;
using OpenHikeMap.Services;

namespace OpenHikeMap;

internal sealed class UpdateWindow : Window
{
    private readonly StackPanel _content = new() { Margin = new Thickness(24) };
    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 22 };

    private UpdateWindow(Window? owner, LocalizationService text)
    {
        if (owner is not null) { Owner = owner; Icon = owner.Icon; }
        Title = text["UpdateTitle"]; Width = 510; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(248, 249, 245)); Foreground = new SolidColorBrush(Color.FromRgb(37, 60, 48));
        ShowInTaskbar = owner is null; _content.Children.Add(_message); Content = _content;
    }
    private void AddButton(string label, bool result)
    {
        var button = new Button { Content = label, MinWidth = 105, Height = 25, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(8, 0, 0, 0), IsCancel = !result };
        button.Click += (_, _) => DialogResult = result; _buttons.Children.Add(button);
    }
    internal static bool Ask(Window owner, LocalizationService text, string message, string acceptKey = "UpdateYes")
    {
        var dialog = new UpdateWindow(owner, text); dialog._message.Text = message;
        dialog.AddButton(text["UpdateNo"], false); dialog.AddButton(text[acceptKey], true); dialog._content.Children.Add(dialog._buttons);
        return dialog.ShowDialog() == true;
    }
    internal static void Message(Window? owner, LocalizationService text, string message)
    {
        var dialog = new UpdateWindow(owner, text); dialog._message.Text = message; dialog.AddButton(text["CloseWindow"], false); dialog._content.Children.Add(dialog._buttons); dialog.ShowDialog();
    }
    internal static async Task<T?> ProgressAsync<T>(Window owner, LocalizationService text, string version, Func<IProgress<UpdateDownloadProgress>, CancellationToken, Task<T>> action, CancellationToken lifetime) where T : class
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        var dialog = new UpdateWindow(owner, text); dialog._message.Text = string.Format(text["UpdateDownload"], version);
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 16, IsIndeterminate = true, Margin = new Thickness(0, 16, 0, 0) };
        var percent = new TextBlock { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 5, 0, 0) };
        dialog._content.Children.Add(bar); dialog._content.Children.Add(percent); dialog.AddButton(text["UpdateCancel"], false); dialog._content.Children.Add(dialog._buttons);
        T? result = null; Exception? error = null; var closed = false; Task work = Task.CompletedTask;
        var progress = new Progress<UpdateDownloadProgress>(value => dialog.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (closed) return;
            if (value.Installing) dialog._message.Text = text["UpdateInstalling"];
            else if (value.Verifying) dialog._message.Text = text["UpdateVerifying"];
            bar.IsIndeterminate = value.Percent is null;
            if (value.Percent is { } number) { bar.Value = number; percent.Text = $"{number:F0}%"; }
        })));
        async Task RunAsync()
        {
            try { result = await action(progress, cancellation.Token); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception ex) { error = ex; }
            finally { if (!closed) dialog.DialogResult = result is not null; }
        }
        dialog.Loaded += (_, _) => work = RunAsync();
        dialog.Closed += (_, _) => { closed = true; if (result is null) cancellation.Cancel(); };
        using var registration = lifetime.Register(() => dialog.Dispatcher.BeginInvoke(new Action(() => { if (!closed) dialog.Close(); })));
        dialog.ShowDialog(); await work;
        if (error is not null) throw error;
        return cancellation.IsCancellationRequested ? null : result;
    }
}
