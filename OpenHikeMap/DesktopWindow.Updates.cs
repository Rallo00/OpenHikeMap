using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using OpenHikeMap.Models;
using OpenHikeMap.Services;

namespace OpenHikeMap;

internal sealed partial class DesktopWindow
{
    private Task? _updateCheck;
    internal void BeginUpdateCheck() => _updateCheck ??= CheckForUpdateAsync();

    private async Task CheckForUpdateAsync()
    {
        var token = _lifetime.Token; var language = new AppSettings().LanguageCode; var text = new LocalizationService(); text.SetLanguage(language);
        try
        {
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            var service = new ApplicationUpdateService(client);
            var update = await service.CheckAsync(ApplicationInfo.InstalledVersion, token);
            if (update is null || IsStopping) return;
            if (_server is not null)
            {
                try { var stored = await _server.Services.GetRequiredService<AppSettingsStore>().LoadAsync(token); language = stored?.Settings.LanguageCode ?? language; text.SetLanguage(language); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException) { StartupDiagnostics.Write("Update language preferences unavailable.", ex); }
            }
            var version = ApplicationInfo.DisplayVersion(update.Version);
            if (update.Asset is null)
            {
                if (UpdateWindow.Ask(this, text, string.Format(text["UpdateNoAsset"], version), "UpdateOpenRelease"))
                    Process.Start(new ProcessStartInfo(ApplicationUpdateService.ReleasesPage) { UseShellExecute = true });
                return;
            }
            if (!UpdateWindow.Ask(this, text, string.Format(text["UpdateAvailable"], version, ApplicationInfo.Version)) || IsStopping) return;
            string target;
            try { target = ApplicationUpdateInstaller.RunningExecutable(); }
            catch (Exception ex) { StartupDiagnostics.Write("Executable cannot be updated in place.", ex); UpdateWindow.Message(this, text, text["UpdatePermission"]); return; }
            string stage;
            try { stage = ApplicationUpdateInstaller.CreateStage(); }
            catch (Exception ex) { StartupDiagnostics.Write("Update staging is unavailable.", ex); UpdateWindow.Message(this, text, text["UpdateFailed"]); return; }
            var handedOff = false; var errorKey = "UpdateFailed";
            try
            {
                var jobPath = await UpdateWindow.ProgressAsync(this, text, version, async (progress, cancellation) =>
                {
                    var download = await service.DownloadAsync(update, stage, progress, cancellation);
                    progress.Report(new(null, Installing: true));
                    if (!await SaveBeforeUpdateAsync(cancellation)) { errorKey = "UpdateSaveFailed"; throw new IOException("Current application data could not be saved."); }
                    var job = await Task.Run(() => ApplicationUpdateInstaller.PrepareAsync(stage, target, download, update.Version, language, cancellation), cancellation);
                    await ApplicationUpdateInstaller.LaunchAsync(job, cancellation);
                    return job;
                }, token);
                if (jobPath is null || IsStopping) return;
                ApplicationUpdateInstaller.Commit(jobPath); handedOff = true; Close();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex) { StartupDiagnostics.Write("Update download or handoff failed.", ex); if (!IsStopping) UpdateWindow.Message(this, text, text[errorKey]); }
            finally { if (!handedOff) await ApplicationUpdateInstaller.CleanupStageAsync(stage); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { StartupDiagnostics.Write("Automatic update check unavailable; normal startup continues.", ex); }
    }

    private async Task<bool> SaveBeforeUpdateAsync(CancellationToken token)
    {
        var core = _browser.CoreWebView2; if (core is null) return false;
        var id = Guid.NewGuid().ToString("N"); var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Received(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (!IsLocal(e.Source)) return;
            try
            {
                using var json = JsonDocument.Parse(e.WebMessageAsJson); var root = json.RootElement;
                if (root.TryGetProperty("type", out var type) && type.GetString() == "openhikemap-update-saved" && root.TryGetProperty("id", out var value) && value.GetString() == id)
                    result.TrySetResult(root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True);
            }
            catch (JsonException) { }
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        core.WebMessageReceived += Received;
        try
        {
            var script = $"Boolean(window.openHikeMapSaveForUpdate?.({JsonSerializer.Serialize(id)}))";
            while (await core.ExecuteScriptAsync(script).WaitAsync(deadline.Token) != "true") await Task.Delay(100, deadline.Token);
            return await result.Task.WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return false; }
        finally { core.WebMessageReceived -= Received; }
    }
}
