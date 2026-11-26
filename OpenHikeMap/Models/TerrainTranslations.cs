namespace OpenHikeMap.Models;

public sealed partial class LocalizationService
{
    private static void ApplyTerrainTranslations()
    {
        string[] keys = ["ToolbarTerrain3D", "TerrainLoading", "TerrainUnavailable", "TerrainDataUnavailable", "TerrainTitle", "TerrainZoomIn", "TerrainZoomOut", "TerrainResetBearing"];
        foreach (var row in new[]
        {
            new[] { "it-IT", "Vista terreno 3D", "Caricamento del terreno 3D…", "Vista 3D non disponibile. Puoi continuare a usare la mappa 2D.", "Dati del terreno non disponibili. Verifica la connessione o torna al 2D.", "Mappa del terreno 3D", "Ingrandisci", "Riduci", "Ripristina il nord" },
            new[] { "en-US", "3D terrain view", "Loading 3D terrain…", "3D view unavailable. You can continue using the 2D map.", "Terrain data unavailable. Check your connection or return to 2D.", "3D terrain map", "Zoom in", "Zoom out", "Reset bearing to north" },
            new[] { "fr-FR", "Vue du relief en 3D", "Chargement du relief 3D…", "Vue 3D indisponible. Vous pouvez continuer à utiliser la carte 2D.", "Données du relief indisponibles. Vérifiez la connexion ou revenez en 2D.", "Carte du relief en 3D", "Zoomer", "Dézoomer", "Rétablir le nord" },
            new[] { "es-ES", "Vista del terreno en 3D", "Cargando terreno 3D…", "Vista 3D no disponible. Puedes seguir usando el mapa 2D.", "Datos del terreno no disponibles. Comprueba la conexión o vuelve a 2D.", "Mapa del terreno en 3D", "Acercar", "Alejar", "Restablecer el norte" },
            new[] { "de-DE", "3D-Geländeansicht", "3D-Gelände wird geladen…", "3D-Ansicht nicht verfügbar. Sie können die 2D-Karte weiter verwenden.", "Geländedaten nicht verfügbar. Prüfen Sie die Verbindung oder wechseln Sie zu 2D.", "3D-Geländekarte", "Vergrößern", "Verkleinern", "Norden wiederherstellen" },
            new[] { "nl-NL", "3D-terreinweergave", "3D-terrein laden…", "3D-weergave niet beschikbaar. U kunt de 2D-kaart blijven gebruiken.", "Terreingegevens niet beschikbaar. Controleer de verbinding of keer terug naar 2D.", "3D-terreinkaart", "Inzoomen", "Uitzoomen", "Noorden herstellen" },
            new[] { "nb-NO", "3D-terrengvisning", "Laster 3D-terreng…", "3D-visning er utilgjengelig. Du kan fortsette å bruke 2D-kartet.", "Terrengdata er utilgjengelige. Kontroller forbindelsen eller gå tilbake til 2D.", "3D-terrengkart", "Zoom inn", "Zoom ut", "Tilbakestill til nord" },
            new[] { "da-DK", "3D-terrænvisning", "Indlæser 3D-terræn…", "3D-visning er ikke tilgængelig. Du kan fortsætte med at bruge 2D-kortet.", "Terrændata er ikke tilgængelige. Kontrollér forbindelsen eller gå tilbage til 2D.", "3D-terrænkort", "Zoom ind", "Zoom ud", "Nulstil til nord" },
            new[] { "sv-SE", "3D-terrängvy", "Läser in 3D-terräng…", "3D-vyn är inte tillgänglig. Du kan fortsätta använda 2D-kartan.", "Terrängdata är inte tillgängliga. Kontrollera anslutningen eller återgå till 2D.", "3D-terrängkarta", "Zooma in", "Zooma ut", "Återställ norr" },
            new[] { "fi-FI", "3D-maastonäkymä", "Ladataan 3D-maastoa…", "3D-näkymä ei ole käytettävissä. Voit jatkaa 2D-kartan käyttöä.", "Maastotiedot eivät ole käytettävissä. Tarkista yhteys tai palaa 2D-näkymään.", "3D-maastokartta", "Lähennä", "Loitonna", "Palauta pohjoinen" }
        }) ApplyTranslations(row[0], keys.Select((key, index) => new KeyValuePair<string, string>(key, row[index + 1])).ToDictionary());
        foreach (var row in new[]
        {
            new[] { "it-IT", "Attiva rilievo", "Disattiva rilievo", "Mostra attribuzioni" },
            new[] { "en-US", "Enable terrain", "Disable terrain", "Show attributions" },
            new[] { "fr-FR", "Activer le relief", "Désactiver le relief", "Afficher les attributions" },
            new[] { "es-ES", "Activar relieve", "Desactivar relieve", "Mostrar atribuciones" },
            new[] { "de-DE", "Gelände aktivieren", "Gelände deaktivieren", "Quellenangaben anzeigen" },
            new[] { "nl-NL", "Terrein inschakelen", "Terrein uitschakelen", "Bronvermeldingen tonen" },
            new[] { "nb-NO", "Aktiver terreng", "Deaktiver terreng", "Vis kildehenvisninger" },
            new[] { "da-DK", "Aktivér terræn", "Deaktivér terræn", "Vis kildeangivelser" },
            new[] { "sv-SE", "Aktivera terräng", "Inaktivera terräng", "Visa källangivelser" },
            new[] { "fi-FI", "Ota maasto käyttöön", "Poista maasto käytöstä", "Näytä lähdemerkinnät" }
        }) ApplyTranslations(row[0], new() { ["TerrainEnable"] = row[1], ["TerrainDisable"] = row[2], ["TerrainAttribution"] = row[3] });
    }
}
