using System.Linq;
using UnityEngine;

/// <summary>
/// Post-processor that runs after WFC collapse to refine the hangar.
/// Enforces interior air, floor role restrictions, and adds sparse perimeter variation.
/// </summary>
public class MicroPopulator : PostProcessor
{
    [Header("Hangar Micro Settings")]
    [Range(0f, 1f)] public float floorVariantProbability = 0.05f;
    [Range(0f, 0.5f)] public float centerClearRadius = 0.35f;
    public bool protectExitPit = true;

    public override void Process(WFCGenerator3D generator)
    {
        if (generator == null) return;

        int W = generator.GridWidth;
        int H = generator.GridHeight;
        int L = generator.GridLength;

        var spawn = generator.FindSpawnCell();
        var exit = generator.FindExitPitCell();

        // Gather valid floor tiles for variations
        var authored = generator.availableTiles ?? new WFCTile3D[0];
        var floorLike = authored.Where(t => t != null && t.macroRole == WFCTile3D.MacroTileRole.Floor).ToArray();

        for (int x = 0; x < W; x++)
        {
            for (int z = 0; z < L; z++)
            {
                WFCTile3D floorTile = generator.GetTileAt(x, 0, z);

                // 1. Force air above interiors (Y >= 1)
                // Use the macro blueprint to decide what should be air.
                bool shouldBeOpen = false;
                if (generator.CurrentBlueprint != null)
                {
                    var region = generator.CurrentBlueprint[x, z];
                    if (region == MacroRegion.Open || region == MacroRegion.CombatRoom || 
                        region == MacroRegion.BossRoom || region == MacroRegion.Shop || 
                        region == MacroRegion.Spawn || region == MacroRegion.Goal || region == MacroRegion.ExitPit)
                    {
                        shouldBeOpen = true;
                    }
                }
                else
                {
                    // Fallback to role-based check if no blueprint
                    if (floorTile != null && (floorTile.macroRole == WFCTile3D.MacroTileRole.Floor || floorTile.macroRole == WFCTile3D.MacroTileRole.Marker))
                        shouldBeOpen = true;
                }

                if (shouldBeOpen)
                {
                    for (int y = 1; y < H; y++)
                    {
                        generator.SetTileAt(x, y, z, generator.airTile);
                    }
                }

                // 2. Restrict floor-layer role (Disallow Wall/Structural)
                if (floorTile != null && (floorTile.macroRole == WFCTile3D.MacroTileRole.Wall || floorTile.macroRole == WFCTile3D.MacroTileRole.Structural))
                {
                    // If WFC accidentally placed a wall on the floor, swap it for a base floor tile.
                    if (generator.baseFloorTile != null)
                    {
                        generator.SetTileAt(x, 0, z, generator.baseFloorTile);
                        floorTile = generator.baseFloorTile;
                    }
                }

                // 3. Protect ExitPit from clutter
                if (protectExitPit && exit.HasValue && exit.Value.x == x && exit.Value.z == z)
                {
                    for (int y = 1; y < H; y++)
                        generator.SetTileAt(x, y, z, generator.airTile);
                    continue; // No decorations in the pit
                }

                // 4. Sparse perimeter variation (Keep center clear)
                float nx = W > 1 ? x / (float)(W - 1) : 0.5f;
                float nz = L > 1 ? z / (float)(L - 1) : 0.5f;
                float distFromCenter = Mathf.Max(Mathf.Abs(nx - 0.5f), Mathf.Abs(nz - 0.5f));

                if (distFromCenter < centerClearRadius) continue; // Keep center clear

                // Apply variation to floor tiles only
                if (floorTile != null && floorTile.macroRole == WFCTile3D.MacroTileRole.Floor)
                {
                    if (floorLike.Length > 0 && Random.value < floorVariantProbability)
                    {
                        var pick = floorLike[Random.Range(0, floorLike.Length)];
                        generator.SetTileAt(x, 0, z, pick);
                    }
                }
            }
        }

        Debug.Log("[MicroPopulator] Refined hangar post-collapse.");
    }
}

