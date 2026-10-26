using Microsoft.JSInterop;
using OpenHikeMap.Models;
using OpenHikeMap.Services;

namespace OpenHikeMap.Components.Pages;

public partial class Home
{
    private CancellationTokenSource? WeatherRequest, AlertsRequest;
    private bool WeatherBusy;
    private List<CivilAlertArea> AlertAreas = [];
    private string CivilAlertSummary = "";
    private RoutePathPoint? AutomaticWeatherPoint;
    private RoutePathPoint? WeatherPoint => Data.Settings.WeatherReferenceLatitude is { } lat && Data.Settings.WeatherReferenceLongitude is { } lon
        ? new RoutePathPoint { Latitude = lat, Longitude = lon } : AutomaticWeatherPoint;

    private async Task ToggleBearing() { Data.Settings.ShowBearingCircle = !Data.Settings.ShowBearingCircle; await Refresh(); }
    private async Task ToggleCivilAlerts()
    {
        Data.Settings.ShowCivilProtectionAlerts = !Data.Settings.ShowCivilProtectionAlerts;
        await Save();
        await RefreshCivilAlertsAsync();
    }

    private async Task LoadWeather() => await UpdateWeatherAsync();

    private async Task UpdateWeatherAsync(CancellationToken parent = default)
    {
        WeatherRequest?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(Lifetime.Token, parent);
        WeatherRequest = request;
        var token = request.Token;
        WeatherBusy = true; Forecast = null; WeatherStatus = L["WeatherLoading"];
        try
        {
            var point = WeatherPoint;
            if (point is null)
            {
                var center = Ready && Module is not null ? await Module.InvokeAsync<double[]>("getCenter") : [Data.Settings.DefaultLatitude, Data.Settings.DefaultLongitude];
                point = new RoutePathPoint { Latitude = center[0], Longitude = center[1] };
            }
            await RenderMap();
            await InvokeAsync(StateHasChanged);
            var forecast = await Weather.GetForecastAsync(point.Latitude, point.Longitude, Data.Settings.LanguageCode, !Imperial, Data.Settings.TemperatureUnit, token);
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

    private async Task RefreshCivilAlertsAsync(CancellationToken parent = default)
    {
        AlertsRequest?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(Lifetime.Token, parent);
        AlertsRequest = request;
        var token = request.Token;
        try
        {
            if (!Data.Settings.ShowCivilProtectionAlerts) { AlertAreas = []; CivilAlertSummary = ""; }
            else
            {
                CivilAlertSummary = L["CivilAlertLoading"];
                await InvokeAsync(StateHasChanged);
                var areas = await CivilProtection.GetAreasAsync(token);
                token.ThrowIfCancellationRequested();
                AlertAreas = areas;
                var reference = Data.RouteGeometry.Count > 1 ? Data.RouteGeometry : WeatherPoint is { } point ? new List<RoutePathPoint> { point } : [];
                var matching = areas.Where(a => CivilProtectionService.Intersects(a, reference)).ToList();
                CivilAlertSummary = matching.Count > 0
                    ? $"{L["CivilAlertTitle"]}: {string.Join(" | ", matching.Select(a => $"{a.Name}: {a.Details}"))}"
                    : L["CivilAlertNone"];
            }
            token.ThrowIfCancellationRequested();
            await RenderMap();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                AlertAreas = [];
                CivilAlertSummary = L["CivilAlertUnavailable"];
                Logger.LogWarning("Allerte SNPC-IT non disponibili: {Reason}", ex.Message);
                await RenderMap();
            }
        }
        finally
        {
            if (ReferenceEquals(AlertsRequest, request))
            {
                AlertsRequest = null;
                if (!Lifetime.IsCancellationRequested) await InvokeAsync(StateHasChanged);
            }
        }
    }
}
