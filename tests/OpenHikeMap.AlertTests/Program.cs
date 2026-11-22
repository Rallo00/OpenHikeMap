using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using OpenHikeMap.Models;
using OpenHikeMap.Services;

var now = DateTimeOffset.UtcNow;
var tests = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); tests++; Console.WriteLine("PASS " + name); }
JsonObject Warning(string id = "one", int level = 2, string language = "en-GB", string? polygon = "39,14 39,16 41,16 41,14", string? geocode = null)
{
    var area = new JsonObject { ["areaDesc"] = "Test area" };
    if (polygon is not null) area["polygon"] = new JsonArray(polygon);
    if (geocode is not null) area["geocode"] = new JsonArray(new JsonObject { ["valueName"] = "EMMA_ID", ["value"] = geocode });
    return new JsonObject { ["alert"] = new JsonObject
    {
        ["identifier"] = id, ["sender"] = "official-test-service", ["status"] = "Actual", ["scope"] = "Public", ["msgType"] = "Alert", ["sent"] = now.AddHours(-1).ToString("O"),
        ["info"] = new JsonArray(new JsonObject
        {
            ["language"] = language, ["event"] = "Wind", ["headline"] = "Test warning", ["description"] = "Original description", ["instruction"] = "Original instruction", ["senderName"] = "Test weather service",
            ["onset"] = now.AddMinutes(-10).ToString("O"), ["expires"] = now.AddHours(2).ToString("O"),
            ["parameter"] = new JsonArray(new JsonObject { ["valueName"] = "awareness_level", ["value"] = level + "; colour; severity" }),
            ["area"] = new JsonArray(area)
        })
    }};
}
JsonObject Alert(JsonObject warning) => warning["alert"]!.AsObject();
JsonObject Info(JsonObject warning) => Alert(warning)["info"]![0]!.AsObject();
string Feed(params JsonObject[] warnings) => new JsonObject { ["warnings"] = new JsonArray(warnings.Select(w => (JsonNode)w).ToArray()) }.ToJsonString();
EumetnetParsedWarnings Parse(string json, EumetnetGeocodes? codes = null, string language = "it-IT") => EumetnetWarningParser.Parse(json, "IT", language, codes, now);
Check(Parse(Feed(Warning())).Areas.Single().Severity == 2, "Yellow awareness warnings included");
Check(Parse(Feed(Warning(level: 3), Warning("red", 4))).Areas.Select(a => a.Severity).SequenceEqual([3, 4]), "Orange and red awareness levels preserved");
Check(Parse(Feed(Warning(level: 1))).Areas.Count == 0, "Green awareness omitted");
var expired = Warning(); Info(expired)["expires"] = now.AddMinutes(-1).ToString("O");
Check(Parse(Feed(expired)).Areas.Count == 0, "Expired warning omitted");
var noExpiry = Warning(); Info(noExpiry).Remove("expires");
Check(Parse(Feed(noExpiry)).Areas.Single().Expires is null, "CAP warnings without optional expiry remain available");
var invalidExpiry = Warning(); Info(invalidExpiry)["expires"] = "invalid";
Check(Parse(Feed(invalidExpiry)).UnmappedAreas == 1, "Malformed expiry reported as unverifiable");
var missingArea = Warning(); Info(missingArea).Remove("area");
Check(Parse(Feed(missingArea)).UnmappedAreas == 1, "Absent warning area cannot produce all-clear");
var unknownSeverity = Warning(); Info(unknownSeverity).Remove("parameter");
Check(Parse(Feed(unknownSeverity)).UnmappedAreas == 1, "Unknown warning severity cannot produce all-clear");
var future = Warning(); Info(future)["onset"] = now.AddHours(1).ToString("O");
Check(Parse(Feed(future)).Areas.Count == 1, "Upcoming warning kept with its future onset");
var test = Warning(); Alert(test)["status"] = "Test";
Check(Parse(Feed(test)).Areas.Count == 0, "Test messages omitted");
var privateWarning = Warning(); Alert(privateWarning)["scope"] = "Private";
Check(Parse(Feed(privateWarning)).Areas.Count == 0, "Private messages omitted");
var old = Warning("old"); var update = Warning("new", 3); Alert(update)["msgType"] = "Update";
Alert(update)["references"] = "official-test-service,old," + now.AddHours(-1).ToString("O");
Check(Parse(Feed(old, update)).Areas.Single().Id == "new", "Updated warning supersedes its reference");
var canceled = Warning("cancel"); Alert(canceled)["msgType"] = "Cancel";
Alert(canceled)["references"] = "official-test-service,old," + now.AddHours(-1).ToString("O");
Check(Parse(Feed(Warning("old"), canceled)).Areas.Count == 0, "Cancellation removes the referenced warning");
var translated = Warning(); var italian = Info(translated).DeepClone().AsObject(); italian["language"] = "it-IT"; italian["headline"] = "Testo italiano";
Alert(translated)["info"]!.AsArray().Add(italian);
Check(Parse(Feed(translated)).Areas.Single().Details.StartsWith("Testo italiano"), "Preferred translation chosen without duplicates");
Check(Parse(Feed(Warning(language: "en")), language: "fr-FR").Areas.Count == 1, "English fallback supported");
var fallback = Warning(language: "de-DE"); var other = Info(fallback).DeepClone().AsObject(); other["language"] = "fr-FR"; Alert(fallback)["info"]!.AsArray().Add(other);
Check(Parse(Feed(fallback)).Areas.Count == 1, "Only one language used when English is unavailable");
var unknown = Warning(polygon: null, geocode: "IT001");
Check(Parse(Feed(unknown)).UnmappedAreas == 1, "Missing geometry reported rather than interpreted as no warning");
const string geo = """{"type":"FeatureCollection","features":[{"type":"Feature","properties":{"code":"IT001","type":"EMMA_ID"},"geometry":{"type":"Polygon","coordinates":[[[14,39],[16,39],[16,41],[14,41],[14,39]]]}}]}""";
var codes = new EumetnetGeocodes(geo, "CODE,ALIAS_CODE,ALIAS_TYPE\nIT001,ITTEST,NUTS3\n");
Check(Parse(Feed(Warning(polygon: null, geocode: "IT001")), codes).Areas.Count == 1, "Official EMMA geometry resolves warning area");
Check(codes.Resolve("NUTS3", "ITTEST") is not null, "Alias catalog resolves alternative area schemes");
var circle = Warning(polygon: null); Info(circle)["area"]![0]!["circle"] = new JsonArray("40,15 10");
Check(Parse(Feed(circle)).Areas.Single().Polygons[0][0].Count == 73, "CAP circles converted with spherical coordinates");
var areaToTest = Parse(Feed(Warning())).Areas.Single();
RoutePathPoint P(double lat, double lon) => new() { Latitude = lat, Longitude = lon };
Check(AlertGeometry.Intersects(areaToTest, [P(40, 15)]), "Point inside a warning polygon detected");
Check(AlertGeometry.Intersects(areaToTest, [P(40, 13), P(40, 17)]), "Route crossing polygon detected even when both endpoints are outside");
Check(!AlertGeometry.Intersects(areaToTest, [P(45, 18)]), "Outside route excluded");
var hole = AlertGeometry.CapPolygon("39.5,14.5 39.5,15.5 40.5,15.5 40.5,14.5"); areaToTest.Polygons[0].Add(hole);
Check(!AlertGeometry.Intersects(areaToTest, [P(40, 15)]), "Polygon holes excluded");
Check(AlertGeometry.Intersects(areaToTest, [P(39.5, 15)]), "Hole boundary included as polygon boundary");
Check(AlertGeometry.Intersects(areaToTest, [P(39, 14)]), "Boundary points included");
var multipolygon = areaToTest with { Polygons = [areaToTest.Polygons[0], [AlertGeometry.CapPolygon("45,20 45,21 46,21 46,20")]] };
Check(AlertGeometry.Intersects(multipolygon, [P(45.5, 20.5)]), "Disjoint multipolygon component supported");
Check(new EumetnetBounds(170, -10, -170, 10).Intersects(new(175, -1, 180, 1)), "Antimeridian viewport bounds supported");
Check(EumetnetCountries.All.Length == 39 && new[] { "IT", "DE", "FR", "AT", "CH", "ES", "NO", "UK" }.All(c => EumetnetCountries.All.Any(country => country.Code == c)), "All 39 published participant feeds registered");
foreach (var language in new[] { "it-IT", "en-US", "fr-FR", "es-ES", "de-DE", "nl-NL", "nb-NO", "da-DK", "sv-SE", "fi-FI" })
{
    var l = new LocalizationService(); l.SetLanguage(language);
    Check(!l["EumetnetAlertPartial"].StartsWith("EumetnetAlert") && l["ToolbarEumetnetAlerts"].Contains("EUMETNET"), "Translated status and controls: " + language);
}
var italyScope = new EumetnetBounds(14.99, 39.99, 15.01, 40.01);
var cacheDirectory = Path.Combine(Path.GetTempPath(), "OpenHikeMap.AlertTests-" + Guid.NewGuid().ToString("N"));
var handler = new FakeHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent(Feed(Warning()), Encoding.UTF8, "application/json") });
var service = new EumetnetService(new HttpClient(handler), NullLogger<EumetnetService>.Instance, cacheDirectory);
var first = await service.GetAreasAsync([italyScope], "en-US"); var second = await service.GetAreasAsync([italyScope], "it-IT");
Check(first.IsComplete && first.Areas.Count == 1 && handler.Count == 1 && second.Areas.Count == 1, "Country feed cached while warnings are reparsed for current language and time");
var outside = await service.GetAreasAsync([new(-120, 35, -119, 36)], "en-US");
Check(outside.RequestedCountries == 0 && handler.Count == 1, "Unsupported coverage distinguished without unrelated downloads");
var failureHandler = new FakeHandler(_ => new(HttpStatusCode.ServiceUnavailable));
var failed = await new EumetnetService(new HttpClient(failureHandler), NullLogger<EumetnetService>.Instance, cacheDirectory).GetAreasAsync([italyScope], "en-US");
Check(!failed.IsComplete && failed.FailedCountries == 1, "Feed outage cannot produce an all-clear response");
var missingHandler = new FakeHandler(uri => uri.Host == "feeds.meteoalarm.org" ? new(HttpStatusCode.OK) { Content = new StringContent(Feed(Warning(polygon: null, geocode: "UNKNOWN"))) } : new(HttpStatusCode.ServiceUnavailable));
var missing = await new EumetnetService(new HttpClient(missingHandler), NullLogger<EumetnetService>.Instance, cacheDirectory).GetAreasAsync([italyScope], "en-US");
Check(!missing.IsComplete && missing.UnmappedAreas == 1, "Catalog outage reported as incomplete spatial coverage");
var cancellation = new CancellationTokenSource(); cancellation.Cancel();
try { await service.GetAreasAsync([italyScope], "en-US", cancellation.Token); throw new Exception("Cancellation ignored"); }
catch (OperationCanceledException) { Check(true, "Cancellation propagated"); }

if (args.Length == 2 && args[0] == "--fixtures")
{
    var path = args[1];
    var liveCodes = new EumetnetGeocodes(File.ReadAllText(Path.Combine(path, "geocodes.json")), File.ReadAllText(Path.Combine(path, "aliases.csv")));
    var liveParsed = EumetnetWarningParser.Parse(File.ReadAllText(Path.Combine(path, "italy-current.json")), "IT", "it-IT", liveCodes, DateTimeOffset.UtcNow);
    Check(liveParsed.Areas.Count > 0 && liveParsed.UnmappedAreas == 0, "Current official Italian CAP bundle and 2019-region catalog parse together");
    Console.WriteLine("Official fixture warnings: " + liveParsed.Areas.Count);
    foreach (var (name, latitude, longitude, expectedRegion) in new[] { ("Bologna", 44.4949, 11.3426, "Emilia e Romagna"), ("Rome", 41.9028, 12.4964, "Lazio") })
    {
        var selectedRegions = EumetnetAlertSelection.ForRoute(liveParsed.Areas, [P(latitude, longitude)], P(43.7696, 11.2558), DateTimeOffset.UtcNow)
            .Select(a => a.Name).Distinct().ToArray();
        Check(selectedRegions.SequenceEqual([expectedRegion]), name + " planning excludes Tuscany even with a saved weather point in Florence");
    }
}
if (args.Length == 2 && args[0] == "--live")
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60), MaxResponseContentBufferSize = 64 * 1024 * 1024 };
    client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenHikeMap-IntegrationCheck/0.95");
    var liveService = new EumetnetService(client, NullLogger<EumetnetService>.Instance, args[1]);
    var snapshot = await liveService.GetAreasAsync([
        new(14.99, 39.99, 15.01, 40.01), new(13.39, 52.49, 13.41, 52.51),
        new(2.34, 48.84, 2.36, 48.86), new(16.09, 48.19, 16.11, 48.21), new(8.49, 47.29, 8.51, 47.31)
    ], "en-US");
    Console.WriteLine($"Live Europe: {snapshot.RequestedCountries} countries, {snapshot.Areas.Count} intersecting warnings, {snapshot.FailedCountries} failed countries, {snapshot.UnmappedAreas} unlocatable areas");
    Check(snapshot.RequestedCountries >= 5 && snapshot.FailedCountries == 0, "Live official feeds for Italy, Germany, France, Austria and Switzerland");
}
var weatherJson = JsonSerializer.Serialize(new
{
    latitude = 40, longitude = 15,
    daily = new
    {
        time = new[] { DateTime.Today.ToString("yyyy-MM-dd") }, weather_code = new[] { 0 },
        temperature_2m_max = new[] { 20.0 }, temperature_2m_min = new[] { 10.0 },
        precipitation_probability_max = new[] { 5.0 }, wind_speed_10m_max = new[] { 16.09344 },
        wind_direction_10m_dominant = new[] { 90.0 }, uv_index_max = new[] { 1.0 },
        sunrise = new[] { "2026-11-22T07:00" }, sunset = new[] { "2026-11-22T17:00" }
    }
});
var weatherHandler = new FakeHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent(weatherJson) });
var weather = new WeatherService(new HttpClient(weatherHandler));
Check(weather.FormatCachedForecast("en-US", true, "C") is null && weatherHandler.Count == 0, "Reformatting empty weather cache never triggers a request");
var metric = await weather.GetForecastAsync(40, 15, "en-US", true, "C");
var imperial = weather.FormatCachedForecast("en-US", false, "F")!;
Check(imperial.Days[0].TemperatureLabel == "50° / 68°" && imperial.Days[0].WindLabel == "10 mph" && weatherHandler.Count == 1, "Changing weather units converts cached values without network requests");
var italianWeather = weather.FormatCachedForecast("it-IT", true, "C")!;
Check(italianWeather.Days[0].TemperatureLabel == "10° / 20°" && italianWeather.Days[0].DayLabel == "Oggi" && italianWeather.UpdatedLabel == metric!.UpdatedLabel && weatherHandler.Count == 1, "Changing weather language uses cached data and preserves download timestamp");
Check(weather.FormatCachedForecastForPoint(40, 15, "en-US", true, "C") is not null && weatherHandler.Count == 1, "Unchanged weather point reuses the forecast from the same day");
Check(weather.FormatCachedForecastForPoint(41, 16, "en-US", true, "C") is null && weatherHandler.Count == 1, "A moved weather point cannot reuse the previous location forecast");
await weather.GetForecastAsync(41, 16, "en-US", true, "C");
Check(weatherHandler.Count == 2, "An explicit forecast request for a new reference point fetches data");
var startupHandler = new FakeHandler(uri => new(HttpStatusCode.OK)
{
    Content = new StringContent(uri.AbsolutePath.EndsWith("feeds-italy") ? Feed(Warning()) :
        uri.AbsolutePath.EndsWith("feeds-germany") ? Feed(Warning("germany", polygon: "50,12 50,14 52,14 52,12")) : "{\"warnings\":[]}")
});
var startupService = new EumetnetService(new HttpClient(startupHandler), NullLogger<EumetnetService>.Instance, cacheDirectory);
var allStartup = await startupService.LoadAllOnceAsync("en-US");
var reusedStartup = await startupService.LoadAllOnceAsync("it-IT");
Check(startupHandler.Count == 39 && ReferenceEquals(allStartup, reusedStartup) && allStartup.Areas.Count == 2, "All European feeds load once and subsequent startup consumers reuse the snapshot");
var selectedForItaly = EumetnetAlertSelection.ForRoute(allStartup.Areas, [P(40, 15)], null, now);
Check(selectedForItaly.Count == 1 && selectedForItaly[0].Country == "IT" && allStartup.Areas.Count == 2, "Planning selects only the route region while the complete map snapshot is retained");
Check(EumetnetAlertSelection.ForRoute(allStartup.Areas, [P(40, 15)], P(51, 13), now).Single().Country == "IT", "An existing trail ignores a weather reference point in another region");
var unrelatedFrance = allStartup.Areas[0] with { Country = "FR", Name = "Unrelated region", Polygons = [[AlertGeometry.CapPolygon("47,1 47,3 49,3 49,1")]] };
var crossingRegions = EumetnetAlertSelection.ForRoute([.. allStartup.Areas, unrelatedFrance], [P(40, 15), P(51, 13)], P(48, 2), now);
Check(crossingRegions.Select(a => a.Country).Order().SequenceEqual(new[] { "DE", "IT" }), "Planning includes every region crossed by the trail and excludes the unrelated weather-point region");
Check(EumetnetAlertSelection.ForRoute(allStartup.Areas, [], null, now).Count == 0, "No route or weather point means no unrelated planning warnings");
Check(EumetnetAlertSelection.ForRoute(allStartup.Areas, [], P(51, 13), now).Single().Country == "DE", "A manually selected weather point filters its own region locally");
var loopPoint = RouteMidpoint.Find([P(40, 15), P(40, 15.1), P(40.1, 15.1), P(40.1, 15), P(40, 15)]);
Check(loopPoint is not null && WebSession.CoordinatesValid(loopPoint.Latitude, loopPoint.Longitude) && (loopPoint.Latitude != 40 || loopPoint.Longitude != 15), "A closed hiking circuit has a valid weather calculation point along the route");
if (args.Length == 2 && args[0] == "--startup-live")
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60), MaxResponseContentBufferSize = 64 * 1024 * 1024 };
    client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenHikeMap-StartupCheck/0.95");
    var allLive = new EumetnetService(client, NullLogger<EumetnetService>.Instance, args[1]);
    var full = await allLive.LoadAllOnceAsync("en-US");
    var same = await allLive.LoadAllOnceAsync("en-US");
    Console.WriteLine($"Live startup: {full.RequestedCountries} countries, {full.Areas.Count} warning areas, {full.FailedCountries} country failures, {full.UnmappedAreas} unlocatable areas");
    Check(full.RequestedCountries == 39 && ReferenceEquals(full, same), "Live full-Europe startup returns one retained snapshot");
}
var midpointSession = new WebSession();
midpointSession.Settings.WeatherReferenceLatitude = 43.7696;
midpointSession.Settings.WeatherReferenceLongitude = 11.2558;
midpointSession.RouteGeometry = [P(0, 0), P(0, 1), P(0, 4)];
var automaticReference = RouteMidpoint.ApplyWeatherReference(midpointSession);
Check(automaticReference is not null && Math.Abs(automaticReference.Longitude - 2) < 0.000001 && automaticReference.Latitude == 0,
    "The weather reference is halfway along route distance, not the middle vertex");
Check(midpointSession.Settings.WeatherReferenceLatitude == automaticReference!.Latitude && midpointSession.Settings.WeatherReferenceLongitude == automaticReference.Longitude,
    "Completing a route replaces an old manual weather reference and persists the automatic midpoint");
midpointSession.RouteGeometry = [P(40, 15), P(40, 15.1), P(40.1, 15.1), P(40.1, 15), P(40, 15)];
var closedReference = RouteMidpoint.ApplyWeatherReference(midpointSession);
Check(closedReference is not null && midpointSession.Settings.WeatherReferenceLatitude == closedReference.Latitude && midpointSession.Settings.WeatherReferenceLongitude == closedReference.Longitude,
    "Completing a closed circuit persists its weather calculation midpoint");
midpointSession.RouteGeometry = [];
RouteMidpoint.ApplyWeatherReference(midpointSession);
Check(midpointSession.Settings.WeatherReferenceLatitude is null && midpointSession.Settings.WeatherReferenceLongitude is null,
    "An absent route cannot retain stale automatic weather coordinates");
Console.WriteLine($"{tests} checks passed.");

sealed class FakeHandler(Func<Uri, HttpResponseMessage> response) : HttpMessageHandler
{
    public int Count;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); Interlocked.Increment(ref Count); return Task.FromResult(response(request.RequestUri!)); }
}
