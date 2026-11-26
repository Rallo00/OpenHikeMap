using Microsoft.JSInterop;

namespace OpenHikeMap.Components.Pages;

public partial class Home
{
    // A session view, intentionally never persisted: every application launch starts in 2D.
    private bool Terrain3D, TerrainSwitching;
    private async Task ToggleTerrain3D()
    {
        if (!Ready || Module is null || TerrainSwitching) return;
        TerrainSwitching = true;
        var previousStatus = Status;
        try
        {
            if (!Terrain3D) Status = L["TerrainLoading"];
            await InvokeAsync(StateHasChanged);
            Terrain3D = await Module.InvokeAsync<bool>("set3D", !Terrain3D);
            Status = previousStatus;
        }
        catch (Exception ex) when (ex is JSException or TaskCanceledException)
        { Terrain3D = false; Status = L["TerrainUnavailable"]; Logger.LogWarning("3D terrain could not be opened: {Reason}", ex.Message); }
        finally { TerrainSwitching = false; }
    }
}
