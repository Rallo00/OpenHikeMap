namespace OpenHikeMap.Models;

public sealed partial class LocalizationService
{
    private static void ApplyWebcamTranslations()
    {
        foreach (var row in new[]
        {
            new[] { "it-IT", "Mostra webcam nell’area", "Ricerca webcam…", "Webcam non disponibili. Sposta la mappa o riprova.", "Ingrandisci per visualizzare tutte le webcam dell’area.", "Nessuna webcam nell’area visualizzata." },
            new[] { "en-US", "Show webcams in this area", "Finding webcams…", "Webcams unavailable. Move the map or try again.", "Zoom in to see all webcams in this area.", "No webcams in the visible area." },
            new[] { "fr-FR", "Afficher les webcams de la zone", "Recherche des webcams…", "Webcams indisponibles. Déplacez la carte ou réessayez.", "Zoomez pour voir toutes les webcams de la zone.", "Aucune webcam dans la zone visible." },
            new[] { "es-ES", "Mostrar webcams de la zona", "Buscando webcams…", "Webcams no disponibles. Mueve el mapa o inténtalo de nuevo.", "Acerca el mapa para ver todas las webcams de la zona.", "No hay webcams en la zona visible." },
            new[] { "de-DE", "Webcams in diesem Gebiet anzeigen", "Webcams werden gesucht…", "Webcams nicht verfügbar. Verschieben Sie die Karte oder versuchen Sie es erneut.", "Vergrößern Sie die Karte, um alle Webcams im Gebiet zu sehen.", "Keine Webcams im sichtbaren Gebiet." },
            new[] { "nl-NL", "Webcams in dit gebied tonen", "Webcams zoeken…", "Webcams niet beschikbaar. Verplaats de kaart of probeer het opnieuw.", "Zoom in om alle webcams in dit gebied te zien.", "Geen webcams in het zichtbare gebied." },
            new[] { "nb-NO", "Vis webkameraer i området", "Søker etter webkameraer…", "Webkameraer er utilgjengelige. Flytt kartet eller prøv igjen.", "Zoom inn for å se alle webkameraene i området.", "Ingen webkameraer i det synlige området." },
            new[] { "da-DK", "Vis webkameraer i området", "Søger efter webkameraer…", "Webkameraer er ikke tilgængelige. Flyt kortet eller prøv igen.", "Zoom ind for at se alle webkameraer i området.", "Ingen webkameraer i det synlige område." },
            new[] { "sv-SE", "Visa webbkameror i området", "Söker efter webbkameror…", "Webbkameror är inte tillgängliga. Flytta kartan eller försök igen.", "Zooma in för att se alla webbkameror i området.", "Inga webbkameror i det synliga området." },
            new[] { "fi-FI", "Näytä alueen verkkokamerat", "Etsitään verkkokameroita…", "Verkkokamerat eivät ole käytettävissä. Siirrä karttaa tai yritä uudelleen.", "Lähennä karttaa nähdäksesi kaikki alueen verkkokamerat.", "Näkyvällä alueella ei ole verkkokameroita." }
        }) ApplyTranslations(row[0], new() { ["ToolbarWebcams"] = row[1], ["WebcamsLoading"] = row[2], ["WebcamsUnavailable"] = row[3], ["WebcamsZoomIn"] = row[4], ["WebcamsEmpty"] = row[5] });
    }
}
