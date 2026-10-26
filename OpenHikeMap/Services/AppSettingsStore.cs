using System.Text.Json;
using OpenHikeMap.Models;

namespace OpenHikeMap.Services;

public sealed record StoredApplicationData(AppSettings Settings, List<SavedFolder>? Folders = null, List<SavedPlace>? Places = null);

public sealed class AppSettingsStore : IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _lastSaved;
    public string FilePath { get; }

    public AppSettingsStore() : this(Path.Combine(
        Environment.GetEnvironmentVariable("OPENHIKEMAP_DATA_DIRECTORY") is { Length: > 0 } directory
            ? directory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenHikeMap"),
        "standalone-settings.json")) { }
    public AppSettingsStore(string filePath) => FilePath = Path.GetFullPath(filePath);

    public async Task<StoredApplicationData?> LoadAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            _lastSaved = null;
            if (!File.Exists(FilePath)) return null;
            await using var input = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var document = await JsonDocument.ParseAsync(input, cancellationToken: token);
            var root = document.RootElement;
            if (Has(root, "Settings") && Has(root, "Folders") && Has(root, "Places"))
            {
                var archive = root.Deserialize<BackupArchive>(Json) ?? throw new InvalidDataException("Invalid application data file.");
                var data = new WebSession { Settings = archive.Settings, Folders = archive.Folders, Places = archive.Places };
                data.Validate();
                _lastSaved = SerializeData(data);
                return new(data.Settings, data.Folders, data.Places);
            }
            // Migrate preferences-only files without replacing the existing browser library.
            if (!Has(root, "DefaultLatitude") || !Has(root, "DefaultLongitude")) throw new InvalidDataException("Invalid application data file.");
            var settings = root.Deserialize<AppSettings>(Json) ?? throw new InvalidDataException("Invalid settings file.");
            new WebSession { Settings = settings }.Validate();
            return new(settings);
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(WebSession data, CancellationToken token = default)
    {
        new WebSession { Settings = data.Settings, Folders = data.Folders, Places = data.Places }.Validate();
        var json = SerializeData(data);
        await _gate.WaitAsync(token);
        string? temporary = null;
        try
        {
            if (json == _lastSaved && File.Exists(FilePath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temporary, json, token);
            File.Move(temporary, FilePath, overwrite: true);
            _lastSaved = json;
        }
        finally
        {
            try { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); }
            finally { _gate.Release(); }
        }
    }

    private static string SerializeData(WebSession data)
    {
        var snapshot = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(data.Settings, Json), Json)!;
        // The weather reference belongs to the route/session, not application preferences.
        snapshot.WeatherReferenceLatitude = null; snapshot.WeatherReferenceLongitude = null;
        return JsonSerializer.Serialize(new BackupArchive { Settings = snapshot, Folders = data.Folders, Places = data.Places }, Json);
    }

    private static bool Has(JsonElement root, string name) => root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public void Dispose() => _gate.Dispose();
}
