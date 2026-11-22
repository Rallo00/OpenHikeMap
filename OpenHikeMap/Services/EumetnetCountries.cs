using OpenHikeMap.Models;

namespace OpenHikeMap.Services;

public sealed record EumetnetCountry(string Code, string Slug, EumetnetBounds Bounds);

public static class EumetnetCountries
{
    // Official participant bounds: https://visservice.meteoalarm.org/api/v1/regions
    // Feed slugs: https://feeds.meteoalarm.org/ (verified 2026-11-22).
    public static readonly EumetnetCountry[] All =
    [
        new("AD", "andorra", new(1.4064563392298404, 42.42867744878597, 1.7650907796726185, 42.649361704606605)),
        new("AT", "austria", new(9.530749, 46.372453, 17.160798, 49.020525)),
        new("BA", "bosnia-herzegovina", new(15.729759, 42.555705, 19.622706, 45.275162)),
        new("BE", "belgium", new(2.546011, 49.497238, 6.407419, 51.504838)),
        new("BG", "bulgaria", new(22.357497, 41.235808, 28.607464, 44.215663)),
        new("CH", "switzerland", new(5.956067, 45.818362, 10.492064, 47.807379)),
        new("CY", "cyprus", new(32.269781, 34.563688, 34.606092, 35.708724)),
        new("CZ", "czechia", new(12.092099, 48.551826, 18.858903, 51.054699)),
        new("DE", "germany", new(5.867697, 47.270114, 15.04116, 55.058165)),
        new("DK", "denmark", new(8.076003, 54.559969, 15.193085, 57.751658)),
        new("EE", "estonia", new(21.773235, 57.50931, 28.209592, 59.685074)),
        new("ES", "spain", new(-18.15997, 27.638479, 4.320511, 43.787928)),
        new("FI", "finland", new(19.311944, 59.765886, 31.586009, 70.092137)),
        new("FR", "france", new(-5.1416, 41.33375, 9.559316, 51.089382)),
        new("GR", "greece", new(19.373661, 34.802386, 29.645353, 41.748812)),
        new("HR", "croatia", new(13.490979, 42.390923, 19.446767, 46.55484)),
        new("HU", "hungary", new(16.113849, 45.737838, 22.896271, 48.584724)),
        new("IE", "ireland", new(-10.661639, 51.422593, -5.998206, 55.435125)),
        new("IL", "israel", new(34.268839, 29.493154, 35.68404, 33.28979)),
        new("IS", "iceland", new(-24.527669, 63.295854, -13.497289, 66.566155)),
        new("IT", "italy", new(6.627429, 35.493039, 18.519314, 47.091748)),
        new("LT", "lithuania", new(20.957153, 53.89702, 26.835133, 56.450151)),
        new("LU", "luxembourg", new(5.736842, 49.447838, 6.529847, 50.182779)),
        new("LV", "latvia", new(20.970778, 55.67513, 28.240576, 58.084425)),
        new("MD", "moldova", new(26.61752, 45.46728, 30.163139, 48.490689)),
        new("ME", "montenegro", new(18.434995, 41.844148, 20.352929, 43.556708)),
        new("MK", "republic-of-north-macedonia", new(20.453261, 40.854683, 23.034123, 42.373293)),
        new("MT", "malta", new(14.184754, 35.806862, 14.57432, 36.08218)),
        new("NL", "netherlands", new(3.359359, 50.750964, 7.225861, 53.555472)),
        new("NO", "norway", new(4.50341, 57.960365, 31.168041, 71.184162)),
        new("PL", "poland", new(14.122904, 49.002845, 24.145443, 54.835685)),
        new("PT", "portugal", new(-31.267909, 30.032172, -6.189352, 42.154419)),
        new("RO", "romania", new(20.264296, 43.619622, 29.71263, 48.264484)),
        new("RS", "serbia", new(18.848844, 41.858095, 23.00621, 46.189202)),
        new("SE", "sweden", new(10.966084, 55.337707, 24.166738, 69.059968)),
        new("SI", "slovenia", new(13.375493, 45.421929, 16.596805, 46.876295)),
        new("SK", "slovakia", new(16.833793, 47.731396, 22.56684, 49.613767)),
        new("UA", "ukraine", new(22.1553, 44.5026, 40.0937, 52.2996)),
        new("UK", "united-kingdom", new(-8.647188, 49.872925, 1.76258, 60.84522)),
    ];
}
