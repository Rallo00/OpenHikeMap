using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;
using OpenHikeMap.Models;
namespace OpenHikeMap.Services;
public sealed class SearchService
{
    private sealed record PoiSearchDefinition(string OsmSearchTerm, string[] TagFilters);
    private static readonly IReadOnlyDictionary<string, PoiSearchDefinition> PoiTerms = new Dictionary<string, PoiSearchDefinition>(StringComparer.OrdinalIgnoreCase)
    {
        ["rifugio"] = new("alpine hut", ["[\"tourism\"=\"alpine_hut\"]"]), ["rifugi"] = new("alpine hut", ["[\"tourism\"=\"alpine_hut\"]"]),
        ["bivacco"] = new("shelter", ["[\"tourism\"=\"wilderness_hut\"]", "[\"amenity\"=\"shelter\"]"]), ["bivacchi"] = new("shelter", ["[\"tourism\"=\"wilderness_hut\"]", "[\"amenity\"=\"shelter\"]"]),
        ["parcheggio"] = new("parking", ["[\"amenity\"=\"parking\"]"]), ["parcheggi"] = new("parking", ["[\"amenity\"=\"parking\"]"]),
        ["fontana"] = new("drinking water", ["[\"amenity\"=\"drinking_water\"]", "[\"man_made\"=\"water_tap\"]"]), ["fontane"] = new("drinking water", ["[\"amenity\"=\"drinking_water\"]", "[\"man_made\"=\"water_tap\"]"]),
        ["cima"] = new("peak", ["[\"natural\"=\"peak\"]"]), ["cime"] = new("peak", ["[\"natural\"=\"peak\"]"]),
        ["lago"] = new("lake", ["[\"natural\"=\"water\"][\"water\"=\"lake\"]"]), ["laghi"] = new("lake", ["[\"natural\"=\"water\"][\"water\"=\"lake\"]"]),
        ["campeggio"] = new("camp site", ["[\"tourism\"=\"camp_site\"]"]), ["campeggi"] = new("camp site", ["[\"tourism\"=\"camp_site\"]"])
    };
    private readonly HttpClient _httpClient;
    public SearchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(ApplicationInfo.UserAgent);
    }
    public async Task<List<SearchResult>> SearchAsync(string query, double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var terms = query.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length > 0 && PoiTerms.TryGetValue(terms[0], out var poi))
        {
            if (terms.Length > 1)
            {
                var locations = await SearchNominatimAsync(terms[1], cancellationToken);
                var location = locations.FirstOrDefault();
                if (location is not null) (latitude, longitude) = (location.Latitude, location.Longitude);
            }
            try
            {
                var poiResults = await SearchPoisAsync(poi, latitude, longitude, cancellationToken);
                if (poiResults.Count > 0) return poiResults;
            }
            catch (HttpRequestException) { }
            return await SearchNominatimAsync($"{poi.OsmSearchTerm} {string.Join(' ', terms.Skip(1))}".Trim(), cancellationToken);
        }
        return await SearchNominatimAsync(query, cancellationToken);
    }
    private async Task<List<SearchResult>> SearchNominatimAsync(string query, CancellationToken cancellationToken)
    {
        var url = $"https://nominatim.openstreetmap.org/search?format=jsonv2&q={Uri.EscapeDataString(query)}&limit=10&accept-language=it";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement
            .EnumerateArray()
            .Select(item => new SearchResult
            {
                DisplayName = item.GetProperty("display_name").GetString() ?? string.Empty,
                Subtitle = item.TryGetProperty("type", out var type) ? type.GetString() ?? string.Empty : string.Empty,
                Latitude = double.Parse(item.GetProperty("lat").GetString() ?? "0", CultureInfo.InvariantCulture),
                Longitude = double.Parse(item.GetProperty("lon").GetString() ?? "0", CultureInfo.InvariantCulture)
            })
            .ToList();
    }
    private async Task<List<SearchResult>> SearchPoisAsync(PoiSearchDefinition poi, double latitude, double longitude, CancellationToken cancellationToken)
    {
        var filters = string.Join(string.Empty, poi.TagFilters.Select(filter => $"nwr(around:25000,{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}){filter};"));
        var query = $"[out:json][timeout:20];({filters});out center tags;";
        using var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", new FormUrlEncodedContent([new KeyValuePair<string, string>("data", query)]), cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("elements").EnumerateArray().Select(element =>
        {
            var tags = element.TryGetProperty("tags", out var tagElement) ? tagElement : default;
            var name = tags.ValueKind == JsonValueKind.Object && tags.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            var point = element.TryGetProperty("center", out var center) ? center : element;
            return new SearchResult { DisplayName = string.IsNullOrWhiteSpace(name) ? poi.OsmSearchTerm : name, Subtitle = poi.OsmSearchTerm, Latitude = point.GetProperty("lat").GetDouble(), Longitude = point.GetProperty("lon").GetDouble() };
        }).Where(result => result.Latitude is >= -90 and <= 90 && result.Longitude is >= -180 and <= 180).DistinctBy(result => $"{result.DisplayName}|{result.Latitude:F5}|{result.Longitude:F5}").Take(50).ToList();
    }
}
public sealed class RoutingService
{
    private readonly HttpClient _httpClient;
    private readonly string _openRouteServiceApiKey;
    public RoutingService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(ApplicationInfo.UserAgent);
        _openRouteServiceApiKey = OpenRouteServiceCredentials.Load(configuration);
    }
    public async Task<List<RoutePathPoint>> BuildRouteAsync(IReadOnlyList<RoutePoint> points, CancellationToken cancellationToken = default)
    {
        if (points.Count < 2)
        {
            return points.Select(point => new RoutePathPoint { Latitude = point.Latitude, Longitude = point.Longitude }).ToList();
        }
        var final = new List<RoutePathPoint>();
        for (var index = 0; index < points.Count - 1; index++)
        {
            var current = points[index];
            var next = points[index + 1];
            var direct = current.PointType == "DirectWaypoint" || next.PointType == "DirectWaypoint";
            var segment = direct
                ? BuildDirectSegment(current, next)
                : await TryBuildWalkingSegmentAsync(current, next, cancellationToken);
            if (final.Count > 0 && segment.Count > 0)
            {
                segment.RemoveAt(0);
            }
            final.AddRange(segment);
        }
        return final;
    }
    private async Task<List<RoutePathPoint>> TryBuildWalkingSegmentAsync(RoutePoint from, RoutePoint to, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_openRouteServiceApiKey))
            throw new InvalidDataException("OpenRouteService: chiave API non configurata sul server.");
        try
        {
            if (!string.IsNullOrWhiteSpace(_openRouteServiceApiKey))
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openrouteservice.org/v2/directions/foot-hiking/geojson");
                request.Headers.TryAddWithoutValidation("Authorization", _openRouteServiceApiKey);
                request.Headers.Accept.ParseAdd("application/geo+json, application/json");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        elevation = true,
                        coordinates = new[]
                        {
                            new[] { from.Longitude, from.Latitude },
                            new[] { to.Longitude, to.Latitude }
                        }
                    }),
                    System.Text.Encoding.UTF8,
                    "application/json");
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    string? detail = null;
                    try
                    {
                        using var error = JsonDocument.Parse(body);
                        if (error.RootElement.TryGetProperty("error", out var errorValue))
                            detail = errorValue.ValueKind == JsonValueKind.String ? errorValue.GetString()
                                : errorValue.TryGetProperty("message", out var message) ? message.GetString() : null;
                    }
                    catch (JsonException) { }
                    var reason = response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => "Chiave API non valida o non autorizzata.",
                        System.Net.HttpStatusCode.TooManyRequests => "Limite di richieste raggiunto. Attendi prima di riprovare.",
                        _ => "Il servizio non riesce a calcolare questo tratto."
                    };
                    detail = detail?.Replace(_openRouteServiceApiKey, "[redacted]", StringComparison.Ordinal);
                    if (detail?.Length > 500) detail = detail[..500];
                    throw new InvalidDataException($"OpenRouteService (HTTP {(int)response.StatusCode}): {reason} {detail}".Trim());
                }
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (document.RootElement.TryGetProperty("features", out var features) &&
                    features.ValueKind == JsonValueKind.Array &&
                    features.GetArrayLength() > 0)
                {
                    var coords = features[0].GetProperty("geometry").GetProperty("coordinates");
                    var segment = coords.EnumerateArray()
                        .Select(item => new RoutePathPoint
                        {
                            Longitude = item[0].GetDouble(),
                            Latitude = item[1].GetDouble(),
                            ElevationMeters = item.GetArrayLength() > 2 ? item[2].GetDouble() : null
                        })
                        .ToList();
                    if (segment.Count >= 2 && segment.All(p => WebSession.CoordinatesValid(p.Latitude, p.Longitude)))
                    {
                        return EnsureSegmentTouchesEndpoints(segment, from, to);
                    }
                }
            }
        }
        catch (InvalidDataException) { throw; }
        catch (HttpRequestException ex)
        {
            throw new InvalidDataException("OpenRouteService non raggiungibile. Controlla la connessione del server.", ex);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new InvalidDataException("OpenRouteService ha restituito una geometria non valida.", ex);
        }
        throw new InvalidDataException("OpenRouteService non ha restituito una geometria del percorso.");
    }
    private static List<RoutePathPoint> BuildDirectSegment(RoutePoint from, RoutePoint to)
        =>
        [
            new RoutePathPoint { Latitude = from.Latitude, Longitude = from.Longitude },
            new RoutePathPoint { Latitude = to.Latitude, Longitude = to.Longitude }
        ];
    private static List<RoutePathPoint> EnsureSegmentTouchesEndpoints(List<RoutePathPoint> segment, RoutePoint from, RoutePoint to)
    {
        if (segment.Count == 0)
        {
            return BuildDirectSegment(from, to);
        }
        var result = new List<RoutePathPoint>();
        var fromPoint = new RoutePathPoint
        {
            Latitude = from.Latitude,
            Longitude = from.Longitude,
            ElevationMeters = segment[0].ElevationMeters
        };
        var toPoint = new RoutePathPoint
        {
            Latitude = to.Latitude,
            Longitude = to.Longitude,
            ElevationMeters = segment[^1].ElevationMeters
        };
        result.Add(fromPoint);
        if (!CoordinatesMatch(result[^1], segment[0]))
        {
            result.Add(segment[0]);
        }
        foreach (var point in segment.Skip(1))
        {
            if (!CoordinatesMatch(result[^1], point))
            {
                result.Add(point);
            }
        }
        if (!CoordinatesMatch(result[^1], toPoint))
        {
            result.Add(toPoint);
        }
        return result;
    }
    private static bool CoordinatesMatch(RoutePathPoint left, RoutePathPoint right)
        => Math.Abs(left.Latitude - right.Latitude) < 0.00001 &&
           Math.Abs(left.Longitude - right.Longitude) < 0.00001;
}
public sealed class ElevationService
{
    private readonly HttpClient _httpClient;
    private readonly string _openRouteServiceApiKey;
    public ElevationService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(ApplicationInfo.UserAgent);
        _openRouteServiceApiKey = OpenRouteServiceCredentials.Load(configuration);
    }
    public async Task<List<RouteElevationPoint>> GetElevationProfileAsync(IReadOnlyList<RoutePathPoint> path, CancellationToken cancellationToken = default)
    {
        if (path.Count < 2 || string.IsNullOrWhiteSpace(_openRouteServiceApiKey))
        {
            return [];
        }
        try
        {
            var requestBody = new
            {
                format_in = "geojson",
                format_out = "geojson",
                geometry = new
                {
                    type = "LineString",
                    coordinates = path.Select(point => new[] { point.Longitude, point.Latitude }).ToArray()
                }
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openrouteservice.org/elevation/line");
            request.Headers.TryAddWithoutValidation("Authorization", _openRouteServiceApiKey);
            request.Headers.Accept.ParseAdd("application/json, application/geo+json; charset=utf-8");
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var coordinates = document.RootElement.GetProperty("geometry").GetProperty("coordinates");
            var result = new List<RouteElevationPoint>();
            foreach (var item in coordinates.EnumerateArray())
            {
                if (item.GetArrayLength() < 3)
                {
                    continue;
                }
                result.Add(new RouteElevationPoint
                {
                    Longitude = item[0].GetDouble(),
                    Latitude = item[1].GetDouble(),
                    ElevationMeters = item[2].GetDouble()
                });
            }
            return result;
        }
        catch
        {
            return [];
        }
    }
}
public sealed class RouteElevationPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double ElevationMeters { get; set; }
}
public sealed class WeatherService
{
    private readonly HttpClient _httpClient;
    public WeatherService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(ApplicationInfo.UserAgent);
    }
    public async Task<WeatherForecastResult?> GetForecastAsync(double latitude, double longitude, string languageCode, bool useMetric, string temperatureUnitSetting, CancellationToken cancellationToken = default)
    {
        var temperatureUnit = string.Equals(temperatureUnitSetting, "F", StringComparison.OrdinalIgnoreCase) ? "fahrenheit" : "celsius";
        var windUnit = useMetric ? "kmh" : "mph";
        var timezone = Uri.EscapeDataString("Europe/Rome");
        var url =
            $"https://api.open-meteo.com/v1/forecast?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,wind_speed_10m_max,wind_direction_10m_dominant,uv_index_max,sunrise,sunset" +
            $"&temperature_unit={temperatureUnit}&wind_speed_unit={windUnit}&timezone={timezone}&forecast_days=15";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var result = new WeatherForecastResult
        {
            Latitude = root.GetProperty("latitude").GetDouble(),
            Longitude = root.GetProperty("longitude").GetDouble()
        };
        result.UpdatedLabel = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (root.TryGetProperty("daily", out var daily))
        {
            var times = GetStringArray(daily, "time");
            var maxTemps = GetDoubleArray(daily, "temperature_2m_max");
            var minTemps = GetDoubleArray(daily, "temperature_2m_min");
            var rainProbability = GetDoubleArray(daily, "precipitation_probability_max");
            var windMax = GetDoubleArray(daily, "wind_speed_10m_max");
            var windDirections = GetDoubleArray(daily, "wind_direction_10m_dominant");
            var uvMax = GetDoubleArray(daily, "uv_index_max");
            var weatherCodes = GetIntArray(daily, "weather_code");
            var sunrises = GetStringArray(daily, "sunrise");
            var sunsets = GetStringArray(daily, "sunset");
            for (var index = 0; index < times.Count; index++)
            {
                var date = DateTime.TryParse(times[index], out var parsed) ? parsed : DateTime.Today.AddDays(index);
                var dayCulture = GetWeatherCulture(languageCode);
                var minTemp = GetArrayValue(minTemps, index);
                var maxTemp = GetArrayValue(maxTemps, index);
                var rain = GetArrayValue(rainProbability, index);
                var wind = GetArrayValue(windMax, index);
                var windDirection = GetArrayValue(windDirections, index);
                var uv = GetArrayValue(uvMax, index);
                var weatherCode = GetArrayValue(weatherCodes, index);
                var sunrise = GetArrayValue(sunrises, index);
                var sunset = GetArrayValue(sunsets, index);
                result.Days.Add(new WeatherForecastDay
                {
                    DayLabel = index == 0
                        ? GetTodayLabel(languageCode)
                        : date.ToString("ddd dd", dayCulture),
                    WeatherIconKey = GetWeatherIconKey(weatherCode, wind, rain),
                    TemperatureLabel = $"{minTemp:F0}° / {maxTemp:F0}°",
                    WeatherLabel = MapWeatherCode(weatherCode, languageCode),
                    WindLabel = $"{wind:F0} {windUnit}",
                    WindDirectionLabel = MapWindDirection(windDirection, languageCode),
                    RainLabel = $"{rain:F0}%",
                    UvLabel = uv > 0 ? $"UV {uv:F0}" : "UV --",
                    SunriseLabel = FormatTimeLabel(sunrise, dayCulture),
                    SunsetLabel = FormatTimeLabel(sunset, dayCulture)
                });
            }
        }
        return result;
    }
    private static string MapWeatherCode(int code, string languageCode)
    {
        return languageCode switch
        {
            "fr-FR" => code switch
            {
                0 => "Clair",
                1 or 2 => "Partiellement nuageux",
                3 => "Couvert",
                45 or 48 => "Brouillard",
                51 or 53 or 55 => "Bruine",
                61 or 63 or 65 => "Pluie",
                71 or 73 or 75 => "Neige",
                80 or 81 or 82 => "Averses",
                95 or 96 or 99 => "Orage",
                _ => "Variable"
            },
            "es-ES" => code switch
            {
                0 => "Despejado",
                1 or 2 => "Parcialmente nublado",
                3 => "Cubierto",
                45 or 48 => "Niebla",
                51 or 53 or 55 => "Llovizna",
                61 or 63 or 65 => "Lluvia",
                71 or 73 or 75 => "Nieve",
                80 or 81 or 82 => "Chubascos",
                95 or 96 or 99 => "Tormenta",
                _ => "Variable"
            },
            "de-DE" => code switch
            {
                0 => "Klar",
                1 or 2 => "Leicht bewölkt",
                3 => "Bedeckt",
                45 or 48 => "Nebel",
                51 or 53 or 55 => "Nieselregen",
                61 or 63 or 65 => "Regen",
                71 or 73 or 75 => "Schnee",
                80 or 81 or 82 => "Schauer",
                95 or 96 or 99 => "Gewitter",
                _ => "Wechselhaft"
            },
            "nl-NL" => code switch
            {
                0 => "Helder",
                1 or 2 => "Half bewolkt",
                3 => "Bewolkt",
                45 or 48 => "Mist",
                51 or 53 or 55 => "Motregen",
                61 or 63 or 65 => "Regen",
                71 or 73 or 75 => "Sneeuw",
                80 or 81 or 82 => "Buien",
                95 or 96 or 99 => "Onweer",
                _ => "Wisselend"
            },
            "nb-NO" => code switch
            {
                0 => "Klart",
                1 or 2 => "Delvis skyet",
                3 => "Overskyet",
                45 or 48 => "Tåke",
                51 or 53 or 55 => "Yr",
                61 or 63 or 65 => "Regn",
                71 or 73 or 75 => "Snø",
                80 or 81 or 82 => "Byger",
                95 or 96 or 99 => "Tordenvær",
                _ => "Variabelt"
            },
            "da-DK" => code switch
            {
                0 => "Klart",
                1 or 2 => "Let skyet",
                3 => "Overskyet",
                45 or 48 => "Tåge",
                51 or 53 or 55 => "Støvregn",
                61 or 63 or 65 => "Regn",
                71 or 73 or 75 => "Sne",
                80 or 81 or 82 => "Byger",
                95 or 96 or 99 => "Tordenvejr",
                _ => "Skiftende"
            },
            "sv-SE" => code switch
            {
                0 => "Klart",
                1 or 2 => "Delvis molnigt",
                3 => "Mulet",
                45 or 48 => "Dimma",
                51 or 53 or 55 => "Duggregn",
                61 or 63 or 65 => "Regn",
                71 or 73 or 75 => "Snö",
                80 or 81 or 82 => "Skurar",
                95 or 96 or 99 => "Åska",
                _ => "Växlande"
            },
            "fi-FI" => code switch
            {
                0 => "Selkeää",
                1 or 2 => "Puolipilvistä",
                3 => "Pilvistä",
                45 or 48 => "Sumua",
                51 or 53 or 55 => "Tihkua",
                61 or 63 or 65 => "Sadetta",
                71 or 73 or 75 => "Lunta",
                80 or 81 or 82 => "Kuuroja",
                95 or 96 or 99 => "Ukkosta",
                _ => "Vaihtelevaa"
            },
            "en-US" => code switch
            {
                0 => "Clear",
                1 or 2 => "Partly cloudy",
                3 => "Overcast",
                45 or 48 => "Fog",
                51 or 53 or 55 => "Drizzle",
                61 or 63 or 65 => "Rain",
                71 or 73 or 75 => "Snow",
                80 or 81 or 82 => "Showers",
                95 or 96 or 99 => "Thunderstorm",
                _ => "Variable"
            },
            _ => code switch
            {
                0 => "Sereno",
                1 or 2 => "Poco nuvoloso",
                3 => "Coperto",
                45 or 48 => "Nebbia",
                51 or 53 or 55 => "Pioviggine",
                61 or 63 or 65 => "Pioggia",
                71 or 73 or 75 => "Neve",
                80 or 81 or 82 => "Rovesci",
                95 or 96 or 99 => "Temporale",
                _ => "Variabile"
            }
        };
    }
    private static CultureInfo GetWeatherCulture(string languageCode)
    {
        try
        {
            return CultureInfo.GetCultureInfo(languageCode);
        }
        catch
        {
            return CultureInfo.GetCultureInfo("it-IT");
        }
    }
    private static string GetTodayLabel(string languageCode)
        => languageCode switch
        {
            "fr-FR" => "Aujourd'hui",
            "es-ES" => "Hoy",
            "de-DE" => "Heute",
            "nl-NL" => "Vandaag",
            "nb-NO" => "I dag",
            "da-DK" => "I dag",
            "sv-SE" => "I dag",
            "fi-FI" => "Tänään",
            "en-US" => "Today",
            _ => "Oggi"
        };
    private static List<string> GetStringArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return property.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : string.Empty)
            .ToList();
    }
    private static List<double> GetDoubleArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return property.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.Number ? item.GetDouble() : 0d)
            .ToList();
    }
    private static List<int> GetIntArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return property.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.Number ? item.GetInt32() : 0)
            .ToList();
    }
    private static T GetArrayValue<T>(IReadOnlyList<T> values, int index)
        => index >= 0 && index < values.Count ? values[index] : default!;
    private static string FormatTimeLabel(string isoValue, CultureInfo culture)
    {
        if (DateTime.TryParse(isoValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            return dateTime.ToString("HH:mm", culture);
        }
        return "--:--";
    }
    private static string GetWeatherIconKey(int weatherCode, double windSpeed, double rainProbability)
    {
        if (weatherCode is 95 or 96 or 99)
        {
            return "thunderstorm";
        }
        if (weatherCode is 71 or 73 or 75)
        {
            return "snow";
        }
        if (weatherCode is 45 or 48)
        {
            return "fog";
        }
        if (weatherCode is 80 or 81 or 82 || (rainProbability >= 50 && windSpeed >= 20))
        {
            return "rain_wind";
        }
        if (weatherCode is 51 or 53 or 55 or 61 or 63 or 65 || rainProbability >= 40)
        {
            return "rain";
        }
        if (weatherCode == 3)
        {
            return "fog";
        }
        if (weatherCode is 1 or 2)
        {
            return "partially";
        }
        if (windSpeed >= 28)
        {
            return "windy";
        }
        return "sun";
    }
    private static string MapWindDirection(double degrees, string languageCode)
    {
        var normalized = ((degrees % 360d) + 360d) % 360d;
        var index = (int)Math.Round(normalized / 45d, MidpointRounding.AwayFromZero) % 8;
        return languageCode switch
        {
            "en-US" => new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" }[index],
            "it-IT" => new[] { "N", "NE", "E", "SE", "S", "SO", "O", "NO" }[index],
            "fr-FR" => new[] { "N", "NE", "E", "SE", "S", "SO", "O", "NO" }[index],
            "es-ES" => new[] { "N", "NE", "E", "SE", "S", "SO", "O", "NO" }[index],
            "de-DE" => new[] { "N", "NO", "O", "SO", "S", "SW", "W", "NW" }[index],
            "nl-NL" => new[] { "N", "NO", "O", "ZO", "Z", "ZW", "W", "NW" }[index],
            "nb-NO" => new[] { "N", "NØ", "Ø", "SØ", "S", "SV", "V", "NV" }[index],
            "da-DK" => new[] { "N", "NØ", "Ø", "SØ", "S", "SV", "V", "NV" }[index],
            "sv-SE" => new[] { "N", "NO", "O", "SO", "S", "SV", "V", "NV" }[index],
            "fi-FI" => new[] { "P", "KO", "I", "KA", "E", "LA", "L", "LU" }[index],
            _ => new[] { "N", "NE", "E", "SE", "S", "SO", "O", "NO" }[index]
        };
    }
}
public sealed class WeatherForecastResult
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string CurrentLabel { get; set; } = string.Empty;
    public string UpdatedLabel { get; set; } = string.Empty;
    public List<WeatherForecastDay> Days { get; } = [];
}
public sealed class GpxService
{
    public GpxImportResult ImportGpx(Stream stream)
    {
        var document = XDocument.Load(System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, MaxCharactersInDocument = 10_000_000 }));
        XNamespace ns = "http://www.topografix.com/GPX/1/1";
        var routePoints = document.Descendants(ns + "rtept")
            .Select((point, index) => CreateRoutePoint(point, index))
            .Where(point => point is not null)
            .Cast<RoutePoint>()
            .ToList();
        var trackPoints = document.Descendants(ns + "trkpt")
            .Select(CreatePathPoint)
            .Where(point => point is not null)
            .Cast<RoutePathPoint>()
            .ToList();
        if (routePoints.Count == 0 && trackPoints.Count > 0)
        {
            routePoints.Add(new RoutePoint
            {
                Latitude = trackPoints[0].Latitude,
                Longitude = trackPoints[0].Longitude,
                Name = "Start",
                PointType = "Start"
            });
            if (trackPoints.Count > 1)
            {
                routePoints.Add(new RoutePoint
                {
                    Latitude = trackPoints[^1].Latitude,
                    Longitude = trackPoints[^1].Longitude,
                    Name = "End",
                    PointType = "End"
                });
            }
        }
        return new GpxImportResult
        {
            RoutePoints = routePoints,
            RouteGeometry = trackPoints
        };
    }
    public string BuildGpx(IReadOnlyList<RoutePoint> routePoints, IReadOnlyList<RoutePathPoint> pathPoints)
    {
        XNamespace ns = "http://www.topografix.com/GPX/1/1";
        var routeName = routePoints.Count > 0 ? "OpenHikeMap Route" : "OpenHikeMap Empty Route";
        var document = new XDocument(
            new XElement(ns + "gpx",
                new XAttribute("version", "1.1"),
                new XAttribute("creator", "OpenHikeMap"),
                new XElement(ns + "metadata",
                    new XElement(ns + "name", routeName),
                    new XElement(ns + "time", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture))),
                new XElement(ns + "rte",
                    new XElement(ns + "name", routeName),
                    routePoints.Select(point => new XElement(ns + "rtept",
                        new XAttribute("lat", point.Latitude.ToString(CultureInfo.InvariantCulture)),
                        new XAttribute("lon", point.Longitude.ToString(CultureInfo.InvariantCulture)),
                        new XElement(ns + "name", point.Name)))),
                new XElement(ns + "trk",
                    new XElement(ns + "name", routeName),
                    new XElement(ns + "trkseg",
                        pathPoints.Select(point => new XElement(ns + "trkpt",
                            new XAttribute("lat", point.Latitude.ToString(CultureInfo.InvariantCulture)),
                            new XAttribute("lon", point.Longitude.ToString(CultureInfo.InvariantCulture)),
                            point.ElevationMeters is { } elevation ? new XElement(ns + "ele", elevation.ToString(CultureInfo.InvariantCulture)) : null))))));
        return document.Declaration + Environment.NewLine + document;
    }
    private static RoutePoint? CreateRoutePoint(XElement point, int index)
    {
        if (!TryReadCoordinates(point, out var latitude, out var longitude))
        {
            return null;
        }
        var pointType = index == 0 ? "Start" : "Waypoint";
        return new RoutePoint
        {
            Latitude = latitude,
            Longitude = longitude,
            Name = point.Element(point.Name.Namespace + "name")?.Value ?? pointType,
            PointType = pointType
        };
    }
    private static RoutePathPoint? CreatePathPoint(XElement point)
    {
        if (!TryReadCoordinates(point, out var latitude, out var longitude))
        {
            return null;
        }
        return new RoutePathPoint
        {
            Latitude = latitude,
            Longitude = longitude,
            ElevationMeters = double.TryParse(point.Element(point.Name.Namespace + "ele")?.Value, CultureInfo.InvariantCulture, out var elevation) ? elevation : null
        };
    }
    private static bool TryReadCoordinates(XElement point, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;
        var latText = point.Attribute("lat")?.Value;
        var lonText = point.Attribute("lon")?.Value;
        return double.TryParse(latText, CultureInfo.InvariantCulture, out latitude)
            && double.TryParse(lonText, CultureInfo.InvariantCulture, out longitude);
    }
}
public sealed class KmlService
{
    public string ExportPlaces(IReadOnlyList<SavedPlace> places)
    {
        XNamespace ns = "http://www.opengis.net/kml/2.2";
        var document = new XDocument(
            new XElement(ns + "kml",
                new XElement(ns + "Document",
                    new XElement(ns + "name", "OpenHikeMap Places"),
                    places.Select(place =>
                        new XElement(ns + "Placemark",
                            new XElement(ns + "name", place.Name),
                            new XElement(ns + "description", place.Description ?? string.Empty),
                            new XElement(ns + "Point",
                                new XElement(ns + "coordinates",
                                    $"{place.Longitude.ToString(CultureInfo.InvariantCulture)},{place.Latitude.ToString(CultureInfo.InvariantCulture)},0")))))));
        return document.Declaration + Environment.NewLine + document;
    }
    public List<SavedPlace> ImportPlaces(Stream stream)
    {
        var document = XDocument.Load(System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, MaxCharactersInDocument = 10_000_000 }));
        XNamespace ns = "http://www.opengis.net/kml/2.2";
        var styles = document.Descendants(ns + "Style")
            .Where(style => style.Attribute("id") is not null)
            .ToDictionary(
                style => $"#{style.Attribute("id")!.Value}",
                style => ConvertKmlColor(style.Descendants(ns + "color").FirstOrDefault()?.Value));
        return document.Descendants(ns + "Placemark")
            .Select(placemark =>
            {
                var coordinatesText = placemark.Descendants(ns + "Point").Descendants(ns + "coordinates").FirstOrDefault()?.Value;
                if (string.IsNullOrWhiteSpace(coordinatesText))
                {
                    return null;
                }
                var parts = coordinatesText.Split(',').Select(part => part.Trim()).ToArray();
                if (parts.Length < 2)
                {
                    return null;
                }
                var styleUrl = placemark.Element(ns + "styleUrl")?.Value;
                return new SavedPlace
                {
                    Name = placemark.Element(ns + "name")?.Value ?? "Luogo da KML",
                    Description = placemark.Element(ns + "description")?.Value ?? string.Empty,
                    Longitude = double.Parse(parts[0], CultureInfo.InvariantCulture),
                    Latitude = double.Parse(parts[1], CultureInfo.InvariantCulture),
                    IconKey = "marker",
                    PinColorHex = styleUrl is not null && styles.TryGetValue(styleUrl, out var color) ? color : "#D94841"
                };
            })
            .Where(place => place is not null)
            .Cast<SavedPlace>()
            .ToList();
    }
    private static string ConvertKmlColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 8)
        {
            return "#D94841";
        }
        var aabbggrr = value.Trim();
        var rr = aabbggrr.Substring(6, 2);
        var gg = aabbggrr.Substring(4, 2);
        var bb = aabbggrr.Substring(2, 2);
        return $"#{rr}{gg}{bb}";
    }
}



