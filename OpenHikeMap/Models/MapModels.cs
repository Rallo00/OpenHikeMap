using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace OpenHikeMap.Models;
public abstract class BindableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
public sealed partial class LocalizationService : BindableBase
{
    private string _languageCode = "it-IT";
    private static readonly Dictionary<string, Dictionary<string, string>> Dictionaries = new()
    {
        ["it-IT"] = new()
        {
            ["WindowTitle"] = "OpenHikeMap",
            ["MenuFile"] = "File",
            ["MenuBackup"] = "Esporta backup completo...",
            ["MenuRestore"] = "Importa backup completo...",
            ["MenuExportKml"] = "Esporta KML...",
            ["MenuImportKml"] = "Importa KML...",
            ["MenuImportGpx"] = "Importa tracciato GPX...",
            ["MenuExportGpx"] = "Esporta tracciato GPX...",
            ["MenuPins"] = "Pin",
            ["MenuNewPinCoordinates"] = "Aggiungi pin da coordinate",
            ["MenuRoute"] = "Traccia",
            ["MenuCalculateRoute"] = "Calcola traccia",
            ["MenuClearRoute"] = "Svuota traccia",
            ["MenuOpenSession"] = "Apri traccia...",
            ["MenuSaveSession"] = "Salva traccia...",
            ["MenuExit"] = "Esci",
            ["MenuSettings"] = "Impostazioni",
            ["MenuOpenSettings"] = "Apri impostazioni",
            ["MenuExportSettings"] = "Esporta impostazioni...",
            ["MenuImportSettings"] = "Importa impostazioni...",
            ["MenuHelp"] = "?",
            ["MenuAbout"] = "Informazioni su...",
            ["ToolbarShowTrails"] = "Mostra sentieri",
            ["ToolbarSatellite"] = "Overlay satellitare",
            ["ToolbarContours"] = "Mostra curve di livello",
            ["ToolbarWeather"] = "Mostra meteo percorso",
            ["ToolbarCivilAlerts"] = "Visualizza allerte SNPC-IT (Protezione Civile Italiana)",
            ["ToolbarBearingCircle"] = "Overlay direzionale",
            ["ToolbarRuler"] = "Strumento righello",
            ["ToolbarMapScale"] = "Scala mappa",
            ["ToolbarGoHome"] = "Vai a home",
            ["ToolbarSearch"] = "Cerca",
            ["ToolbarSearchPlaceholder"] = "Ricerca qui un luogo",
            ["BrandTagline"] = "YOUR NEXT HIKE.",
            ["FitRouteButton"] = "Inquadra traccia",
            ["SearchPlaceholder"] = "Luoghi, rifugi, cime…",
            ["ExploreKicker"] = "ESPLORA",
            ["PlanKicker"] = "PIANIFICA",
            ["RouteEmpty"] = "Aggiungi partenza e arrivo cliccando sulla mappa.",
            ["RouteStartLabel"] = "Partenza",
            ["RouteEndLabel"] = "Arrivo",
            ["RouteWaypointLabel"] = "Tappa",
            ["RouteCalculating"] = "Calcolo traccia in corso…",
            ["CalculateRouteButton"] = "Calcola traccia",
            ["DeleteRouteButton"] = "Elimina traccia",
            ["ExportGpxButton"] = "Esporta GPX",
            ["RefreshButton"] = "Aggiorna",
            ["WeatherInitialPrompt"] = "Premi Aggiorna per caricare le previsioni.",
            ["MapLegendGroup"] = "Legenda mappa",
            ["MapLegendEmpty"] = "Nessun sentiero visibile nell'area corrente.",
            ["ToolbarMapStyle"] = "Mappa",
            ["ToolbarShowSaved"] = "Mostra elementi salvati",
            ["LeftPaneHeader"] = "Ricerca e Libreria",
            ["SearchGroup"] = "Ricerca luoghi",
            ["SearchLabel"] = "Testo ricerca",
            ["SearchButton"] = "Cerca",
            ["CenterButton"] = "Centra",
            ["CreatePinButton"] = "Crea pin",
            ["SavedPlacesGroup"] = "Elementi salvati",
            ["SavedPlacesSearchLabel"] = "Cerca nei punti salvati",
            ["NewFolderButton"] = "Nuova cartella",
            ["FolderEditorTitle"] = "Modifica cartella",
            ["FolderDefaultIcon"] = "Icona di default",
            ["FolderDefaultColor"] = "Colore di default",
            ["InheritAppearance"] = "Eredita dalla cartella superiore",
            ["FolderInheritanceHint"] = "Le impostazioni vengono ereditate dagli elementi e dalle sottocartelle senza un valore specificato. I valori espliciti restano invariati.",
            ["FolderNameRequired"] = "Inserisci un nome per la cartella.",
            ["FolderForceColors"] = "Forza impostazioni sugli elementi contenuti",
            ["FolderForceColorsConfirm"] = "Salvare le impostazioni della cartella e rimuovere i colori personalizzati di tutti i pin contenuti, incluse le sottocartelle? I pin erediteranno il colore della cartella di appartenenza. Le icone non saranno modificate.",
            ["SettingsDiskReadFailed"] = "Impossibile leggere le impostazioni su disco. Il file non verrà sovrascritto automaticamente.",
            ["SettingsDiskSaveFailed"] = "Impossibile salvare le impostazioni su disco. Controlla i permessi della cartella OpenHikeMap.",
            ["SettingsDiskReplaceConfirm"] = "Il file delle impostazioni su disco non è leggibile. Sostituirlo con le impostazioni attuali?",
            ["ColorGreen"] = "Verde", ["ColorRed"] = "Rosso", ["ColorBlue"] = "Blu", ["ColorOrange"] = "Arancione",
            ["ColorBrown"] = "Marrone", ["ColorPurple"] = "Viola", ["ColorGold"] = "Oro", ["ColorGray"] = "Grigio", ["ColorCurrent"] = "Colore attuale",
            ["Icon_poi"] = "Punto panoramico", ["Icon_bivouac"] = "Bivacco", ["Icon_fountain"] = "Fontana", ["Icon_food"] = "Ristoro",
            ["Icon_paid_hut"] = "Rifugio", ["Icon_single_tent"] = "Tenda", ["Icon_tent_area"] = "Campeggio", ["Icon_parking"] = "Parcheggio",
            ["Icon_cable_car"] = "Funivia", ["Icon_hammocks"] = "Bosco", ["Icon_waterfall"] = "Cascata", ["Icon_water"] = "Acqua", ["Icon_summit_cross"] = "Croce di vetta",
            ["RootFolderName"] = "Punti salvati",
            ["NewButton"] = "Nuovo",
            ["DuplicateButton"] = "Duplica",
            ["DeleteButton"] = "Elimina",
            ["RightPaneHeader"] = "Dettagli e Traccia",
            ["SelectedPinGroup"] = "Elemento selezionato",
            ["FolderLabel"] = "Cartella",
            ["NameLabel"] = "Nome",
            ["DescriptionLabel"] = "Descrizione",
            ["IconLabel"] = "Icona",
            ["ColorLabel"] = "Colore pin",
            ["LatitudeLabel"] = "Latitudine",
            ["LongitudeLabel"] = "Longitudine",
            ["RefreshMapButton"] = "Aggiorna elemento",
            ["UseAsDefaultCenterButton"] = "Usa come centro iniziale",
            ["RoutePlanningGroup"] = "Pianificazione traccia",
            ["SelectedPointTypeLabel"] = "Tipo punto selezionato",
            ["AddCoordinatesButton"] = "Aggiungi da coordinate",
            ["DeleteWaypointButton"] = "Elimina tappa",
            ["QuickSettingsGroup"] = "Impostazioni rapide",
            ["SatelliteOverlay"] = "Overlay satellitare",
            ["TrailsOverlay"] = "Sentieri",
            ["ContoursOverlay"] = "Curve di livello",
            ["OpenFullSettingsButton"] = "Apri impostazioni complete",
            ["BottomPaneHeader"] = "Profilo altimetrico",
            ["ProfileDistanceLabel"] = "Distanza totale",
            ["ProfileTimeLabel"] = "Tempo",
            ["ProfileElevationLabel"] = "Dislivello",
            ["ProfileSlopeLabel"] = "Pendenza media",
            ["ProfileAltitudeLabel"] = "Altitudine",
            ["ProfileHoverSlope"] = "Pendenza",
            ["ProfileChartDescription"] = "Altitudine in funzione della distanza. Muovi il puntatore o usa le frecce per esplorare il percorso.",
            ["ProfileNoElevation"] = "Quote non disponibili per questa traccia.",
            ["ProfileEmpty"] = "Imposta partenza e arrivo per visualizzare il profilo altimetrico.",
            ["OverlayGroup"] = "Overlay",
            ["ColoredTrails"] = "Sentieri colorati",
            ["SearchContextCenter"] = "Centra su risultato",
            ["SearchContextDefaultCenter"] = "Imposta come centro iniziale",
            ["MapContextAddPin"] = "Aggiungi pin",
            ["MapContextDeleteRoutePoint"] = "Elimina punto traccia",
            ["MapContextSetEnd"] = "Imposta come punto di arrivo",
            ["MapContextSetStart"] = "Imposta come punto di partenza",
            ["MapContextSetWaypoint"] = "Imposta come waypoint",
            ["MapContextSetDirectWaypoint"] = "Imposta come waypoint diretto",
            ["MapContextEdit"] = "Modifica elemento",
            ["MapContextSave"] = "Salva modifiche",
            ["SettingsWindowTitle"] = "Impostazioni",
            ["SettingsTabAppearance"] = "Aspetto",
            ["SettingsTabUnits"] = "Unità",
            ["SettingsTabView"] = "Vista",
            ["SettingsAppearanceGroup"] = "Aspetto",
            ["SettingsLanguage"] = "Lingua",
            ["SettingsTheme"] = "Tema applicazione",
            ["SettingsMap"] = "Mappa OpenStreetMap",
            ["SettingsTrails"] = "Attiva overlay sentieri",
            ["SettingsContours"] = "Attiva curve di livello",
            ["SettingsSatellite"] = "Attiva overlay satellitare",
            ["SettingsWeather"] = "Attiva meteo percorso",
            ["SettingsCivilAlerts"] = "Attiva allerte SNPC-IT (Protezione Civile Italiana)",
            ["SettingsWeatherGradients"] = "Usa sfumature nei riquadri meteo",
            ["SettingsUnitsGroup"] = "Unità di misura",
            ["SettingsUnitPreset"] = "Sistema unità",
            ["SettingsUnitPresetMetric"] = "Metrico",
            ["SettingsUnitPresetImperial"] = "Imperiale",
            ["SettingsUnitPresetCustom"] = "Personalizzato",
            ["SettingsMetric"] = "Usa sistema metrico",
            ["SettingsDistance"] = "Distanza",
            ["SettingsAltitude"] = "Altitudine",
            ["SettingsSpeed"] = "Velocità",
            ["SettingsTravelSpeed"] = "Velocità di percorrenza",
            ["SettingsPressure"] = "Pressione",
            ["SettingsTemperature"] = "Temperatura",
            ["SettingsViewGroup"] = "Vista iniziale",
            ["SettingsInitialZoom"] = "Zoom iniziale",
            ["SettingsInitialLatitude"] = "Latitudine iniziale",
            ["SettingsInitialLongitude"] = "Longitudine iniziale",
            ["SettingsSearchCenterGroup"] = "Ricerca centro vista",
            ["SettingsSearchCenterLabel"] = "Cerca un luogo per impostarlo come centro mappa",
            ["SettingsSearchCenterButton"] = "Cerca e usa il primo risultato",
            ["CancelButton"] = "Annulla",
            ["SaveButton"] = "Salva"
            ,
            ["WeatherGroup"] = "Meteo",
            ["WeatherLoading"] = "Caricamento previsioni in corso...",
            ["WeatherUnavailable"] = "Previsioni non disponibili.",
            ["WeatherNoRoute"] = "Aggiungi almeno due punti alla traccia per visualizzare il meteo.",
            ["WeatherMidpoint"] = "Punto medio traccia",
            ["WeatherUpdated"] = "Aggiornato",
            ["WeatherToday"] = "Adesso",
            ["WeatherPrecipitation"] = "Pioggia",
            ["WeatherWind"] = "Vento",
            ["WeatherNoData"] = "Nessun dato meteo.",
            ["WeatherReferenceTitle"] = "Punto di riferimento per meteo",
            ["MapContextSetWeatherPoint"] = "Imposta come punto di calcolo meteo"
            ,
            ["CivilAlertTitle"] = "Allerta Protezione Civile",
            ["CivilAlertNone"] = "Nessuna allerta SNPC-IT sulla zona della traccia.",
            ["CivilAlertLoading"] = "Verifica allerte SNPC-IT in corso...",
            ["CivilAlertUnavailable"] = "Allerte SNPC-IT non disponibili. Impossibile verificare il bollettino.",
            ["WeatherSunrise"] = "Alba",
            ["WeatherSunset"] = "Tramonto",
            ["RoutePointsInserted"] = "Tappe inserite"
        },
        ["en-US"] = new()
        {
            ["WindowTitle"] = "OpenHikeMap",
            ["MenuFile"] = "File",
            ["MenuBackup"] = "Export full backup...",
            ["MenuRestore"] = "Import full backup...",
            ["MenuExportKml"] = "Export KML...",
            ["MenuImportKml"] = "Import KML...",
            ["MenuImportGpx"] = "Import GPX track...",
            ["MenuExportGpx"] = "Export GPX track...",
            ["MenuPins"] = "Pins",
            ["MenuNewPinCoordinates"] = "Add pin from coordinates",
            ["MenuRoute"] = "Route",
            ["MenuCalculateRoute"] = "Calculate route",
            ["MenuClearRoute"] = "Clear route",
            ["MenuOpenSession"] = "Open track...",
            ["MenuSaveSession"] = "Save track...",
            ["MenuExit"] = "Exit",
            ["MenuSettings"] = "Settings",
            ["MenuOpenSettings"] = "Open settings",
            ["MenuExportSettings"] = "Export settings...",
            ["MenuImportSettings"] = "Import settings...",
            ["MenuHelp"] = "?",
            ["MenuAbout"] = "About...",
            ["ToolbarShowTrails"] = "Show trails",
            ["ToolbarSatellite"] = "Satellite overlay",
            ["ToolbarContours"] = "Show contour lines",
            ["ToolbarWeather"] = "Show route weather",
            ["ToolbarCivilAlerts"] = "Show SNPC-IT alerts (Italian Civil Protection)",
            ["ToolbarBearingCircle"] = "Show bearing circle",
            ["ToolbarRuler"] = "Ruler tool",
            ["ToolbarMapScale"] = "Map scale",
            ["ToolbarGoHome"] = "Go home",
            ["ToolbarSearch"] = "Search",
            ["ToolbarSearchPlaceholder"] = "Search a place here",
            ["BrandTagline"] = "YOUR NEXT HIKE.",
            ["FitRouteButton"] = "Fit route",
            ["SearchPlaceholder"] = "Places, huts, peaks…",
            ["ExploreKicker"] = "EXPLORE",
            ["PlanKicker"] = "PLAN",
            ["RouteEmpty"] = "Add a start and destination by clicking on the map.",
            ["RouteStartLabel"] = "Start",
            ["RouteEndLabel"] = "Destination",
            ["RouteWaypointLabel"] = "Stop",
            ["RouteCalculating"] = "Calculating route…",
            ["CalculateRouteButton"] = "Calculate route",
            ["DeleteRouteButton"] = "Delete route",
            ["ExportGpxButton"] = "Export GPX",
            ["RefreshButton"] = "Refresh",
            ["WeatherInitialPrompt"] = "Press Refresh to load the forecast.",
            ["MapLegendGroup"] = "Map legend",
            ["MapLegendEmpty"] = "No trails visible in the current area.",
            ["ToolbarMapStyle"] = "Map",
            ["ToolbarShowSaved"] = "Show saved items",
            ["LeftPaneHeader"] = "Search and Library",
            ["SearchGroup"] = "Search places",
            ["SearchLabel"] = "Search text",
            ["SearchButton"] = "Search",
            ["CenterButton"] = "Center",
            ["CreatePinButton"] = "Create pin",
            ["SavedPlacesGroup"] = "Saved items",
            ["SavedPlacesSearchLabel"] = "Search saved points",
            ["NewFolderButton"] = "New folder",
            ["FolderEditorTitle"] = "Edit folder",
            ["FolderDefaultIcon"] = "Default icon",
            ["FolderDefaultColor"] = "Default color",
            ["InheritAppearance"] = "Inherit from parent folder",
            ["FolderInheritanceHint"] = "Items and subfolders without an explicit value inherit these defaults. Explicit values remain unchanged.",
            ["FolderNameRequired"] = "Enter a folder name.",
            ["FolderForceColors"] = "Force settings on contained items",
            ["FolderForceColorsConfirm"] = "Save the folder settings and remove custom colors from every contained pin, including subfolders? Pins will inherit their containing folder's color. Icons will not be changed.",
            ["SettingsDiskReadFailed"] = "Cannot read settings from disk. The file will not be overwritten automatically.",
            ["SettingsDiskSaveFailed"] = "Cannot save settings to disk. Check permissions for the OpenHikeMap folder.",
            ["SettingsDiskReplaceConfirm"] = "The settings file cannot be read. Replace it with the current settings?",
            ["ColorGreen"] = "Green", ["ColorRed"] = "Red", ["ColorBlue"] = "Blue", ["ColorOrange"] = "Orange",
            ["ColorBrown"] = "Brown", ["ColorPurple"] = "Purple", ["ColorGold"] = "Gold", ["ColorGray"] = "Gray", ["ColorCurrent"] = "Current color",
            ["Icon_poi"] = "Scenic point", ["Icon_bivouac"] = "Bivouac", ["Icon_fountain"] = "Fountain", ["Icon_food"] = "Food",
            ["Icon_paid_hut"] = "Mountain hut", ["Icon_single_tent"] = "Tent", ["Icon_tent_area"] = "Campsite", ["Icon_parking"] = "Parking",
            ["Icon_cable_car"] = "Cable car", ["Icon_hammocks"] = "Woodland", ["Icon_waterfall"] = "Waterfall", ["Icon_water"] = "Water", ["Icon_summit_cross"] = "Summit cross",
            ["RootFolderName"] = "Saved points",
            ["NewButton"] = "New",
            ["DuplicateButton"] = "Duplicate",
            ["DeleteButton"] = "Delete",
            ["RightPaneHeader"] = "Details and Route",
            ["SelectedPinGroup"] = "Selected item",
            ["FolderLabel"] = "Folder",
            ["NameLabel"] = "Name",
            ["DescriptionLabel"] = "Description",
            ["IconLabel"] = "Icon",
            ["ColorLabel"] = "Pin color",
            ["LatitudeLabel"] = "Latitude",
            ["LongitudeLabel"] = "Longitude",
            ["RefreshMapButton"] = "Update item",
            ["UseAsDefaultCenterButton"] = "Use as default center",
            ["RoutePlanningGroup"] = "Route planning",
            ["SelectedPointTypeLabel"] = "Selected point type",
            ["AddCoordinatesButton"] = "Add from coordinates",
            ["DeleteWaypointButton"] = "Delete waypoint",
            ["QuickSettingsGroup"] = "Quick settings",
            ["SatelliteOverlay"] = "Satellite overlay",
            ["TrailsOverlay"] = "Trails",
            ["ContoursOverlay"] = "Contour lines",
            ["OpenFullSettingsButton"] = "Open full settings",
            ["BottomPaneHeader"] = "Elevation profile",
            ["ProfileDistanceLabel"] = "Total distance",
            ["ProfileTimeLabel"] = "Time",
            ["ProfileElevationLabel"] = "Elevation",
            ["ProfileSlopeLabel"] = "Average slope",
            ["ProfileAltitudeLabel"] = "Altitude",
            ["ProfileHoverSlope"] = "Gradient",
            ["ProfileChartDescription"] = "Elevation along the route distance. Move the pointer or use arrow keys to explore the route.",
            ["ProfileNoElevation"] = "Elevation data is unavailable for this route.",
            ["ProfileEmpty"] = "Set a start and destination to display the elevation profile.",
            ["OverlayGroup"] = "Overlay",
            ["ColoredTrails"] = "Colored trails",
            ["SearchContextCenter"] = "Center on result",
            ["SearchContextDefaultCenter"] = "Set as default center",
            ["MapContextAddPin"] = "Add pin",
            ["MapContextDeleteRoutePoint"] = "Delete route point",
            ["MapContextSetEnd"] = "Set as end point",
            ["MapContextSetStart"] = "Set as start point",
            ["MapContextSetWaypoint"] = "Set as waypoint",
            ["MapContextSetDirectWaypoint"] = "Set as direct waypoint",
            ["MapContextEdit"] = "Edit item",
            ["MapContextSave"] = "Save changes",
            ["SettingsWindowTitle"] = "Settings",
            ["SettingsTabAppearance"] = "Appearance",
            ["SettingsTabUnits"] = "Units",
            ["SettingsTabView"] = "View",
            ["SettingsAppearanceGroup"] = "Appearance",
            ["SettingsLanguage"] = "Language",
            ["SettingsTheme"] = "Application theme",
            ["SettingsMap"] = "OpenStreetMap map",
            ["SettingsTrails"] = "Enable trails overlay",
            ["SettingsContours"] = "Enable contour lines",
            ["SettingsSatellite"] = "Enable satellite overlay",
            ["SettingsWeather"] = "Enable route weather",
            ["SettingsCivilAlerts"] = "Enable SNPC-IT alerts (Italian Civil Protection)",
            ["SettingsWeatherGradients"] = "Use gradients in weather cards",
            ["SettingsUnitsGroup"] = "Measurement units",
            ["SettingsUnitPreset"] = "Unit system",
            ["SettingsUnitPresetMetric"] = "Metric",
            ["SettingsUnitPresetImperial"] = "Imperial",
            ["SettingsUnitPresetCustom"] = "Custom",
            ["SettingsMetric"] = "Use metric system",
            ["SettingsDistance"] = "Distance",
            ["SettingsAltitude"] = "Altitude",
            ["SettingsSpeed"] = "Speed",
            ["SettingsTravelSpeed"] = "Travel speed",
            ["SettingsPressure"] = "Pressure",
            ["SettingsTemperature"] = "Temperature",
            ["SettingsViewGroup"] = "Initial view",
            ["SettingsInitialZoom"] = "Initial zoom",
            ["SettingsInitialLatitude"] = "Initial latitude",
            ["SettingsInitialLongitude"] = "Initial longitude",
            ["SettingsSearchCenterGroup"] = "Default center search",
            ["SettingsSearchCenterLabel"] = "Search a place to use as map center",
            ["SettingsSearchCenterButton"] = "Search and use first result",
            ["CancelButton"] = "Cancel",
            ["SaveButton"] = "Save",
            ["WeatherGroup"] = "Weather",
            ["WeatherLoading"] = "Loading forecast...",
            ["WeatherUnavailable"] = "Forecast unavailable.",
            ["WeatherNoRoute"] = "Add at least two route points to view the forecast.",
            ["WeatherMidpoint"] = "Route midpoint",
            ["WeatherUpdated"] = "Updated",
            ["WeatherToday"] = "Now",
            ["WeatherPrecipitation"] = "Rain",
            ["WeatherWind"] = "Wind",
            ["WeatherNoData"] = "No weather data.",
            ["WeatherReferenceTitle"] = "Weather reference point",
            ["MapContextSetWeatherPoint"] = "Set as weather calculation point",
            ["CivilAlertTitle"] = "Civil Protection alert",
            ["CivilAlertNone"] = "No SNPC-IT alert for the route area.",
            ["CivilAlertLoading"] = "Checking SNPC-IT alerts...",
            ["CivilAlertUnavailable"] = "SNPC-IT alerts unavailable. The bulletin could not be checked.",
            ["WeatherSunrise"] = "Sunrise",
            ["WeatherSunset"] = "Sunset",
            ["RoutePointsInserted"] = "Route points inserted"
        },
        ["fr-FR"] = new(),
        ["es-ES"] = new(),
        ["de-DE"] = new(),
        ["nl-NL"] = new(),
        ["nb-NO"] = new(),
        ["da-DK"] = new(),
        ["sv-SE"] = new(),
        ["fi-FI"] = new()
    };
    static LocalizationService()
    {
        var english = Dictionaries["en-US"];
        foreach (var languageCode in new[] { "fr-FR", "es-ES", "de-DE", "nl-NL", "nb-NO", "da-DK", "sv-SE", "fi-FI" })
        {
            foreach (var pair in english)
            {
                Dictionaries[languageCode][pair.Key] = pair.Value;
            }
        }
        ApplyTranslations("fr-FR", new Dictionary<string, string>
        {
            ["MenuFile"] = "Fichier", ["MenuBackup"] = "Sauvegarde complète", ["MenuRestore"] = "Restaurer la sauvegarde", ["MenuImportKml"] = "Importer KML", ["MenuExportGpx"] = "Exporter GPX",
            ["MenuPins"] = "Repères", ["MenuNewPinCoordinates"] = "Ajouter un repère par coordonnées", ["MenuRoute"] = "Itinéraire", ["MenuRouteClickMode"] = "Mode ajout des points par clic",
            ["MenuCalculateRoute"] = "Calculer l'itinéraire", ["MenuClearRoute"] = "Vider l'itinéraire", ["MenuOpenSession"] = "Ouvrir...", ["MenuSaveSession"] = "Enregistrer...",
            ["MenuExit"] = "Quitter", ["MenuSettings"] = "Paramètres", ["MenuOpenSettings"] = "Ouvrir les paramètres", ["MenuExportSettings"] = "Exporter les paramètres",
            ["MenuImportSettings"] = "Importer les paramètres", ["MenuAbout"] = "À propos...", ["ToolbarShowTrails"] = "Afficher les sentiers", ["ToolbarSatellite"] = "Superposition satellite",
            ["ToolbarContours"] = "Afficher les courbes de niveau", ["ToolbarWeather"] = "Afficher la météo du parcours", ["ToolbarGoHome"] = "Aller à l'accueil", ["ToolbarShowSaved"] = "Afficher les éléments enregistrés",
            ["MapLegendGroup"] = "Légende de la carte", ["MapLegendEmpty"] = "Aucun sentier visible dans la zone actuelle.",
            ["LeftPaneHeader"] = "Recherche et bibliothèque", ["SearchGroup"] = "Rechercher des lieux", ["SearchLabel"] = "Texte de recherche", ["SearchButton"] = "Rechercher", ["SavedPlacesGroup"] = "Éléments enregistrés",
            ["SavedPlacesSearchLabel"] = "Rechercher dans les éléments", ["NewFolderButton"] = "Nouveau dossier", ["NewButton"] = "Nouveau", ["DeleteButton"] = "Supprimer", ["RightPaneHeader"] = "Détails et itinéraire",
            ["SelectedPinGroup"] = "Élément sélectionné", ["FolderLabel"] = "Dossier", ["NameLabel"] = "Nom", ["DescriptionLabel"] = "Description", ["IconLabel"] = "Icône", ["ColorLabel"] = "Couleur du repère",
            ["LatitudeLabel"] = "Latitude", ["LongitudeLabel"] = "Longitude", ["RefreshMapButton"] = "Mettre à jour l'élément", ["RoutePlanningGroup"] = "Planification de l'itinéraire",
            ["AddCoordinatesButton"] = "Ajouter par coordonnées", ["DeleteWaypointButton"] = "Supprimer l'étape", ["SettingsWindowTitle"] = "Paramètres", ["SettingsTabAppearance"] = "Apparence",
            ["SettingsTabUnits"] = "Unités", ["SettingsTabView"] = "Vue", ["SettingsAppearanceGroup"] = "Apparence", ["SettingsLanguage"] = "Langue", ["SettingsTheme"] = "Thème de l'application",
            ["SettingsMap"] = "Carte OpenStreetMap", ["SettingsTrails"] = "Activer la superposition des sentiers", ["SettingsContours"] = "Activer les courbes de niveau",
            ["SettingsSatellite"] = "Activer la superposition satellite", ["SettingsWeather"] = "Activer la météo du parcours", ["SettingsUnitsGroup"] = "Unités de mesure", ["SettingsUnitPreset"] = "Système d'unités",
            ["SettingsDistance"] = "Distance", ["SettingsAltitude"] = "Altitude", ["SettingsSpeed"] = "Vitesse", ["SettingsPressure"] = "Pression", ["SettingsViewGroup"] = "Vue initiale",
            ["SettingsInitialZoom"] = "Zoom initial", ["SettingsInitialLatitude"] = "Latitude initiale", ["SettingsInitialLongitude"] = "Longitude initiale", ["CancelButton"] = "Annuler", ["SaveButton"] = "Enregistrer",
            ["WeatherGroup"] = "Météo", ["WeatherLoading"] = "Chargement des prévisions...", ["WeatherUnavailable"] = "Prévisions indisponibles.", ["WeatherNoRoute"] = "Ajoutez au moins deux points à l'itinéraire pour afficher la météo.",
            ["WeatherUpdated"] = "Mis à jour", ["WeatherWind"] = "Vent", ["WeatherReferenceTitle"] = "Point de référence météo", ["MapContextSetWeatherPoint"] = "Définir comme point de calcul météo",
            ["ToolbarSearch"] = "Rechercher", ["ToolbarSearchPlaceholder"] = "Recherchez un lieu ici", ["ToolbarShowSaved"] = "Afficher les éléments enregistrés", ["ToolbarBearingCircle"] = "Outil directionnel", ["ToolbarRuler"] = "Outil règle", ["ToolbarMapScale"] = "Échelle de la carte",
            ["ProfileDistanceLabel"] = "Distance totale", ["ProfileTimeLabel"] = "Temps", ["ProfileElevationLabel"] = "Dénivelé", ["ProfileSlopeLabel"] = "Pente moyenne",
            ["MapContextAddPin"] = "Ajouter un repère", ["MapContextDeleteRoutePoint"] = "Supprimer le point de trace", ["MapContextSetEnd"] = "Définir comme point d'arrivée", ["MapContextSetStart"] = "Définir comme point de départ", ["MapContextSetWaypoint"] = "Définir comme étape", ["MapContextSetDirectWaypoint"] = "Définir comme étape directe",
            ["CivilAlertNone"] = "Aucune alerte SNPC-IT sur la zone de l'itinéraire.", ["RoutePointsInserted"] = "Étapes insérées"
        });
        ApplyTranslations("es-ES", new Dictionary<string, string>
        {
            ["MenuFile"] = "Archivo", ["MenuBackup"] = "Copia de seguridad completa", ["MenuRestore"] = "Restaurar copia de seguridad", ["MenuImportKml"] = "Importar KML", ["MenuExportGpx"] = "Exportar GPX",
            ["MenuPins"] = "Marcadores", ["MenuNewPinCoordinates"] = "Añadir marcador por coordenadas", ["MenuRoute"] = "Ruta", ["MenuRouteClickMode"] = "Modo de puntos por clic",
            ["MenuCalculateRoute"] = "Calcular ruta", ["MenuClearRoute"] = "Vaciar ruta", ["MenuOpenSession"] = "Abrir...", ["MenuSaveSession"] = "Guardar...", ["MenuExit"] = "Salir",
            ["MenuSettings"] = "Configuración", ["MenuOpenSettings"] = "Abrir configuración", ["MenuExportSettings"] = "Exportar configuración", ["MenuImportSettings"] = "Importar configuración",
            ["MenuAbout"] = "Acerca de...", ["ToolbarShowTrails"] = "Mostrar senderos", ["ToolbarSatellite"] = "Superposición satelital", ["ToolbarContours"] = "Mostrar curvas de nivel",
            ["ToolbarWeather"] = "Mostrar tiempo de la ruta", ["ToolbarGoHome"] = "Ir al inicio", ["MapLegendGroup"] = "Leyenda del mapa", ["MapLegendEmpty"] = "No hay senderos visibles en la zona actual.", ["LeftPaneHeader"] = "Búsqueda y biblioteca", ["SearchGroup"] = "Buscar lugares", ["SearchButton"] = "Buscar",
            ["SavedPlacesGroup"] = "Elementos guardados", ["RightPaneHeader"] = "Detalles y ruta", ["SelectedPinGroup"] = "Elemento seleccionado", ["NameLabel"] = "Nombre", ["DescriptionLabel"] = "Descripción",
            ["RefreshMapButton"] = "Actualizar elemento", ["RoutePlanningGroup"] = "Planificación de ruta", ["SettingsWindowTitle"] = "Configuración", ["SettingsTabAppearance"] = "Apariencia", ["SettingsTabUnits"] = "Unidades",
            ["SettingsTabView"] = "Vista", ["SettingsLanguage"] = "Idioma", ["SettingsTheme"] = "Tema de la aplicación", ["SettingsMap"] = "Mapa OpenStreetMap", ["SettingsTrails"] = "Activar senderos",
            ["SettingsContours"] = "Activar curvas de nivel", ["SettingsSatellite"] = "Activar superposición satelital", ["SettingsWeather"] = "Activar tiempo de la ruta", ["CancelButton"] = "Cancelar", ["SaveButton"] = "Guardar",
            ["WeatherGroup"] = "Tiempo", ["WeatherLoading"] = "Cargando previsión...", ["WeatherUnavailable"] = "Previsión no disponible.", ["WeatherNoRoute"] = "Añade al menos dos puntos a la ruta para ver el tiempo.",
            ["WeatherUpdated"] = "Actualizado", ["WeatherWind"] = "Viento", ["WeatherReferenceTitle"] = "Punto de referencia meteorológico", ["MapContextSetWeatherPoint"] = "Establecer como punto de cálculo meteorológico",
            ["ToolbarSearch"] = "Buscar", ["ToolbarSearchPlaceholder"] = "Busca un lugar aquí", ["ToolbarShowSaved"] = "Mostrar elementos guardados", ["ToolbarBearingCircle"] = "Herramienta direccional", ["ToolbarRuler"] = "Herramienta regla", ["ToolbarMapScale"] = "Escala del mapa",
            ["ProfileDistanceLabel"] = "Distancia total", ["ProfileTimeLabel"] = "Tiempo", ["ProfileElevationLabel"] = "Desnivel", ["ProfileSlopeLabel"] = "Pendiente media",
            ["MapContextAddPin"] = "Añadir marcador", ["MapContextDeleteRoutePoint"] = "Eliminar punto de ruta", ["MapContextSetEnd"] = "Establecer como destino", ["MapContextSetStart"] = "Establecer como inicio", ["MapContextSetWaypoint"] = "Establecer como punto de paso", ["MapContextSetDirectWaypoint"] = "Establecer como punto directo",
            ["CivilAlertNone"] = "No hay alertas SNPC-IT en la zona de la ruta.", ["RoutePointsInserted"] = "Puntos de ruta insertados"
        });
        ApplyTranslations("de-DE", new Dictionary<string, string>
        {
            ["MenuFile"] = "Datei", ["MenuBackup"] = "Vollständige Sicherung", ["MenuRestore"] = "Sicherung wiederherstellen", ["MenuImportKml"] = "KML importieren", ["MenuExportGpx"] = "GPX exportieren",
            ["MenuPins"] = "Pins", ["MenuNewPinCoordinates"] = "Pin aus Koordinaten hinzufügen", ["MenuRoute"] = "Route", ["MenuCalculateRoute"] = "Route berechnen", ["MenuClearRoute"] = "Route leeren",
            ["MenuOpenSession"] = "Öffnen...", ["MenuSaveSession"] = "Speichern...", ["MenuExit"] = "Beenden", ["MenuSettings"] = "Einstellungen", ["MenuAbout"] = "Info...", ["ToolbarShowTrails"] = "Wege anzeigen",
            ["ToolbarSatellite"] = "Satellitenüberlagerung", ["ToolbarContours"] = "Höhenlinien anzeigen", ["ToolbarWeather"] = "Routenwetter anzeigen", ["ToolbarGoHome"] = "Zur Startansicht", ["MapLegendGroup"] = "Kartenlegende", ["MapLegendEmpty"] = "Keine Wege im aktuellen Bereich sichtbar.", ["SearchButton"] = "Suchen",
            ["SavedPlacesGroup"] = "Gespeicherte Elemente", ["RightPaneHeader"] = "Details und Route", ["SelectedPinGroup"] = "Ausgewähltes Element", ["NameLabel"] = "Name", ["DescriptionLabel"] = "Beschreibung",
            ["RefreshMapButton"] = "Element aktualisieren", ["RoutePlanningGroup"] = "Routenplanung", ["SettingsWindowTitle"] = "Einstellungen", ["SettingsTabAppearance"] = "Darstellung", ["SettingsTabUnits"] = "Einheiten",
            ["SettingsTabView"] = "Ansicht", ["SettingsLanguage"] = "Sprache", ["SettingsTheme"] = "Anwendungsthema", ["SettingsMap"] = "OpenStreetMap-Karte", ["SettingsTrails"] = "Wege-Overlay aktivieren",
            ["SettingsContours"] = "Höhenlinien aktivieren", ["SettingsSatellite"] = "Satelliten-Overlay aktivieren", ["SettingsWeather"] = "Routenwetter aktivieren", ["CancelButton"] = "Abbrechen", ["SaveButton"] = "Speichern",
            ["WeatherGroup"] = "Wetter", ["WeatherLoading"] = "Vorhersage wird geladen...", ["WeatherUnavailable"] = "Vorhersage nicht verfügbar.", ["WeatherNoRoute"] = "Fügen Sie mindestens zwei Punkte zur Route hinzu, um das Wetter anzuzeigen.",
            ["WeatherUpdated"] = "Aktualisiert", ["WeatherWind"] = "Wind", ["WeatherReferenceTitle"] = "Wetterbezugspunkt", ["MapContextSetWeatherPoint"] = "Als Wetterberechnungspunkt festlegen",
            ["ToolbarSearch"] = "Suchen", ["ToolbarSearchPlaceholder"] = "Ort hier suchen", ["ToolbarShowSaved"] = "Gespeicherte Elemente anzeigen", ["ToolbarBearingCircle"] = "Richtungswerkzeug", ["ToolbarRuler"] = "Linealwerkzeug", ["ToolbarMapScale"] = "Maßstab",
            ["ProfileDistanceLabel"] = "Gesamtdistanz", ["ProfileTimeLabel"] = "Zeit", ["ProfileElevationLabel"] = "Höhenunterschied", ["ProfileSlopeLabel"] = "Durchschnittssteigung",
            ["MapContextAddPin"] = "Pin hinzufügen", ["MapContextDeleteRoutePoint"] = "Routenpunkt löschen", ["MapContextSetEnd"] = "Als Zielpunkt setzen", ["MapContextSetStart"] = "Als Startpunkt setzen", ["MapContextSetWaypoint"] = "Als Wegpunkt setzen", ["MapContextSetDirectWaypoint"] = "Als direkten Wegpunkt setzen",
            ["CivilAlertNone"] = "Keine SNPC-IT-Warnung im Routenbereich.", ["RoutePointsInserted"] = "Routenpunkte eingefügt"
        });
        ApplyTranslations("nl-NL", new Dictionary<string, string> { ["MenuFile"] = "Bestand", ["MenuSettings"] = "Instellingen", ["SearchButton"] = "Zoeken", ["SavedPlacesGroup"] = "Opgeslagen elementen", ["MapLegendGroup"] = "Kaartlegenda", ["MapLegendEmpty"] = "Geen paden zichtbaar in het huidige gebied.", ["WeatherGroup"] = "Weer", ["WeatherUpdated"] = "Bijgewerkt", ["ToolbarSearch"] = "Zoeken", ["ToolbarSearchPlaceholder"] = "Zoek hier een plaats", ["ToolbarShowSaved"] = "Opgeslagen elementen tonen", ["ToolbarBearingCircle"] = "Richtingstool", ["ToolbarRuler"] = "Liniaal", ["ToolbarMapScale"] = "Kaartschaal", ["ProfileDistanceLabel"] = "Totale afstand", ["ProfileTimeLabel"] = "Tijd", ["ProfileElevationLabel"] = "Hoogteverschil", ["ProfileSlopeLabel"] = "Gemiddelde helling", ["MapContextAddPin"] = "Pin toevoegen", ["MapContextDeleteRoutePoint"] = "Routepunt verwijderen", ["MapContextSetEnd"] = "Als eindpunt instellen", ["MapContextSetStart"] = "Als startpunt instellen", ["MapContextSetWaypoint"] = "Als waypoint instellen", ["MapContextSetDirectWaypoint"] = "Als direct waypoint instellen", ["WeatherReferenceTitle"] = "Weerreferentiepunt", ["MapContextSetWeatherPoint"] = "Instellen als weerberekeningspunt", ["CivilAlertNone"] = "Geen SNPC-IT-waarschuwing voor het routegebied.", ["RoutePointsInserted"] = "Routepunten ingevoerd" });
        ApplyTranslations("nb-NO", new Dictionary<string, string> { ["MenuFile"] = "Fil", ["MenuSettings"] = "Innstillinger", ["SearchButton"] = "Søk", ["SavedPlacesGroup"] = "Lagrede elementer", ["MapLegendGroup"] = "Kartforklaring", ["MapLegendEmpty"] = "Ingen stier synlige i gjeldende område.", ["WeatherGroup"] = "Vær", ["WeatherUpdated"] = "Oppdatert", ["ToolbarSearch"] = "Søk", ["ToolbarSearchPlaceholder"] = "Søk etter et sted her", ["ToolbarShowSaved"] = "Vis lagrede elementer", ["ToolbarBearingCircle"] = "Retningsverktøy", ["ToolbarRuler"] = "Linjalverktøy", ["ToolbarMapScale"] = "Kartskala", ["ProfileDistanceLabel"] = "Total avstand", ["ProfileTimeLabel"] = "Tid", ["ProfileElevationLabel"] = "Høydemeter", ["ProfileSlopeLabel"] = "Gjennomsnittlig stigning", ["MapContextAddPin"] = "Legg til pin", ["MapContextDeleteRoutePoint"] = "Slett rutepunkt", ["MapContextSetEnd"] = "Sett som endepunkt", ["MapContextSetStart"] = "Sett som startpunkt", ["MapContextSetWaypoint"] = "Sett som veipunkt", ["MapContextSetDirectWaypoint"] = "Sett som direkte veipunkt", ["WeatherReferenceTitle"] = "Værreferansepunkt", ["MapContextSetWeatherPoint"] = "Sett som værberegningspunkt", ["CivilAlertNone"] = "Ingen SNPC-IT-varsel i ruteområdet.", ["RoutePointsInserted"] = "Rutekpunkter lagt til" });
        ApplyTranslations("da-DK", new Dictionary<string, string> { ["MenuFile"] = "Fil", ["MenuSettings"] = "Indstillinger", ["SearchButton"] = "Søg", ["SavedPlacesGroup"] = "Gemte elementer", ["MapLegendGroup"] = "Kortforklaring", ["MapLegendEmpty"] = "Ingen stier synlige i det aktuelle område.", ["WeatherGroup"] = "Vejr", ["WeatherUpdated"] = "Opdateret", ["ToolbarSearch"] = "Søg", ["ToolbarSearchPlaceholder"] = "Søg efter et sted her", ["ToolbarShowSaved"] = "Vis gemte elementer", ["ToolbarBearingCircle"] = "Retningsværktøj", ["ToolbarRuler"] = "Linealværktøj", ["ToolbarMapScale"] = "Kortskala", ["ProfileDistanceLabel"] = "Samlet afstand", ["ProfileTimeLabel"] = "Tid", ["ProfileElevationLabel"] = "Højdeforskel", ["ProfileSlopeLabel"] = "Gennemsnitlig hældning", ["MapContextAddPin"] = "Tilføj pin", ["MapContextDeleteRoutePoint"] = "Slet rutepunkt", ["MapContextSetEnd"] = "Sæt som slutpunkt", ["MapContextSetStart"] = "Sæt som startpunkt", ["MapContextSetWaypoint"] = "Sæt som waypoint", ["MapContextSetDirectWaypoint"] = "Sæt som direkte waypoint", ["WeatherReferenceTitle"] = "Vejrreferencepunkt", ["MapContextSetWeatherPoint"] = "Sæt som vejrberegningspunkt", ["CivilAlertNone"] = "Ingen SNPC-IT-varsel i ruteområdet.", ["RoutePointsInserted"] = "Rutepunkter indsat" });
        ApplyTranslations("sv-SE", new Dictionary<string, string> { ["MenuFile"] = "Fil", ["MenuSettings"] = "Inställningar", ["SearchButton"] = "Sök", ["SavedPlacesGroup"] = "Sparade objekt", ["MapLegendGroup"] = "Kartlegend", ["MapLegendEmpty"] = "Inga leder synliga i det aktuella området.", ["WeatherGroup"] = "Väder", ["WeatherUpdated"] = "Uppdaterad", ["ToolbarSearch"] = "Sök", ["ToolbarSearchPlaceholder"] = "Sök en plats här", ["ToolbarShowSaved"] = "Visa sparade objekt", ["ToolbarBearingCircle"] = "Riktningsverktyg", ["ToolbarRuler"] = "Linjalsverktyg", ["ToolbarMapScale"] = "Kartskala", ["ProfileDistanceLabel"] = "Total sträcka", ["ProfileTimeLabel"] = "Tid", ["ProfileElevationLabel"] = "Höjdskillnad", ["ProfileSlopeLabel"] = "Genomsnittlig lutning", ["MapContextAddPin"] = "Lägg till pin", ["MapContextDeleteRoutePoint"] = "Ta bort ruttpunkt", ["MapContextSetEnd"] = "Ange som slutpunkt", ["MapContextSetStart"] = "Ange som startpunkt", ["MapContextSetWaypoint"] = "Ange som waypoint", ["MapContextSetDirectWaypoint"] = "Ange som direkt waypoint", ["WeatherReferenceTitle"] = "Väderreferenspunkt", ["MapContextSetWeatherPoint"] = "Ange som väderberäkningspunkt", ["CivilAlertNone"] = "Ingen SNPC-IT-varning i ruttområdet.", ["RoutePointsInserted"] = "Ruttpunkter infogade" });
        ApplyTranslations("fi-FI", new Dictionary<string, string> { ["MenuFile"] = "Tiedosto", ["MenuSettings"] = "Asetukset", ["SearchButton"] = "Hae", ["SavedPlacesGroup"] = "Tallennetut kohteet", ["MapLegendGroup"] = "Kartan selite", ["MapLegendEmpty"] = "Nykyisellä alueella ei näy polkuja.", ["WeatherGroup"] = "Sää", ["WeatherUpdated"] = "Päivitetty", ["ToolbarSearch"] = "Hae", ["ToolbarSearchPlaceholder"] = "Hae paikkaa tästä", ["ToolbarShowSaved"] = "Näytä tallennetut kohteet", ["ToolbarBearingCircle"] = "Suuntaustyökalu", ["ToolbarRuler"] = "Viivaintyökalu", ["ToolbarMapScale"] = "Karttakaava", ["ProfileDistanceLabel"] = "Kokonaismatka", ["ProfileTimeLabel"] = "Aika", ["ProfileElevationLabel"] = "Korkeusero", ["ProfileSlopeLabel"] = "Keskimääräinen kaltevuus", ["MapContextAddPin"] = "Lisää pinni", ["MapContextDeleteRoutePoint"] = "Poista reittipiste", ["MapContextSetEnd"] = "Aseta päätepisteeksi", ["MapContextSetStart"] = "Aseta aloituspisteeksi", ["MapContextSetWaypoint"] = "Aseta välietapiksi", ["MapContextSetDirectWaypoint"] = "Aseta suoraksi välietapiksi", ["WeatherReferenceTitle"] = "Sään viitepiste", ["MapContextSetWeatherPoint"] = "Aseta sään laskentapisteeksi", ["CivilAlertNone"] = "Ei SNPC-IT-hälytystä reitin alueella.", ["RoutePointsInserted"] = "Reittipisteitä lisätty" });
        ApplyFolderActionTranslations();
        ApplyTransferTranslations();
    }
    private static void ApplyFolderActionTranslations()
    {
        ApplyTranslations("fr-FR", new() { ["FolderForceColors"] = "Appliquer les paramètres aux éléments contenus", ["FolderForceColorsConfirm"] = "Enregistrer les paramètres du dossier et supprimer les couleurs personnalisées de tous les repères contenus, y compris dans les sous-dossiers ? Les repères hériteront de la couleur de leur dossier. Les icônes ne seront pas modifiées." });
        ApplyTranslations("es-ES", new() { ["FolderForceColors"] = "Forzar la configuración en los elementos contenidos", ["FolderForceColorsConfirm"] = "¿Guardar la configuración de la carpeta y eliminar los colores personalizados de todos los marcadores contenidos, incluidas las subcarpetas? Los marcadores heredarán el color de su carpeta. Los iconos no se modificarán." });
        ApplyTranslations("de-DE", new() { ["FolderForceColors"] = "Einstellungen auf enthaltene Elemente anwenden", ["FolderForceColorsConfirm"] = "Ordnereinstellungen speichern und benutzerdefinierte Farben aller enthaltenen Pins einschließlich der Unterordner entfernen? Die Pins erben die Farbe ihres Ordners. Symbole werden nicht geändert." });
        ApplyTranslations("nl-NL", new() { ["FolderForceColors"] = "Instellingen toepassen op onderliggende elementen", ["FolderForceColorsConfirm"] = "De mapinstellingen opslaan en aangepaste kleuren van alle pins in deze map en submappen verwijderen? Pins erven de kleur van hun map. Pictogrammen worden niet gewijzigd." });
        ApplyTranslations("nb-NO", new() { ["FolderForceColors"] = "Bruk innstillingene på innholdet", ["FolderForceColorsConfirm"] = "Lagre mappeinnstillingene og fjerne egendefinerte farger fra alle kartnåler i mappen og undermappene? Kartnålene arver fargen fra mappen sin. Ikonene endres ikke." });
        ApplyTranslations("da-DK", new() { ["FolderForceColors"] = "Anvend indstillinger på de indeholdte elementer", ["FolderForceColorsConfirm"] = "Gem mappeindstillingerne og fjern brugerdefinerede farver fra alle markører i mappen og undermapperne? Markørerne arver farven fra deres mappe. Ikonerne ændres ikke." });
        ApplyTranslations("sv-SE", new() { ["FolderForceColors"] = "Tillämpa inställningarna på innehållet", ["FolderForceColorsConfirm"] = "Spara mappinställningarna och ta bort anpassade färger från alla kartnålar i mappen och undermapparna? Kartnålarna ärver färgen från sin mapp. Ikonerna ändras inte." });
        ApplyTranslations("fi-FI", new() { ["FolderForceColors"] = "Käytä asetuksia kansion kohteisiin", ["FolderForceColorsConfirm"] = "Tallennetaanko kansion asetukset ja poistetaanko mukautetut värit kaikista kansion ja alikansioiden karttamerkeistä? Merkit perivät oman kansionsa värin. Kuvakkeita ei muuteta." });
    }
    private static void ApplyTranslations(string languageCode, Dictionary<string, string> values)
    {
        foreach (var pair in values)
        {
            Dictionaries[languageCode][pair.Key] = pair.Value;
        }
    }
    public string this[string key]
    {
        get
        {
            if (Dictionaries.TryGetValue(_languageCode, out var language) && language.TryGetValue(key, out var value))
            {
                return value;
            }
            return Dictionaries["it-IT"].TryGetValue(key, out var fallback) ? fallback : key;
        }
    }
    public void SetLanguage(string languageCode)
    {
        _languageCode = Dictionaries.ContainsKey(languageCode) ? languageCode : "it-IT";
        RaisePropertyChanged("Item[]");
    }
}
public sealed class AppSettings : BindableBase
{
    private bool _isMetric = true;
    private string _languageCode = "it-IT";
    private string _themeMode = "System";
    private double _defaultLatitude = 45.4642;
    private double _defaultLongitude = 9.19;
    private int _defaultZoom = 11;
    private string _mapStyle = "OpenStreetMap";
    private bool _showSatelliteOverlay;
    private bool _showHikingTrails = true;
    private bool _showContourLines = true;
    private bool _showSavedPoints = true;
    private bool _showWeather;
    private bool _showCivilProtectionAlerts;
    private bool _showBearingCircle;
    private bool _showRulerTool;
    private bool _showMapScale;
    private bool _showWeatherGradients;
    private string _unitPreset = "Metric";
    private string _distanceUnit = "km";
    private string _altitudeUnit = "m";
    private string _speedUnit = "km/h";
    private double _travelSpeed = 2.0;
    private string _pressureUnit = "hPa";
    private string _temperatureUnit = "C";
    private double? _weatherReferenceLatitude;
    private double? _weatherReferenceLongitude;
    public bool IsMetric { get => _isMetric; set => SetProperty(ref _isMetric, value); }
    public string LanguageCode { get => _languageCode; set => SetProperty(ref _languageCode, value); }
    public string ThemeMode { get => _themeMode; set => SetProperty(ref _themeMode, value); }
    public double DefaultLatitude { get => _defaultLatitude; set => SetProperty(ref _defaultLatitude, value); }
    public double DefaultLongitude { get => _defaultLongitude; set => SetProperty(ref _defaultLongitude, value); }
    public int DefaultZoom { get => _defaultZoom; set => SetProperty(ref _defaultZoom, value); }
    public string MapStyle { get => _mapStyle; set => SetProperty(ref _mapStyle, value); }
    public bool ShowSatelliteOverlay { get => _showSatelliteOverlay; set => SetProperty(ref _showSatelliteOverlay, value); }
    public bool ShowHikingTrails { get => _showHikingTrails; set => SetProperty(ref _showHikingTrails, value); }
    public bool ShowContourLines { get => _showContourLines; set => SetProperty(ref _showContourLines, value); }
    public bool ShowSavedPoints { get => _showSavedPoints; set => SetProperty(ref _showSavedPoints, value); }
    public bool ShowWeather { get => _showWeather; set => SetProperty(ref _showWeather, value); }
    public bool ShowCivilProtectionAlerts { get => _showCivilProtectionAlerts; set => SetProperty(ref _showCivilProtectionAlerts, value); }
    public bool ShowBearingCircle { get => _showBearingCircle; set => SetProperty(ref _showBearingCircle, value); }
    public bool ShowRulerTool { get => _showRulerTool; set => SetProperty(ref _showRulerTool, value); }
    public bool ShowMapScale { get => _showMapScale; set => SetProperty(ref _showMapScale, value); }
    public bool ShowWeatherGradients { get => _showWeatherGradients; set => SetProperty(ref _showWeatherGradients, value); }
    public string UnitPreset { get => _unitPreset; set => SetProperty(ref _unitPreset, value); }
    public string DistanceUnit { get => _distanceUnit; set => SetProperty(ref _distanceUnit, value); }
    public string AltitudeUnit { get => _altitudeUnit; set => SetProperty(ref _altitudeUnit, value); }
    public string SpeedUnit { get => _speedUnit; set => SetProperty(ref _speedUnit, value); }
    public double TravelSpeed { get => _travelSpeed; set => SetProperty(ref _travelSpeed, value); }
    public string PressureUnit { get => _pressureUnit; set => SetProperty(ref _pressureUnit, value); }
    public string TemperatureUnit { get => _temperatureUnit; set => SetProperty(ref _temperatureUnit, value); }
    public double? WeatherReferenceLatitude { get => _weatherReferenceLatitude; set => SetProperty(ref _weatherReferenceLatitude, value); }
    public double? WeatherReferenceLongitude { get => _weatherReferenceLongitude; set => SetProperty(ref _weatherReferenceLongitude, value); }
}
public sealed class SavedPlace : BindableBase
{
    public const string DefaultPinColor = "#D65A5A";
    private Guid _id = Guid.NewGuid();
    private string _name = "Nuovo luogo";
    private string _description = string.Empty;
    private double _latitude;
    private double _longitude;
    private Guid? _folderId;
    private bool _isVisible = true;
    private string _iconKey = string.Empty;
    private string _pinColorHex = string.Empty;
    public Guid Id { get => _id; set => SetProperty(ref _id, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public double Latitude { get => _latitude; set { if (SetProperty(ref _latitude, value)) RaisePropertyChanged(nameof(CoordinateSummary)); } }
    public double Longitude { get => _longitude; set { if (SetProperty(ref _longitude, value)) RaisePropertyChanged(nameof(CoordinateSummary)); } }
    public Guid? FolderId { get => _folderId; set => SetProperty(ref _folderId, value); }
    public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
    public string IconKey { get => _iconKey; set => SetProperty(ref _iconKey, value); }
    public string PinColorHex { get => _pinColorHex; set => SetProperty(ref _pinColorHex, value); }
    public string CoordinateSummary => $"{Latitude:F5}, {Longitude:F5}";
    public SavedPlace Clone() => new()
    {
        Name = $"{Name} copia",
        Description = Description,
        Latitude = Latitude,
        Longitude = Longitude,
        FolderId = FolderId,
        IsVisible = IsVisible,
        IconKey = IconKey,
        PinColorHex = PinColorHex
    };
}
public sealed class SavedFolder : BindableBase
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Nuova cartella";
    private Guid? _parentId;
    private bool _isEnabled = true;
    private string _iconKey = string.Empty;
    private string _pinColorHex = string.Empty;
    public Guid Id { get => _id; set => SetProperty(ref _id, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public Guid? ParentId { get => _parentId; set => SetProperty(ref _parentId, value); }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public string IconKey { get => _iconKey; set => SetProperty(ref _iconKey, value); }
    public string PinColorHex { get => _pinColorHex; set => SetProperty(ref _pinColorHex, value); }
}
public sealed class RoutePoint : BindableBase
{
    private string _name = "Tappa";
    private double _latitude;
    private double _longitude;
    private string _pointType = "Waypoint";
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public double Latitude { get => _latitude; set { if (SetProperty(ref _latitude, value)) RaisePropertyChanged(nameof(CoordinateSummary)); } }
    public double Longitude { get => _longitude; set { if (SetProperty(ref _longitude, value)) RaisePropertyChanged(nameof(CoordinateSummary)); } }
    public string PointType { get => _pointType; set => SetProperty(ref _pointType, value); }
    public string CoordinateSummary => $"{Latitude:F5}, {Longitude:F5}";
}
public sealed class RoutePathPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? ElevationMeters { get; set; }
}
public sealed class RouteProfileSample
{
    public double DistanceKm { get; set; }
    public double ElevationMeters { get; set; }
}
public sealed class ProfileHoverPoint
{
    public double CanvasX { get; set; }
    public double CanvasY { get; set; }
    public double DistanceKm { get; set; }
    public double ElapsedHours { get; set; }
    public double ElevationMeters { get; set; }
    public double SlopePercent { get; set; }
}
public sealed class ProfileColoredSegment
{
    public string Geometry { get; set; } = string.Empty;
    public string StrokeHex { get; set; } = "#4CAF50";
}
public sealed class WeatherForecastDay
{
    public string DayLabel { get; set; } = string.Empty;
    public string WeatherIconKey { get; set; } = string.Empty;
    public string TemperatureLabel { get; set; } = string.Empty;
    public string WeatherLabel { get; set; } = string.Empty;
    public string WindLabel { get; set; } = string.Empty;
    public string WindDirectionLabel { get; set; } = string.Empty;
    public string RainLabel { get; set; } = string.Empty;
    public string UvLabel { get; set; } = string.Empty;
    public string SunriseLabel { get; set; } = string.Empty;
    public string SunsetLabel { get; set; } = string.Empty;
}
public sealed class SearchResult
{
    public string DisplayName { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
public sealed class BackupArchive
{
    public AppSettings Settings { get; set; } = new();
    public List<SavedFolder> Folders { get; set; } = [];
    public List<SavedPlace> Places { get; set; } = [];
}
public sealed class SessionArchive
{
    public AppSettings Settings { get; set; } = new();
    public List<SavedFolder> Folders { get; set; } = [];
    public List<SavedPlace> Places { get; set; } = [];
    public List<RoutePoint> RoutePoints { get; set; } = [];
    public List<RoutePathPoint> RouteGeometry { get; set; } = [];
}
public sealed class GpxImportResult
{
    public List<RoutePoint> RoutePoints { get; set; } = [];
    public List<RoutePathPoint> RouteGeometry { get; set; } = [];
}
public sealed class AppDataModel
{
    public AppSettings Settings { get; set; } = new();
    public List<SavedFolder> Folders { get; set; } = [];
    public List<SavedPlace> Places { get; set; } = [];
    public List<RoutePoint> RoutePoints { get; set; } = [];
}
public sealed class IconOption
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
public sealed class PinColorOption
{
    public string Hex { get; init; } = SavedPlace.DefaultPinColor;
    public string DisplayName { get; init; } = string.Empty;
}
public sealed class PlaceTreeNode : BindableBase
{
    private bool _isExpanded = true;
    public string Name { get; init; } = string.Empty;
    public SavedFolder? Folder { get; init; }
    public SavedPlace? Place { get; init; }
    public ObservableCollection<PlaceTreeNode> Children { get; } = [];
    public bool IsFolder => Folder is not null;
    public bool IsPlace => Place is not null;
    public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }
}

