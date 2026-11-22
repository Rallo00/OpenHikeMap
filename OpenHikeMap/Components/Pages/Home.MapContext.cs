using System.Globalization;
using Microsoft.JSInterop;
using OpenHikeMap.Models;
using OpenHikeMap.Services;

namespace OpenHikeMap.Components.Pages;

public partial class Home
{
    private CancellationTokenSource? WeatherRequest;
    private bool WeatherBusy;
    private List<EumetnetAlertArea> AlertAreas = [];
    private string EumetnetAlertSummary = "";
    private RoutePathPoint? AutomaticWeatherPoint;
    private RoutePathPoint? WeatherPoint => Data.Settings.WeatherReferenceLatitude is { } lat && Data.Settings.WeatherReferenceLongitude is { } lon
        ? new RoutePathPoint { Latitude = lat, Longitude = lon } : AutomaticWeatherPoint;

    private async Task ToggleBearing() { Data.Settings.ShowBearingCircle = !Data.Settings.ShowBearingCircle; await Refresh(); }
    private async Task ToggleEumetnetAlerts()
    {
        Data.Settings.ShowEumetnetAlerts = !Data.Settings.ShowEumetnetAlerts;
        await Save();
        ReformatTrailContext();
        await RenderMap();
    }

    private Task LoadWeather() => RefreshTrailWeatherAsync(force: true);

    private async Task RefreshTrailWeatherAsync(CancellationToken token = default, bool force = false)
    {
        if (WeatherPoint is null) return;
        if (LastAlertSnapshot is not null) UpdateEumetnetSummary(LastAlertSnapshot);
        await RenderMap();
        await UpdateWeatherAsync(token, force);
    }

    private EumetnetAlertSnapshot? LastAlertSnapshot;

    private void ClearTrailContext()
    {
        WeatherRequest?.Cancel();
        AutomaticWeatherPoint = null; Forecast = null; WeatherBusy = false;
        EumetnetAlertSummary = "";
        WeatherStatus = L["WeatherInitialPrompt"];
    }

    private void ReformatTrailContext()
    {
        if (Forecast is not null)
            Forecast = Weather.FormatCachedForecast(Data.Settings.LanguageCode, !Imperial, Data.Settings.TemperatureUnit);
        if (LastAlertSnapshot is not null) UpdateEumetnetSummary(LastAlertSnapshot);
        if (Forecast is null) WeatherStatus = L["WeatherInitialPrompt"];
    }

    private void UpdateEumetnetSummary(EumetnetAlertSnapshot snapshot)
    {
        if (Path.Count == 0 && WeatherPoint is null) { EumetnetAlertSummary = ""; return; }
        var matching = EumetnetAlertSelection.ForRoute(AlertAreas, Path, WeatherPoint, DateTimeOffset.UtcNow);
        EumetnetAlertSummary = snapshot.RequestedCountries == 0 ? L["EumetnetAlertNoCoverage"]
            : snapshot.FailedCountries == snapshot.RequestedCountries ? L["EumetnetAlertUnavailable"]
            : matching.Count > 0 ? $"{L["EumetnetAlertTitle"]}:\n\n{string.Join("\n\n────────────────────\n\n", matching.Select(FormatEumetnetWarning))}"
            : snapshot.IsComplete ? L["EumetnetAlertNone"] : L["EumetnetAlertPartial"];
        if (!snapshot.IsComplete && matching.Count > 0) EumetnetAlertSummary += "\n" + L["EumetnetAlertPartial"];
    }

    private async Task UpdateWeatherAsync(CancellationToken parent = default, bool force = false)
    {
        WeatherRequest?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(Lifetime.Token, parent);
        WeatherRequest = request;
        var token = request.Token;
        WeatherBusy = true; Forecast = null; WeatherStatus = L["WeatherLoading"];
        try
        {
            var point = WeatherPoint;
            if (point is null) { WeatherStatus = L["WeatherInitialPrompt"]; return; }
            await RenderMap();
            await InvokeAsync(StateHasChanged);
            var forecast = force ? null : Weather.FormatCachedForecastForPoint(point.Latitude, point.Longitude,
                Data.Settings.LanguageCode, !Imperial, Data.Settings.TemperatureUnit);
            forecast ??= await Weather.GetForecastAsync(point.Latitude, point.Longitude, Data.Settings.LanguageCode, !Imperial, Data.Settings.TemperatureUnit, token);
            token.ThrowIfCancellationRequested();
            Forecast = forecast;
            WeatherStatus = forecast is null ? L["WeatherUnavailable"] : "";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                WeatherStatus = L["WeatherUnavailable"];
                Logger.LogWarning("Previsioni meteo non disponibili: {Reason}", ex.Message);
            }
        }
        finally
        {
            if (ReferenceEquals(WeatherRequest, request))
            {
                WeatherRequest = null; WeatherBusy = false;
                if (!Lifetime.IsCancellationRequested) await InvokeAsync(StateHasChanged);
            }
        }
    }

    private string FormatEumetnetWarning(EumetnetAlertArea area)
    {
        var culture = CultureInfo.GetCultureInfo(Data.Settings.LanguageCode);
        string Date(DateTimeOffset value) => value.ToLocalTime().ToString("G", culture);
        return string.Join("\n\n", new[]
        {
            $"{area.Name}: {area.Event}",
            area.Details,
            $"{L["EumetnetAlertSender"]}: {area.Sender}",
            $"{L["EumetnetAlertIssued"]}: {Date(area.Issued)}",
            $"{L["EumetnetAlertOnset"]}: {Date(area.Onset)}",
            $"{L["EumetnetAlertExpires"]}: {(area.Expires is { } expires ? Date(expires) : L["EumetnetAlertNoExpiry"])}"
        });
    }

    private async Task LoadEumetnetAlertsAtStartupAsync()
    {
        var token = Lifetime.Token;
        try
        {
            EumetnetAlertSummary = L["EumetnetAlertLoading"];
            await InvokeAsync(StateHasChanged);
            var snapshot = await Eumetnet.LoadAllOnceAsync(Data.Settings.LanguageCode, HostLifetime.ApplicationStopping).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            AlertAreas = snapshot.Areas; LastAlertSnapshot = snapshot;
            UpdateEumetnetSummary(snapshot);
            await RenderMap();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || HostLifetime.ApplicationStopping.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                EumetnetAlertSummary = L["EumetnetAlertUnavailable"];
                Logger.LogWarning("EUMETNET startup warning load failed: {Reason}", ex.Message);
            }
        }
        finally
        {
            if (!token.IsCancellationRequested) await InvokeAsync(StateHasChanged);
        }
    }
}
