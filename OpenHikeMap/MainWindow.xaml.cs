using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
namespace OpenHikeMap;
public partial class MainWindow : Window
{
    private static readonly HashSet<string> AllowedPinColors =
    [
        "#2E7D5B",
        "#D65A5A",
        "#3F6F8F",
        "#D9824B",
        "#8B6B4A",
        "#7657A8",
        "#C49A3A",
        "#59636E"
    ];
    private readonly MainWindowViewModel _viewModel = new();
    private readonly AppStorageService _storageService = new();
    private readonly SearchService _searchService = new(new HttpClient());
    private readonly RoutingService _routingService = new(new HttpClient());
    private readonly WeatherService _weatherService = new(new HttpClient());
    private readonly ElevationService _elevationService = new(new HttpClient());
    private readonly CivilProtectionAlertService _civilProtectionAlertService = new(new HttpClient());
    private readonly RegionBoundaryService _regionBoundaryService = new(new HttpClient());
    private readonly GpxService _gpxService = new();
    private readonly KmlService _kmlService = new();
    private readonly ThemeService _themeService = new();
    private bool _placePickMode;
    private bool _routePickMode;
    private bool _mapReady;
    private double? _mapLatitude;
    private double? _mapLongitude;
    private int _refreshVersion;
    private List<RoutePathPoint> _draftRouteGeometry = [];
    private bool _draftRouteHasDirectSegments;
    private bool _suppressRouteCollectionsChanged;
    private Guid? _selectedPlaceIdOnMap;
    private CancellationTokenSource? _weatherCancellationTokenSource;
    private List<object> _civilAlertAreas = [];
    private readonly ToolTip _profileToolTip = new() { Placement = System.Windows.Controls.Primitives.PlacementMode.Relative, StaysOpen = true };
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += MainWindow_Loaded;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.Folders.CollectionChanged += FoldersChanged;
        _viewModel.Places.CollectionChanged += PlacesChanged;
        _viewModel.RoutePoints.CollectionChanged += RouteCollectionsChanged;
        _viewModel.RouteGeometry.CollectionChanged += RouteGeometryChanged;
    }
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
        HookFolderEvents();
        HookPlaceEvents();
        ApplyTheme();
        ApplyLanguage();
        await InitializeMapAsync();
    }
    private async Task LoadDataAsync()
    {
        var appData = await _storageService.LoadAsync();
        _viewModel.Settings = appData.Settings;
        _viewModel.Settings.WeatherReferenceLatitude = null;
        _viewModel.Settings.WeatherReferenceLongitude = null;
        AttachSettingsChanged();
        _viewModel.L.SetLanguage(_viewModel.Settings.LanguageCode);
        _viewModel.UpdateIconOptions(_viewModel.Settings.LanguageCode);
        _viewModel.Folders.Clear();
        foreach (var folder in appData.Folders)
        {
            folder.Name = SanitizeMarkup(folder.Name);
            folder.PinColorHex = NormalizePinColor(folder.PinColorHex);
            _viewModel.Folders.Add(folder);
        }
        _viewModel.Places.Clear();
        foreach (var place in appData.Places)
        {
            place.Name = SanitizeMarkup(place.Name);
            place.Description = SanitizeMarkup(place.Description);
            place.PinColorHex = NormalizePinColor(place.PinColorHex);
            _viewModel.Places.Add(place);
        }
        _viewModel.RoutePoints.Clear();
        _viewModel.RouteGeometry.Clear();
        _viewModel.SelectedPlace = _viewModel.Places.FirstOrDefault();
        RebuildPlaceTree();
        UpdateRouteSummary();
        UpdateProfilePaneState();
        await UpdateProfileSummaryAsync();
        ResetWeatherState();
        DataContext = null;
        DataContext = _viewModel;
    }
    private void AttachSettingsChanged()
    {
        _viewModel.Settings.PropertyChanged -= Settings_PropertyChanged;
        _viewModel.Settings.PropertyChanged += Settings_PropertyChanged;
    }
    private async void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.ThemeMode))
        {
            ApplyTheme();
        }
        if (e.PropertyName == nameof(AppSettings.LanguageCode))
        {
            ApplyLanguage();
            if (_mapReady)
            {
                MapWebView.NavigateToString(BuildMapHtml());
            }
        }
        await SaveDataAsync();
        await RefreshMapAsync();
        await UpdateProfileSummaryAsync();
        await RefreshWeatherAsync();
    }
    private void ApplyLanguage()
    {
        _viewModel.L.SetLanguage(_viewModel.Settings.LanguageCode);
        _viewModel.UpdateIconOptions(_viewModel.Settings.LanguageCode);
        RebuildPlaceTree();
        Title = _viewModel.L["WindowTitle"];
    }
    private async Task InitializeMapAsync()
    {
        await MapWebView.EnsureCoreWebView2Async();
        MapWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        MapWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        MapWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        MapWebView.NavigateToString(BuildMapHtml());
    }
    private void HookFolderEvents()
    {
        foreach (var folder in _viewModel.Folders)
        {
            folder.PropertyChanged += Folder_PropertyChanged;
        }
    }
    private void FoldersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<SavedFolder>())
            {
                item.PropertyChanged += Folder_PropertyChanged;
            }
        }
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<SavedFolder>())
            {
                item.PropertyChanged -= Folder_PropertyChanged;
            }
        }
        RebuildPlaceTree();
        _ = SaveAndRefreshPlacesAsync();
    }
    private void HookPlaceEvents()
    {
        foreach (var place in _viewModel.Places)
        {
            place.PropertyChanged += Place_PropertyChanged;
        }
    }
    private void PlacesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<SavedPlace>())
            {
                item.PropertyChanged += Place_PropertyChanged;
            }
        }
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<SavedPlace>())
            {
                item.PropertyChanged -= Place_PropertyChanged;
            }
        }
        RebuildPlaceTree();
        _ = SaveAndRefreshPlacesAsync();
    }
    private void Folder_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SavedFolder.IsEnabled))
        {
            RebuildPlaceTree();
            _ = SaveAndRefreshPlacesAsync();
        }
    }
    private void RouteCollectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_suppressRouteCollectionsChanged)
        {
            return;
        }
        if (_viewModel.RouteGeometry.Count > 0)
        {
            try
            {
                _suppressRouteCollectionsChanged = true;
                _viewModel.RouteGeometry.Clear();
            }
            finally
            {
                _suppressRouteCollectionsChanged = false;
            }
        }
        _ = RebuildDraftRouteAsync();
        UpdateRouteSummary();
        UpdateProfilePaneState();
        _ = UpdateProfileSummaryAsync();
        _ = RefreshWeatherAsync();
    }
    private void RouteGeometryChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = RefreshMapAsync();
        UpdateRouteSummary();
        UpdateProfilePaneState();
        _ = UpdateProfileSummaryAsync();
        _ = RefreshWeatherAsync();
    }
    private void Place_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SavedPlace.IsVisible))
        {
            RebuildPlaceTree();
            _ = SaveAndRefreshPlacesAsync();
        }
    }
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SavedPlacesSearchQuery))
        {
            RebuildPlaceTree();
        }
    }
    private async Task SaveAndRefreshPlacesAsync()
    {
        RebuildPlaceTree();
        await SaveDataAsync();
        await RefreshMapAsync();
    }
    private void UpdateProfilePaneState()
    {
        var hasEndPoint = _viewModel.RoutePoints.Any(point => string.Equals(point.PointType, "End", StringComparison.OrdinalIgnoreCase));
        var hasGeometry = _viewModel.RouteGeometry.Count > 1 || _draftRouteGeometry.Count > 1;
        if (!hasEndPoint || !hasGeometry)
        {
            _viewModel.IsProfilePaneExpanded = false;
        }
    }
    private async Task SaveDataAsync()
    {
        await _storageService.SaveAsync(new AppDataModel
        {
            Settings = CreateSettingsSnapshot(includeWeatherReference: false),
            Folders = _viewModel.Folders.ToList(),
            Places = _viewModel.Places.ToList(),
            RoutePoints = []
        });
    }
    private AppSettings CreateSettingsSnapshot(bool includeWeatherReference)
    {
        var json = JsonSerializer.Serialize(_viewModel.Settings);
        var copy = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        if (!includeWeatherReference)
        {
            copy.WeatherReferenceLatitude = null;
            copy.WeatherReferenceLongitude = null;
        }
        return copy;
    }
    private async Task RefreshMapAsync()
    {
        if (!_mapReady || MapWebView.CoreWebView2 is null)
        {
            return;
        }
        var weatherLatitude = 0d;
        var weatherLongitude = 0d;
        var hasWeatherReferencePoint = _viewModel.Settings.ShowWeather && TryGetWeatherReferencePoint(out weatherLatitude, out weatherLongitude);
        var refreshVersion = Interlocked.Increment(ref _refreshVersion);
        var payload = new
        {
            settings = new
            {
                latitude = _viewModel.Settings.DefaultLatitude,
                longitude = _viewModel.Settings.DefaultLongitude,
                zoom = _viewModel.Settings.DefaultZoom,
                mapStyle = _viewModel.Settings.MapStyle,
                showSatelliteOverlay = _viewModel.Settings.ShowSatelliteOverlay,
                showHikingTrails = _viewModel.Settings.ShowHikingTrails,
                showContourLines = _viewModel.Settings.ShowContourLines,
                showSavedPoints = _viewModel.Settings.ShowSavedPoints,
                showCivilAlerts = _viewModel.Settings.ShowCivilProtectionAlerts,
                showBearingCircle = _viewModel.Settings.ShowBearingCircle,
                showRulerTool = _viewModel.Settings.ShowRulerTool,
                showMapScale = _viewModel.Settings.ShowMapScale,
                distanceUnit = _viewModel.Settings.DistanceUnit
            },
            places = _viewModel.Places.Where(IsPlaceVisibleOnMap).Select(place => new
            {
                id = place.Id,
                name = place.Name,
                description = place.Description,
                latitude = place.Latitude,
                longitude = place.Longitude,
                iconKey = place.IconKey,
                pinColorHex = GetEffectivePinColor(place),
                iconSvg = GetPinheadSvg(place.IconKey)
            }),
            routePoints = _viewModel.RoutePoints.Select(point => new
            {
                index = _viewModel.RoutePoints.IndexOf(point),
                name = point.Name,
                latitude = point.Latitude,
                longitude = point.Longitude,
                pointType = point.PointType
            }),
            routeGeometry = _viewModel.RouteGeometry.Select(point => new { latitude = point.Latitude, longitude = point.Longitude }),
            draftRouteGeometry = _draftRouteGeometry.Select(point => new { latitude = point.Latitude, longitude = point.Longitude }),
            draftRouteHasDirectSegments = _draftRouteHasDirectSegments,
            civilAlertAreas = _civilAlertAreas,
            weatherReferencePoint = hasWeatherReferencePoint
                ? new { latitude = weatherLatitude, longitude = weatherLongitude, title = "Punto di riferimento per meteo" }
                : null,
            refreshVersion
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        try
        {
            await MapWebView.CoreWebView2.ExecuteScriptAsync($"window.renderState({json});");
        }
        catch
        {
        }
    }
    private bool IsPlaceVisibleOnMap(SavedPlace place)
    {
        if (!place.IsVisible)
        {
            return false;
        }
        var folderId = place.FolderId;
        while (folderId is not null)
        {
            var folder = _viewModel.Folders.FirstOrDefault(item => item.Id == folderId.Value);
            if (folder is null)
            {
                break;
            }
            if (!folder.IsEnabled)
            {
                return false;
            }
            folderId = folder.ParentId;
        }
        return true;
    }
    private string GetEffectivePinColor(SavedPlace place)
    {
        var placeColor = NormalizePinColor(place.PinColorHex);
        if (!string.Equals(placeColor, SavedPlace.DefaultPinColor, StringComparison.OrdinalIgnoreCase))
        {
            return placeColor;
        }
        var folderId = place.FolderId;
        while (folderId is not null)
        {
            var folder = _viewModel.Folders.FirstOrDefault(item => item.Id == folderId.Value);
            if (folder is null)
            {
                break;
            }
            var folderColor = NormalizePinColor(folder.PinColorHex);
            if (!string.Equals(folderColor, SavedPlace.DefaultPinColor, StringComparison.OrdinalIgnoreCase))
            {
                return folderColor;
            }
            folderId = folder.ParentId;
        }
        return SavedPlace.DefaultPinColor;
    }
    private static string NormalizePinColor(string? pinColorHex)
        => AllowedPinColors.Contains(pinColorHex ?? string.Empty) ? pinColorHex! : SavedPlace.DefaultPinColor;
    private static string SanitizeMarkup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        var normalized = value
            .Replace("<br>", Environment.NewLine, StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", Environment.NewLine, StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", Environment.NewLine, StringComparison.OrdinalIgnoreCase)
            .Replace("<div>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("</div>", Environment.NewLine, StringComparison.OrdinalIgnoreCase);
        normalized = Regex.Replace(normalized, "<.*?>", string.Empty);
        return normalized.Trim();
    }
    private static void NormalizePlaces(IEnumerable<SavedPlace> places)
    {
        foreach (var place in places)
        {
            place.Name = SanitizeMarkup(place.Name);
            place.Description = SanitizeMarkup(place.Description);
            place.PinColorHex = NormalizePinColor(place.PinColorHex);
        }
    }
    private static void NormalizeFolders(IEnumerable<SavedFolder> folders)
    {
        foreach (var folder in folders)
        {
            folder.Name = SanitizeMarkup(folder.Name);
            folder.PinColorHex = NormalizePinColor(folder.PinColorHex);
        }
    }
    private void RebuildPlaceTree()
    {
        var query = _viewModel.SavedPlacesSearchQuery?.Trim();
        var folderLookup = _viewModel.Folders.ToDictionary(folder => folder.Id, folder => new PlaceTreeNode
        {
            Name = folder.Name,
            Folder = folder
        });
        foreach (var folder in _viewModel.Folders)
        {
            if (folder.ParentId is Guid parentId && folderLookup.TryGetValue(parentId, out var parentNode))
            {
                parentNode.Children.Add(folderLookup[folder.Id]);
            }
        }
        var roots = new List<PlaceTreeNode>();
        foreach (var folder in _viewModel.Folders.Where(folder => folder.ParentId is null))
        {
            roots.Add(folderLookup[folder.Id]);
        }
        var filteredPlaces = _viewModel.Places.Where(place =>
            string.IsNullOrWhiteSpace(query) ||
            place.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        foreach (var place in filteredPlaces)
        {
            var node = new PlaceTreeNode
            {
                Name = place.Name,
                Place = place
            };
            if (place.FolderId is Guid folderId && folderLookup.TryGetValue(folderId, out var folderNode))
            {
                folderNode.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }
        foreach (var folderNode in folderLookup.Values)
        {
            SortTreeNodes(folderNode.Children);
        }
        SortTreeNodes(roots);
        _viewModel.PlaceTree.Clear();
        foreach (var root in roots)
        {
            _viewModel.PlaceTree.Add(root);
        }
    }
    private static void SortTreeNodes(IList<PlaceTreeNode> nodes)
    {
        var ordered = nodes
            .OrderBy(node => node.IsPlace)
            .ThenBy(node => node.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        nodes.Clear();
        foreach (var node in ordered)
        {
            if (node.Children.Count > 0)
            {
                SortTreeNodes(node.Children);
            }
            nodes.Add(node);
        }
    }
    private async Task RebuildDraftRouteAsync()
    {
        if (_viewModel.RoutePoints.Count < 2)
        {
            _draftRouteHasDirectSegments = false;
            _draftRouteGeometry = _viewModel.RoutePoints
                .Select(point => new RoutePathPoint
                {
                    Latitude = point.Latitude,
                    Longitude = point.Longitude
                })
                .ToList();
        }
        else
        {
            _draftRouteHasDirectSegments = _viewModel.RoutePoints.Any(point => point.PointType == "DirectWaypoint");
            _draftRouteGeometry = await _routingService.BuildRouteAsync(_viewModel.RoutePoints.ToList());
        }
        await UpdateProfileSummaryAsync();
        await RefreshMapAsync();
        await RefreshWeatherAsync();
    }
    private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var document = JsonDocument.Parse(e.WebMessageAsJson);
        var root = document.RootElement;
        var type = root.GetProperty("type").GetString();
        if (type == "map-ready")
        {
            _mapReady = true;
            await RefreshMapAsync();
            return;
        }
        if (type == "map-center")
        {
            _mapLatitude = root.GetProperty("latitude").GetDouble();
            _mapLongitude = root.GetProperty("longitude").GetDouble();
            return;
        }
        if (type == "map-click")
        {
            var latitude = root.GetProperty("latitude").GetDouble();
            var longitude = root.GetProperty("longitude").GetDouble();
            if (_placePickMode)
            {
                var place = CreatePlace(latitude, longitude, $"Place {_viewModel.Places.Count + 1}");
                _viewModel.Places.Add(place);
                _viewModel.SelectedPlace = place;
                _selectedPlaceIdOnMap = place.Id;
                _viewModel.StatusMessage = "Pin created from map click.";
                DisablePickModes();
                await SaveAndRefreshPlacesAsync();
            }
            else if (_routePickMode)
            {
                AddRoutePoint(latitude, longitude, "Waypoint");
            }
            return;
        }
        if (type == "map-context")
        {
            var latitude = root.GetProperty("latitude").GetDouble();
            var longitude = root.GetProperty("longitude").GetDouble();
            var action = root.GetProperty("action").GetString();
            var placeName = root.TryGetProperty("placeName", out var placeNameElement)
                ? placeNameElement.GetString()
                : null;
            switch (action)
            {
                case "pin":
                    var place = CreatePlace(latitude, longitude, $"Place {_viewModel.Places.Count + 1}");
                    _viewModel.Places.Add(place);
                    RebuildPlaceTree();
                    _viewModel.SelectedFolder = null;
                    _viewModel.SelectedPlace = place;
                    _selectedPlaceIdOnMap = place.Id;
                    await SaveAndRefreshPlacesAsync();
                    break;
                case "start":
                    AddRoutePoint(latitude, longitude, "Start", placeName);
                    break;
                case "waypoint":
                    AddRoutePoint(latitude, longitude, "Waypoint", placeName);
                    break;
                case "directWaypoint":
                    AddRoutePoint(latitude, longitude, "DirectWaypoint", placeName);
                    break;
                case "end":
                    AddRoutePoint(latitude, longitude, "End", placeName);
                    break;
                case "weatherReference":
                    await SetWeatherReferencePointAsync(latitude, longitude);
                    break;
                case "deleteRoutePoint":
                    if (root.TryGetProperty("routePointIndex", out var routePointIndexElement))
                    {
                        var index = routePointIndexElement.GetInt32();
                        if (index >= 0 && index < _viewModel.RoutePoints.Count)
                        {
                            _viewModel.RoutePoints.RemoveAt(index);
                            _viewModel.RouteGeometry.Clear();
                        }
                    }
                    break;
            }
        }
        if (type == "place-selected")
        {
            if (!root.TryGetProperty("id", out var idElement) || !Guid.TryParse(idElement.GetString(), out var placeId))
            {
                return;
            }
            var place = _viewModel.Places.FirstOrDefault(item => item.Id == placeId);
            if (place is null)
            {
                return;
            }
            _viewModel.SelectedFolder = null;
            _viewModel.SelectedPlace = place;
            _selectedPlaceIdOnMap = place.Id;
            RebuildPlaceTree();
            return;
        }
        if (type == "delete-selected-place")
        {
            if (!root.TryGetProperty("id", out var deleteIdElement) || !Guid.TryParse(deleteIdElement.GetString(), out var deletePlaceId))
            {
                return;
            }
            var place = _viewModel.Places.FirstOrDefault(item => item.Id == deletePlaceId);
            if (place is null)
            {
                return;
            }
            _viewModel.SelectedFolder = null;
            _viewModel.SelectedPlace = place;
            _selectedPlaceIdOnMap = place.Id;
            DeletePlace_Click(this, new RoutedEventArgs());
        }
    }
    private void AddRoutePoint(double latitude, double longitude, string type, string? displayName = null)
    {
        var resolvedName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        if (type == "Start")
        {
            if (_viewModel.RoutePoints.Count > 0)
            {
                var message = _viewModel.Settings.LanguageCode == "en-US"
                    ? "A start point already exists. Continuing will clear the current route. Do you want to continue?"
                    : "Esiste già un punto di partenza. Continuando verrà eliminata la traccia attuale. Vuoi proseguire?";
                var title = _viewModel.Settings.LanguageCode == "en-US" ? "Confirm route reset" : "Conferma reset traccia";
                if (MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }
                _viewModel.RoutePoints.Clear();
                _viewModel.RouteGeometry.Clear();
            }
        }
        if (type == "End")
        {
            var existingEnd = _viewModel.RoutePoints.FirstOrDefault(point => point.PointType == "End");
            if (existingEnd is not null)
            {
                _viewModel.RoutePoints.Remove(existingEnd);
            }
        }
        if (type == "Waypoint")
        {
            var existingEnd = _viewModel.RoutePoints.FirstOrDefault(point => point.PointType == "End");
            if (existingEnd is not null)
            {
                var message = _viewModel.Settings.LanguageCode == "en-US"
                    ? "The route is already complete. Do you want to keep the destination unchanged?"
                    : "La traccia è già stata completata. Vuoi mantenere il punto di destinazione invariato?";
                var title = _viewModel.Settings.LanguageCode == "en-US" ? "Completed route" : "Traccia completata";
                var keepDestination = MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                if (keepDestination)
                {
                    var endIndex = _viewModel.RoutePoints.IndexOf(existingEnd);
                    _viewModel.RoutePoints.Insert(endIndex, new RoutePoint
                    {
                        Name = resolvedName ?? $"Waypoint {endIndex + 1}",
                        Latitude = latitude,
                        Longitude = longitude,
                        PointType = "Waypoint"
                    });
                    _viewModel.RouteGeometry.Clear();
                    return;
                }
                _viewModel.RoutePoints.Remove(existingEnd);
            }
        }
        _viewModel.RoutePoints.Add(new RoutePoint
        {
            Name = resolvedName ?? type switch
            {
                "Start" => "Start",
                "End" => "End",
                "DirectWaypoint" => $"Direct {_viewModel.RoutePoints.Count + 1}",
                _ => $"Waypoint {_viewModel.RoutePoints.Count + 1}"
            },
            Latitude = latitude,
            Longitude = longitude,
            PointType = type
        });
    }
    private SavedPlace CreatePlace(double latitude, double longitude, string name) => new()
    {
        Name = name,
        Latitude = latitude,
        Longitude = longitude,
        Description = string.Empty,
        FolderId = _viewModel.SelectedFolder?.Id,
        IconKey = "poi",
        PinColorHex = SavedPlace.DefaultPinColor
    };
    private void DeleteFolderBranch(Guid folderId)
    {
        var childFolderIds = _viewModel.Folders
            .Where(folder => folder.ParentId == folderId)
            .Select(folder => folder.Id)
            .ToList();
        foreach (var childFolderId in childFolderIds)
        {
            DeleteFolderBranch(childFolderId);
        }
        var placesToRemove = _viewModel.Places
            .Where(place => place.FolderId == folderId)
            .ToList();
        foreach (var place in placesToRemove)
        {
            _viewModel.Places.Remove(place);
        }
        var folderToRemove = _viewModel.Folders.FirstOrDefault(folder => folder.Id == folderId);
        if (folderToRemove is not null)
        {
            _viewModel.Folders.Remove(folderToRemove);
        }
    }
    private async Task CenterMapAsync(double latitude, double longitude, int? zoom = null)
    {
        if (!_mapReady || MapWebView.CoreWebView2 is null)
        {
            return;
        }
        var actualZoom = zoom.HasValue
            ? zoom.Value.ToString(CultureInfo.InvariantCulture)
            : "null";
        await MapWebView.CoreWebView2.ExecuteScriptAsync($"window.centerMap({latitude.ToString(CultureInfo.InvariantCulture)}, {longitude.ToString(CultureInfo.InvariantCulture)}, {actualZoom});");
    }
    private void DisablePickModes()
    {
        _placePickMode = false;
        _routePickMode = false;
    }
    private void ApplyTheme() => _themeService.ApplyTheme(Application.Current, _viewModel.Settings.ThemeMode);
    private void UpdateRouteSummary()
    {
        var directCount = _viewModel.RoutePoints.Count(point => point.PointType == "DirectWaypoint");
        _viewModel.RouteSummary = _viewModel.Settings.LanguageCode == "en-US"
            ? (_viewModel.RouteGeometry.Count > 1
                ? $"Route ready with {_viewModel.RouteGeometry.Count} points, {_viewModel.RoutePoints.Count} stops, {directCount} direct waypoints."
                : $"{_viewModel.L["RoutePointsInserted"]}: {_viewModel.RoutePoints.Count}.")
            : (_viewModel.RouteGeometry.Count > 1
                ? $"Traccia pronta con {_viewModel.RouteGeometry.Count} punti, {_viewModel.RoutePoints.Count} tappe, {directCount} waypoint diretti."
                : $"{_viewModel.L["RoutePointsInserted"]}: {_viewModel.RoutePoints.Count}.");
    }
    private async Task UpdateProfileSummaryAsync()
    {
        var path = _viewModel.RouteGeometry.Count > 1
            ? _viewModel.RouteGeometry.ToList()
            : _draftRouteGeometry.ToList();
        if (path.Count < 2)
        {
            _viewModel.ProfileDistanceValueText = "0";
            _viewModel.ProfileDistanceUnitText = GetDistanceUnitLabel();
            _viewModel.ProfileDistanceText = $"0 {GetDistanceUnitLabel()}";
            _viewModel.ProfileDurationText = _viewModel.Settings.LanguageCode == "en-US" ? "0h 00m" : "0h 00m";
            _viewModel.ProfileElevationTotalText = "0 m";
            _viewModel.ProfileElevationDetailsText = "(↑0 m - ↓0 m)";
            _viewModel.ProfileSlopeDetailsText = "0%";
            _viewModel.ProfileElevationSummaryText = "0 m (↑0 m - ↓0 m)";
            _viewModel.ProfileGainText = _viewModel.Settings.LanguageCode == "en-US" ? "0 m +" : "0 m +";
            _viewModel.ProfileLossText = _viewModel.Settings.LanguageCode == "en-US" ? "0 m -" : "0 m -";
            _viewModel.ProfileLinePoints = string.Empty;
            _viewModel.ProfileFillPoints = string.Empty;
            _viewModel.ProfileLineGeometry = string.Empty;
            _viewModel.ProfileFillGeometry = string.Empty;
            _viewModel.ProfileLineGeometryGreen = string.Empty;
            _viewModel.ProfileLineGeometryYellow = string.Empty;
            _viewModel.ProfileLineGeometryOrange = string.Empty;
            _viewModel.ProfileLineGeometryRed = string.Empty;
            _viewModel.ProfileColoredSegments.Clear();
            _viewModel.ProfileHoverPoints.Clear();
            _viewModel.ProfileHoverText = string.Empty;
            _viewModel.ProfileMaxLabel = string.Empty;
            _viewModel.ProfileUpperMidLabel = string.Empty;
            _viewModel.ProfileLowerMidLabel = string.Empty;
            _viewModel.ProfileMinLabel = string.Empty;
            _viewModel.ProfileStartDistanceLabel = $"0 {GetDistanceUnitLabel()}";
            _viewModel.ProfileQuarterDistanceLabel = string.Empty;
            _viewModel.ProfileMidDistanceLabel = string.Empty;
            _viewModel.ProfileThreeQuarterDistanceLabel = string.Empty;
            _viewModel.ProfileEndDistanceLabel = string.Empty;
            return;
        }
        var totalDistance = 0d;
        for (var index = 1; index < path.Count; index++)
        {
            totalDistance += EstimateDistanceKm(path[index - 1], path[index]);
        }
        var totalDistanceDisplay = ConvertDistanceFromKm(totalDistance);
        var distanceUnitLabel = GetDistanceUnitLabel();
        _viewModel.ProfileDistanceValueText = totalDistanceDisplay.ToString("F1", CultureInfo.InvariantCulture).Replace('.', ',');
        _viewModel.ProfileDistanceUnitText = distanceUnitLabel;
        _viewModel.ProfileDistanceText = $"{totalDistanceDisplay:F1} {distanceUnitLabel}";
        _viewModel.ProfileDurationText = "--h --m";
        var sampledProfilePath = BuildProfilePath(path);
        var elevationProfile = await _elevationService.GetElevationProfileAsync(sampledProfilePath);
        if (elevationProfile.Count < 2)
        {
            elevationProfile = BuildElevationProfileFromPath(path);
        }
        if (elevationProfile.Count < 2)
        {
            _viewModel.ProfileElevationTotalText = "-- m";
            _viewModel.ProfileElevationDetailsText = "(↑-- m - ↓-- m)";
            _viewModel.ProfileSlopeDetailsText = "--%";
            _viewModel.ProfileElevationSummaryText = "-- m (↑-- m - ↓-- m)";
            _viewModel.ProfileGainText = "-- m +";
            _viewModel.ProfileLossText = "-- m -";
            _viewModel.ProfileLinePoints = string.Empty;
            _viewModel.ProfileFillPoints = string.Empty;
            _viewModel.ProfileLineGeometry = string.Empty;
            _viewModel.ProfileFillGeometry = string.Empty;
            _viewModel.ProfileLineGeometryGreen = string.Empty;
            _viewModel.ProfileLineGeometryYellow = string.Empty;
            _viewModel.ProfileLineGeometryOrange = string.Empty;
            _viewModel.ProfileLineGeometryRed = string.Empty;
            _viewModel.ProfileColoredSegments.Clear();
            _viewModel.ProfileHoverPoints.Clear();
            _viewModel.ProfileHoverText = string.Empty;
            _viewModel.ProfileMaxLabel = "-- m";
            _viewModel.ProfileUpperMidLabel = "-- m";
            _viewModel.ProfileLowerMidLabel = "-- m";
            _viewModel.ProfileMinLabel = "-- m";
            var fallbackDuration = TimeSpan.FromHours(CalculateTravelDurationHours(totalDistance));
            _viewModel.ProfileDurationText = $"{(int)fallbackDuration.TotalHours}h {fallbackDuration.Minutes:00}m";
            _viewModel.ProfileStartDistanceLabel = $"0 {distanceUnitLabel}";
            _viewModel.ProfileQuarterDistanceLabel = string.Empty;
            _viewModel.ProfileMidDistanceLabel = string.Empty;
            _viewModel.ProfileThreeQuarterDistanceLabel = string.Empty;
            _viewModel.ProfileEndDistanceLabel = $"{totalDistanceDisplay:F1} {distanceUnitLabel}";
            return;
        }
        elevationProfile = ResampleElevationProfile(SmoothElevationProfile(elevationProfile), 10d);
        var minElevation = elevationProfile.Min(point => point.ElevationMeters);
        var maxElevation = elevationProfile.Max(point => point.ElevationMeters);
        var gain = 0d;
        var loss = 0d;
        var distances = new List<double> { 0d };
        var cumulativeDistance = 0d;
        for (var index = 1; index < elevationProfile.Count; index++)
        {
            cumulativeDistance += EstimateDistanceKm(
                new RoutePathPoint { Latitude = elevationProfile[index - 1].Latitude, Longitude = elevationProfile[index - 1].Longitude },
                new RoutePathPoint { Latitude = elevationProfile[index].Latitude, Longitude = elevationProfile[index].Longitude });
            distances.Add(cumulativeDistance);
            var delta = elevationProfile[index].ElevationMeters - elevationProfile[index - 1].ElevationMeters;
            if (delta > 0)
            {
                gain += delta;
            }
            else
            {
                loss += Math.Abs(delta);
            }
        }
        var totalElevation = gain + loss;
        var averageSlope = CalculateAverageSlope(elevationProfile, distances, 100d);
        var gainText = FormatAltitude(gain);
        var lossText = FormatAltitude(loss);
        var totalElevationText = FormatAltitude(totalElevation);
        _viewModel.ProfileElevationTotalText = totalElevationText;
        _viewModel.ProfileElevationDetailsText = $"(↑{gainText} - ↓{lossText})";
        _viewModel.ProfileSlopeDetailsText = $"{Math.Round(averageSlope):F0}%";
        _viewModel.ProfileElevationSummaryText = $"{FormatAltitude(totalElevation)} (↑{FormatAltitude(gain)} - ↓{FormatAltitude(loss)})";
        _viewModel.ProfileGainText = $"{gain:F0} m +";
        _viewModel.ProfileLossText = $"{loss:F0} m -";
        var adjustedHours = CalculateTravelDurationHours(totalDistance);
        var duration = TimeSpan.FromHours(adjustedHours);
        _viewModel.ProfileDurationText = $"{(int)duration.TotalHours}h {duration.Minutes:00}m";
        const double left = 60d;
        var chartWidth = Math.Max(240d, _viewModel.ProfileAxisWidth);
        const double chartBottom = 160d;
        const double chartTop = 10d;
        var chartHeight = chartBottom - chartTop;
        var yAxisValues = BuildNiceYAxisValues(minElevation, maxElevation, 4);
        _viewModel.ProfileMaxLabel = FormatAltitude(yAxisValues[^1]);
        _viewModel.ProfileUpperMidLabel = FormatAltitude(yAxisValues[^2]);
        _viewModel.ProfileLowerMidLabel = FormatAltitude(yAxisValues[^3]);
        _viewModel.ProfileMinLabel = FormatAltitude(yAxisValues[0]);
        var axisMinElevation = yAxisValues[0];
        var axisMaxElevation = yAxisValues[^1];
        var elevationRange = Math.Max(1d, axisMaxElevation - axisMinElevation);
        var totalProfileDistance = Math.Max(0.001d, distances[^1]);
        var xAxisDistanceValues = BuildLinearAxisValues(0d, totalDistanceDisplay, 5);
        _viewModel.ProfileStartDistanceLabel = $"{FormatAxisDistanceValue(xAxisDistanceValues[0], distanceUnitLabel)} {distanceUnitLabel}";
        _viewModel.ProfileQuarterDistanceLabel = xAxisDistanceValues.Count > 1 ? $"{FormatAxisDistanceValue(xAxisDistanceValues[Math.Min(1, xAxisDistanceValues.Count - 1)], distanceUnitLabel)} {distanceUnitLabel}" : string.Empty;
        _viewModel.ProfileMidDistanceLabel = xAxisDistanceValues.Count > 2 ? $"{FormatAxisDistanceValue(xAxisDistanceValues[Math.Min(2, xAxisDistanceValues.Count - 1)], distanceUnitLabel)} {distanceUnitLabel}" : string.Empty;
        _viewModel.ProfileThreeQuarterDistanceLabel = xAxisDistanceValues.Count > 3 ? $"{FormatAxisDistanceValue(xAxisDistanceValues[Math.Min(3, xAxisDistanceValues.Count - 1)], distanceUnitLabel)} {distanceUnitLabel}" : string.Empty;
        _viewModel.ProfileEndDistanceLabel = $"{FormatAxisDistanceValue(xAxisDistanceValues[^1], distanceUnitLabel)} {distanceUnitLabel}";
        var plottedPoints = elevationProfile.Select((point, index) =>
        {
            var x = left + ((distances[index] / totalProfileDistance) * chartWidth);
            var normalizedElevation = (point.ElevationMeters - axisMinElevation) / elevationRange;
            var y = chartBottom - (normalizedElevation * chartHeight);
            return new Point(x, y);
        }).ToList();
        var linePoints = plottedPoints.Select(point =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"{point.X:F1},{point.Y:F1}")).ToList();
        _viewModel.ProfileLinePoints = string.Join(" ", linePoints);
        _viewModel.ProfileFillPoints =
            string.Create(CultureInfo.InvariantCulture, $"{left:F1},{chartBottom:F1}") +
            " " + string.Join(" ", linePoints) + " " +
            string.Create(CultureInfo.InvariantCulture, $"{left + chartWidth:F1},{chartBottom:F1}");
        _viewModel.ProfileLineGeometry = BuildSmoothLineGeometry(plottedPoints);
        _viewModel.ProfileFillGeometry = BuildSmoothFillGeometry(plottedPoints, chartBottom);
        _viewModel.ProfileLineGeometryGreen = string.Empty;
        _viewModel.ProfileLineGeometryYellow = string.Empty;
        _viewModel.ProfileLineGeometryOrange = string.Empty;
        _viewModel.ProfileLineGeometryRed = string.Empty;
        var coloredSegments = BuildSlopeSegments(plottedPoints, elevationProfile, distances);
        _viewModel.ProfileColoredSegments.Clear();
        foreach (var segment in coloredSegments)
        {
            _viewModel.ProfileColoredSegments.Add(segment);
        }
        if (_viewModel.RoutePoints.Any(point => string.Equals(point.PointType, "End", StringComparison.OrdinalIgnoreCase)))
        {
            _viewModel.IsProfilePaneExpanded = true;
        }
        _viewModel.ProfileHoverPoints.Clear();
        for (var index = 0; index < plottedPoints.Count && index < distances.Count; index++)
        {
            var slopePercent = CalculateSlopeAtIndex(elevationProfile, distances, index, 20d);
            _viewModel.ProfileHoverPoints.Add(new ProfileHoverPoint
            {
                CanvasX = plottedPoints[index].X - left,
                CanvasY = plottedPoints[index].Y,
                DistanceKm = distances[index],
                ElapsedHours = adjustedHours * (distances[index] / totalProfileDistance),
                ElevationMeters = elevationProfile[index].ElevationMeters,
                SlopePercent = slopePercent
            });
        }
        _viewModel.ProfileHoverText = string.Empty;
    }
    private string FormatAltitude(double meters)
    {
        var unit = _viewModel.Settings.AltitudeUnit;
        if (string.Equals(unit, "ft", StringComparison.OrdinalIgnoreCase))
        {
            return $"{meters * 3.28084:F0} ft";
        }
        return $"{meters:F0} m";
    }
    private double ConvertDistanceFromKm(double kilometers)
    {
        var unit = _viewModel.Settings.DistanceUnit;
        return unit switch
        {
            "mi" => kilometers * 0.621371,
            "m" => kilometers * 1000d,
            "ft" => kilometers * 3280.84,
            _ => kilometers
        };
    }
    private string GetDistanceUnitLabel()
        => _viewModel.Settings.DistanceUnit switch
        {
            "mi" => "mi",
            "m" => "m",
            "ft" => "ft",
            _ => "km"
        };
    private static string BuildSmoothLineGeometry(IReadOnlyList<Point> points)
    {
        if (points.Count == 0)
        {
            return string.Empty;
        }
        if (points.Count == 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"M {points[0].X:F1},{points[0].Y:F1}");
        }
        var builder = new StringBuilder();
        builder.AppendFormat(CultureInfo.InvariantCulture, "M {0:F1},{1:F1}", points[0].X, points[0].Y);
        for (var index = 0; index < points.Count - 1; index++)
        {
            var p0 = index == 0 ? points[index] : points[index - 1];
            var p1 = points[index];
            var p2 = points[index + 1];
            var p3 = index + 2 < points.Count ? points[index + 2] : p2;
            var cp1X = p1.X + ((p2.X - p0.X) / 6d);
            var cp1Y = p1.Y + ((p2.Y - p0.Y) / 6d);
            var cp2X = p2.X - ((p3.X - p1.X) / 6d);
            var cp2Y = p2.Y - ((p3.Y - p1.Y) / 6d);
            builder.AppendFormat(
                CultureInfo.InvariantCulture,
                " C {0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{5:F1}",
                cp1X,
                cp1Y,
                cp2X,
                cp2Y,
                p2.X,
                p2.Y);
        }
        return builder.ToString();
    }
    private static string BuildSmoothFillGeometry(IReadOnlyList<Point> points, double bottomY)
    {
        if (points.Count == 0)
        {
            return string.Empty;
        }
        var lineGeometry = BuildSmoothLineGeometry(points);
        var builder = new StringBuilder(lineGeometry);
        builder.AppendFormat(
            CultureInfo.InvariantCulture,
            " L {0:F1},{1:F1} L {2:F1},{3:F1} Z",
            points[^1].X,
            bottomY,
            points[0].X,
            bottomY);
        return builder.ToString();
    }
    private static List<RouteElevationPoint> SmoothElevationProfile(IReadOnlyList<RouteElevationPoint> profile)
    {
        if (profile.Count < 5)
        {
            return profile.ToList();
        }
        var smoothed = new List<RouteElevationPoint>(profile.Count);
        for (var index = 0; index < profile.Count; index++)
        {
            var start = Math.Max(0, index - 2);
            var end = Math.Min(profile.Count - 1, index + 2);
            var weightedElevation = 0d;
            var totalWeight = 0d;
            for (var sampleIndex = start; sampleIndex <= end; sampleIndex++)
            {
                var distance = Math.Abs(sampleIndex - index);
                var weight = distance switch
                {
                    0 => 4d,
                    1 => 2d,
                    _ => 1d
                };
                weightedElevation += profile[sampleIndex].ElevationMeters * weight;
                totalWeight += weight;
            }
            smoothed.Add(new RouteElevationPoint
            {
                Latitude = profile[index].Latitude,
                Longitude = profile[index].Longitude,
                ElevationMeters = weightedElevation / totalWeight
            });
        }
        return smoothed;
    }
    private static List<RouteElevationPoint> ResampleElevationProfile(IReadOnlyList<RouteElevationPoint> profile, double intervalMeters)
    {
        if (profile.Count < 2 || intervalMeters <= 0d)
        {
            return profile.ToList();
        }
        var cumulativeDistancesKm = new List<double>(profile.Count) { 0d };
        for (var index = 1; index < profile.Count; index++)
        {
            cumulativeDistancesKm.Add(
                cumulativeDistancesKm[^1] +
                EstimateDistanceKm(
                    new RoutePathPoint { Latitude = profile[index - 1].Latitude, Longitude = profile[index - 1].Longitude },
                    new RoutePathPoint { Latitude = profile[index].Latitude, Longitude = profile[index].Longitude }));
        }
        var totalDistanceMeters = cumulativeDistancesKm[^1] * 1000d;
        if (totalDistanceMeters <= intervalMeters)
        {
            return profile.ToList();
        }
        var resampled = new List<RouteElevationPoint>();
        var sampleDistanceMeters = 0d;
        var segmentIndex = 1;
        while (sampleDistanceMeters <= totalDistanceMeters)
        {
            while (segmentIndex < cumulativeDistancesKm.Count - 1 &&
                   (cumulativeDistancesKm[segmentIndex] * 1000d) < sampleDistanceMeters)
            {
                segmentIndex++;
            }
            var previousIndex = Math.Max(0, segmentIndex - 1);
            var startMeters = cumulativeDistancesKm[previousIndex] * 1000d;
            var endMeters = cumulativeDistancesKm[segmentIndex] * 1000d;
            var spanMeters = Math.Max(0.001d, endMeters - startMeters);
            var ratio = Math.Clamp((sampleDistanceMeters - startMeters) / spanMeters, 0d, 1d);
            var start = profile[previousIndex];
            var end = profile[segmentIndex];
            resampled.Add(new RouteElevationPoint
            {
                Latitude = start.Latitude + ((end.Latitude - start.Latitude) * ratio),
                Longitude = start.Longitude + ((end.Longitude - start.Longitude) * ratio),
                ElevationMeters = start.ElevationMeters + ((end.ElevationMeters - start.ElevationMeters) * ratio)
            });
            sampleDistanceMeters += intervalMeters;
        }
        var last = profile[^1];
        if (resampled.Count == 0 ||
            Math.Abs(resampled[^1].Latitude - last.Latitude) > 0.0000001 ||
            Math.Abs(resampled[^1].Longitude - last.Longitude) > 0.0000001)
        {
            resampled.Add(new RouteElevationPoint
            {
                Latitude = last.Latitude,
                Longitude = last.Longitude,
                ElevationMeters = last.ElevationMeters
            });
        }
        return resampled;
    }
    private static List<ProfileColoredSegment> BuildSlopeSegments(
        IReadOnlyList<Point> plottedPoints,
        IReadOnlyList<RouteElevationPoint> elevationProfile,
        IReadOnlyList<double> distances)
    {
        var result = new List<ProfileColoredSegment>();
        for (var index = 1; index < plottedPoints.Count && index < elevationProfile.Count && index < distances.Count; index++)
        {
            var slopePercent = CalculateSlopeAtIndex(elevationProfile, distances, index, 20d);
            result.Add(new ProfileColoredSegment
            {
                Geometry = string.Create(
                    CultureInfo.InvariantCulture,
                    $"M {plottedPoints[index - 1].X:F1},{plottedPoints[index - 1].Y:F1} L {plottedPoints[index].X:F1},{plottedPoints[index].Y:F1}"),
                StrokeHex = InterpolateSlopeColor(slopePercent)
            });
        }
        return result;
    }
    private static double CalculateSlopeAtIndex(
        IReadOnlyList<RouteElevationPoint> elevationProfile,
        IReadOnlyList<double> distances,
        int centerIndex,
        double minimumWindowMeters)
    {
        if (centerIndex < 0 || centerIndex >= elevationProfile.Count || centerIndex >= distances.Count)
        {
            return 0d;
        }
        var halfWindowKm = minimumWindowMeters / 2000d;
        var centerDistance = distances[centerIndex];
        var targetStartDistance = Math.Max(0d, centerDistance - halfWindowKm);
        var targetEndDistance = Math.Min(distances[^1], centerDistance + halfWindowKm);
        var startIndex = centerIndex;
        while (startIndex > 0 && distances[startIndex] > targetStartDistance)
        {
            startIndex--;
        }
        var endIndex = centerIndex;
        while (endIndex < distances.Count - 1 && distances[endIndex] < targetEndDistance)
        {
            endIndex++;
        }
        if (endIndex <= startIndex)
        {
            return 0d;
        }
        // Use a local best-fit slope across the window rather than only the two
        // endpoints. This is closer to profile tools like Komoot and reduces
        // false steep spikes caused by DEM noise.
        var count = endIndex - startIndex + 1;
        if (count < 2)
        {
            return 0d;
        }
        var originMeters = distances[startIndex] * 1000d;
        double sumX = 0d;
        double sumY = 0d;
        double sumXY = 0d;
        double sumXX = 0d;
        for (var index = startIndex; index <= endIndex; index++)
        {
            var x = (distances[index] * 1000d) - originMeters;
            var y = elevationProfile[index].ElevationMeters;
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumXX += x * x;
        }
        var denominator = (count * sumXX) - (sumX * sumX);
        if (Math.Abs(denominator) < 0.0001d)
        {
            return 0d;
        }
        var slopeMetersPerMeter = ((count * sumXY) - (sumX * sumY)) / denominator;
        return Math.Abs(slopeMetersPerMeter) * 100d;
    }
    private static double CalculateRobustMaxSlope(
        IReadOnlyList<RouteElevationPoint> elevationProfile,
        IReadOnlyList<double> distances,
        double minimumWindowMeters,
        double percentile)
    {
        if (elevationProfile.Count < 2 || distances.Count < 2)
        {
            return 0d;
        }
        var localSlopes = new List<double>(Math.Min(elevationProfile.Count, distances.Count));
        for (var index = 0; index < elevationProfile.Count && index < distances.Count; index++)
        {
            localSlopes.Add(CalculateSlopeAtIndex(elevationProfile, distances, index, minimumWindowMeters));
        }
        if (localSlopes.Count == 0)
        {
            return 0d;
        }
        var smoothedSlopes = new List<double>(localSlopes.Count);
        for (var index = 0; index < localSlopes.Count; index++)
        {
            double weightedSlope = 0d;
            double totalWeight = 0d;
            for (var offset = -2; offset <= 2; offset++)
            {
                var sampleIndex = index + offset;
                if (sampleIndex < 0 || sampleIndex >= localSlopes.Count)
                {
                    continue;
                }
                var weight = offset switch
                {
                    0 => 3d,
                    -1 or 1 => 2d,
                    _ => 1d
                };
                weightedSlope += localSlopes[sampleIndex] * weight;
                totalWeight += weight;
            }
            smoothedSlopes.Add(totalWeight <= 0d ? 0d : weightedSlope / totalWeight);
        }
        smoothedSlopes.Sort();
        percentile = Math.Clamp(percentile, 0d, 1d);
        var rank = (int)Math.Round((smoothedSlopes.Count - 1) * percentile, MidpointRounding.AwayFromZero);
        return smoothedSlopes[Math.Clamp(rank, 0, smoothedSlopes.Count - 1)];
    }
    private static double CalculateAverageSlope(
        IReadOnlyList<RouteElevationPoint> elevationProfile,
        IReadOnlyList<double> distances,
        double windowMeters)
    {
        if (elevationProfile.Count < 2 || distances.Count < 2)
        {
            return 0d;
        }
        var slopes = new List<double>(Math.Min(elevationProfile.Count, distances.Count));
        for (var index = 0; index < elevationProfile.Count && index < distances.Count; index++)
        {
            slopes.Add(CalculateSlopeAtIndex(elevationProfile, distances, index, windowMeters));
        }
        if (slopes.Count == 0)
        {
            return 0d;
        }
        return slopes.Average();
    }
    private static string InterpolateSlopeColor(double slopePercent)
    {
        return slopePercent switch
        {
            <= 5d => "#4CAF50",
            <= 10d => InterpolateHexColor("#4CAF50", "#D6C43A", (slopePercent - 5d) / 5d),
            <= 15d => InterpolateHexColor("#D6C43A", "#D96F32", (slopePercent - 10d) / 5d),
            <= 20d => InterpolateHexColor("#D96F32", "#8B1E1E", (slopePercent - 15d) / 5d),
            _ => "#8B1E1E"
        };
    }
    private static string InterpolateHexColor(string fromHex, string toHex, double ratio)
    {
        ratio = Math.Clamp(ratio, 0d, 1d);
        var from = System.Drawing.ColorTranslator.FromHtml(fromHex);
        var to = System.Drawing.ColorTranslator.FromHtml(toHex);
        var red = (int)Math.Round(from.R + ((to.R - from.R) * ratio));
        var green = (int)Math.Round(from.G + ((to.G - from.G) * ratio));
        var blue = (int)Math.Round(from.B + ((to.B - from.B) * ratio));
        return $"#{red:X2}{green:X2}{blue:X2}";
    }
    private void ProfilePanelGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var availableWidth = Math.Max(300d, e.NewSize.Width);
        var axisWidth = Math.Max(240d, availableWidth - 60d);
        var canvasWidth = axisWidth + 60d;
        if (Math.Abs(_viewModel.ProfileAxisWidth - axisWidth) < 0.5d &&
            Math.Abs(_viewModel.ProfileChartCanvasWidth - canvasWidth) < 0.5d)
        {
            return;
        }
        _viewModel.ProfileAxisWidth = axisWidth;
        _viewModel.ProfileChartCanvasWidth = canvasWidth;
        _ = UpdateProfileSummaryAsync();
    }
    private double CalculateTravelDurationHours(double distanceKm)
    {
        var speedUnit = _viewModel.Settings.SpeedUnit;
        var speed = Math.Max(0.1d, _viewModel.Settings.TravelSpeed);
        var speedKmPerHour = speedUnit switch
        {
            "mph" => speed * 1.609344,
            "m/s" => speed * 3.6,
            _ => speed
        };
        return Math.Max(0.1d, distanceKm / speedKmPerHour);
    }
    private static List<double> BuildLinearAxisValues(double minValue, double maxValue, int count)
    {
        if (count <= 1)
        {
            return [maxValue];
        }
        if (Math.Abs(maxValue - minValue) < 0.0001d)
        {
            return Enumerable.Range(0, count).Select(_ => maxValue).ToList();
        }
        var step = (maxValue - minValue) / (count - 1);
        return Enumerable.Range(0, count)
            .Select(index => minValue + (step * index))
            .ToList();
    }
    private static List<double> BuildNiceDistanceAxisValues(double maxValue, int desiredCount)
    {
        if (desiredCount <= 1 || maxValue <= 0d)
        {
            return [Math.Max(0d, maxValue)];
        }
        var rawStep = maxValue / Math.Max(1, desiredCount - 1);
        var step = RoundAxisStep(Math.Max(rawStep, maxValue / 20d));
        var ticks = new List<double> { 0d };
        var next = step;
        while (next < maxValue && ticks.Count < desiredCount - 1)
        {
            ticks.Add(next);
            next += step;
        }
        if (ticks.Count == 1)
        {
            ticks.Add(maxValue / 2d);
        }
        if (Math.Abs(ticks[^1] - maxValue) > 0.0001d)
        {
            ticks.Add(maxValue);
        }
        return ticks;
    }
    private static List<double> BuildNiceYAxisValues(double minValue, double maxValue, int count)
    {
        if (count <= 1)
        {
            return [maxValue];
        }
        if (Math.Abs(maxValue - minValue) < 0.0001d)
        {
            return Enumerable.Range(0, count).Select(index => minValue + index).ToList();
        }
        var rawStep = Math.Max(1d, (maxValue - minValue) / (count - 1));
        var step = RoundAxisStep(rawStep);
        var axisMin = Math.Floor(minValue / step) * step;
        var axisMax = axisMin + (step * (count - 1));
        while (axisMax < maxValue)
        {
            axisMax += step;
            axisMin = axisMax - (step * (count - 1));
        }
        if (axisMin > minValue)
        {
            axisMin -= step;
            axisMax -= step;
        }
        while (axisMax < maxValue)
        {
            axisMin += step;
            axisMax += step;
        }
        return Enumerable.Range(0, count)
            .Select(index => axisMin + (step * index))
            .ToList();
    }
    private static double RoundAxisStep(double value)
    {
        var exponent = Math.Pow(10d, Math.Floor(Math.Log10(Math.Max(value, 0.0001d))));
        var normalized = value / exponent;
        var rounded = normalized switch
        {
            <= 1d => 1d,
            <= 2d => 2d,
            <= 2.5d => 2.5d,
            <= 5d => 5d,
            _ => 10d
        };
        return rounded * exponent;
    }
    private static string FormatAxisDistanceValue(double value, string unit)
    {
        if (unit is "m" or "ft")
        {
            return Math.Round(value).ToString("F0", CultureInfo.InvariantCulture);
        }
        var decimals = value < 1d ? 2 : value < 10d ? 1 : 0;
        return Math.Round(value, decimals).ToString($"F{decimals}", CultureInfo.InvariantCulture);
    }
    private static string FormatDurationLabel(double hours)
    {
        var duration = TimeSpan.FromHours(Math.Max(0d, hours));
        return $"{(int)duration.TotalHours}h {duration.Minutes:00}m";
    }
    private string GetLocalizedMapText(string english, string italian)
        => _viewModel.Settings.LanguageCode == "it-IT" ? italian : english;
    private void ProfileChartOverlay_MouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel.ProfileHoverPoints.Count == 0 || sender is not FrameworkElement element)
        {
            return;
        }
        var position = e.GetPosition(element);
        var nearest = _viewModel.ProfileHoverPoints
            .OrderBy(point => Math.Abs(point.CanvasX - position.X))
            .FirstOrDefault();
        if (nearest is null)
        {
            return;
        }
        _viewModel.ProfileHoverText =
            $"Dopo: {ConvertDistanceFromKm(nearest.DistanceKm):F1} {GetDistanceUnitLabel()} ({FormatDurationLabel(nearest.ElapsedHours)}){Environment.NewLine}" +
            $"Altitudine: {FormatAltitude(nearest.ElevationMeters)}{Environment.NewLine}" +
            $"Pendenza: {nearest.SlopePercent:F0}%";
        _profileToolTip.Content = _viewModel.ProfileHoverText;
        _profileToolTip.PlacementTarget = element;
        _profileToolTip.HorizontalOffset = position.X + 16;
        _profileToolTip.VerticalOffset = position.Y + 16;
        _profileToolTip.IsOpen = true;
    }
    private void ProfileChartOverlay_MouseLeave(object sender, MouseEventArgs e)
    {
        _viewModel.ProfileHoverText = string.Empty;
        _profileToolTip.IsOpen = false;
    }
    private static List<RoutePathPoint> BuildProfilePath(IReadOnlyList<RoutePathPoint> path)
    {
        if (path.Count < 2)
        {
            return path.ToList();
        }
        var simplified = new List<RoutePathPoint> { path[0] };
        for (var index = 1; index < path.Count; index++)
        {
            var current = path[index];
            if (EstimateDistanceKm(simplified[^1], current) >= 0.003 || index == path.Count - 1)
            {
                simplified.Add(current);
            }
        }
        if (simplified.Count < 2)
        {
            return path.ToList();
        }
        var sampled = new List<RoutePathPoint> { simplified[0] };
        const double stepKm = 0.02;
        for (var index = 1; index < simplified.Count; index++)
        {
            var from = simplified[index - 1];
            var to = simplified[index];
            var segmentDistance = EstimateDistanceKm(from, to);
            if (segmentDistance <= 0)
            {
                continue;
            }
            var steps = Math.Max(1, (int)Math.Ceiling(segmentDistance / stepKm));
            for (var step = 1; step <= steps; step++)
            {
                var ratio = step / (double)steps;
                sampled.Add(new RoutePathPoint
                {
                    Latitude = from.Latitude + ((to.Latitude - from.Latitude) * ratio),
                    Longitude = from.Longitude + ((to.Longitude - from.Longitude) * ratio)
                });
            }
        }
        return sampled;
    }
    private static List<RouteElevationPoint> BuildElevationProfileFromPath(IReadOnlyList<RoutePathPoint> path)
    {
        if (path.Count < 2)
        {
            return [];
        }
        var hasElevation = path.Count(point => point.ElevationMeters.HasValue);
        if (hasElevation < Math.Max(2, path.Count / 2))
        {
            return [];
        }
        var profile = new List<RouteElevationPoint>(path.Count);
        double? lastKnownElevation = null;
        for (var index = 0; index < path.Count; index++)
        {
            var current = path[index];
            var elevation = current.ElevationMeters;
            if (!elevation.HasValue)
            {
                elevation = FindNextKnownElevation(path, index) ?? lastKnownElevation;
            }
            if (!elevation.HasValue)
            {
                return [];
            }
            lastKnownElevation = elevation.Value;
            profile.Add(new RouteElevationPoint
            {
                Latitude = current.Latitude,
                Longitude = current.Longitude,
                ElevationMeters = elevation.Value
            });
        }
        return profile;
    }
    private static double? FindNextKnownElevation(IReadOnlyList<RoutePathPoint> path, int startIndex)
    {
        for (var index = startIndex + 1; index < path.Count; index++)
        {
            var elevation = path[index].ElevationMeters;
            if (elevation.HasValue)
            {
                return elevation.Value;
            }
        }
        return null;
    }
    private void ResetWeatherState()
    {
        _viewModel.WeatherForecast.Clear();
        _viewModel.WeatherLocationLabel = string.Empty;
        _viewModel.WeatherCurrentLabel = string.Empty;
        _viewModel.WeatherUpdatedLabel = string.Empty;
        _viewModel.WeatherStatus = _viewModel.L["WeatherNoRoute"];
        _viewModel.CivilAlertSummary = string.Empty;
    }
    private async Task RefreshWeatherAsync()
    {
        _weatherCancellationTokenSource?.Cancel();
        _weatherCancellationTokenSource?.Dispose();
        _weatherCancellationTokenSource = null;
        if (!_viewModel.Settings.ShowWeather)
        {
            _viewModel.WeatherForecast.Clear();
            _viewModel.WeatherLocationLabel = string.Empty;
            _viewModel.WeatherCurrentLabel = string.Empty;
            _viewModel.WeatherUpdatedLabel = string.Empty;
            _viewModel.WeatherStatus = string.Empty;
            _viewModel.CivilAlertSummary = string.Empty;
            return;
        }
        if (!TryGetWeatherReferencePoint(out var latitude, out var longitude))
        {
            ResetWeatherState();
            return;
        }
        var cancellationTokenSource = new CancellationTokenSource();
        _weatherCancellationTokenSource = cancellationTokenSource;
        _viewModel.WeatherForecast.Clear();
        _viewModel.WeatherStatus = _viewModel.L["WeatherLoading"];
        _viewModel.WeatherLocationLabel = string.Empty;
        _viewModel.WeatherCurrentLabel = string.Empty;
        _viewModel.WeatherUpdatedLabel = string.Empty;
        try
        {
            var result = await _weatherService.GetForecastAsync(
                latitude,
                longitude,
                _viewModel.Settings.LanguageCode,
                _viewModel.Settings.IsMetric,
                _viewModel.Settings.TemperatureUnit,
                cancellationTokenSource.Token);
            if (cancellationTokenSource.IsCancellationRequested || result is null)
            {
                return;
            }
            _viewModel.WeatherForecast.Clear();
            foreach (var day in result.Days.Take(15))
            {
                _viewModel.WeatherForecast.Add(day);
            }
            _viewModel.WeatherCurrentLabel = result.CurrentLabel;
            _viewModel.WeatherUpdatedLabel = string.IsNullOrWhiteSpace(result.UpdatedLabel)
                ? string.Empty
                : $"{_viewModel.L["WeatherUpdated"]}: {result.UpdatedLabel}";
            _viewModel.WeatherStatus = _viewModel.WeatherForecast.Count == 0
                ? _viewModel.L["WeatherNoData"]
                : string.Empty;
            await RefreshCivilAlertsAsync(latitude, longitude);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                _viewModel.WeatherForecast.Clear();
                _viewModel.WeatherCurrentLabel = string.Empty;
                _viewModel.WeatherUpdatedLabel = string.Empty;
                _viewModel.WeatherStatus = _viewModel.L["WeatherUnavailable"];
                _viewModel.CivilAlertSummary = string.Empty;
            }
        }
        finally
        {
            if (ReferenceEquals(_weatherCancellationTokenSource, cancellationTokenSource))
            {
                _weatherCancellationTokenSource = null;
            }
            cancellationTokenSource.Dispose();
        }
    }
    private bool TryGetWeatherReferencePoint(out double latitude, out double longitude)
    {
        if (_viewModel.Settings.WeatherReferenceLatitude.HasValue && _viewModel.Settings.WeatherReferenceLongitude.HasValue)
        {
            latitude = _viewModel.Settings.WeatherReferenceLatitude.Value;
            longitude = _viewModel.Settings.WeatherReferenceLongitude.Value;
            return true;
        }
        var path = _viewModel.RouteGeometry.Count > 1
            ? _viewModel.RouteGeometry.ToList()
            : _draftRouteGeometry.ToList();
        if (path.Count < 2)
        {
            latitude = 0;
            longitude = 0;
            return false;
        }
        var totalDistance = 0d;
        for (var index = 1; index < path.Count; index++)
        {
            totalDistance += EstimateDistanceKm(path[index - 1], path[index]);
        }
        if (totalDistance <= 0)
        {
            var midpoint = path[path.Count / 2];
            latitude = midpoint.Latitude;
            longitude = midpoint.Longitude;
            return true;
        }
        var targetDistance = totalDistance / 2d;
        var accumulated = 0d;
        for (var index = 1; index < path.Count; index++)
        {
            var segmentDistance = EstimateDistanceKm(path[index - 1], path[index]);
            if (accumulated + segmentDistance >= targetDistance && segmentDistance > 0)
            {
                var remaining = targetDistance - accumulated;
                var ratio = remaining / segmentDistance;
                latitude = path[index - 1].Latitude + ((path[index].Latitude - path[index - 1].Latitude) * ratio);
                longitude = path[index - 1].Longitude + ((path[index].Longitude - path[index - 1].Longitude) * ratio);
                return true;
            }
            accumulated += segmentDistance;
        }
        latitude = path[^1].Latitude;
        longitude = path[^1].Longitude;
        return true;
    }
    private async Task SetWeatherReferencePointAsync(double latitude, double longitude)
    {
        _viewModel.Settings.WeatherReferenceLatitude = latitude;
        _viewModel.Settings.WeatherReferenceLongitude = longitude;
        await SaveDataAsync();
        await RefreshMapAsync();
        await RefreshWeatherAsync();
    }
    private async Task RefreshCivilAlertsAsync(double referenceLatitude, double referenceLongitude)
    {
        if (!_viewModel.Settings.ShowCivilProtectionAlerts)
        {
            _civilAlertAreas = [];
            _viewModel.CivilAlertSummary = string.Empty;
            await RefreshMapAsync();
            return;
        }
        _viewModel.CivilAlertSummary = _viewModel.L["CivilAlertLoading"];
        try
        {
            var alerts = await _civilProtectionAlertService.GetCurrentAlertsAsync();
            var regions = await _regionBoundaryService.GetRegionsAsync();
            var alertLookup = alerts.ToDictionary(alert => alert.RegionName, alert => alert, StringComparer.OrdinalIgnoreCase);
            _civilAlertAreas = regions
                .Where(region => alertLookup.TryGetValue(region.Name, out var alert) && alert.Severity >= 2)
                .Select(region => new
                {
                    name = region.Name,
                    severity = alertLookup[region.Name].Severity,
                    details = string.Join(" | ", alertLookup[region.Name].Details),
                    polygons = region.Polygons.Select(polygon => polygon.Select(point => new { latitude = point.Latitude, longitude = point.Longitude }))
                })
                .Cast<object>()
                .ToList();
            var matchingRegion = regions.FirstOrDefault(region =>
                alertLookup.TryGetValue(region.Name, out var candidateAlert) &&
                candidateAlert.Severity >= 2 &&
                region.Polygons.Any(polygon => IsPointInPolygon(referenceLatitude, referenceLongitude, polygon)));
            if (matchingRegion is not null)
            {
                var alert = alertLookup[matchingRegion.Name];
                _viewModel.CivilAlertSummary = $"{_viewModel.L["CivilAlertTitle"]}: {string.Join(" | ", alert.Details)}";
            }
            else
            {
                _viewModel.CivilAlertSummary = _viewModel.L["CivilAlertNone"];
            }
        }
        catch
        {
            _civilAlertAreas = [];
            _viewModel.CivilAlertSummary = string.Empty;
        }
        await RefreshMapAsync();
    }
    private static bool IsPointInPolygon(double latitude, double longitude, IReadOnlyList<RoutePathPoint> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var xi = polygon[i].Longitude;
            var yi = polygon[i].Latitude;
            var xj = polygon[j].Longitude;
            var yj = polygon[j].Latitude;
            var intersects = ((yi > latitude) != (yj > latitude)) &&
                             (longitude < (xj - xi) * (latitude - yi) / ((yj - yi) == 0 ? double.Epsilon : (yj - yi)) + xi);
            if (intersects)
            {
                inside = !inside;
            }
        }
        return inside;
    }
    private static double EstimateDistanceKm(RoutePathPoint a, RoutePathPoint b)
    {
        const double radiusKm = 6371d;
        var dLat = DegreesToRadians(b.Latitude - a.Latitude);
        var dLon = DegreesToRadians(b.Longitude - a.Longitude);
        var lat1 = DegreesToRadians(a.Latitude);
        var lat2 = DegreesToRadians(b.Latitude);
        var haversine = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                        Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
        return radiusKm * c;
    }
    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
    private string BuildMapHtml()
    {
        var end = GetLocalizedMapText("Set as end point", "Imposta come punto di arrivo");
        var deleteRoutePoint = GetLocalizedMapText("Delete route point", "Elimina punto traccia");
        var addPin = GetLocalizedMapText("Add pin", "Aggiungi pin");
        var start = GetLocalizedMapText("Set as start point", "Imposta come punto di partenza");
        var waypoint = GetLocalizedMapText("Set as waypoint", "Imposta come waypoint");
        var directWaypoint = GetLocalizedMapText("Set as direct waypoint", "Imposta come waypoint diretto");
        var weatherReferenceTitle = _viewModel.L["WeatherReferenceTitle"];
        var setWeatherPoint = _viewModel.L["MapContextSetWeatherPoint"];
        var svgMapJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["bivouac"] = GetPinheadSvg("bivouac"),
            ["fountain"] = GetPinheadSvg("fountain"),
            ["food"] = GetPinheadSvg("food"),
            ["paid_hut"] = GetPinheadSvg("paid_hut"),
            ["single_tent"] = GetPinheadSvg("single_tent"),
            ["tent_area"] = GetPinheadSvg("tent_area"),
            ["parking"] = GetPinheadSvg("parking"),
            ["cable_car"] = GetPinheadSvg("cable_car"),
            ["hammocks"] = GetPinheadSvg("hammocks"),
            ["waterfall"] = GetPinheadSvg("waterfall"),
            ["poi"] = GetPinheadSvg("poi"),
            ["water"] = GetPinheadSvg("water"),
            ["summit_cross"] = GetPinheadSvg("summit_cross"),
            ["route_start"] = GetPinheadSvg("route_start"),
            ["route_waypoint"] = GetPinheadSvg("route_waypoint"),
            ["route_end"] = GetPinheadSvg("route_end"),
            ["weather_reference"] = "<svg width='18' height='18' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M12 2v2'/><path d='m4.93 4.93 1.41 1.41'/><path d='M20 12h2'/><path d='m19.07 4.93-1.41 1.41'/><path d='M15.947 12.65a4 4 0 0 0-5.925-4.128'/><path d='M13 22H7a5 5 0 1 1 4.9-6H13a3 3 0 0 1 0 6Z'/></svg>"
        }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        return $$"""
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8" />
  <meta http-equiv="Content-Security-Policy" content="default-src 'self' data: blob: https://unpkg.com https://tile.openstreetmap.org https://*.tile.openstreetmap.org https://*.tile.opentopomap.org https://*.tile-cyclosm.openstreetmap.fr https://server.arcgisonline.com https://tile.waymarkedtrails.org https://tile.openstreetmap.de https://*.basemaps.cartocdn.com https://tile.osm.ch; style-src 'self' 'unsafe-inline' https://unpkg.com; script-src 'self' 'unsafe-inline' https://unpkg.com; img-src * data: blob:;" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
  <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
  <style>
    html, body, #map { height: 100%; margin: 0; }
    body { font-family: Segoe UI, sans-serif; background: #1b1b1c; overflow: hidden; }
    .place-label { font-size: 12px; }
    #bearingOverlay {
      position: absolute;
      inset: 0;
      pointer-events: none;
      z-index: 650;
      display: none;
    }
    #bearingOverlay svg {
      width: 100%;
      height: 100%;
      overflow: visible;
    }
    .bearing-circle {
      fill: rgba(255,255,255,0.04);
      stroke: rgba(20,20,20,0.6);
      stroke-width: 1.5;
    }
    .bearing-tick-major {
      stroke: rgba(20,20,20,0.8);
      stroke-width: 1.4;
    }
    .bearing-tick-minor {
      stroke: rgba(20,20,20,0.45);
      stroke-width: 1;
    }
    .bearing-label {
      fill: rgba(24,24,24,0.92);
      font: 600 12px 'Segoe UI', sans-serif;
      text-anchor: middle;
      dominant-baseline: middle;
      paint-order: stroke;
      stroke: rgba(255,255,255,0.85);
      stroke-width: 2px;
      stroke-linejoin: round;
    }
    .bearing-cardinal {
      font-size: 13px;
    }
    .modern-scale.leaflet-control {
      margin: 0 0 14px 14px;
      background: rgba(255,255,255,0.92);
      border: 1px solid rgba(25,25,25,0.08);
      border-radius: 18px;
      box-shadow: 0 8px 20px rgba(0,0,0,.14);
      padding: 8px 10px 9px;
      backdrop-filter: blur(6px);
    }
    .modern-scale-label {
      font: 700 11px 'Segoe UI', sans-serif;
      color: #1f2937;
      text-align: center;
      margin: 0 0 6px;
      line-height: 1;
    }
    .modern-scale-pill {
      height: 8px;
      border-radius: 999px;
      background: linear-gradient(90deg, #1f2937 0%, #334155 100%);
      box-shadow: inset 0 0 0 1px rgba(255,255,255,.22);
    }
    #contextMenu { position: fixed; display: none; z-index: 10000; width: max-content; min-width: 0; max-width: 280px; background: #2d2d30; color: #f1f1f1; border: 1px solid #3f3f46; border-radius: 6px; box-shadow: 0 8px 18px rgba(0,0,0,.35); padding: 4px 0; }
    #contextMenu button { display: block; width: 100%; min-width: 0; background: transparent; border: 0; color: inherit; text-align: left; white-space: normal; word-break: break-word; padding: 9px 12px; cursor: pointer; font: inherit; }
    #contextMenu button:hover { background: #3e3e42; }
  </style>
</head>
<body>
  <div id="map"></div>
  <div id="bearingOverlay"><svg id="bearingSvg"></svg></div>
  <div id="contextMenu">
    <button data-action="pin">{{addPin}}</button>
    <button data-action="deleteRoutePoint" style="display:none">{{deleteRoutePoint}}</button>
    <button data-action="end">{{end}}</button>
    <button data-action="start">{{start}}</button>
    <button data-action="weatherReference">{{setWeatherPoint}}</button>
    <button data-action="waypoint">{{waypoint}}</button>
    <button data-action="directWaypoint">{{directWaypoint}}</button>
  </div>
  <script>
    const map = L.map('map', {
      zoomAnimation: true,
      fadeAnimation: true,
      markerZoomAnimation: true,
      zoomSnap: 0.5,
      zoomDelta: 0.5,
      wheelPxPerZoomLevel: 90
    }).setView([{{_viewModel.Settings.DefaultLatitude.ToString(CultureInfo.InvariantCulture)}}, {{_viewModel.Settings.DefaultLongitude.ToString(CultureInfo.InvariantCulture)}}], {{_viewModel.Settings.DefaultZoom}});
    let mapScaleControl = null;
    const pinheadSvgs = {{svgMapJson}};
    const placeLayer = L.layerGroup().addTo(map);
    const routePointLayer = L.layerGroup().addTo(map);
    const civilAlertLayer = L.layerGroup().addTo(map);
    const rulerLayer = L.layerGroup().addTo(map);
    const bearingOverlay = document.getElementById('bearingOverlay');
    const bearingSvg = document.getElementById('bearingSvg');
    let routeLine = null;
    let draftRouteLine = null;
    let contextLatLng = null;
    let rulerEnabled = false;
    let rulerStartLatLng = null;
    let rulerEndLatLng = null;
    let rulerLine = null;
    let rulerLabelMarker = null;
    const contextMenu = document.getElementById('contextMenu');
    const baseLayers = {
      OpenStreetMap: L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19 }),
      OpenTopoMap: L.tileLayer('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png', { maxZoom: 17 }),
      CyclOSM: L.tileLayer('https://{s}.tile-cyclosm.openstreetmap.fr/cyclosm/{z}/{x}/{y}.png', { maxZoom: 20 }),
      Humanitarian: L.tileLayer('https://{s}.tile.openstreetmap.fr/hot/{z}/{x}/{y}.png', { maxZoom: 19 }),
      'OSM DE': L.tileLayer('https://tile.openstreetmap.de/{z}/{x}/{y}.png', { maxZoom: 18 }),
      'Carto Voyager': L.tileLayer('https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png', { maxZoom: 20, subdomains: 'abcd' }),
      'Carto Light': L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', { maxZoom: 20, subdomains: 'abcd' }),
      'Carto Dark': L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', { maxZoom: 20, subdomains: 'abcd' })
    };
    const overlays = {
      satellite: L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', { maxZoom: 19 }),
      hiking: L.tileLayer('https://tile.waymarkedtrails.org/hiking/{z}/{x}/{y}.png', { maxZoom: 18 }),
      contours: L.tileLayer('https://tile.osm.ch/contours/{z}/{x}/{y}.png', { maxZoom: 18, opacity: 0.75 })
    };
    let currentBaseLayer = baseLayers.OpenStreetMap;
    let currentRoutePoints = [];
    let currentSelectedPlaceId = null;
    currentBaseLayer.addTo(map);
    function hideContextMenu() { contextMenu.style.display = 'none'; }
    function showContextMenuAt(clientX, clientY, latlng, routePointIndex, placeName) {
      contextLatLng = latlng || null;
      const deleteButton = contextMenu.querySelector('[data-action="deleteRoutePoint"]');
      contextMenu.setAttribute('data-place-name', placeName || '');
      if (routePointIndex !== null && routePointIndex !== undefined) {
        deleteButton.style.display = 'block';
        deleteButton.setAttribute('data-route-point-index', routePointIndex);
      } else {
        deleteButton.style.display = 'none';
        deleteButton.removeAttribute('data-route-point-index');
      }
      contextMenu.style.left = clientX + 'px';
      contextMenu.style.top = clientY + 'px';
      contextMenu.style.display = 'block';
    }
    function switchBaseLayer(name) {
      if (currentBaseLayer) map.removeLayer(currentBaseLayer);
      currentBaseLayer = baseLayers[name] || baseLayers.OpenTopoMap;
      currentBaseLayer.addTo(map);
      currentBaseLayer.bringToBack();
    }
    function setOverlayState(layer, enabled) {
      if (enabled) { if (!map.hasLayer(layer)) layer.addTo(map); layer.bringToFront(); }
      else if (map.hasLayer(layer)) map.removeLayer(layer);
    }
    function createModernScaleControl() {
      return L.control({ position: 'bottomleft' });
    }
    function getNiceScaleDistance(maxMeters) {
      const baseValues = [1, 2, 5];
      let exponent = Math.pow(10, Math.floor(Math.log10(Math.max(maxMeters, 1))));
      while (exponent > 0.0001) {
        for (let i = baseValues.length - 1; i >= 0; i--) {
          const candidate = baseValues[i] * exponent;
          if (candidate <= maxMeters) {
            return candidate;
          }
        }
        exponent /= 10;
      }
      return maxMeters;
    }
    function formatScaleDistance(meters, distanceUnit) {
      if (distanceUnit === 'ft') {
        const feet = meters * 3.28084;
        return feet >= 5280 ? `${(feet / 5280).toFixed(feet / 5280 >= 10 ? 0 : 1)} mi` : `${Math.round(feet)} ft`;
      }
      if (distanceUnit === 'mi') {
        const miles = meters / 1609.344;
        return miles >= 0.2 ? `${miles.toFixed(miles >= 10 ? 0 : 1)} mi` : `${Math.round(meters * 3.28084)} ft`;
      }
      if (distanceUnit === 'm') {
        return meters >= 1000 ? `${(meters / 1000).toFixed(meters / 1000 >= 10 ? 0 : 1)} km` : `${Math.round(meters)} m`;
      }
      return meters >= 1000 ? `${(meters / 1000).toFixed(meters / 1000 >= 10 ? 0 : 1)} km` : `${Math.round(meters)} m`;
    }
    function updateModernScale(distanceUnit) {
      if (!mapScaleControl || !mapScaleControl._container) {
        return;
      }
      const centerY = map.getSize().y / 2;
      const leftPoint = L.point(0, centerY);
      const rightPoint = L.point(140, centerY);
      const leftLatLng = map.containerPointToLatLng(leftPoint);
      const rightLatLng = map.containerPointToLatLng(rightPoint);
      const maxMeters = map.distance(leftLatLng, rightLatLng);
      const niceMeters = getNiceScaleDistance(maxMeters);
      const widthPx = Math.max(36, Math.min(140, (niceMeters / Math.max(maxMeters, 1)) * 140));
      const label = formatScaleDistance(niceMeters, distanceUnit);
      const labelNode = mapScaleControl._container.querySelector('.modern-scale-label');
      const pillNode = mapScaleControl._container.querySelector('.modern-scale-pill');
      labelNode.textContent = label;
      pillNode.style.width = `${widthPx.toFixed(0)}px`;
    }
    function setMapScaleState(enabled, distanceUnit) {
      if (enabled) {
        if (!mapScaleControl) {
          mapScaleControl = createModernScaleControl();
          mapScaleControl.onAdd = function() {
            const container = L.DomUtil.create('div', 'modern-scale leaflet-control');
            container.innerHTML = `<div class="modern-scale-label"></div><div class="modern-scale-pill"></div>`;
            L.DomEvent.disableClickPropagation(container);
            return container;
          };
        }
        if (!mapScaleControl._map) {
          mapScaleControl.addTo(map);
        }
        updateModernScale(distanceUnit);
      } else if (mapScaleControl && mapScaleControl._map) {
        mapScaleControl.remove();
      }
    }
    function bearingLabel(degrees) {
      if (degrees === 0) return 'N';
      if (degrees === 90) return 'E';
      if (degrees === 180) return 'S';
      if (degrees === 270) return 'O';
      return `${degrees}°`;
    }
    function renderBearingCircle(enabled) {
      if (!enabled) {
        bearingOverlay.style.display = 'none';
        bearingSvg.innerHTML = '';
        return;
      }
      bearingOverlay.style.display = 'block';
      const width = map.getSize().x;
      const height = map.getSize().y;
      const cx = width / 2;
      const cy = height / 2;
      const radius = Math.max(90, Math.min(width, height) * 0.28);
      let markup = `<circle class="bearing-circle" cx="${cx}" cy="${cy}" r="${radius}"></circle>`;
      for (let degrees = 0; degrees < 360; degrees += 15) {
        const angle = ((degrees - 90) * Math.PI) / 180;
        const isMajor = degrees % 30 === 0;
        const innerRadius = radius - (isMajor ? 14 : 8);
        const x1 = cx + Math.cos(angle) * innerRadius;
        const y1 = cy + Math.sin(angle) * innerRadius;
        const x2 = cx + Math.cos(angle) * radius;
        const y2 = cy + Math.sin(angle) * radius;
        markup += `<line class="${isMajor ? 'bearing-tick-major' : 'bearing-tick-minor'}" x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}"></line>`;
        if (isMajor) {
          const labelRadius = radius + 18;
          const lx = cx + Math.cos(angle) * labelRadius;
          const ly = cy + Math.sin(angle) * labelRadius;
          const labelClass = degrees % 90 === 0 ? 'bearing-label bearing-cardinal' : 'bearing-label';
          markup += `<text class="${labelClass}" x="${lx}" y="${ly}">${bearingLabel(degrees)}</text>`;
        }
      }
      bearingSvg.innerHTML = markup;
    }
    function clearRulerMeasurement() {
      rulerStartLatLng = null;
      rulerEndLatLng = null;
      rulerLayer.clearLayers();
      rulerLine = null;
      rulerLabelMarker = null;
    }
    function formatRulerDistance(meters, unit) {
      if (unit === 'mi') return `${(meters / 1609.344).toFixed(1)} mi`;
      if (unit === 'ft') return `${(meters * 3.28084).toFixed(1)} ft`;
      if (unit === 'm') return `${meters.toFixed(1)} m`;
      return `${(meters / 1000).toFixed(1)} km`;
    }
    function calculateAzimuthDegrees(from, to) {
      const lat1 = from.lat * Math.PI / 180;
      const lat2 = to.lat * Math.PI / 180;
      const deltaLon = (to.lng - from.lng) * Math.PI / 180;
      const y = Math.sin(deltaLon) * Math.cos(lat2);
      const x = Math.cos(lat1) * Math.sin(lat2) - Math.sin(lat1) * Math.cos(lat2) * Math.cos(deltaLon);
      const bearing = Math.atan2(y, x) * 180 / Math.PI;
      return (bearing + 360) % 360;
    }
    function renderRulerMeasurement(currentLatLng, distanceUnit) {
      if (!rulerStartLatLng || !currentLatLng) return;
      const activeEnd = currentLatLng;
      const distanceMeters = map.distance(rulerStartLatLng, activeEnd);
      const azimuth = calculateAzimuthDegrees(rulerStartLatLng, activeEnd);
      const midLat = (rulerStartLatLng.lat + activeEnd.lat) / 2;
      const midLng = (rulerStartLatLng.lng + activeEnd.lng) / 2;
      rulerLayer.clearLayers();
      rulerLine = L.polyline([rulerStartLatLng, activeEnd], { color: '#1F6FEB', weight: 3, dashArray: '8 6', opacity: 0.95 }).addTo(rulerLayer);
      L.circleMarker(rulerStartLatLng, { radius: 5, color: '#1F6FEB', fillColor: '#ffffff', fillOpacity: 1, weight: 2 }).addTo(rulerLayer);
      L.circleMarker(activeEnd, { radius: 5, color: '#1F6FEB', fillColor: '#ffffff', fillOpacity: 1, weight: 2 }).addTo(rulerLayer);
      rulerLabelMarker = L.marker([midLat, midLng], {
        interactive: false,
        icon: L.divIcon({
          className: '',
          html: `<div style="background:transparent;border:none;padding:0;color:#1F6FEB;font:700 12px 'Segoe UI',sans-serif;white-space:nowrap;text-shadow:-1px -1px 0 rgba(255,255,255,.95),1px -1px 0 rgba(255,255,255,.95),-1px 1px 0 rgba(255,255,255,.95),1px 1px 0 rgba(255,255,255,.95);">${formatRulerDistance(distanceMeters, distanceUnit)} | ${Math.round(azimuth)}°</div>`,
          iconSize: [0, 0],
          iconAnchor: [0, 0]
        })
      }).addTo(rulerLayer);
    }
    function setRulerState(enabled, distanceUnit) {
      rulerEnabled = enabled;
      if (!enabled) {
        clearRulerMeasurement();
        return;
      }
      if (rulerStartLatLng) {
        renderRulerMeasurement(rulerEndLatLng || rulerStartLatLng, distanceUnit);
      }
    }
    function iconHtml(color, iconKey, iconSvg) {
      const svg = iconSvg || pinheadSvgs[iconKey] || '';
      return `<div style="position:relative;width:40px;height:52px;">
        <svg width="40" height="52" viewBox="0 0 40 52" style="position:absolute;left:0;top:0;filter:drop-shadow(0 2px 6px rgba(0,0,0,.35));">
          <path d="M20 2C10.1 2 2 10 2 19.9C2 33.3 18.2 47.3 19 48.1C19.6 48.6 20.4 48.6 21 48.1C21.8 47.3 38 33.3 38 19.9C38 10 29.9 2 20 2Z" fill="${color}" stroke="#ffffff" stroke-width="2"/>
        </svg>
        <div style="position:absolute;left:9px;top:9px;width:22px;height:22px;display:flex;align-items:center;justify-content:center;">${svg}</div>
      </div>`;
    }
    function makeIcon(color, iconKey, iconSvg) {
      return L.divIcon({ className: 'place-label', html: iconHtml(color, iconKey, iconSvg), iconSize: [40, 52], iconAnchor: [20, 50], popupAnchor: [0, -40] });
    }
    window.centerMap = function(lat, lon, zoom) {
      const targetZoom = zoom === null || zoom === undefined ? map.getZoom() : zoom;
      map.flyTo([lat, lon], targetZoom, {
        animate: true,
        duration: 3,
        easeLinearity: 0.22,
        noMoveStart: false
      });
    };
    map.on('moveend', function() {
      const center = map.getCenter();
      window.chrome.webview.postMessage({ type: 'map-center', latitude: center.lat, longitude: center.lng });
    });
    window.renderState = function(state) {
      window.__openhikemapDistanceUnit = state.settings.distanceUnit || 'km';
      switchBaseLayer(state.settings.mapStyle);
      setOverlayState(overlays.satellite, state.settings.showSatelliteOverlay);
      setOverlayState(overlays.hiking, state.settings.showHikingTrails);
      setOverlayState(overlays.contours, state.settings.showContourLines);
      renderBearingCircle(state.settings.showBearingCircle);
      setRulerState(state.settings.showRulerTool, state.settings.distanceUnit);
      setMapScaleState(state.settings.showMapScale, state.settings.distanceUnit);
      currentRoutePoints = state.routePoints || [];
      currentSelectedPlaceId = null;
      placeLayer.clearLayers();
      routePointLayer.clearLayers();
      civilAlertLayer.clearLayers();
      if (routeLine) map.removeLayer(routeLine);
      if (draftRouteLine) map.removeLayer(draftRouteLine);
      if (state.settings.showCivilAlerts && state.civilAlertAreas) {
        for (const area of state.civilAlertAreas) {
          const color = area.severity >= 3 ? '#C62828' : area.severity === 2 ? '#EF6C00' : '#FBC02D';
          for (const polygon of area.polygons) {
            L.polygon(polygon.map(point => [point.latitude, point.longitude]), {
              color,
              weight: 2,
              fillColor: color,
              fillOpacity: 0.18
            }).bindTooltip(area.name)
              .bindPopup(`<strong>${area.name}</strong><br/>${area.details || ''}`)
              .addTo(civilAlertLayer);
          }
        }
      }
      if (state.settings.showSavedPoints) {
        for (const place of state.places) {
          L.marker([place.latitude, place.longitude], { icon: makeIcon(place.pinColorHex || '#D65A5A', place.iconKey || 'marker', place.iconSvg || '') }).addTo(placeLayer)
            .on('click', function() {
              currentSelectedPlaceId = place.id;
              window.chrome.webview.postMessage({ type: 'place-selected', id: place.id });
            })
            .on('contextmenu', function(event) {
              currentSelectedPlaceId = place.id;
              window.chrome.webview.postMessage({ type: 'place-selected', id: place.id });
              showContextMenuAt(event.originalEvent.clientX, event.originalEvent.clientY, event.latlng, null, place.name || '');
            })
            .bindPopup(`<strong>${place.name}</strong><br/>${place.description || ''}<br/>${place.latitude.toFixed(5)}, ${place.longitude.toFixed(5)}`);
        }
      }
      if (state.weatherReferencePoint) {
        L.marker(
          [state.weatherReferencePoint.latitude, state.weatherReferencePoint.longitude],
          { icon: makeIcon('#CCC', 'weather_reference', pinheadSvgs.weather_reference || '') }
        ).addTo(placeLayer).bindTooltip(state.weatherReferencePoint.title || '{{weatherReferenceTitle}}');
      }
      for (const point of state.routePoints) {
        if (point.pointType === 'DirectWaypoint') {
          L.circleMarker([point.latitude, point.longitude], { radius: 7, color: '#ffd166', fillColor: '#ffd166', fillOpacity: 0.95 }).addTo(routePointLayer);
          continue;
        }
        const iconKey = point.pointType === 'Start' ? 'route_start' : point.pointType === 'End' ? 'route_end' : 'route_waypoint';
        const color = point.pointType === 'Start' ? '#2E7D5B' : point.pointType === 'End' ? '#D9824B' : '#3F6F8F';
        L.marker([point.latitude, point.longitude], { icon: makeIcon(color, iconKey, pinheadSvgs[iconKey] || '') }).addTo(routePointLayer);
      }
      if (state.routeGeometry.length > 1) {
        routeLine = L.polyline(state.routeGeometry.map(point => [point.latitude, point.longitude]), { color: '#2E7D5B', weight: 4 }).addTo(map);
      }
      if (state.draftRouteGeometry.length > 1) {
        const draftStyle = state.draftRouteHasDirectSegments
          ? { color: '#ffd166', weight: 2, opacity: 0.9, dashArray: '8 8' }
          : { color: '#2E7D5B', weight: 3, opacity: 0.95 };
        draftRouteLine = L.polyline(state.draftRouteGeometry.map(point => [point.latitude, point.longitude]), draftStyle).addTo(map);
      }
      placeLayer.bringToFront();
      if (map.hasLayer(overlays.hiking)) overlays.hiking.bringToBack();
      routePointLayer.bringToFront();
      if (map.hasLayer(overlays.hiking)) overlays.hiking.bringToBack();
      civilAlertLayer.bringToFront();
      placeLayer.bringToFront();
      routePointLayer.bringToFront();
      if (routeLine) routeLine.bringToFront();
      if (draftRouteLine) draftRouteLine.bringToFront();
    };
    map.on('click', function(event) {
      hideContextMenu();
      if (rulerEnabled) {
        if (!rulerStartLatLng || rulerEndLatLng) {
          rulerStartLatLng = event.latlng;
          rulerEndLatLng = null;
          renderRulerMeasurement(event.latlng, window.__openhikemapDistanceUnit || 'km');
        } else {
          rulerEndLatLng = event.latlng;
          renderRulerMeasurement(rulerEndLatLng, window.__openhikemapDistanceUnit || 'km');
        }
        return;
      }
      window.chrome.webview.postMessage({ type: 'map-click', latitude: event.latlng.lat, longitude: event.latlng.lng });
    });
    map.on('mousemove', function(event) {
      if (rulerEnabled && rulerStartLatLng && !rulerEndLatLng) {
        renderRulerMeasurement(event.latlng, window.__openhikemapDistanceUnit || 'km');
      }
    });
    map.on('resize move zoom', function() {
      if (bearingOverlay.style.display !== 'none') {
        renderBearingCircle(true);
      }
      if (mapScaleControl && mapScaleControl._map) {
        updateModernScale(window.__openhikemapDistanceUnit || 'km');
      }
      if (rulerEnabled && rulerStartLatLng) {
        renderRulerMeasurement(rulerEndLatLng || rulerStartLatLng, window.__openhikemapDistanceUnit || 'km');
      }
    });
    map.on('contextmenu', function(event) {
      const matchingRoutePoint = currentRoutePoints.find(point => map.distance([point.latitude, point.longitude], event.latlng) < 16);
      showContextMenuAt(
        event.originalEvent.clientX,
        event.originalEvent.clientY,
        event.latlng,
        matchingRoutePoint ? matchingRoutePoint.index : null,
        '');
    });
    document.addEventListener('click', function(event) {
      if (event.target.closest('#contextMenu button')) {
        const action = event.target.getAttribute('data-action');
        const routePointIndex = event.target.getAttribute('data-route-point-index');
        const placeName = contextMenu.getAttribute('data-place-name') || '';
        if (contextLatLng) window.chrome.webview.postMessage({ type: 'map-context', action, latitude: contextLatLng.lat, longitude: contextLatLng.lng, routePointIndex, placeName });
      }
      hideContextMenu();
    });
    document.addEventListener('keydown', function(event) {
      if ((event.key === 'Delete' || event.key === 'Del') && currentSelectedPlaceId) {
        window.chrome.webview.postMessage({ type: 'delete-selected-place', id: currentSelectedPlaceId });
        event.preventDefault();
      }
    });
    window.chrome.webview.postMessage({ type: 'map-ready' });
  </script>
</body>
</html>
""";
    }
    private async void SaveAll_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedPlace is not null)
        {
            _viewModel.SelectedPlace.Name = SanitizeMarkup(_viewModel.SelectedPlace.Name);
            _viewModel.SelectedPlace.Description = SanitizeMarkup(_viewModel.SelectedPlace.Description);
            _viewModel.SelectedPlace.PinColorHex = NormalizePinColor(_viewModel.SelectedPlace.PinColorHex);
        }
        if (_viewModel.SelectedFolder is not null)
        {
            _viewModel.SelectedFolder.Name = SanitizeMarkup(_viewModel.SelectedFolder.Name);
            _viewModel.SelectedFolder.PinColorHex = NormalizePinColor(_viewModel.SelectedFolder.PinColorHex);
        }
        ApplyTheme();
        await SaveAndRefreshPlacesAsync();
        _viewModel.StatusMessage = _viewModel.Settings.LanguageCode == "en-US" ? "Data saved." : "Dati salvati.";
    }
    private async void SearchPlace_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.SearchQuery))
        {
            _viewModel.StatusMessage = _viewModel.Settings.LanguageCode == "en-US" ? "Enter text to search." : "Inserisci un testo da cercare.";
            return;
        }
        try
        {
            var results = await _searchService.SearchAsync(_viewModel.SearchQuery, _mapLatitude ?? _viewModel.Settings.DefaultLatitude, _mapLongitude ?? _viewModel.Settings.DefaultLongitude);
            _viewModel.SearchResults.Clear();
            foreach (var result in results)
            {
                _viewModel.SearchResults.Add(result);
            }
            _viewModel.SelectedSearchResult = _viewModel.SearchResults.FirstOrDefault();
            _viewModel.StatusMessage = _viewModel.Settings.LanguageCode == "en-US"
                ? $"Search completed: {_viewModel.SearchResults.Count} results."
                : $"Ricerca completata: {_viewModel.SearchResults.Count} risultati.";
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = exception.Message;
        }
    }
    private async void SearchResults_DoubleClick(object sender, MouseButtonEventArgs e)
        => await CenterSelectedSearchAsync();
    private void SearchResultItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SearchResult result)
        {
            _viewModel.SelectedSearchResult = result;
        }
    }
    private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SearchPlace_Click(sender, new RoutedEventArgs());
        }
    }
    private async Task CenterSelectedSearchAsync()
    {
        if (_viewModel.SelectedSearchResult is not null)
        {
            await CenterMapAsync(_viewModel.SelectedSearchResult.Latitude, _viewModel.SelectedSearchResult.Longitude, 14);
        }
    }
    private async void CenterOnSearchResult_Click(object sender, RoutedEventArgs e)
        => await CenterSelectedSearchAsync();
    private void SetSearchResultAsDefaultCenter_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSearchResult is null)
        {
            return;
        }
        _viewModel.Settings.DefaultLatitude = _viewModel.SelectedSearchResult.Latitude;
        _viewModel.Settings.DefaultLongitude = _viewModel.SelectedSearchResult.Longitude;
        _viewModel.Settings.DefaultZoom = 13;
    }
    private async void AddSearchResultAsStart_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSearchResult is null)
        {
            return;
        }
        AddRoutePoint(_viewModel.SelectedSearchResult.Latitude, _viewModel.SelectedSearchResult.Longitude, "Start");
    }
    private async void AddSearchResultAsEnd_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSearchResult is null)
        {
            return;
        }
        AddRoutePoint(_viewModel.SelectedSearchResult.Latitude, _viewModel.SelectedSearchResult.Longitude, "End");
    }
    private async void AddSearchResultAsWaypoint_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSearchResult is null)
        {
            return;
        }
        AddRoutePoint(_viewModel.SelectedSearchResult.Latitude, _viewModel.SelectedSearchResult.Longitude, "Waypoint");
    }
    private async void AddPlaceFromSearchResult_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSearchResult is null)
        {
            return;
        }
        var result = _viewModel.SelectedSearchResult;
        var place = CreatePlace(result.Latitude, result.Longitude, result.DisplayName.Split(',')[0]);
        place.Description = result.DisplayName;
        _viewModel.Places.Add(place);
        RebuildPlaceTree();
        _viewModel.SelectedFolder = null;
        _viewModel.SelectedPlace = place;
        await SaveAndRefreshPlacesAsync();
    }
    private async void AddRoutePointFromSearchResult_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSearchResult is null)
        {
            return;
        }
        AddRoutePoint(_viewModel.SelectedSearchResult.Latitude, _viewModel.SelectedSearchResult.Longitude, "Waypoint");
        await RefreshMapAsync();
    }
    private void EnablePlacePickMode_Click(object sender, RoutedEventArgs e)
    {
        _placePickMode = true;
        _routePickMode = false;
    }
    private void EnableRoutePickMode_Click(object sender, RoutedEventArgs e)
    {
        _routePickMode = true;
        _placePickMode = false;
    }
    private async void CenterOnSelectedPlace_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedPlace is not null)
        {
            await CenterMapAsync(_viewModel.SelectedPlace.Latitude, _viewModel.SelectedPlace.Longitude, 14);
        }
    }
    private async void CreateBlankPlace_Click(object sender, RoutedEventArgs e)
    {
        var place = CreatePlace(_viewModel.Settings.DefaultLatitude, _viewModel.Settings.DefaultLongitude, "Place");
        _viewModel.Places.Add(place);
        RebuildPlaceTree();
        _viewModel.SelectedFolder = null;
        _viewModel.SelectedPlace = place;
        _selectedPlaceIdOnMap = place.Id;
        await SaveAndRefreshPlacesAsync();
    }
    private async void DeletePlace_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedPlace is null && _viewModel.SelectedFolder is null)
        {
            return;
        }
        var isFolder = _viewModel.SelectedFolder is not null;
        var message = _viewModel.Settings.LanguageCode == "en-US"
            ? (isFolder ? "Are you sure you want to delete the selected folder and its contents?" : "Are you sure you want to delete the selected item?")
            : (isFolder ? "Sei sicuro di voler eliminare la cartella selezionata e tutto il suo contenuto?" : "Sei sicuro di voler eliminare l'elemento selezionato?");
        var title = _viewModel.Settings.LanguageCode == "en-US" ? "Confirm deletion" : "Conferma eliminazione";
        if (MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        if (_viewModel.SelectedPlace is not null)
        {
            var placeToRemove = _viewModel.SelectedPlace;
            _viewModel.Places.Remove(placeToRemove);
            _viewModel.SelectedPlace = null;
            _selectedPlaceIdOnMap = null;
            RebuildPlaceTree();
            await SaveAndRefreshPlacesAsync();
            return;
        }
        if (_viewModel.SelectedFolder is not null)
        {
            DeleteFolderBranch(_viewModel.SelectedFolder.Id);
            _viewModel.SelectedFolder = null;
            RebuildPlaceTree();
            await SaveAndRefreshPlacesAsync();
        }
    }
    private void UsePlaceAsDefaultCenter_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedPlace is null)
        {
            return;
        }
        _viewModel.Settings.DefaultLatitude = _viewModel.SelectedPlace.Latitude;
        _viewModel.Settings.DefaultLongitude = _viewModel.SelectedPlace.Longitude;
        _viewModel.Settings.DefaultZoom = 14;
    }
    private void CreateFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = new SavedFolder
        {
            Name = _viewModel.Settings.LanguageCode == "en-US" ? "New folder" : "Nuova cartella",
            ParentId = _viewModel.SelectedFolder?.Id
        };
        _viewModel.Folders.Add(folder);
        _viewModel.SelectedFolder = folder;
        _viewModel.SelectedPlace = null;
        _selectedPlaceIdOnMap = null;
        Dispatcher.BeginInvoke(() =>
        {
            SelectedFolderNameTextBox.Focus();
            SelectedFolderNameTextBox.SelectAll();
        }, DispatcherPriority.Background);
    }
    private void SavedPlacesTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not PlaceTreeNode node)
        {
            return;
        }
        if (node.IsFolder)
        {
            _viewModel.SelectedFolder = node.Folder;
            _viewModel.SelectedPlace = null;
            _selectedPlaceIdOnMap = null;
            return;
        }
        _viewModel.SelectedFolder = null;
        _viewModel.SelectedPlace = node.Place;
        _selectedPlaceIdOnMap = node.Place?.Id;
    }
    private async void SavedPlacesTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SavedPlacesTreeView.SelectedItem is PlaceTreeNode { Place: not null } node)
        {
            _viewModel.SelectedPlace = node.Place;
            _selectedPlaceIdOnMap = node.Place.Id;
            await CenterMapAsync(node.Place.Latitude, node.Place.Longitude, 14);
        }
    }
    private async void AddPlaceFromCoordinates_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddPinCoordinatesWindow(_viewModel.Settings.DefaultLatitude, _viewModel.Settings.DefaultLongitude)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        var place = CreatePlace(dialog.Latitude, dialog.Longitude, "Place");
        _viewModel.Places.Add(place);
        _viewModel.SelectedPlace = place;
        _selectedPlaceIdOnMap = place.Id;
        await SaveAndRefreshPlacesAsync();
        await CenterMapAsync(dialog.Latitude, dialog.Longitude);
    }
    private async void AddRoutePointFromCoordinates_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddPinCoordinatesWindow(_viewModel.Settings.DefaultLatitude, _viewModel.Settings.DefaultLongitude)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        AddRoutePoint(dialog.Latitude, dialog.Longitude, "Waypoint");
        await RefreshMapAsync();
    }
    private async void DeleteRoutePoint_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedRoutePoint is null) return;
        _viewModel.RoutePoints.Remove(_viewModel.SelectedRoutePoint);
        _viewModel.SelectedRoutePoint = _viewModel.RoutePoints.FirstOrDefault();
        _viewModel.RouteGeometry.Clear();
        await RefreshMapAsync();
    }
    private async void MoveRoutePointUp_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedRoutePoint is null)
        {
            return;
        }
        var index = _viewModel.RoutePoints.IndexOf(_viewModel.SelectedRoutePoint);
        if (index <= 0)
        {
            return;
        }
        _viewModel.RoutePoints.Move(index, index - 1);
        await RebuildDraftRouteAsync();
    }
    private async void MoveRoutePointDown_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedRoutePoint is null)
        {
            return;
        }
        var index = _viewModel.RoutePoints.IndexOf(_viewModel.SelectedRoutePoint);
        if (index < 0 || index >= _viewModel.RoutePoints.Count - 1)
        {
            return;
        }
        _viewModel.RoutePoints.Move(index, index + 1);
        await RebuildDraftRouteAsync();
    }
    private async void ClearRoute_Click(object sender, RoutedEventArgs e)
    {
        var message = _viewModel.Settings.LanguageCode == "en-US"
            ? "Are you sure you want to clear the current route?"
            : "Sei sicuro di voler svuotare la traccia attuale?";
        var title = _viewModel.Settings.LanguageCode == "en-US" ? "Clear route" : "Svuota traccia";
        if (MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        _viewModel.RoutePoints.Clear();
        _viewModel.RouteGeometry.Clear();
        UpdateProfilePaneState();
        await RefreshMapAsync();
    }
    private async void CalculateRoute_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.RoutePoints.Count < 2)
        {
            _viewModel.StatusMessage = _viewModel.Settings.LanguageCode == "en-US" ? "At least two stops are required." : "Servono almeno due tappe per calcolare la traccia.";
            return;
        }
        var route = await _routingService.BuildRouteAsync(_viewModel.RoutePoints.ToList());
        _viewModel.RouteGeometry.Clear();
        foreach (var point in route)
        {
            _viewModel.RouteGeometry.Add(point);
        }
        UpdateRouteSummary();
        UpdateProfilePaneState();
        await RefreshMapAsync();
    }
    private async void ExportGpx_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "GPX (*.gpx)|*.gpx", FileName = "openhikemap-route.gpx" };
        if (dialog.ShowDialog() != true) return;
        var gpx = _gpxService.BuildGpx(_viewModel.RoutePoints.ToList(), _viewModel.RouteGeometry.ToList());
        await File.WriteAllTextAsync(dialog.FileName, gpx);
    }
    private async void ImportGpx_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "GPX (*.gpx)|*.gpx|XML (*.xml)|*.xml|All files (*.*)|*.*" };
        if (dialog.ShowDialog() != true) return;
        var imported = _gpxService.ImportGpx(dialog.FileName);
        _viewModel.RoutePoints.Clear();
        foreach (var point in imported.RoutePoints)
        {
            _viewModel.RoutePoints.Add(point);
        }
        if (_viewModel.RoutePoints.Count > 1)
        {
            _viewModel.RoutePoints[0].PointType = "Start";
            _viewModel.RoutePoints[^1].PointType = "End";
            for (var index = 1; index < _viewModel.RoutePoints.Count - 1; index++)
            {
                _viewModel.RoutePoints[index].PointType = "Waypoint";
            }
        }
        _viewModel.RouteGeometry.Clear();
        foreach (var point in imported.RouteGeometry)
        {
            _viewModel.RouteGeometry.Add(point);
        }
        UpdateRouteSummary();
        UpdateProfilePaneState();
        await UpdateProfileSummaryAsync();
        await RefreshMapAsync();
        await RefreshWeatherAsync();
    }
    private async void ExportPlaces_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "OpenHikeMap places (*.ohmplaces.json)|*.ohmplaces.json", FileName = "places.ohmplaces.json" };
        if (dialog.ShowDialog() != true) return;
        await _storageService.SaveFileAsync(dialog.FileName, _viewModel.Places.ToList());
    }
    private async void ImportPlaces_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "OpenHikeMap places (*.ohmplaces.json)|*.ohmplaces.json|JSON (*.json)|*.json" };
        if (dialog.ShowDialog() != true) return;
        var places = await _storageService.LoadFileAsync<List<SavedPlace>>(dialog.FileName) ?? [];
        NormalizePlaces(places);
        foreach (var place in places) _viewModel.Places.Add(place);
        await SaveAndRefreshPlacesAsync();
    }
    private async void ExportSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "OpenHikeMap settings (*.ohmsettings.json)|*.ohmsettings.json", FileName = "settings.ohmsettings.json" };
        if (dialog.ShowDialog() != true) return;
        await _storageService.SaveFileAsync(dialog.FileName, CreateSettingsSnapshot(includeWeatherReference: false));
    }
    private async void ImportSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "OpenHikeMap settings (*.ohmsettings.json)|*.ohmsettings.json|JSON (*.json)|*.json" };
        if (dialog.ShowDialog() != true) return;
        var settings = await _storageService.LoadFileAsync<AppSettings>(dialog.FileName);
        if (settings is null) return;
        settings.WeatherReferenceLatitude = null;
        settings.WeatherReferenceLongitude = null;
        _viewModel.Settings = settings;
        AttachSettingsChanged();
        ApplyTheme();
        ApplyLanguage();
        DataContext = null;
        DataContext = _viewModel;
        await SaveDataAsync();
        if (_mapReady) MapWebView.NavigateToString(BuildMapHtml());
    }
    private async void CreateBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "OpenHikeMap backup (*.ohmbackup.json)|*.ohmbackup.json", FileName = $"backup-{DateTime.Now:yyyyMMdd-HHmm}.ohmbackup.json" };
        if (dialog.ShowDialog() != true) return;
        var backup = new BackupArchive
        {
            Settings = CreateSettingsSnapshot(includeWeatherReference: false),
            Folders = _viewModel.Folders.ToList(),
            Places = _viewModel.Places.ToList()
        };
        await _storageService.SaveFileAsync(dialog.FileName, backup);
    }
    private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "OpenHikeMap backup (*.ohmbackup.json)|*.ohmbackup.json|JSON (*.json)|*.json" };
        if (dialog.ShowDialog() != true) return;
        var backup = await _storageService.LoadFileAsync<BackupArchive>(dialog.FileName);
        if (backup is null) return;
        backup.Settings ??= new AppSettings();
        backup.Settings.WeatherReferenceLatitude = null;
        backup.Settings.WeatherReferenceLongitude = null;
        NormalizeFolders(backup.Folders);
        NormalizePlaces(backup.Places);
        _viewModel.Settings = backup.Settings;
        AttachSettingsChanged();
        _viewModel.Folders.Clear();
        foreach (var folder in backup.Folders)
        {
            _viewModel.Folders.Add(folder);
        }
        _viewModel.Places.Clear();
        foreach (var place in backup.Places) _viewModel.Places.Add(place);
        _viewModel.RoutePoints.Clear();
        _viewModel.RouteGeometry.Clear();
        _viewModel.SelectedFolder = null;
        _viewModel.SelectedPlace = null;
        _selectedPlaceIdOnMap = null;
        RebuildPlaceTree();
        ApplyTheme();
        ApplyLanguage();
        DataContext = null;
        DataContext = _viewModel;
        await SaveDataAsync();
        await RefreshMapAsync();
    }
    private async void SaveSession_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "OpenHikeMap session (*.ohk)|*.ohk", FileName = $"session-{DateTime.Now:yyyyMMdd-HHmm}.ohk" };
        if (dialog.ShowDialog() != true) return;
        var session = new SessionArchive
        {
            Settings = CreateSettingsSnapshot(includeWeatherReference: true),
            Folders = _viewModel.Folders.ToList(),
            Places = _viewModel.Places.ToList(),
            RoutePoints = _viewModel.RoutePoints.ToList(),
            RouteGeometry = _viewModel.RouteGeometry.ToList()
        };
        await _storageService.SaveFileAsync(dialog.FileName, session);
    }
    private async void OpenSession_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "OpenHikeMap session (*.ohk)|*.ohk|JSON (*.json)|*.json" };
        if (dialog.ShowDialog() != true) return;
        var session = await _storageService.LoadFileAsync<SessionArchive>(dialog.FileName);
        if (session is null) return;
        _viewModel.Settings = session.Settings;
        AttachSettingsChanged();
        NormalizeFolders(session.Folders);
        _viewModel.Folders.Clear();
        foreach (var folder in session.Folders)
        {
            _viewModel.Folders.Add(folder);
        }
        _viewModel.Places.Clear();
        NormalizePlaces(session.Places);
        foreach (var place in session.Places)
        {
            _viewModel.Places.Add(place);
        }
        _viewModel.RoutePoints.Clear();
        foreach (var routePoint in session.RoutePoints)
        {
            _viewModel.RoutePoints.Add(routePoint);
        }
        _viewModel.RouteGeometry.Clear();
        foreach (var point in session.RouteGeometry)
        {
            _viewModel.RouteGeometry.Add(point);
        }
        if (_viewModel.RouteGeometry.Count == 0)
        {
            await RebuildDraftRouteAsync();
        }
        _viewModel.SelectedFolder = null;
        _viewModel.SelectedPlace = null;
        _selectedPlaceIdOnMap = null;
        RebuildPlaceTree();
        ApplyTheme();
        ApplyLanguage();
        DataContext = null;
        DataContext = _viewModel;
        await SaveDataAsync();
        if (_mapReady) MapWebView.NavigateToString(BuildMapHtml());
    }
    private async void ImportKml_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "KML (*.kml)|*.kml" };
        if (dialog.ShowDialog() != true) return;
        var places = _kmlService.ImportPlaces(dialog.FileName);
        NormalizePlaces(places);
        foreach (var place in places) _viewModel.Places.Add(place);
        await SaveAndRefreshPlacesAsync();
    }
    private async void ExportKml_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "KML (*.kml)|*.kml", FileName = "openhikemap-places.kml" };
        if (dialog.ShowDialog() != true) return;
        var kml = _kmlService.ExportPlaces(_viewModel.Places.ToList());
        await File.WriteAllTextAsync(dialog.FileName, kml);
    }
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete)
        {
            return;
        }
        if (Keyboard.FocusedElement is TextBox or ComboBox)
        {
            return;
        }
        if (_viewModel.SelectedPlace is null && _viewModel.SelectedFolder is null)
        {
            return;
        }
        DeletePlace_Click(this, new RoutedEventArgs());
        e.Handled = true;
    }
    private async void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_viewModel) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            ApplyTheme();
            ApplyLanguage();
            await SaveDataAsync();
            await RefreshWeatherAsync();
            if (_mapReady) MapWebView.NavigateToString(BuildMapHtml());
        }
    }
    private void OpenAbout_Click(object sender, RoutedEventArgs e)
    {
        var window = new AboutWindow(_viewModel.Settings.LanguageCode);
        if (IsLoaded && IsVisible)
        {
            window.Owner = this;
        }
        try
        {
            window.ShowDialog();
        }
        catch (InvalidOperationException)
        {
            window.Show();
            window.Activate();
        }
    }
    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    private async void GoHome_Click(object sender, RoutedEventArgs e)
    {
        await CenterMapAsync(_viewModel.Settings.DefaultLatitude, _viewModel.Settings.DefaultLongitude, _viewModel.Settings.DefaultZoom);
    }
    private string GetPinheadSvg(string iconKey)
    {
        var fileName = iconKey switch
        {
            "bivouac" => "house_with_flag.svg",
            "fountain" => "water_tap_with_droplet.svg",
            "food" => "fork_and_knife.svg",
            "paid_hut" => "campsite_with_euro.svg",
            "single_tent" => "campsite.svg",
            "tent_area" => "campsites.svg",
            "parking" => "p_wide.svg",
            "cable_car" => "aerial_tram.svg",
            "hammocks" => "conifer_tree_beside_conifer_tree.svg",
            "waterfall" => "waterfall.svg",
            "water" => "waters.svg",
            "summit_cross" => "latin_cross_on_square.svg",
            "route_start" => "person_wearing_backpack_walking_with_hiking_pole.svg",
            "route_waypoint" => "flagpole_with_flag.svg",
            "route_end" => "flag_checkered.svg",
            _ => "mountain.svg"
        };
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Pinhead", fileName);
        if (!File.Exists(path))
        {
            path = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? string.Empty, "Assets", "Pinhead", fileName);
        }
        if (!File.Exists(path))
        {
            return string.Empty;
        }
        var svg = File.ReadAllText(path);
        return svg.Replace(Environment.NewLine, string.Empty).Replace("\"", "'").Replace("<svg", "<svg width='18' height='18'").Replace("fill='#000000'", "fill='#111111'").Replace("fill='#000'", "fill='#111111'").Replace("fill=\"#000000\"", "fill=\"#111111\"").Replace("fill=\"#000\"", "fill=\"#111111\"");
    }
}
