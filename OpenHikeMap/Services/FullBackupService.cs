using System.Text.Json;
using System.Text.Json.Serialization;
using OpenHikeMap.Models;

namespace OpenHikeMap.Services;

public sealed class FullBackupDocument
{
    [JsonRequired]
    public string Format { get; set; } = "OpenHikeMap.FullBackup";
    [JsonRequired]
    public int Version { get; set; } = 1;
    [JsonRequired]
    public WebSession? Data { get; set; }
}

public static class FullBackupService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public static string Serialize(WebSession data)
    {
        data.Validate();
        return JsonSerializer.Serialize(new FullBackupDocument { Data = data }, Json);
    }
    public static WebSession Parse(Stream input)
    {
        using var document = JsonDocument.Parse(input);
        return Parse(document.RootElement);
    }
    internal static WebSession Parse(JsonElement root)
    {
        var backup = root.Deserialize<FullBackupDocument>(Json);
        var data = root.ValueKind == JsonValueKind.Object
            ? root.EnumerateObject().FirstOrDefault(p => p.Name.Equals("Data", StringComparison.OrdinalIgnoreCase)).Value
            : default;
        if (backup is not { Format: "OpenHikeMap.FullBackup", Version: 1, Data: not null } ||
            data.ValueKind != JsonValueKind.Object ||
            new[] { "Settings", "Folders", "Places", "RoutePoints", "RouteGeometry", "RouteKind" }
                .Any(key => !data.EnumerateObject().Any(property => property.Name.Equals(key, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidDataException("Invalid full backup.");
        backup.Data.Validate();
        return backup.Data;
    }
}
