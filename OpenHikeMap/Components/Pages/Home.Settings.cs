using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using OpenHikeMap.Models;
using OpenHikeMap.Services;

namespace OpenHikeMap.Components.Pages;

public partial class Home
{
    private static readonly (string Id, string Label)[] SettingsTabs = [("general", "General"), ("measurement", "Measurement"), ("display", "Display"), ("api", "Api"), ("backup", "Backup")];
    private string SettingsTab = "general", ApiKeyDraft = "", ApiKeyStatusKey = "", SettingsFeedbackKey = "";
    private bool ShowApiKey;
    private string S(string key) => L["SettingsUi" + key];

    private async Task ChangeSettingsPreferences(Func<Task> action)
    {
        SettingsFeedbackKey = "";
        try { Data.Validate(); await action(); }
        catch (InvalidDataException) { SettingsFeedbackKey = "Invalid"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JSException)
        { SettingsFeedbackKey = "SaveError"; }
    }
    private Task RefreshSettingsPreferences() => ChangeSettingsPreferences(async () => { await Save(); await RenderMap(); });
    private Task ApplySettingsPreferences() => ChangeSettingsPreferences(ApplySettings);
    private Task ChangeSettingsUnits() => ChangeSettingsPreferences(ChangeUnits);

    private async Task UseMapAsHome()
    {
        if (!Ready || Module is null) return;
        var center = await Module.InvokeAsync<double[]>("getCenter");
        Data.Settings.DefaultLatitude = center[0];
        Data.Settings.DefaultLongitude = center[1];
        await AutoSaveSettings();
    }

    private async Task SetDefaultApiKey()
    {
        ApiKeyDraft = OpenRouteServiceCredentials.DefaultApiKey;
        ApiKeyStatusKey = "ApiPending";
        await JS.InvokeVoidAsync("alert", S("DefaultWarning"));
    }

    private void SaveApiKey()
    {
        try
        {
            OpenRouteServiceCredentials.Save(ApiKeyDraft);
            ApiKeyDraft = OpenRouteServiceCredentials.Load(Configuration);
            ShowApiKey = false;
            ApiKeyStatusKey = "ApiSaved";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ApiKeyStatusKey = "ApiError";
        }
    }

    private async Task ExportFullBackup()
    {
        if (Busy || IsRouting || Module is null) return;
        Busy = true;
        try
        {
            await Module.InvokeVoidAsync("download", "openhikemap-full-backup.json", FullBackupService.Serialize(Data), "application/json");
            SettingsFeedbackKey = "BackupDone";
        }
        catch (Exception ex) when (ex is InvalidDataException or JSException) { SettingsFeedbackKey = "BackupError"; }
        finally { Busy = false; }
    }

    private async Task RestoreFullBackup(InputFileChangeEventArgs e)
    {
        if (Busy || IsRouting) return;
        Busy = true;
        try
        {
            await using var input = e.File.OpenReadStream(32 * 1024 * 1024, Lifetime.Token);
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, Lifetime.Token); buffer.Position = 0;
            var plan = TransferService.Parse(e.File.Name, buffer, Data, Gpx);
            if (plan.Kind == ImportKind.Route) throw new InvalidDataException("Not a backup.");
            var restored = plan.Data;
            var confirmation = plan.Kind == ImportKind.FullBackup ? S("RestoreConfirm") : L["ImportBackupConfirm"];
            if (!await JS.InvokeAsync<bool>("confirm", confirmation)) return;
            await PersistSettingsSession(restored);
            WeatherRequest?.Cancel(); AlertsRequest?.Cancel();
            Data = restored; DiskSettingsWritable = true; StorageWritable = true;
            SelectedPlace = null; SelectedFolder = null; CollapsedFolders.Clear();
            Forecast = null; AutomaticWeatherPoint = RouteMidpoint.Find(Data.RouteGeometry);
            L.SetLanguage(Data.Settings.LanguageCode);
            await Save(); await RenderMap();
            if (Path.Count > 0) await FitRoute();
            else await Center(Data.Settings.DefaultLatitude, Data.Settings.DefaultLongitude);
            SettingsFeedbackKey = "RestoreDone";
        }
        catch (OperationCanceledException) when (Lifetime.IsCancellationRequested) { }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ImportFormatException or IOException or UnauthorizedAccessException or JSException)
        { SettingsFeedbackKey = "RestoreError"; }
        finally { Busy = false; }
        await Task.WhenAll(RefreshCivilAlertsAsync(), WeatherPoint is not null ? UpdateWeatherAsync() : Task.CompletedTask);
    }

    private async Task PersistSettingsSession(WebSession snapshot)
    {
        snapshot.Validate();
        if (Module is null) throw new IOException("Browser storage is unavailable.");
        static string RouteJson(WebSession session) => JsonSerializer.Serialize(new
        {
            session.RoutePoints, session.RouteGeometry, session.RouteKind,
            Settings = new { session.Settings.WeatherReferenceLatitude, session.Settings.WeatherReferenceLongitude }
        }, Json);
        if (!await Module.InvokeAsync<bool>("writeRouteStorage", RouteJson(snapshot)))
            throw new IOException("Cannot save the route.");
        try { await SettingsStore.SaveAsync(snapshot, Lifetime.Token); }
        catch
        {
            await Module.InvokeAsync<bool>("writeRouteStorage", RouteJson(Data));
            throw;
        }
    }

    private async Task ResetSettings()
    {
        if (Busy || IsRouting || !await JS.InvokeAsync<bool>("confirm", S("ResetConfirm"))) return;
        Busy = true;
        try
        {
            var defaults = new AppSettings();
            defaults.WeatherReferenceLatitude = Data.Settings.WeatherReferenceLatitude;
            defaults.WeatherReferenceLongitude = Data.Settings.WeatherReferenceLongitude;
            var snapshot = new WebSession { Settings = defaults, Folders = Data.Folders, Places = Data.Places };
            await SettingsStore.SaveAsync(snapshot, Lifetime.Token);
            OpenRouteServiceCredentials.Save(OpenRouteServiceCredentials.DefaultApiKey);
            Data.Settings = defaults; DiskSettingsWritable = true;
            ApiKeyDraft = OpenRouteServiceCredentials.DefaultApiKey; ShowApiKey = false; ApiKeyStatusKey = "";
            L.SetLanguage(defaults.LanguageCode);
            await Save(); await RenderMap();
            SettingsFeedbackKey = "ResetDone";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JSException or InvalidDataException)
        { SettingsFeedbackKey = "ResetError"; }
        finally { Busy = false; }
        await Task.WhenAll(RefreshCivilAlertsAsync(), WeatherPoint is not null ? UpdateWeatherAsync() : Task.CompletedTask);
    }
}
