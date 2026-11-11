namespace OpenHikeMap.Models;

public sealed partial class LocalizationService
{
    private static void ApplyMapGridTranslations()
    {
        foreach (var row in new[]
        {
            new[] { "it-IT", "Reticolo chilometrico UTM", "N,E,S,O", "Passo", "Ingrandisci per visualizzare il reticolo UTM" },
            new[] { "en-US", "UTM metric grid", "N,E,S,W", "Spacing", "Zoom in to show the UTM grid" },
            new[] { "fr-FR", "Quadrillage kilométrique UTM", "N,E,S,O", "Maille", "Zoomez pour afficher le quadrillage UTM" },
            new[] { "es-ES", "Cuadrícula kilométrica UTM", "N,E,S,O", "Paso", "Acerca el mapa para ver la cuadrícula UTM" },
            new[] { "de-DE", "UTM-Kilometergitter", "N,O,S,W", "Rasterweite", "Vergrößern Sie die Karte, um das UTM-Gitter anzuzeigen" },
            new[] { "nl-NL", "UTM-kilometerraster", "N,O,Z,W", "Rasterafstand", "Zoom in om het UTM-raster te tonen" },
            new[] { "nb-NO", "UTM-kilometerrutenett", "N,Ø,S,V", "Ruteavstand", "Zoom inn for å vise UTM-rutenettet" },
            new[] { "da-DK", "UTM-kilometernet", "N,Ø,S,V", "Maskevidde", "Zoom ind for at vise UTM-nettet" },
            new[] { "sv-SE", "UTM-kilometernät", "N,Ö,S,V", "Rutstorlek", "Zooma in för att visa UTM-nätet" },
            new[] { "fi-FI", "UTM-kilometriruudukko", "P,I,E,L", "Ruudukon väli", "Lähennä karttaa nähdäksesi UTM-ruudukon" }
        }) ApplyTranslations(row[0], new() { ["ToolbarCoordinateGrid"] = row[1], ["MapGridCardinals"] = row[2], ["MapGridStep"] = row[3], ["MapGridZoomIn"] = row[4], ["MapGridUnavailable"] = "UTM: 80°S–84°N" });
    }
}
