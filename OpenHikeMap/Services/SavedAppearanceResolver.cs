using OpenHikeMap.Models;

namespace OpenHikeMap.Services;

public sealed record SavedAppearance(string IconKey, string PinColorHex);

public sealed class SavedAppearanceResolver(IEnumerable<SavedFolder> folders)
{
    private readonly Dictionary<Guid, SavedFolder> _folders = folders.ToDictionary(f => f.Id);

    public SavedAppearance Resolve(SavedPlace place)
    {
        var icon = place.IconKey;
        var color = place.PinColorHex;
        var parent = place.FolderId;
        var seen = new HashSet<Guid>();
        // Resolve each property independently, starting at the closest folder.
        while (parent is { } id && seen.Add(id) && _folders.TryGetValue(id, out var folder))
        {
            if (string.IsNullOrWhiteSpace(icon)) icon = folder.IconKey;
            if (!IsColor(color)) color = folder.PinColorHex;
            if (!string.IsNullOrWhiteSpace(icon) && IsColor(color)) break;
            parent = folder.ParentId;
        }
        return new(string.IsNullOrWhiteSpace(icon) ? "poi" : icon, IsColor(color) ? color : SavedPlace.DefaultPinColor);
    }

    public static bool IsColor(string? value) => value is { Length: 7 } && value[0] == '#' && value.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;

    public bool IsVisible(SavedPlace place)
    {
        if (!place.IsVisible) return false;
        var parent = place.FolderId;
        var seen = new HashSet<Guid>();
        while (parent is { } id && seen.Add(id) && _folders.TryGetValue(id, out var folder))
        {
            if (!folder.IsEnabled) return false;
            parent = folder.ParentId;
        }
        return true;
    }

    public static int ResetPinColors(Guid folderId, IEnumerable<SavedFolder> folders, IEnumerable<SavedPlace> places)
    {
        var children = folders.Where(f => f.ParentId.HasValue).ToLookup(f => f.ParentId!.Value);
        var contained = new HashSet<Guid>();
        var pending = new Queue<Guid>(); pending.Enqueue(folderId);
        while (pending.TryDequeue(out var id))
            if (contained.Add(id)) foreach (var folder in children[id]) pending.Enqueue(folder.Id);
        var changed = 0;
        foreach (var place in places.Where(p => p.FolderId is { } id && contained.Contains(id)))
        {
            if (string.IsNullOrEmpty(place.PinColorHex)) continue;
            place.PinColorHex = ""; changed++;
        }
        return changed;
    }
}
