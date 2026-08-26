using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace OpenHikeMap;
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
public sealed class LocalizationService : BindableBase
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
    private string _iconKey = "poi";
    private string _pinColorHex = DefaultPinColor;
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
    private string _pinColorHex = SavedPlace.DefaultPinColor;
    public Guid Id { get => _id; set => SetProperty(ref _id, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public Guid? ParentId { get => _parentId; set => SetProperty(ref _parentId, value); }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
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
public sealed class MainWindowViewModel : BindableBase
{
    private SavedPlace? _selectedPlace;
    private SavedFolder? _selectedFolder;
    private RoutePoint? _selectedRoutePoint;
    private SearchResult? _selectedSearchResult;
    private string _searchQuery = string.Empty;
    private string _savedPlacesSearchQuery = string.Empty;
    private string _statusMessage = "Pronto.";
    private string _selectionModeMessage = string.Empty;
    private string _routeSummary = "Nessuna traccia calcolata.";
    private string _profileSummary = "Curve di livello pronte.";
    private bool _isProfilePaneExpanded;
    private string _profileLinePoints = string.Empty;
    private string _profileFillPoints = string.Empty;
    private string _profileLineGeometry = string.Empty;
    private string _profileFillGeometry = string.Empty;
    private string _profileLineGeometryGreen = string.Empty;
    private string _profileLineGeometryYellow = string.Empty;
    private string _profileLineGeometryOrange = string.Empty;
    private string _profileLineGeometryRed = string.Empty;
    private string _profileMaxLabel = string.Empty;
    private string _profileUpperMidLabel = string.Empty;
    private string _profileLowerMidLabel = string.Empty;
    private string _profileMinLabel = string.Empty;
    private string _profileStartDistanceLabel = "0 km";
    private string _profileQuarterDistanceLabel = string.Empty;
    private string _profileMidDistanceLabel = string.Empty;
    private string _profileThreeQuarterDistanceLabel = string.Empty;
    private string _profileEndDistanceLabel = string.Empty;
    private string _profileDistanceValueText = string.Empty;
    private string _profileDistanceUnitText = string.Empty;
    private string _profileDistanceText = string.Empty;
    private string _profileDurationText = string.Empty;
    private string _profileHoverText = string.Empty;
    private string _profileElevationTotalText = string.Empty;
    private string _profileElevationDetailsText = string.Empty;
    private string _profileSlopeDetailsText = string.Empty;
    private string _profileElevationSummaryText = string.Empty;
    private string _profileGainText = string.Empty;
    private string _profileLossText = string.Empty;
    private double _profileChartCanvasWidth = 1180d;
    private double _profileAxisWidth = 1120d;
    private string _weatherStatus = string.Empty;
    private string _weatherLocationLabel = string.Empty;
    private string _weatherCurrentLabel = string.Empty;
    private string _weatherUpdatedLabel = string.Empty;
    private string _civilAlertSummary = string.Empty;
    public MainWindowViewModel()
    {
        Settings.PropertyChanged += (_, _) => RaisePropertyChanged(nameof(Settings));
    }
    public LocalizationService L { get; } = new();
    public AppSettings Settings { get; set; } = new();
    public ObservableCollection<SavedFolder> Folders { get; } = [];
    public ObservableCollection<SavedPlace> Places { get; } = [];
    public ObservableCollection<PlaceTreeNode> PlaceTree { get; } = [];
    public ObservableCollection<RoutePoint> RoutePoints { get; } = [];
    public ObservableCollection<SearchResult> SearchResults { get; } = [];
    public ObservableCollection<RoutePathPoint> RouteGeometry { get; } = [];
    public ObservableCollection<WeatherForecastDay> WeatherForecast { get; } = [];
    public ObservableCollection<ProfileColoredSegment> ProfileColoredSegments { get; } = [];
    public ObservableCollection<ProfileHoverPoint> ProfileHoverPoints { get; } = [];
    public ObservableCollection<IconOption> AvailableIcons { get; } = [];
    public ObservableCollection<PinColorOption> AvailablePinColors { get; } = [];
    public IReadOnlyList<string> AvailableThemes { get; } = ["System", "Light", "Dark"];
    public IReadOnlyList<string> AvailableLanguages { get; } = ["it-IT", "en-US", "fr-FR", "es-ES", "de-DE", "nl-NL", "nb-NO", "da-DK", "sv-SE", "fi-FI"];
    public IReadOnlyList<string> AvailableMapStyles { get; } = ["OpenStreetMap", "OpenTopoMap", "CyclOSM", "Humanitarian", "OSM DE", "Carto Voyager", "Carto Light", "Carto Dark"];
    public IReadOnlyList<string> AvailableUnitPresets { get; } = ["Metric", "Imperial", "Custom"];
    public IReadOnlyList<string> AvailableDistanceUnits { get; } = ["km", "mi", "m", "ft"];
    public IReadOnlyList<string> AvailableAltitudeUnits { get; } = ["m", "ft"];
    public IReadOnlyList<string> AvailableSpeedUnits { get; } = ["km/h", "mph", "m/s"];
    public IReadOnlyList<string> AvailablePressureUnits { get; } = ["hPa", "mbar", "mmHg", "inHg"];
    public IReadOnlyList<string> AvailableTemperatureUnits { get; } = ["C", "F"];
    public IReadOnlyList<string> AvailableRoutePointTypes { get; } = ["Start", "Waypoint", "DirectWaypoint", "End"];
    public SavedPlace? SelectedPlace { get => _selectedPlace; set { if (SetProperty(ref _selectedPlace, value)) RaisePropertyChanged(nameof(HasSelectedPlace)); } }
    public SavedFolder? SelectedFolder { get => _selectedFolder; set => SetProperty(ref _selectedFolder, value); }
    public RoutePoint? SelectedRoutePoint { get => _selectedRoutePoint; set => SetProperty(ref _selectedRoutePoint, value); }
    public SearchResult? SelectedSearchResult { get => _selectedSearchResult; set => SetProperty(ref _selectedSearchResult, value); }
    public string SearchQuery { get => _searchQuery; set => SetProperty(ref _searchQuery, value); }
    public string SavedPlacesSearchQuery { get => _savedPlacesSearchQuery; set => SetProperty(ref _savedPlacesSearchQuery, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string SelectionModeMessage { get => _selectionModeMessage; set => SetProperty(ref _selectionModeMessage, value); }
    public string RouteSummary { get => _routeSummary; set => SetProperty(ref _routeSummary, value); }
    public string ProfileSummary { get => _profileSummary; set => SetProperty(ref _profileSummary, value); }
    public bool IsProfilePaneExpanded { get => _isProfilePaneExpanded; set => SetProperty(ref _isProfilePaneExpanded, value); }
    public string ProfileLinePoints { get => _profileLinePoints; set => SetProperty(ref _profileLinePoints, value); }
    public string ProfileFillPoints { get => _profileFillPoints; set => SetProperty(ref _profileFillPoints, value); }
    public string ProfileLineGeometry { get => _profileLineGeometry; set => SetProperty(ref _profileLineGeometry, value); }
    public string ProfileFillGeometry { get => _profileFillGeometry; set => SetProperty(ref _profileFillGeometry, value); }
    public string ProfileLineGeometryGreen { get => _profileLineGeometryGreen; set => SetProperty(ref _profileLineGeometryGreen, value); }
    public string ProfileLineGeometryYellow { get => _profileLineGeometryYellow; set => SetProperty(ref _profileLineGeometryYellow, value); }
    public string ProfileLineGeometryOrange { get => _profileLineGeometryOrange; set => SetProperty(ref _profileLineGeometryOrange, value); }
    public string ProfileLineGeometryRed { get => _profileLineGeometryRed; set => SetProperty(ref _profileLineGeometryRed, value); }
    public string ProfileMaxLabel { get => _profileMaxLabel; set => SetProperty(ref _profileMaxLabel, value); }
    public string ProfileUpperMidLabel { get => _profileUpperMidLabel; set => SetProperty(ref _profileUpperMidLabel, value); }
    public string ProfileLowerMidLabel { get => _profileLowerMidLabel; set => SetProperty(ref _profileLowerMidLabel, value); }
    public string ProfileMinLabel { get => _profileMinLabel; set => SetProperty(ref _profileMinLabel, value); }
    public string ProfileStartDistanceLabel { get => _profileStartDistanceLabel; set => SetProperty(ref _profileStartDistanceLabel, value); }
    public string ProfileQuarterDistanceLabel { get => _profileQuarterDistanceLabel; set => SetProperty(ref _profileQuarterDistanceLabel, value); }
    public string ProfileMidDistanceLabel { get => _profileMidDistanceLabel; set => SetProperty(ref _profileMidDistanceLabel, value); }
    public string ProfileThreeQuarterDistanceLabel { get => _profileThreeQuarterDistanceLabel; set => SetProperty(ref _profileThreeQuarterDistanceLabel, value); }
    public string ProfileEndDistanceLabel { get => _profileEndDistanceLabel; set => SetProperty(ref _profileEndDistanceLabel, value); }
    public string ProfileDistanceValueText { get => _profileDistanceValueText; set => SetProperty(ref _profileDistanceValueText, value); }
    public string ProfileDistanceUnitText { get => _profileDistanceUnitText; set => SetProperty(ref _profileDistanceUnitText, value); }
    public string ProfileDistanceText { get => _profileDistanceText; set => SetProperty(ref _profileDistanceText, value); }
    public string ProfileDurationText { get => _profileDurationText; set => SetProperty(ref _profileDurationText, value); }
    public string ProfileHoverText { get => _profileHoverText; set => SetProperty(ref _profileHoverText, value); }
    public string ProfileElevationTotalText { get => _profileElevationTotalText; set => SetProperty(ref _profileElevationTotalText, value); }
    public string ProfileElevationDetailsText { get => _profileElevationDetailsText; set => SetProperty(ref _profileElevationDetailsText, value); }
    public string ProfileSlopeDetailsText { get => _profileSlopeDetailsText; set => SetProperty(ref _profileSlopeDetailsText, value); }
    public string ProfileElevationSummaryText { get => _profileElevationSummaryText; set => SetProperty(ref _profileElevationSummaryText, value); }
    public string ProfileGainText { get => _profileGainText; set => SetProperty(ref _profileGainText, value); }
    public string ProfileLossText { get => _profileLossText; set => SetProperty(ref _profileLossText, value); }
    public double ProfileChartCanvasWidth { get => _profileChartCanvasWidth; set => SetProperty(ref _profileChartCanvasWidth, value); }
    public double ProfileAxisWidth { get => _profileAxisWidth; set => SetProperty(ref _profileAxisWidth, value); }
    public string WeatherStatus { get => _weatherStatus; set => SetProperty(ref _weatherStatus, value); }
    public string WeatherLocationLabel { get => _weatherLocationLabel; set => SetProperty(ref _weatherLocationLabel, value); }
    public string WeatherCurrentLabel { get => _weatherCurrentLabel; set => SetProperty(ref _weatherCurrentLabel, value); }
    public string WeatherUpdatedLabel { get => _weatherUpdatedLabel; set => SetProperty(ref _weatherUpdatedLabel, value); }
    public string CivilAlertSummary { get => _civilAlertSummary; set => SetProperty(ref _civilAlertSummary, value); }
    public bool HasSelectedPlace => SelectedPlace is not null;
    public void UpdateIconOptions(string languageCode)
    {
        var names = languageCode == "en-US"
            ? new Dictionary<string, string>
            {
                ["bivouac"] = "Bivouac",
                ["fountain"] = "Fountain",
                ["food"] = "Food",
                ["paid_hut"] = "Paid hut",
                ["single_tent"] = "Single tent",
                ["tent_area"] = "Tent area",
                ["parking"] = "Parking",
                ["cable_car"] = "Cable car",
                ["hammocks"] = "Hammock area",
                ["waterfall"] = "Waterfall",
                ["poi"] = "POI",
                ["water"] = "River or lake",
                ["summit_cross"] = "Summit cross"
            }
            : new Dictionary<string, string>
            {
                ["bivouac"] = "Bivacco",
                ["fountain"] = "Fontana",
                ["food"] = "Ristoro",
                ["paid_hut"] = "Rifugio a pagamento",
                ["single_tent"] = "Spazio tenda singola",
                ["tent_area"] = "Spazio tende",
                ["parking"] = "Parcheggio",
                ["cable_car"] = "Funivia",
                ["hammocks"] = "Spazio amache",
                ["waterfall"] = "Cascate",
                ["poi"] = "POI",
                ["water"] = "Fiume o lago",
                ["summit_cross"] = "Croce di vetta"
            };
        AvailableIcons.Clear();
        foreach (var pair in names)
        {
            AvailableIcons.Add(new IconOption { Key = pair.Key, DisplayName = pair.Value });
        }
        var colorNames = languageCode == "en-US"
            ? new Dictionary<string, string>
            {
                ["#2E7D5B"] = "Green",
                ["#D65A5A"] = "Red",
                ["#3F6F8F"] = "Blue",
                ["#D9824B"] = "Orange",
                ["#8B6B4A"] = "Brown",
                ["#7657A8"] = "Purple",
                ["#C49A3A"] = "Yellow",
                ["#59636E"] = "Gray"
            }
            : new Dictionary<string, string>
            {
                ["#2E7D5B"] = "Verde",
                ["#D65A5A"] = "Rosso",
                ["#3F6F8F"] = "Blu",
                ["#D9824B"] = "Arancione",
                ["#8B6B4A"] = "Marrone",
                ["#7657A8"] = "Viola",
                ["#C49A3A"] = "Giallo",
                ["#59636E"] = "Grigio"
            };
        AvailablePinColors.Clear();
        foreach (var pair in colorNames)
        {
            AvailablePinColors.Add(new PinColorOption { Hex = pair.Key, DisplayName = pair.Value });
        }
    }
}
