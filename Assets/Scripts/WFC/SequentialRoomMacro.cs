using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a single room layout for the Hermes Inc. procedural megastructure.
/// Each call creates one room to be played sequentially.
/// 
/// Flow: Combat rooms 1-5 (same theme) → Boss room → Shop → repeat
/// 
/// Layout:
/// - Spawn point: near entrance (x=1, z=1)
/// - Combat space: fills the middle
/// - Exit pit: far end (x=width-2, z=length-2) for one-way progression
/// </summary>
public class SequentialRoomMacro : MacroGenerator
{
    public enum RoomType
    {
        Combat,     // Standard combat encounter room
        Boss,       // Boss arena (preset layout)
        Shop        // Shop/rest area
    }

    [Header("Room Type")]
    public RoomType roomType = RoomType.Combat;

    [Header("Combat Room Settings")]
    public int minPlatformSize = 2;
    public int maxPlatformSize = 4;
    public int platformCount = 3;  // How many platform features to add

    [Header("Megastructure Quality")]
    [Range(0f, 1f)] public float minimumInfrastructureScore = 0.68f;
    public int generationRetries = 8;
    public int wallThickness = 2;
    public int mainHallWidth = 5;
    public int baySpacing = 7;
    public int bayDepth = 4;
    public int supportColumnSpacing = 6;
    public int exitPitRadius = 1;

    [Header("Difficulty Scaling")]
    [Range(1, 5)] public int roomNumber = 1;  // 1-5 = first tier, 6 = boss, 7 = shop, 8-12 = second tier, etc.

    public override MacroRegion[,] Generate(int width, int length, int seed)
    {
        int retries = Mathf.Max(1, generationRetries);
        for (int attempt = 0; attempt < retries; attempt++)
        {
            int attemptSeed = seed + (attempt * 7919);
            var rng = new System.Random(attemptSeed);
            var map = CreateWallFilledMap(width, length);

            Vector2Int spawn = new Vector2Int(2, 2);
            Vector2Int exit = new Vector2Int(width - 3, length - 3);

            switch (roomType)
            {
                case RoomType.Combat:
                    GenerateCombatRoom(map, width, length, rng, spawn, exit);
                    break;
                case RoomType.Boss:
                    GenerateBossRoom(map, width, length, spawn, exit);
                    break;
                case RoomType.Shop:
                    GenerateShopRoom(map, width, length, spawn, exit);
                    break;
            }

            float infrastructureScore = EvaluateInfrastructure(map, spawn, exit);
            if (infrastructureScore >= minimumInfrastructureScore)
            {
                Debug.Log($"[SequentialRoomMacro] Accepted {roomType} layout with score {infrastructureScore:0.00}.");
                return map;
            }

            if (attempt == retries - 1)
            {
                Debug.LogWarning($"[SequentialRoomMacro] Using fallback {roomType} layout with score {infrastructureScore:0.00}.");
                return map;
            }
        }

        return CreateWallFilledMap(width, length);
    }

    private MacroRegion[,] CreateWallFilledMap(int width, int length)
    {
        var map = new MacroRegion[width, length];
        for (int x = 0; x < width; x++)
            for (int z = 0; z < length; z++)
                map[x, z] = MacroRegion.Wall;
        return map;
    }

    private void GenerateCombatRoom(MacroRegion[,] map, int width, int length, System.Random rng, Vector2Int spawn, Vector2Int exit)
    {
        int left = wallThickness;
        int right = width - wallThickness - 1;
        int bottom = wallThickness;
        int top = length - wallThickness - 1;

        // The old version made too many internal wall bands, which turned the room into
        // a skyline. This version keeps a large open hangar volume and pushes structure
        // to the perimeter with only a few shallow side alcoves.
        CarveRect(map, left + 1, bottom + 1, right - 1, top - 1, MacroRegion.Open);

        // Perimeter ribs on the long edges only. These create a hangar feel without
        // breaking the center into lots of tiny blocks.
        int ribCount = Mathf.Clamp((right - left) / Mathf.Max(4, baySpacing), 2, 4);
        for (int i = 0; i < ribCount; i++)
        {
            int rx = Mathf.RoundToInt(Mathf.Lerp(left + 2, right - 2, (i + 1f) / (ribCount + 1f)));
            map[rx, bottom] = MacroRegion.Wall;
            map[rx, top] = MacroRegion.Wall;
        }

        // Very shallow side alcoves near the far ends only.
        int alcoveDepth = Mathf.Max(2, bayDepth - 1);
        int alcoveWidth = Mathf.Clamp(width / 5, 3, 6);
        int leftAlcoveX = left + 1;
        int rightAlcoveX = Mathf.Max(left + 1, right - alcoveWidth);

        CarveRect(map, leftAlcoveX, bottom + 1, leftAlcoveX + alcoveWidth, Mathf.Min(bottom + alcoveDepth, top - 1), MacroRegion.CombatRoom);
        CarveRect(map, rightAlcoveX, top - alcoveDepth, rightAlcoveX + alcoveWidth, top - 1, MacroRegion.CombatRoom);

        // --- NEW: Internal Infrastructure ---
        // Add a few internal support pillars or small "island" structures in the large open volume
        // to provide cover and break up the space.
        for (int x = left + 4; x < right - 4; x += supportColumnSpacing)
        {
            for (int z = bottom + 4; z < top - 4; z += supportColumnSpacing)
            {
                // Jitter the column slightly or skip some to avoid a perfect grid
                double roll = rng.NextDouble();
                if (roll < 0.2)
                    map[x, z] = MacroRegion.Platform;
                else if (roll < 0.5)
                    map[x, z] = MacroRegion.Wall;
            }
        }

        // Add edge bridges occasionally
        if (rng.NextDouble() < 0.3)
        {
            int side = rng.Next(4);
            if (side == 0) CarveRect(map, left + 1, bottom + 1, left + 2, top - 1, MacroRegion.Bridge);
            else if (side == 1) CarveRect(map, right - 2, bottom + 1, right - 1, top - 1, MacroRegion.Bridge);
        }

        // Small anchor blocks near the entry only so the spawn area still has a readable
        // threshold, but avoid placing a chain of supports through the room.
        CarveRect(map, 1, 1, Mathf.Min(4, right - 1), Mathf.Min(4, top - 1), MacroRegion.Open);
        CarveExitPit(map, exit.x, exit.y, exitPitRadius);

        map[spawn.x, spawn.y] = MacroRegion.Spawn;
    }

    private void GenerateBossRoom(MacroRegion[,] map, int width, int length, Vector2Int spawn, Vector2Int exit)
    {
        int left = wallThickness;
        int right = width - wallThickness - 1;
        int bottom = wallThickness;
        int top = length - wallThickness - 1;
        int centerX = width / 2;
        int centerZ = length / 2;

        CarveRect(map, left, bottom, right, top, MacroRegion.Open);

        int ringInset = Mathf.Max(2, wallThickness + 1);
        for (int x = left + ringInset; x <= right - ringInset; x++)
        {
            map[x, centerZ - 2] = MacroRegion.Wall;
            map[x, centerZ + 2] = MacroRegion.Wall;
        }

        for (int z = bottom + ringInset; z <= top - ringInset; z++)
        {
            map[centerX - 2, z] = MacroRegion.Wall;
            map[centerX + 2, z] = MacroRegion.Wall;
        }

        CarveRect(map, centerX - 1, centerZ - 1, centerX + 1, centerZ + 1, MacroRegion.BossRoom);
        CarveRect(map, 1, 1, Mathf.Min(4, right - 1), Mathf.Min(4, top - 1), MacroRegion.Open);
        map[spawn.x, spawn.y] = MacroRegion.Spawn;
        CarveExitPit(map, exit.x, exit.y, exitPitRadius);
    }

    private void GenerateShopRoom(MacroRegion[,] map, int width, int length, Vector2Int spawn, Vector2Int exit)
    {
        int left = wallThickness;
        int right = width - wallThickness - 1;
        int bottom = wallThickness;
        int top = length - wallThickness - 1;
        int centerX = width / 2;

        CarveRect(map, left, bottom, right, top, MacroRegion.Shop);
        for (int z = bottom; z <= top; z++)
        {
            if (Mathf.Abs(z - (length / 2)) <= 1)
                continue;
            map[centerX, z] = MacroRegion.Wall;
        }

        CarveRect(map, 1, 1, Mathf.Min(4, right - 1), Mathf.Min(4, top - 1), MacroRegion.Open);
        map[spawn.x, spawn.y] = MacroRegion.Spawn;
        CarveExitPit(map, exit.x, exit.y, exitPitRadius);
    }

    private void CarveRect(MacroRegion[,] map, int xMin, int zMin, int xMax, int zMax, MacroRegion region)
    {
        int width = map.GetLength(0);
        int length = map.GetLength(1);
        for (int x = Mathf.Clamp(xMin, 0, width - 1); x <= Mathf.Clamp(xMax, 0, width - 1); x++)
        {
            for (int z = Mathf.Clamp(zMin, 0, length - 1); z <= Mathf.Clamp(zMax, 0, length - 1); z++)
            {
                if (x == 0 || z == 0 || x == width - 1 || z == length - 1) continue;
                map[x, z] = region;
            }
        }
    }

    private void CarveExitPit(MacroRegion[,] map, int centerX, int centerZ, int radius)
    {
        int width = map.GetLength(0);
        int length = map.GetLength(1);
        for (int x = centerX - radius; x <= centerX + radius; x++)
        {
            for (int z = centerZ - radius; z <= centerZ + radius; z++)
            {
                if (x <= 0 || z <= 0 || x >= width - 1 || z >= length - 1) continue;
                map[x, z] = MacroRegion.ExitPit;
            }
        }
    }

    private float EvaluateInfrastructure(MacroRegion[,] map, Vector2Int spawn, Vector2Int exit)
    {
        bool hasPath = HasPassablePath(map, spawn, exit);
        int wallCount = 0;
        int structuralCount = 0;
        int total = map.GetLength(0) * map.GetLength(1);

        for (int x = 0; x < map.GetLength(0); x++)
        {
            for (int z = 0; z < map.GetLength(1); z++)
            {
                if (map[x, z] == MacroRegion.Wall) wallCount++;
                if (IsStructuralCell(map, x, z)) structuralCount++;
            }
        }

        float wallRatio = wallCount / (float)total;
        float wallScore = 1f - Mathf.Clamp01(Mathf.Abs(wallRatio - 0.43f) / 0.43f);
        float structureScore = Mathf.Clamp01(structuralCount / Mathf.Max(1f, total * 0.10f));
        float pathScore = hasPath ? 1f : 0f;

        return (pathScore * 0.55f) + (wallScore * 0.25f) + (structureScore * 0.20f);
    }

    private bool HasPassablePath(MacroRegion[,] map, Vector2Int start, Vector2Int goal)
    {
        int width = map.GetLength(0);
        int length = map.GetLength(1);
        var visited = new bool[width, length];
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        visited[start.x, start.y] = true;

        int[] dx = { 1, -1, 0, 0 };
        int[] dz = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == goal) return true;

            for (int i = 0; i < 4; i++)
            {
                int nx = current.x + dx[i];
                int nz = current.y + dz[i];
                if (nx < 0 || nz < 0 || nx >= width || nz >= length) continue;
                if (visited[nx, nz]) continue;
                if (map[nx, nz] == MacroRegion.Wall) continue;
                visited[nx, nz] = true;
                queue.Enqueue(new Vector2Int(nx, nz));
            }
        }

        return false;
    }

    private bool IsStructuralCell(MacroRegion[,] map, int x, int z)
    {
        if (map[x, z] != MacroRegion.Wall) return false;

        int width = map.GetLength(0);
        int length = map.GetLength(1);
        int openNeighbors = 0;

        if (x > 0 && map[x - 1, z] != MacroRegion.Wall) openNeighbors++;
        if (x < width - 1 && map[x + 1, z] != MacroRegion.Wall) openNeighbors++;
        if (z > 0 && map[x, z - 1] != MacroRegion.Wall) openNeighbors++;
        if (z < length - 1 && map[x, z + 1] != MacroRegion.Wall) openNeighbors++;

        return openNeighbors >= 2;
    }
}
