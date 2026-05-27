using UnityEngine;

/// <summary>
/// Post-processor that creates a fixed boss arena layout.
/// Detects if the current room is intended for a boss and applies structural presets.
/// </summary>
public class BossRoomPreset : PostProcessor
{
    [Header("Boss Arena Assets")]
    public WFCTile3D platformTile;
    public WFCTile3D pillarTile;
    public bool forceInAllRooms = false; // For testing

    public override void Process(WFCGenerator3D generator)
    {
        // In a real scenario, we might check RoomProgressionManager or a flag in WFCGenerator3D.
        // Here we'll check if the HangarGenerator is set to Boss mode or if we force it.
        bool isBossRoom = forceInAllRooms;
        
        if (generator.macroGenerator is HangarGenerator hangarGen)
        {
            if (hangarGen.roomType == HangarGenerator.RoomType.Boss)
                isBossRoom = true;
        }

        if (!isBossRoom) return;

        int W = generator.GridWidth;
        int L = generator.GridLength;
        int H = generator.GridHeight;

        int centerX = W / 2;
        int centerZ = L / 2;

        // Apply a fixed "Diamond" platform preset
        if (platformTile != null)
        {
            for (int x = centerX - 2; x <= centerX + 2; x++)
            {
                for (int z = centerZ - 2; z <= centerZ + 2; z++)
                {
                    if (Mathf.Abs(x - centerX) + Mathf.Abs(z - centerZ) <= 2)
                    {
                        generator.SetTileAt(x, 0, z, platformTile);
                        // Ensure clear space above the platform
                        for (int y = 1; y < H; y++)
                            generator.SetTileAt(x, y, z, generator.airTile);
                    }
                }
            }
        }

        // Add 4 corner pillars for the arena
        if (pillarTile != null)
        {
            Vector2Int[] pillars = {
                new Vector2Int(centerX - 3, centerZ - 3),
                new Vector2Int(centerX + 3, centerZ - 3),
                new Vector2Int(centerX - 3, centerZ + 3),
                new Vector2Int(centerX + 3, centerZ + 3)
            };

            foreach (var p in pillars)
            {
                if (p.x >= 0 && p.x < W && p.y >= 0 && p.y < L)
                {
                    // Stack pillars
                    for (int y = 0; y < H - 1; y++)
                        generator.SetTileAt(p.x, y, p.y, pillarTile);
                }
            }
        }

        Debug.Log("[BossRoomPreset] Applied fixed diamond arena preset.");
    }
}

