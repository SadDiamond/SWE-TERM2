using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a single room layout for the Hermes Inc. procedural megastructure.
/// Each call creates one room to be played sequentially.
/// 
/// Flow: Combat rooms 1-5 (same theme) → Shop room → Boss room → next theme
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
    [Range(0f, 1f)] public float pitChance = 0.18f;
    [Range(0f, 1f)] public float hazardChance = 0.10f;
    [Range(0f, 1f)] public float microDetailChance = 0.20f;

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
    [Range(1, 5)] public int roomNumber = 1;  // 1-5 = combat tier, 6 = shop, 7 = boss, 8-12 = next tier, etc.

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
        int centerX = width / 2;
        int centerZ = length / 2;

        // Cybergrind-like read: a clean combat floor inside a hard border, with the
        // interesting geometry coming from raised bridges, side platforms and gaps.
        CarveRect(map, left, bottom, right, top, MacroRegion.Open);

        int platformRadius = Mathf.Clamp((minPlatformSize + maxPlatformSize) / 2, 2, 4);
        CarveRect(map, centerX - platformRadius, centerZ - platformRadius, centerX + platformRadius, centerZ + platformRadius, MacroRegion.Platform);

        int bridgeHalfWidth = Mathf.Clamp(mainHallWidth / 2, 1, 3);
        CarveRect(map, centerX - bridgeHalfWidth, bottom + 1, centerX + bridgeHalfWidth, top - 1, MacroRegion.Bridge);
        CarveRect(map, left + 1, centerZ - bridgeHalfWidth, right - 1, centerZ + bridgeHalfWidth, MacroRegion.Bridge);

        int cornerSize = Mathf.Clamp(platformCount + 1, 3, 5);
        CarveRect(map, left + 1, bottom + 1, left + cornerSize, bottom + cornerSize, MacroRegion.Platform);
        CarveRect(map, right - cornerSize, bottom + 1, right - 1, bottom + cornerSize, MacroRegion.Platform);
        CarveRect(map, left + 1, top - cornerSize, left + cornerSize, top - 1, MacroRegion.Platform);
        CarveRect(map, right - cornerSize, top - cornerSize, right - 1, top - 1, MacroRegion.Platform);

        StampArenaDetails(map, width, length, rng, left, right, bottom, top, centerX, centerZ, spawn, exit);
        StampPerimeterInfrastructure(map, rng, left, right, bottom, top);
        ReserveMicroSlots(map, rng, left, right, bottom, top);

        // Keep progression cells and their local approach lanes readable.
        CarveRect(map, spawn.x - 1, spawn.y - 1, spawn.x + 2, spawn.y + 2, MacroRegion.Open);
        CarveRect(map, exit.x - 2, exit.y - 2, exit.x + 1, exit.y + 1, MacroRegion.Open);
        map[spawn.x, spawn.y] = MacroRegion.Spawn;
        CarveExitPit(map, exit.x, exit.y, exitPitRadius);
    }

    private void StampArenaDetails(
        MacroRegion[,] map,
        int width,
        int length,
        System.Random rng,
        int left,
        int right,
        int bottom,
        int top,
        int centerX,
        int centerZ,
        Vector2Int spawn,
        Vector2Int exit)
    {
        int avoidRadius = Mathf.Max(3, mainHallWidth);
        for (int x = left + 2; x <= right - 2; x++)
        {
            for (int z = bottom + 2; z <= top - 2; z++)
            {
                if (IsNear(x, z, spawn, 4) || IsNear(x, z, exit, 4)) continue;
                if (Mathf.Abs(x - centerX) <= avoidRadius || Mathf.Abs(z - centerZ) <= avoidRadius) continue;
                if (((x + z) & 1) == 1) continue;

                double roll = rng.NextDouble();
                if (roll < pitChance)
                    map[x, z] = MacroRegion.Pit;
                else if (roll < pitChance + hazardChance)
                    map[x, z] = MacroRegion.Hazard;
            }
        }

        int ringInset = Mathf.Max(3, wallThickness + 2);
        for (int x = left + ringInset; x <= right - ringInset; x += Mathf.Max(4, supportColumnSpacing))
        {
            StampCoverPair(map, x, centerZ - avoidRadius - 1, centerZ + avoidRadius + 1, bottom, top, rng);
        }
    }

    private void StampCoverPair(MacroRegion[,] map, int x, int zA, int zB, int bottom, int top, System.Random rng)
    {
        if (zA > bottom && zA < top)
            map[x, zA] = rng.NextDouble() < 0.55 ? MacroRegion.LowCover : MacroRegion.HighCover;
        if (zB > bottom && zB < top)
            map[x, zB] = rng.NextDouble() < 0.55 ? MacroRegion.LowCover : MacroRegion.HighCover;
    }

    private void StampPerimeterInfrastructure(MacroRegion[,] map, System.Random rng, int left, int right, int bottom, int top)
    {
        int step = Mathf.Max(4, baySpacing);
        for (int x = left + 2; x <= right - 2; x += step)
        {
            map[x, bottom] = MacroRegion.Wall;
            map[x, top] = MacroRegion.Wall;
            if (rng.NextDouble() < 0.5)
            {
                map[x, bottom + 1] = MacroRegion.MicroDetail;
                map[x, top - 1] = MacroRegion.MicroDetail;
            }
        }

        for (int z = bottom + 2; z <= top - 2; z += step)
        {
            map[left, z] = MacroRegion.Wall;
            map[right, z] = MacroRegion.Wall;
            if (rng.NextDouble() < 0.5)
            {
                map[left + 1, z] = MacroRegion.MicroCrate;
                map[right - 1, z] = MacroRegion.MicroCrate;
            }
        }
    }

    private void ReserveMicroSlots(MacroRegion[,] map, System.Random rng, int left, int right, int bottom, int top)
    {
        for (int x = left + 2; x <= right - 2; x++)
        {
            for (int z = bottom + 2; z <= top - 2; z++)
            {
                if (map[x, z] != MacroRegion.Open) continue;
                if (rng.NextDouble() > microDetailChance) continue;

                bool edgeBiased = x - left <= 3 || right - x <= 3 || z - bottom <= 3 || top - z <= 3;
                if (!edgeBiased && rng.NextDouble() > 0.25) continue;

                map[x, z] = rng.NextDouble() < 0.55 ? MacroRegion.MicroDetail : MacroRegion.MicroCrate;
            }
        }
    }

    private bool IsNear(int x, int z, Vector2Int point, int radius)
    {
        return Mathf.Abs(x - point.x) + Mathf.Abs(z - point.y) <= radius;
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
