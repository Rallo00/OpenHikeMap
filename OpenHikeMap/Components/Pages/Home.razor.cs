using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using OpenHikeMap.Models;
using OpenHikeMap.Services;

namespace OpenHikeMap.Components.Pages;

public partial class Home
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private WebSession Data = new();
    private readonly LocalizationService L = new();
    private IJSObjectReference? Module;
    private DotNetObjectReference<Home>? Self;
    private readonly CancellationTokenSource Lifetime = new();
    private CancellationTokenSource? RouteCalculation;
    private bool IsRouting;
    private bool Busy, Ready, DataLoaded;
    private bool MapStarting, MapInitializationFailed;
    private Task StartupContextRefresh = Task.CompletedTask;
    private List<RoutePathPoint>? RenderedPath;
    private bool RenderedDraft;
    private bool StorageWritable = true;
    private bool DiskSettingsWritable = true;
    private char Dialog;
    private string Query = "", Filter = "", Mode = "explore", Status = "Pronto per esplorare", CoordinateName = "Nuovo punto", CoordinateTarget = "pin";
    private double CoordinateLat, CoordinateLon;
    private List<SearchResult> Results = [];
    private bool SearchOpen;
    private SavedAppearanceResolver Appearances = new([]);
    private string FolderDraftName = "", FolderDraftIcon = "", FolderDraftColor = "", FolderError = "";
    private SavedPlace? SelectedPlace;
    private SavedFolder? SelectedFolder;
    private WeatherForecastResult? Forecast;
    private readonly HashSet<Guid> CollapsedFolders = [];
    private string WeatherStatus = "";
    private string RouteError = "";
    private static readonly string[] MapStyles = ["OpenStreetMap", "OpenTopoMap", "CyclOSM", "Humanitarian", "OSM DE"];
    private static readonly Dictionary<string, string> Icons = new()
    {
        ["poi"] = "Punto panoramico", ["bivouac"] = "Bivacco", ["fountain"] = "Fontana", ["food"] = "Ristoro", ["paid_hut"] = "Rifugio", ["single_tent"] = "Tenda", ["tent_area"] = "Campeggio", ["parking"] = "Parcheggio", ["cable_car"] = "Funivia", ["hammocks"] = "Bosco", ["waterfall"] = "Cascata", ["water"] = "Acqua", ["summit_cross"] = "Croce di vetta"
    };
    private static readonly Dictionary<string, string> PinColors = new()
    {
        ["#2E7D5B"] = "ColorGreen", ["#D65A5A"] = "ColorRed", ["#3F6F8F"] = "ColorBlue", ["#D9824B"] = "ColorOrange",
        ["#8B6B4A"] = "ColorBrown", ["#7657A8"] = "ColorPurple", ["#C49A3A"] = "ColorGold", ["#59636E"] = "ColorGray"
    };
    private sealed record SavedTreeEntry(int Depth, SavedFolder? Folder, SavedPlace? Place);
    private IEnumerable<SavedTreeEntry> SavedTree
    {
        get
        {
            foreach (var entry in BuildTree(null, 0)) yield return entry;
        }
    }
    private string ModeHint => Mode switch { "pin" => "Clicca sulla mappa per salvare un luogo", "route" => "Clicca sulla mappa per aggiungere una tappa", "weather" => "Clicca per scegliere il riferimento meteo", "ruler" => "Clicca due punti per misurare la distanza", _ => "Esplora la mappa · Tasto destro per le azioni sul punto" };
    private List<RoutePathPoint> Path => Data.RouteGeometry.Count > 0 ? Data.RouteGeometry : Data.RoutePoints.Select(p => new RoutePathPoint { Latitude = p.Latitude, Longitude = p.Longitude }).ToList();
    private bool Imperial => Data.Settings.UnitPreset == "Imperial";
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        await InitializeMap();
    }
    private async Task InitializeMap()
    {
        if (MapStarting || Ready) return;
        var loadingStatus = L["MapLoading"];
        MapStarting = true; MapInitializationFailed = false; Status = loadingStatus;
        await InvokeAsync(StateHasChanged);
        var watch = Stopwatch.StartNew();
        var phase = "local data";
        try
        {
            var moduleTask = JS.InvokeAsync<IJSObjectReference>("import", TimeSpan.FromSeconds(20), [$"./js/map.js?v={ClientAssetVersion.Value}"]).AsTask();
            var stored = await LoadDiskSettings(includeLibrary: true);
            var hasNativeLibrary = stored?.Folders is not null && stored.Places is not null;
            DataLoaded = hasNativeLibrary;
            L.SetLanguage(Data.Settings.LanguageCode);
            Appearances = new(Data.Folders);
            await InvokeAsync(StateHasChanged);
            phase = "client module";
            Module = await moduleTask;
            phase = "browser data stream";
            try
            {
                await using var saved = await Module.InvokeAsync<IJSStreamReference>("readStorageStream", !hasNativeLibrary);
                if (saved.Length > 0)
                {
                    await using var input = await saved.OpenReadStreamAsync(16 * 1024 * 1024, Lifetime.Token);
                    var restored = await JsonSerializer.DeserializeAsync<WebSession>(input, Json, Lifetime.Token) ?? throw new InvalidDataException();
                    restored.Validate();
                    if (!hasNativeLibrary)
                    {
                        Data.Folders = restored.Folders; Data.Places = restored.Places;
                        if (stored is null) Data.Settings = restored.Settings;
                    }
                    Data.RoutePoints = restored.RoutePoints; Data.RouteGeometry = restored.RouteGeometry; Data.RouteKind = restored.RouteKind;
                    Data.Settings.WeatherReferenceLatitude = restored.Settings.WeatherReferenceLatitude;
                    Data.Settings.WeatherReferenceLongitude = restored.Settings.WeatherReferenceLongitude;
                }
                DataLoaded = true; StorageWritable = true;
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or JSException or IOException)
            {
                StorageWritable = false; Status = "Dati locali non leggibili: esporta una copia prima di sostituirli.";
                StartupDiagnostics.Write("Browser data could not be restored; existing data retained.", ex);
            }
            if (!MapStyles.Contains(Data.Settings.MapStyle)) Data.Settings.MapStyle = "OpenStreetMap";
            L.SetLanguage(Data.Settings.LanguageCode);
            WeatherStatus = L["WeatherInitialPrompt"];
            Self?.Dispose(); Self = DotNetObjectReference.Create(this);
            phase = "Leaflet";
            await Module.InvokeVoidAsync("initialize", "map", Self, Data.Settings);
            Ready = true;
            if (Status == loadingStatus) Status = "";
            RenderedPath = null;
            phase = "map layers";
            await RenderMap();
            AutomaticWeatherPoint = RouteMidpoint.Find(Data.RouteGeometry);
            await InvokeAsync(StateHasChanged);
            StartupDiagnostics.Write($"Map ready in {watch.ElapsedMilliseconds} ms; saved items={Data.Places.Count}.");
            StartupContextRefresh = CompleteStartupAsync();
        }
        catch (OperationCanceledException) when (Lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            MapInitializationFailed = !Ready; Status = $"{L["MapStartupFailed"]} {ex.Message}";
            Logger.LogWarning("Map initialization failed: {Reason}", ex.Message);
            StartupDiagnostics.Write($"Map initialization failed at {phase} after {watch.ElapsedMilliseconds} ms.", ex);
        }
        MapStarting = false;
        if (!Lifetime.IsCancellationRequested) await InvokeAsync(StateHasChanged);
    }
    private async Task CompleteStartupAsync()
    {
        try { await SaveDiskSettings(); await Task.WhenAll(Data.Settings.ShowCivilProtectionAlerts ? RefreshCivilAlertsAsync() : Task.CompletedTask, AutomaticWeatherPoint is not null ? UpdateWeatherAsync() : Task.CompletedTask); }
        catch (OperationCanceledException) when (Lifetime.IsCancellationRequested) { }
        catch (JSDisconnectedException) { }
        catch (Exception ex) { StartupDiagnostics.Write("Background startup update failed.", ex); }
    }
    private async Task Run(Func<Task> action)
    {
        if (Busy) return;
        Busy = true;
        try { await action(); }
        catch (OperationCanceledException) { if (!Lifetime.IsCancellationRequested) Status = "Il servizio non ha risposto in tempo. Riprova."; }
        catch (InvalidDataException ex) { Status = ex.Message; }
        catch (Exception) { Status = "Operazione non riuscita. Controlla il file o la connessione e riprova."; }
        finally { Busy = false; }
    }
    private async Task Save()
    {
        if (!DataLoaded) return;
        Data.Validate();
        await SaveDiskSettings();
        if (Module is null) return;
        if (!StorageWritable) { Status = "Salvataggio sospeso: i dati locali precedenti non sono stati sovrascritti."; return; }
        var route = new { Data.RoutePoints, Data.RouteGeometry, Data.RouteKind, Settings = new { Data.Settings.WeatherReferenceLatitude, Data.Settings.WeatherReferenceLongitude } };
        var ok = await Module.InvokeAsync<bool>("writeRouteStorage", JsonSerializer.Serialize(route, Json));
        if (!ok) Status = "Spazio browser insufficiente o archiviazione disabilitata: esporta la sessione per conservarla.";
    }
    private async Task RenderMap()
    {
        Appearances = new(Data.Folders);
        var path = Path;
        var draft = Data.RouteKind.StartsWith("Bozza");
        var routeUpdate = ReferenceEquals(RenderedPath, path) && RenderedDraft == draft ? null : path;
        if (Module is not null && Ready)
        {
            await Module.InvokeVoidAsync("render", Data.Settings, Data.Places.Where(Appearances.IsVisible).Select(MapPlace).ToList(), Data.RoutePoints, routeUpdate, draft, SelectedPlace?.Id, new { addPin = L["MapContextAddPin"], deletePin = L["DeleteButton"], edit = L["MapContextEdit"], save = L["MapContextSave"], cancel = L["CancelButton"], start = L["MapContextSetStart"], waypoint = L["MapContextSetWaypoint"], end = L["MapContextSetEnd"], weather = L["MapContextSetWeatherPoint"], directWaypoint = L["MapContextSetDirectWaypoint"], deleteRoutePoint = L["MapContextDeleteRoutePoint"], name = L["NameLabel"], description = L["DescriptionLabel"], icon = L["IconLabel"], color = L["ColorLabel"], inherit = L["InheritAppearance"], currentColor = L["ColorCurrent"], icons = Icons.Keys.ToDictionary(k => k, k => L["Icon_" + k]), colors = PinColors.ToDictionary(p => p.Key, p => L[p.Value]), weatherReference = L["WeatherReferenceTitle"], cardinals = Data.Settings.LanguageCode == "it-IT" ? new[] { "N", "E", "S", "O" } : new[] { "N", "E", "S", "W" } }, AlertAreas, WeatherPoint);
            RenderedPath = path; RenderedDraft = draft;
        }
    }
    private object MapPlace(SavedPlace place)
    {
        var appearance = Appearances.Resolve(place);
        return new { place.Id, place.Name, place.Description, place.Latitude, place.Longitude, place.IconKey, place.PinColorHex, effectiveIconKey = appearance.IconKey, effectivePinColorHex = appearance.PinColorHex };
    }
    private async Task Refresh() => await Run(async () => { await Save(); await RenderMap(); });
    private string FolderPath(SavedFolder folder)
    {
        var names = new List<string> { folder.Name }; var parent = folder.ParentId; var seen = new HashSet<Guid> { folder.Id };
        while (parent is { } id && seen.Add(id) && Data.Folders.FirstOrDefault(f => f.Id == id) is { } ancestor) { names.Insert(0, ancestor.Name); parent = ancestor.ParentId; }
        return string.Join(" / ", names);
    }
    private IEnumerable<SavedTreeEntry> BuildTree(Guid? parentId, int depth)
    {
        foreach (var folder in Data.Folders.Where(f => f.ParentId == parentId).OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            yield return new SavedTreeEntry(depth, folder, null);
            if (IsFolderCollapsed(folder)) continue;
            foreach (var entry in BuildTree(folder.Id, depth + 1)) yield return entry;
            foreach (var place in Data.Places.Where(p => p.FolderId == folder.Id && p.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase)).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)) yield return new SavedTreeEntry(depth + 1, null, place);
        }
        if (parentId is null)
            foreach (var place in Data.Places.Where(p => p.FolderId is null && p.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase)).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)) yield return new SavedTreeEntry(depth, null, place);
    }
    private bool FolderHasChildren(SavedFolder folder) => Data.Folders.Any(f => f.ParentId == folder.Id) || Data.Places.Any(p => p.FolderId == folder.Id && p.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase));
    private bool IsFolderCollapsed(SavedFolder folder) => CollapsedFolders.Contains(folder.Id);
    private void ToggleFolder(SavedFolder folder) { if (!CollapsedFolders.Add(folder.Id)) CollapsedFolders.Remove(folder.Id); }
    private async Task SetMode(string mode) { Mode = Mode == mode ? "explore" : mode; if (Module is not null) await Module.InvokeVoidAsync("setMode", Mode); }
    private async Task ToggleTrails() { Data.Settings.ShowHikingTrails = !Data.Settings.ShowHikingTrails; await Refresh(); }
    private async Task ToggleSatellite() { Data.Settings.ShowSatelliteOverlay = !Data.Settings.ShowSatelliteOverlay; await Refresh(); }
    private async Task ToggleContours() { Data.Settings.ShowContourLines = !Data.Settings.ShowContourLines; await Refresh(); }
    private async Task ToggleScale() { Data.Settings.ShowMapScale = !Data.Settings.ShowMapScale; await Refresh(); }
    private async Task ToggleSavedPoints() { Data.Settings.ShowSavedPoints = !Data.Settings.ShowSavedPoints; await Refresh(); }
    private async Task Center(double lat, double lon) { if (Module is not null && Ready) await Module.InvokeVoidAsync("center", lat, lon, 14); }
    private async Task FitRoute() { if (Module is not null && Ready) await Module.InvokeVoidAsync("fit", Path); }
    private async Task Find() => await Run(async () =>
    {
        SearchOpen = true;
        if (Query.Trim().Length < 2) { Status = "Inserisci almeno due caratteri."; return; }
        var center = Ready && Module is not null ? await Module.InvokeAsync<double[]>("getCenter") : [Data.Settings.DefaultLatitude, Data.Settings.DefaultLongitude];
        Results = await Search.SearchAsync(Query.Trim(), center[0], center[1], Lifetime.Token);
        Status = Results.Count == 0 ? "Nessun risultato trovato." : $"{Results.Count} risultati trovati";
    });
    private void ShowSearchResults() => SearchOpen = true;
    [JSInvokable] public async Task DismissSearch()
    {
        if (!SearchOpen) return;
        SearchOpen = false;
        await InvokeAsync(StateHasChanged);
    }
    private async Task SaveResult(SearchResult result) => await AddPin(result.Latitude, result.Longitude, result.DisplayName.Split(',')[0]);
    private async Task AddPin(double lat, double lon, string name) => await Run(async () =>
    {
        if (!WebSession.CoordinatesValid(lat, lon)) throw new InvalidDataException("Coordinate non valide.");
        SelectedPlace = new SavedPlace { Name = name, Latitude = lat, Longitude = lon, FolderId = SelectedFolder?.Id };
        Data.Places.Add(SelectedPlace); SelectedFolder = null;
        await Save(); await RenderMap(); Status = "Punto aggiunto alla libreria";
    });
    private async Task SelectPlace(SavedPlace p) { SelectedPlace = p; SelectedFolder = null; await RenderMap(); }
    private async Task OpenPlaceContext(Guid id) { if (Module is not null && Ready) await Module.InvokeVoidAsync("openPlaceContext", id.ToString()); }
    private void SelectFolder(SavedFolder folder) { SelectedFolder = folder; SelectedPlace = null; }
    private void OpenFolderEditor(SavedFolder folder)
    {
        SelectFolder(folder);
        FolderDraftName = folder.Name; FolderDraftIcon = folder.IconKey ?? ""; FolderDraftColor = folder.PinColorHex ?? "";
        FolderError = ""; SearchOpen = false; Dialog = 'd';
    }
    private async Task SaveFolderEditor() => await SaveFolderChanges(false);
    private async Task ForceFolderColors()
    {
        if (SelectedFolder is null || Busy) return;
        if (string.IsNullOrWhiteSpace(FolderDraftName)) { FolderError = L["FolderNameRequired"]; return; }
        if (await JS.InvokeAsync<bool>("confirm", L["FolderForceColorsConfirm"])) await SaveFolderChanges(true);
    }
    private async Task SaveFolderChanges(bool resetPinColors)
    {
        if (SelectedFolder is null || Busy) return;
        if (string.IsNullOrWhiteSpace(FolderDraftName)) { FolderError = L["FolderNameRequired"]; return; }
        SelectedFolder.Name = FolderDraftName.Trim();
        SelectedFolder.IconKey = Icons.ContainsKey(FolderDraftIcon) ? FolderDraftIcon : "";
        SelectedFolder.PinColorHex = SavedAppearanceResolver.IsColor(FolderDraftColor) ? FolderDraftColor : "";
        if (resetPinColors) SavedAppearanceResolver.ResetPinColors(SelectedFolder.Id, Data.Folders, Data.Places);
        await Refresh(); Dialog = '\0';
    }
    private async Task NewFolder() { var folder = new SavedFolder { ParentId = SelectedFolder?.Id }; Data.Folders.Add(folder); SelectFolder(folder); await Refresh(); }
    private async Task DeleteFolder()
    {
        if (SelectedFolder is null || !await JS.InvokeAsync<bool>("confirm", "Eliminare la cartella? I punti saranno conservati nella libreria.")) return;
        foreach (var p in Data.Places.Where(p => p.FolderId == SelectedFolder.Id)) p.FolderId = null;
        foreach (var f in Data.Folders.Where(f => f.ParentId == SelectedFolder.Id)) f.ParentId = SelectedFolder.ParentId;
        Data.Folders.Remove(SelectedFolder); SelectedFolder = null; await Refresh();
    }
    private async Task DuplicatePlace() { if (SelectedPlace is null) return; SelectedPlace = SelectedPlace.Clone(); Data.Places.Add(SelectedPlace); await Refresh(); }
    private async Task DeletePlace() { if (SelectedPlace is null || !await JS.InvokeAsync<bool>("confirm", "Eliminare il punto selezionato?")) return; Data.Places.Remove(SelectedPlace); SelectedPlace = null; await Refresh(); }
    private async Task AddStop(double lat, double lon, string name = "Tappa", string pointType = "Waypoint")
    {
        if (Busy) return;
        if (!WebSession.CoordinatesValid(lat, lon)) return;
        if (pointType == "End") Data.RoutePoints.RemoveAll(p => p.PointType == "End");
        if (Data.RoutePoints.Count == 0 && pointType != "End") pointType = "Start";
        var point = new RoutePoint { Name = name, Latitude = lat, Longitude = lon, PointType = pointType };
        if (pointType is "Waypoint" or "DirectWaypoint" && Data.RoutePoints.LastOrDefault()?.PointType == "End") Data.RoutePoints.Insert(Data.RoutePoints.Count - 1, point);
        else Data.RoutePoints.Add(point);
        NormalizeStops(); await DraftRoute();
    }
    private void NormalizeStops()
    {
        var end = Data.RoutePoints.LastOrDefault(p => p.PointType == "End");
        Data.RoutePoints.RemoveAll(p => p.PointType == "End" && p != end);
        if (end is not null) { Data.RoutePoints.Remove(end); Data.RoutePoints.Add(end); }
        for (var i = 0; i < Data.RoutePoints.Count; i++)
            if (Data.RoutePoints[i] != end && Data.RoutePoints[i].PointType != "DirectWaypoint")
                Data.RoutePoints[i].PointType = i == 0 ? "Start" : "Waypoint";
    }
    private string StopNumber(int index) => Data.RoutePoints[index].PointType switch
    {
        "Start" => "A", "End" => "B",
        _ => Data.RoutePoints.Take(index + 1).Count(p => p.PointType is "Waypoint" or "DirectWaypoint").ToString(CultureInfo.InvariantCulture)
    };
    private string StopTitle(int index) => Data.RoutePoints[index].PointType switch
    {
        "Start" => L["RouteStartLabel"], "End" => L["RouteEndLabel"],
        _ => $"{L["RouteWaypointLabel"]} {StopNumber(index)}"
    };
    private async Task DraftRoute()
    {
        RouteCalculation?.Cancel();
        WeatherRequest?.Cancel(); AutomaticWeatherPoint = null; WeatherBusy = false;
        WeatherStatus = L["WeatherInitialPrompt"];
        NormalizeStops();
        using var calculation = CancellationTokenSource.CreateLinkedTokenSource(Lifetime.Token);
        RouteCalculation = calculation;
        var token = calculation.Token;
        RouteError = "";
        Data.RouteGeometry = []; Data.RouteKind = "Bozza diretta"; Forecast = null;
        IsRouting = Data.RoutePoints.Count >= 2 && Data.RoutePoints[0].PointType == "Start" && Data.RoutePoints[^1].PointType == "End";
        Status = IsRouting ? "Calcolo traccia in corso…" : "Aggiungi un punto di arrivo per calcolare la traccia.";
        var stops = Data.RoutePoints.Select(p => new RoutePoint { Name = p.Name, Latitude = p.Latitude, Longitude = p.Longitude, PointType = p.PointType }).ToList();
        try
        {
            Data.Validate();
            await Save();
            token.ThrowIfCancellationRequested();
            await RenderMap();
            await InvokeAsync(StateHasChanged);
            if (!IsRouting) return;
            await Task.Delay(250, token);
            var path = await Routing.BuildRouteAsync(stops, token);
            token.ThrowIfCancellationRequested();
            Data.RouteGeometry = path;
            Data.RouteKind = stops.Any(p => p.PointType == "DirectWaypoint") ? "Percorso con segmenti diretti" : "Percorso escursionistico";
            await Save();
            token.ThrowIfCancellationRequested();
            await RenderMap();
            token.ThrowIfCancellationRequested();
            Status = "Traccia calcolata";
            IsRouting = false;
            AutomaticWeatherPoint = RouteMidpoint.Find(path);
            await Task.WhenAll(UpdateWeatherAsync(token), RefreshCivilAlertsAsync(token));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (OperationCanceledException)
        {
            if (!token.IsCancellationRequested) RouteError = Status = "OpenRouteService non ha risposto in tempo. Riprova modificando un punto.";
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                RouteError = Status = ex is InvalidDataException ? ex.Message : "Impossibile aggiornare la traccia. Controlla la connessione e riprova.";
                Logger.LogWarning("Calcolo traccia fallito: {Reason}", RouteError);
            }
        }
        finally
        {
            if (ReferenceEquals(RouteCalculation, calculation))
            {
                RouteCalculation = null;
                IsRouting = false;
                if (!Lifetime.IsCancellationRequested) await InvokeAsync(StateHasChanged);
            }
        }
    }
    private async Task MoveStop(int index, int direction) { if (Busy || index + direction < 0 || index + direction >= Data.RoutePoints.Count) return; (Data.RoutePoints[index], Data.RoutePoints[index + direction]) = (Data.RoutePoints[index + direction], Data.RoutePoints[index]); NormalizeStops(); await DraftRoute(); }
    private async Task RemoveStop(int index) { if (Busy) return; Data.RoutePoints.RemoveAt(index); NormalizeStops(); await DraftRoute(); }
    private async Task ClearRoute() { if (Busy || !await JS.InvokeAsync<bool>("confirm", "Svuotare la traccia attuale?")) return; Data.RoutePoints.Clear(); await DraftRoute(); }
    private void OpenCoordinates(string target) { CoordinateTarget = target; CoordinateLat = Data.Settings.DefaultLatitude; CoordinateLon = Data.Settings.DefaultLongitude; CoordinateName = target == "pin" ? "Nuovo luogo" : "Tappa"; Dialog = 'c'; }
    private async Task AddCoordinates()
    {
        if (!WebSession.CoordinatesValid(CoordinateLat, CoordinateLon)) { Status = "Latitudine tra −90 e 90, longitudine tra −180 e 180."; return; }
        if (CoordinateTarget == "pin") await AddPin(CoordinateLat, CoordinateLon, CoordinateName); else await AddStop(CoordinateLat, CoordinateLon, CoordinateName);
        await Center(CoordinateLat, CoordinateLon); Dialog = '\0';
    }
    [JSInvokable] public async Task MapClick(double lat, double lon, string mode)
    {
        if (Busy || !WebSession.CoordinatesValid(lat, lon)) return;
        if (mode == "pin") await AddPin(lat, lon, "Nuovo luogo");
        else if (mode == "route") await AddStop(lat, lon);
        else if (mode == "weather") { Data.Settings.WeatherReferenceLatitude = lat; Data.Settings.WeatherReferenceLongitude = lon; await Refresh(); await LoadWeather(); }
        await InvokeAsync(StateHasChanged);
    }
    [JSInvokable] public async Task MapAction(double lat, double lon, string action, string name, int routeIndex)
    {
        if (Busy || !WebSession.CoordinatesValid(lat, lon)) return;
        var pointName = string.IsNullOrWhiteSpace(name) ? "Tappa" : name;
        if (action == "deleteRoutePoint" && routeIndex >= 0 && routeIndex < Data.RoutePoints.Count) await RemoveStop(routeIndex);
        else if (action == "pin") await AddPin(lat, lon, string.IsNullOrWhiteSpace(name) ? "Nuovo luogo" : name);
        else if (action == "start")
        {
            if (Data.RoutePoints.Count > 0 && !await JS.InvokeAsync<bool>("confirm", "Impostare un nuovo punto di partenza eliminerà la traccia attuale. Continuare?")) return;
            Data.RoutePoints.Clear();
            await AddStop(lat, lon, pointName, "Start");
        }
        else if (action == "end") await AddStop(lat, lon, pointName, "End");
        else if (action == "waypoint") await AddStop(lat, lon, pointName, "Waypoint");
        else if (action == "directWaypoint") await AddStop(lat, lon, pointName, "DirectWaypoint");
        else if (action == "weather")
        {
            Data.Settings.WeatherReferenceLatitude = lat;
            Data.Settings.WeatherReferenceLongitude = lon;
            await Refresh();
            await LoadWeather();
        }
        await InvokeAsync(StateHasChanged);
    }
    [JSInvokable] public async Task PinSelected(string id)
    {
        if (Guid.TryParse(id, out var guid) && Data.Places.FirstOrDefault(p => p.Id == guid) is { } place) { SelectedPlace = place; SelectedFolder = null; }
        await InvokeAsync(StateHasChanged);
    }
    [JSInvokable] public async Task DeletePlaceFromMap(string id)
    {
        if (Guid.TryParse(id, out var guid) && Data.Places.FirstOrDefault(p => p.Id == guid) is { } place) { SelectedPlace = place; SelectedFolder = null; await DeletePlace(); }
        await InvokeAsync(StateHasChanged);
    }
    [JSInvokable] public async Task EditPlace(string id, string name, string description, string iconKey, string pinColorHex)
    {
        if (!Guid.TryParse(id, out var guid) || Data.Places.FirstOrDefault(p => p.Id == guid) is not { } place) return;
        place.Name = string.IsNullOrWhiteSpace(name) ? "Nuovo luogo" : name.Trim();
        place.Description = description?.Trim() ?? string.Empty;
        place.IconKey = Icons.ContainsKey(iconKey) ? iconKey : "";
        place.PinColorHex = SavedAppearanceResolver.IsColor(pinColorHex) ? pinColorHex : "";
        SelectedPlace = place; SelectedFolder = null;
        await Refresh();
        await InvokeAsync(StateHasChanged);
    }
    private async Task ChangeUnits()
    {
        Data.Settings.TravelSpeed *= Imperial ? 0.621371 : 1.609344;
        Data.Settings.IsMetric = !Imperial; Data.Settings.DistanceUnit = Imperial ? "mi" : "km"; Data.Settings.AltitudeUnit = Imperial ? "ft" : "m"; Data.Settings.SpeedUnit = Imperial ? "mph" : "km/h"; Data.Settings.TemperatureUnit = Imperial ? "F" : "C";
        Forecast = null; await ApplySettings();
    }
    private async Task ApplySettings() { L.SetLanguage(Data.Settings.LanguageCode); if (Forecast is null) WeatherStatus = L["WeatherInitialPrompt"]; await Refresh(); await Task.WhenAll(RefreshCivilAlertsAsync(), WeatherPoint is not null ? UpdateWeatherAsync() : Task.CompletedTask); }
    private async Task<StoredApplicationData?> LoadDiskSettings(bool includeLibrary = false)
    {
        try
        {
            var stored = await SettingsStore.LoadAsync(Lifetime.Token);
            if (stored is not null)
            {
                stored.Settings.WeatherReferenceLatitude = Data.Settings.WeatherReferenceLatitude;
                stored.Settings.WeatherReferenceLongitude = Data.Settings.WeatherReferenceLongitude;
                Data.Settings = stored.Settings;
                if (includeLibrary && stored.Folders is not null && stored.Places is not null)
                {
                    Data.Folders = stored.Folders; Data.Places = stored.Places; StorageWritable = true;
                }
            }
            DiskSettingsWritable = true;
            return stored;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            DiskSettingsWritable = false; Status = L["SettingsDiskReadFailed"];
            Logger.LogWarning("Cannot load application settings: {Reason}", ex.Message);
            return null;
        }
    }
    private async Task SaveDiskSettings()
    {
        if (!DiskSettingsWritable || !DataLoaded) return;
        try { await SettingsStore.SaveAsync(Data, Lifetime.Token); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = L["SettingsDiskSaveFailed"];
            Logger.LogWarning("Cannot save application settings: {Reason}", ex.Message);
        }
    }
    private async Task OpenSettings()
    {
        await LoadDiskSettings();
        L.SetLanguage(Data.Settings.LanguageCode); SearchOpen = false; Dialog = 's';
        await ApplySettings();
    }
    private async Task AutoSaveSettings() => await Run(Save);
    private async Task SaveSettings()
    {
        try { Data.Validate(); } catch (InvalidDataException e) { Status = e.Message; return; }
        if (!DiskSettingsWritable)
        {
            if (!await JS.InvokeAsync<bool>("confirm", L["SettingsDiskReplaceConfirm"])) return;
            DiskSettingsWritable = true;
        }
        await ApplySettings(); Dialog = '\0';
    }
    private async Task Export(string kind) => await Run(async () =>
    {
        Data.Validate();
        object content = kind switch
        {
            "backup" => TransferService.BuildBackup(Data),
            _ => TransferService.BuildRoute(Data)
        };
        var text = kind == "gpx" ? Gpx.BuildGpx(Data.RoutePoints, Path) : JsonSerializer.Serialize(content, Json);
        var extension = kind switch { "gpx" => "gpx", "backup" => "ohmbackup.json", _ => "ohk" };
        await Module!.InvokeVoidAsync("download", $"openhikemap-{kind}.{extension}", text, kind == "gpx" ? "application/xml" : "application/json");
        Status = L["ExportDone"];
    });
    private async Task Import(InputFileChangeEventArgs e) => await Run(async () =>
    {
        await using var input = e.File.OpenReadStream(8 * 1024 * 1024, Lifetime.Token);
        using var stream = new MemoryStream(); await input.CopyToAsync(stream, Lifetime.Token); stream.Position = 0;
        ImportPlan plan;
        try { plan = TransferService.Parse(e.File.Name, stream, Data, Gpx); }
        catch (ImportFormatException ex) { throw new InvalidDataException(L[ex.Message]); }
        catch (Exception ex) when (ex is JsonException or System.Xml.XmlException or InvalidDataException) { throw new InvalidDataException(L["ImportUnsupported"]); }
        if (!await JS.InvokeAsync<bool>("confirm", L[plan.Kind == ImportKind.Backup ? "ImportBackupConfirm" : "ImportRouteConfirm"])) return;
        if (!StorageWritable && !await JS.InvokeAsync<bool>("confirm", L["ImportStorageConfirm"])) return;
        RouteCalculation?.Cancel(); RouteCalculation = null; IsRouting = false; RouteError = "";
        WeatherRequest?.Cancel(); AlertsRequest?.Cancel();
        Data = plan.Data; StorageWritable = true; if (plan.Kind == ImportKind.Backup) { DiskSettingsWritable = true; CollapsedFolders.Clear(); }
        L.SetLanguage(Data.Settings.LanguageCode); SelectedPlace = null; SelectedFolder = null; Forecast = null;
        AutomaticWeatherPoint = RouteMidpoint.Find(Data.RouteGeometry);
        await Save(); await RenderMap(); if (Path.Count > 0) await FitRoute(); else await Center(Data.Settings.DefaultLatitude, Data.Settings.DefaultLongitude); Dialog = '\0'; Status = L["ImportDone"];
        await Task.WhenAll(RefreshCivilAlertsAsync(), AutomaticWeatherPoint is not null ? UpdateWeatherAsync() : Task.CompletedTask);
    });
    public async ValueTask DisposeAsync()
    {
        RouteCalculation?.Cancel(); RouteCalculation = null;
        WeatherRequest?.Cancel(); AlertsRequest?.Cancel();
        Lifetime.Cancel(); Lifetime.Dispose();
        try { await StartupContextRefresh.WaitAsync(TimeSpan.FromSeconds(2)); } catch (Exception ex) when (ex is OperationCanceledException or TimeoutException) { }
        if (Module is not null && !HostLifetime.ApplicationStopping.IsCancellationRequested)
        {
            try { await Module.InvokeVoidAsync("dispose"); await Module.DisposeAsync(); } catch (JSDisconnectedException) { }
        }
        Self?.Dispose();
    }
}
