namespace OpenHikeMap.Models;

public sealed partial class LocalizationService
{
    private static void ApplyTransferTranslations()
    {
        string[] keys = ["ToolbarExport", "ToolbarImport", "ToolbarSettings", "ToolbarAbout", "TransferToolbarLabel", "ExportTitle", "ImportTitle", "ExportIntro", "ImportIntro", "ExportRouteAction", "ExportGpxAction", "ExportBackupAction", "ImportFileLabel", "ImportRouteConfirm", "ImportBackupConfirm", "ImportStorageConfirm", "ImportUnsupported", "ImportEmptyRoute", "ExportDone", "ImportDone", "StorageLocalLabel", "CloseWindow"];
        var translations = new Dictionary<string, string[]>
        {
            ["it-IT"] = ["Esporta", "Importa", "Impostazioni", "Informazioni", "File e impostazioni", "Esporta", "Importa",
                "Salva la traccia in OHK o GPX. Il backup completo include impostazioni ed elementi salvati, ma non la traccia.",
                "Carica una traccia GPX, una traccia OpenHikeMap (.ohk) o un backup completo (.json).",
                "Traccia creata (.ohk)", "Tracciato GPX (.gpx)", "Backup completo (.json)", "Scegli un file GPX, OHK o backup JSON",
                "Sostituire la traccia attuale? Impostazioni ed elementi salvati rimarranno invariati.",
                "Ripristinare impostazioni ed elementi salvati dal backup? La traccia attuale non verrà modificata.",
                "Sostituire anche i dati locali precedenti non leggibili?", "File non valido. Sono supportati tracciati GPX, tracce OHK e backup completi OpenHikeMap.",
                "Il file non contiene punti di traccia.", "File esportato", "Importazione completata", "Dati salvati sul computer", "Chiudi"],
            ["en-US"] = ["Export", "Import", "Settings", "About", "Files and settings", "Export", "Import",
                "Save the route as OHK or GPX. A full backup includes settings and saved items, but not the current route.",
                "Load a GPX track, an OpenHikeMap route (.ohk), or a full backup (.json).",
                "Created route (.ohk)", "GPX track (.gpx)", "Full backup (.json)", "Choose a GPX, OHK or JSON backup file",
                "Replace the current route? Settings and saved items will remain unchanged.",
                "Restore settings and saved items from the backup? The current route will remain unchanged.",
                "Also replace the previous unreadable local data?", "Invalid file. Supported formats are GPX tracks, OHK routes and full OpenHikeMap backups.",
                "The file contains no route points.", "File exported", "Import completed", "Data saved on this computer", "Close"],
            ["fr-FR"] = ["Exporter", "Importer", "Paramètres", "À propos", "Fichiers et paramètres", "Exporter", "Importer",
                "Enregistrez le parcours au format OHK ou GPX. La sauvegarde complète inclut les paramètres et les éléments enregistrés, mais pas le parcours.",
                "Chargez une trace GPX, un parcours OpenHikeMap (.ohk) ou une sauvegarde complète (.json).",
                "Parcours créé (.ohk)", "Trace GPX (.gpx)", "Sauvegarde complète (.json)", "Choisir un fichier GPX, OHK ou une sauvegarde JSON",
                "Remplacer le parcours actuel ? Les paramètres et les éléments enregistrés resteront inchangés.",
                "Restaurer les paramètres et les éléments enregistrés depuis la sauvegarde ? Le parcours actuel restera inchangé.",
                "Remplacer aussi les anciennes données locales illisibles ?", "Fichier non valide. Formats acceptés : traces GPX, parcours OHK et sauvegardes complètes OpenHikeMap.",
                "Le fichier ne contient aucun point de parcours.", "Fichier exporté", "Importation terminée", "Données enregistrées sur cet ordinateur", "Fermer"],
            ["es-ES"] = ["Exportar", "Importar", "Configuración", "Acerca de", "Archivos y configuración", "Exportar", "Importar",
                "Guarda la ruta en OHK o GPX. La copia completa incluye la configuración y los elementos guardados, pero no la ruta.",
                "Carga un recorrido GPX, una ruta OpenHikeMap (.ohk) o una copia de seguridad completa (.json).",
                "Ruta creada (.ohk)", "Recorrido GPX (.gpx)", "Copia de seguridad completa (.json)", "Elegir un archivo GPX, OHK o una copia JSON",
                "¿Reemplazar la ruta actual? La configuración y los elementos guardados no cambiarán.",
                "¿Restaurar la configuración y los elementos guardados desde la copia? La ruta actual no cambiará.",
                "¿Reemplazar también los datos locales anteriores que no se pueden leer?", "Archivo no válido. Se admiten recorridos GPX, rutas OHK y copias completas de OpenHikeMap.",
                "El archivo no contiene puntos de ruta.", "Archivo exportado", "Importación completada", "Datos guardados en este equipo", "Cerrar"],
            ["de-DE"] = ["Exportieren", "Importieren", "Einstellungen", "Info", "Dateien und Einstellungen", "Exportieren", "Importieren",
                "Route als OHK oder GPX speichern. Die vollständige Sicherung enthält Einstellungen und gespeicherte Elemente, aber nicht die Route.",
                "Einen GPX-Track, eine OpenHikeMap-Route (.ohk) oder eine vollständige Sicherung (.json) laden.",
                "Erstellte Route (.ohk)", "GPX-Track (.gpx)", "Vollständige Sicherung (.json)", "GPX-, OHK- oder JSON-Sicherungsdatei auswählen",
                "Aktuelle Route ersetzen? Einstellungen und gespeicherte Elemente bleiben unverändert.",
                "Einstellungen und gespeicherte Elemente aus der Sicherung wiederherstellen? Die aktuelle Route bleibt unverändert.",
                "Auch die bisherigen unlesbaren lokalen Daten ersetzen?", "Ungültige Datei. Unterstützt werden GPX-Tracks, OHK-Routen und vollständige OpenHikeMap-Sicherungen.",
                "Die Datei enthält keine Routenpunkte.", "Datei exportiert", "Import abgeschlossen", "Daten auf diesem Computer gespeichert", "Schließen"],
            ["nl-NL"] = ["Exporteren", "Importeren", "Instellingen", "Over", "Bestanden en instellingen", "Exporteren", "Importeren",
                "Sla de route op als OHK of GPX. Een volledige back-up bevat instellingen en opgeslagen elementen, maar niet de route.",
                "Laad een GPX-track, een OpenHikeMap-route (.ohk) of een volledige back-up (.json).",
                "Gemaakte route (.ohk)", "GPX-track (.gpx)", "Volledige back-up (.json)", "Kies een GPX-, OHK- of JSON-back-upbestand",
                "De huidige route vervangen? Instellingen en opgeslagen elementen blijven ongewijzigd.",
                "Instellingen en opgeslagen elementen uit de back-up herstellen? De huidige route blijft ongewijzigd.",
                "Ook de eerdere onleesbare lokale gegevens vervangen?", "Ongeldig bestand. Ondersteund worden GPX-tracks, OHK-routes en volledige OpenHikeMap-back-ups.",
                "Het bestand bevat geen routepunten.", "Bestand geëxporteerd", "Import voltooid", "Gegevens opgeslagen op deze computer", "Sluiten"],
            ["nb-NO"] = ["Eksporter", "Importer", "Innstillinger", "Om", "Filer og innstillinger", "Eksporter", "Importer",
                "Lagre ruten som OHK eller GPX. En full sikkerhetskopi inneholder innstillinger og lagrede elementer, men ikke ruten.",
                "Last inn et GPX-spor, en OpenHikeMap-rute (.ohk) eller en full sikkerhetskopi (.json).",
                "Opprettet rute (.ohk)", "GPX-spor (.gpx)", "Full sikkerhetskopi (.json)", "Velg en GPX-, OHK- eller JSON-sikkerhetskopifil",
                "Erstatte den gjeldende ruten? Innstillinger og lagrede elementer forblir uendret.",
                "Gjenopprette innstillinger og lagrede elementer fra sikkerhetskopien? Den gjeldende ruten forblir uendret.",
                "Også erstatte de tidligere uleselige lokale dataene?", "Ugyldig fil. GPX-spor, OHK-ruter og fulle OpenHikeMap-sikkerhetskopier støttes.",
                "Filen inneholder ingen rutepunkter.", "Fil eksportert", "Import fullført", "Data lagret på denne datamaskinen", "Lukk"],
            ["da-DK"] = ["Eksportér", "Importér", "Indstillinger", "Om", "Filer og indstillinger", "Eksportér", "Importér",
                "Gem ruten som OHK eller GPX. En fuld sikkerhedskopi indeholder indstillinger og gemte elementer, men ikke ruten.",
                "Indlæs et GPX-spor, en OpenHikeMap-rute (.ohk) eller en fuld sikkerhedskopi (.json).",
                "Oprettet rute (.ohk)", "GPX-spor (.gpx)", "Fuld sikkerhedskopi (.json)", "Vælg en GPX-, OHK- eller JSON-sikkerhedskopifil",
                "Erstat den aktuelle rute? Indstillinger og gemte elementer forbliver uændrede.",
                "Gendan indstillinger og gemte elementer fra sikkerhedskopien? Den aktuelle rute forbliver uændret.",
                "Erstat også de tidligere ulæselige lokale data?", "Ugyldig fil. GPX-spor, OHK-ruter og fulde OpenHikeMap-sikkerhedskopier understøttes.",
                "Filen indeholder ingen rutepunkter.", "Fil eksporteret", "Import fuldført", "Data gemt på denne computer", "Luk"],
            ["sv-SE"] = ["Exportera", "Importera", "Inställningar", "Om", "Filer och inställningar", "Exportera", "Importera",
                "Spara rutten som OHK eller GPX. En fullständig säkerhetskopia innehåller inställningar och sparade objekt, men inte rutten.",
                "Läs in ett GPX-spår, en OpenHikeMap-rutt (.ohk) eller en fullständig säkerhetskopia (.json).",
                "Skapad rutt (.ohk)", "GPX-spår (.gpx)", "Fullständig säkerhetskopia (.json)", "Välj en GPX-, OHK- eller JSON-säkerhetskopiefil",
                "Ersätt den aktuella rutten? Inställningar och sparade objekt förblir oförändrade.",
                "Återställ inställningar och sparade objekt från säkerhetskopian? Den aktuella rutten förblir oförändrad.",
                "Ersätt även tidigare oläsbara lokala data?", "Ogiltig fil. GPX-spår, OHK-rutter och fullständiga OpenHikeMap-säkerhetskopior stöds.",
                "Filen innehåller inga ruttpunkter.", "Fil exporterad", "Import slutförd", "Data sparade på den här datorn", "Stäng"],
            ["fi-FI"] = ["Vie", "Tuo", "Asetukset", "Tietoja", "Tiedostot ja asetukset", "Vie", "Tuo",
                "Tallenna reitti OHK- tai GPX-muodossa. Täydellinen varmuuskopio sisältää asetukset ja tallennetut kohteet, mutta ei reittiä.",
                "Lataa GPX-jälki, OpenHikeMap-reitti (.ohk) tai täydellinen varmuuskopio (.json).",
                "Luotu reitti (.ohk)", "GPX-jälki (.gpx)", "Täydellinen varmuuskopio (.json)", "Valitse GPX-, OHK- tai JSON-varmuuskopiotiedosto",
                "Korvataanko nykyinen reitti? Asetukset ja tallennetut kohteet säilyvät ennallaan.",
                "Palautetaanko asetukset ja tallennetut kohteet varmuuskopiosta? Nykyinen reitti säilyy ennallaan.",
                "Korvataanko myös aiemmat lukukelvottomat paikalliset tiedot?", "Virheellinen tiedosto. Tuettuja ovat GPX-jäljet, OHK-reitit ja täydelliset OpenHikeMap-varmuuskopiot.",
                "Tiedosto ei sisällä reittipisteitä.", "Tiedosto viety", "Tuonti valmis", "Tiedot tallennettu tälle tietokoneelle", "Sulje"]
        };
        foreach (var (language, values) in translations)
        {
            if (values.Length != keys.Length) throw new InvalidOperationException("Incomplete transfer translations: " + language);
            ApplyTranslations(language, keys.Select((key, index) => new KeyValuePair<string, string>(key, values[index])).ToDictionary());
        }
        foreach (var row in new[]
        {
            new[] { "it-IT", "Caricamento mappa…", "Impossibile inizializzare la mappa.", "Riprova" },
            new[] { "en-US", "Loading map…", "Cannot initialize the map.", "Retry" },
            new[] { "fr-FR", "Chargement de la carte…", "Impossible d’initialiser la carte.", "Réessayer" },
            new[] { "es-ES", "Cargando mapa…", "No se puede inicializar el mapa.", "Reintentar" },
            new[] { "de-DE", "Karte wird geladen…", "Die Karte kann nicht initialisiert werden.", "Erneut versuchen" },
            new[] { "nl-NL", "Kaart laden…", "Kan de kaart niet initialiseren.", "Opnieuw proberen" },
            new[] { "nb-NO", "Laster kart…", "Kan ikke initialisere kartet.", "Prøv igjen" },
            new[] { "da-DK", "Indlæser kort…", "Kan ikke initialisere kortet.", "Prøv igen" },
            new[] { "sv-SE", "Läser in kartan…", "Kan inte initiera kartan.", "Försök igen" },
            new[] { "fi-FI", "Ladataan karttaa…", "Karttaa ei voi alustaa.", "Yritä uudelleen" }
        }) ApplyTranslations(row[0], new() { ["MapLoading"] = row[1], ["MapStartupFailed"] = row[2], ["MapRetry"] = row[3] });
    }
}
