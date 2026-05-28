using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Produces large hangar-style combat rooms with open central volumes,
/// perimeter ribs, and shallow side alcoves.
/// </summary>
public class HangarGenerator : MacroGenerator
{
    public enum RoomType { Combat, Boss, Shop }

    [Header("Generation Settings")]
    public RoomType roomType = RoomType.Combat;
    public int generationRetries = 10;
    [Range(0f, 1f)] public float minAcceptableScore = 0.4f;

    [Header("Hangar Layout Parameters")]
    public int wallThickness = 5;
    public int ribSpacing = 6;
    public int alcoveDepth = 3;
    public int exitPitRadius = 1;

    [Header("Infrastructure Goals")]
    public float targetWallRatio = 0.43f;
    public float targetStructuralCoverage = 0.10f;

    [Header("Macro Infrastructure")]
    [Range(0f, 1f)] public float platformChance = 0.2f;
    [Range(0f, 1f)] public float mezzanineChance = 0.35f;
    [Range(0f, 1f)] public float pillarDensity = 0.05f;
    [Range(0f, 1f)] public float pitDensity = 0.03f;
    [Min(0)] public int minimumReservedMicroCells = 20;

    [Header("Terrain Settings")]
    public float terrainFrequency = 0.12f;
    [Range(0f, 1f)] public float hillThreshold = 0.72f;
    [Range(0f, 1f)] public float bumpThreshold = 0.58f;

    public override MacroRegion[,] Generate(int width, int length, int seed)
    {
        for (int i = 0; i < generationRetries; i++)
        {
            int attemptSeed = seed + (i * 12345);
            var rng = new System.Random(attemptSeed);
            var map = InitializeMap(width, length);

            GenerateHangar(map, width, length, rng);
            GenerateTerrain(map, width, length, attemptSeed);
            GenerateInteriorInfrastructure(map, width, length, rng);
            GenerateMicroReserves(map, width, length, rng);
            EnsureMinimumMicroReserves(map, width, length, rng);

            float score = EvaluateInfrastructure(map);
            if (score >= minAcceptableScore)
            {
                Debug.Log($"[HangarGenerator] Accepted {roomType} layout (Attempt {i+1}) with score {score:F2}");
                return map;
            }
        }

        Debug.LogWarning("[HangarGenerator] All retries failed to meet infrastructure score. Using fallback.");
        var fallbackMap = InitializeMap(width, length);
        GenerateHangar(fallbackMap, width, length, new System.Random(seed));
        GenerateTerrain(fallbackMap, width, length, seed);
        GenerateInteriorInfrastructure(fallbackMap, width, length, new System.Random(seed));
        EnsureMinimumMicroReserves(fallbackMap, width, length, new System.Random(seed ^ 0x51ED));
        return fallbackMap;
    }

    private void GenerateTerrain(MacroRegion[,] map, int width, int length, int seed)
    {
        float offsetX = (seed % 1000) * 1.3f;
        float offsetZ = (seed / 1000) * 1.7f;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                if (map[x, z] != MacroRegion.Open) continue;

                float noise = Mathf.PerlinNoise(x * terrainFrequency + offsetX, z * terrainFrequency + offsetZ);
                if (noise > hillThreshold)
                {
                    map[x, z] = MacroRegion.Hill;
                }
                else if (noise > bumpThreshold)
                {
                    map[x, z] = MacroRegion.Terrain;
                }
            }
        }
    }

    private void GenerateMicroReserves(MacroRegion[,] map, int width, int length, System.Random rng)
    {
        // Reserve spots for micro-generation (staircases, walkways) near structures or in clumps
        // This creates intentional connection pathways between platforms/bridges
        for (int x = 4; x < width - 4; x++)
        {
            for (int z = 4; z < length - 4; z++)
            {
                if (map[x, z] == MacroRegion.Open || map[x, z] == MacroRegion.Terrain || map[x, z] == MacroRegion.Hill || map[x, z] == MacroRegion.Platform)
                {
                    float reserveChance = 0.12f;  // Increased from 5%
                    if (map[x, z] == MacroRegion.Terrain || map[x, z] == MacroRegion.Hill) reserveChance = 0.35f;  // Increased from 18%
                    else if (map[x, z] == MacroRegion.Platform) reserveChance = 0.28f;  // Increased from 12%

                    if (rng.NextDouble() < reserveChance)
                    {
                        if (CountWallNeighbors(map, x, z) > 0 || map[x, z] == MacroRegion.Hill || map[x, z] == MacroRegion.Platform)
                        {
                            map[x, z] = MacroRegion.MicroDetail;
                        }
                        else if (rng.NextDouble() < 0.2)
                        {
                            map[x, z] = MacroRegion.MicroCrate;
                        }
                    }
                }
            }
        }
    }

    private void EnsureMinimumMicroReserves(MacroRegion[,] map, int width, int length, System.Random rng)
    {
        int target = Mathf.Max(0, minimumReservedMicroCells);
        if (target == 0) return;

        int current = CountReservedMicroCells(map);
        if (current >= target) return;

        var candidates = new List<(Vector2Int pos, int score, double tieBreaker)>();
        for (int x = 4; x < width - 4; x++)
        {
            for (int z = 4; z < length - 4; z++)
            {
                if (map[x, z] != MacroRegion.Open && map[x, z] != MacroRegion.Terrain && map[x, z] != MacroRegion.Hill && map[x, z] != MacroRegion.Platform)
                    continue;

                int score = 0;
                if (map[x, z] == MacroRegion.Terrain || map[x, z] == MacroRegion.Hill) score += 3;
                else if (map[x, z] == MacroRegion.Platform) score += 2;

                score += Mathf.Clamp(CountWallNeighbors(map, x, z), 0, 8);

                if (IsNearInfrastructure(map, x, z)) score += 2;

                candidates.Add((new Vector2Int(x, z), score, rng.NextDouble()));
            }
        }

        candidates.Sort((a, b) =>
        {
            int cmp = b.score.CompareTo(a.score);
            return cmp != 0 ? cmp : a.tieBreaker.CompareTo(b.tieBreaker);
        });

        int needed = target - current;
        for (int i = 0; i < candidates.Count && needed > 0; i++)
        {
            Vector2Int c = candidates[i].pos;
            if (map[c.x, c.y] == MacroRegion.Hill || map[c.x, c.y] == MacroRegion.Platform || CountWallNeighbors(map, c.x, c.y) > 0)
                map[c.x, c.y] = MacroRegion.MicroDetail;
            else
                map[c.x, c.y] = MacroRegion.MicroCrate;

            needed--;
        }
    }

    private int CountReservedMicroCells(MacroRegion[,] map)
    {
        int count = 0;
        for (int x = 0; x < map.GetLength(0); x++)
            for (int z = 0; z < map.GetLength(1); z++)
                if (map[x, z] == MacroRegion.MicroDetail || map[x, z] == MacroRegion.MicroCrate)
                    count++;
        return count;
    }

    private bool IsNearInfrastructure(MacroRegion[,] map, int x, int z)
    {
        int width = map.GetLength(0);
        int length = map.GetLength(1);
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dz = -2; dz <= 2; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                int nx = x + dx;
                int nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= width || nz >= length) continue;
                var r = map[nx, nz];
                if (r == MacroRegion.Wall || r == MacroRegion.Platform || r == MacroRegion.Bridge || r == MacroRegion.Terrain || r == MacroRegion.Hill)
                    return true;
            }
        }

        return false;
    }

    private void GenerateHangar(MacroRegion[,] map, int width, int length, System.Random rng)
    {
        // 1. Initialize with Walls (the "Shell")
        for (int x = 0; x < width; x++)
            for (int z = 0; z < length; z++)
                map[x, z] = MacroRegion.Wall;

        // 2. Carve a HUGE open central volume (The Arena)
        // Keep only a 1-tile thick outer wall by default
        int padding = 1;
        CarveRect(map, padding, padding, width - 1 - padding, length - 1 - padding, MacroRegion.Open, forceCarve: true);

        // 3. Markers
        // Entrance and Exit are at opposite ends
        map[2, 2] = MacroRegion.Spawn;
        
        int ex = width - 3;
        int ez = length - 3;
        for (int x = ex - exitPitRadius; x <= ex + exitPitRadius; x++)
            for (int z = ez - exitPitRadius; z <= ez + exitPitRadius; z++)
                map[x, z] = MacroRegion.ExitPit;
    }

    private void GenerateInteriorInfrastructure(MacroRegion[,] map, int width, int length, System.Random rng)
    {
        int xMin = 3;
        int xMax = width - 4;
        int zMin = 3;
        int zMax = length - 4;

        // 1. Strategic Pillar Anchors - these guide micro generation (staircases, bridges, supports)
        // Create anchor points at strategic intervals that micro structures can build around
        int pillarCount = rng.Next(6, 10);
        var pillarAnchors = new List<Vector2Int>();
        
        for (int i = 0; i < pillarCount; i++)
        {
            int rx = rng.Next(xMin + 2, xMax - 2);
            int rz = rng.Next(zMin + 2, zMax - 2);
            
            if (Vector2.Distance(new Vector2(rx, rz), new Vector2(2, 2)) < 4) continue;
            if (Vector2.Distance(new Vector2(rx, rz), new Vector2(width - 3, length - 3)) < 4) continue;
            
            // Check if too close to other pillars
            bool tooClose = false;
            foreach (var anchor in pillarAnchors)
                if (Vector2.Distance(new Vector2(rx, rz), new Vector2(anchor.x, anchor.y)) < 5)
                    tooClose = true;
            if (tooClose) continue;

            // Place pillar (Wall region to mark anchor point)
            map[rx, rz] = MacroRegion.Wall;
            pillarAnchors.Add(new Vector2Int(rx, rz));
        }

        // 2. Bridge Paths - Connect elevated platforms with bridge regions for micro to build upon
        // Paint bridge tiles between platforms to guide structure placement
        for (int i = 0; i < pillarAnchors.Count - 1; i++)
        {
            Vector2Int p1 = pillarAnchors[i];
            Vector2Int p2 = pillarAnchors[i + 1];
            
            // Simple line between pillars - mark as Bridge region
            int dx = p2.x - p1.x;
            int dz = p2.y - p1.y;
            int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));
            
            if (steps > 0)
            {
                for (int t = 0; t <= steps; t++)
                {
                    float lerp = steps > 0 ? (float)t / steps : 0f;
                    int x = (int)(p1.x + dx * lerp);
                    int z = (int)(p1.y + dz * lerp);
                    
                    if (x >= xMin && x <= xMax && z >= zMin && z <= zMax && map[x, z] == MacroRegion.Open)
                        map[x, z] = MacroRegion.Bridge;
                }
            }
        }

        // 3. Structural Islands / Vertical terrain - Platforms, Hills, Terrain
        int islandCount = rng.Next(7, 12);
        for (int i = 0; i < islandCount; i++)
        {
            int rx = rng.Next(xMin, xMax);
            int rz = rng.Next(zMin, zMax);
            
            if (Vector2.Distance(new Vector2(rx, rz), new Vector2(2, 2)) < 5) continue;
            if (Vector2.Distance(new Vector2(rx, rz), new Vector2(width - 3, length - 3)) < 5) continue;

            double roll = rng.NextDouble();
            if (roll < 0.30) // Raised Platform
            {
                int size = rng.Next(2, 4);
                CarveRect(map, rx - size, rz - size, rx + size, rz + size, MacroRegion.Platform);
            }
            else if (roll < 0.55) // Hill / stepped rise
            {
                int size = rng.Next(1, 3);
                CarveRect(map, rx - size, rz - size, rx + size, rz + size, MacroRegion.Hill);
            }
            else if (roll < 0.75) // Terrain mound
            {
                int size = rng.Next(1, 3);
                CarveRect(map, rx - size, rz - size, rx + size, rz + size, MacroRegion.Terrain);
            }
        }

        // 4. Scattered Cover and Hazards, biased toward the outer shell for readability
        for (int x = xMin; x <= xMax; x++)
        {
            for (int z = zMin; z <= zMax; z++)
            {
                if (map[x, z] != MacroRegion.Open) continue;

                double roll = rng.NextDouble();
                float edgeFactor = Mathf.Min(
                    Mathf.Min((x - xMin) / (float)Mathf.Max(1, xMax - xMin), (xMax - x) / (float)Mathf.Max(1, xMax - xMin)),
                    Mathf.Min((z - zMin) / (float)Mathf.Max(1, zMax - zMin), (zMax - z) / (float)Mathf.Max(1, zMax - zMin)));

                if (roll < 0.02 + (0.03 * (1f - edgeFactor))) map[x, z] = MacroRegion.Pit;
                else if (roll < 0.05 + (0.04 * (1f - edgeFactor))) map[x, z] = MacroRegion.HighCover;
                else if (roll < 0.09 + (0.05 * (1f - edgeFactor))) map[x, z] = MacroRegion.LowCover;
                else if (roll < 0.11 + (0.05 * (1f - edgeFactor))) map[x, z] = MacroRegion.Hazard;
            }
        }
    }

    private void CarveCircle(MacroRegion[,] map, int cx, int cz, int radius, MacroRegion region)
    {
        int width = map.GetLength(0);
        int length = map.GetLength(1);
        for (int x = cx - radius; x <= cx + radius; x++)
        {
            for (int z = cz - radius; z <= cz + radius; z++)
            {
                if (x < 3 || z < 3 || x >= width - 3 || z >= length - 3) continue;
                if (Vector2.Distance(new Vector2(cx, cz), new Vector2(x, z)) <= radius)
                {
                    // Only overwrite Open space to avoid breaking paths/markers
                    if (map[x, z] == MacroRegion.Open)
                        map[x, z] = region;
                }
            }
        }
    }

    private int CountWallNeighbors(MacroRegion[,] map, int x, int z)
    {
        int count = 0;
        int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
        for (int i = 0; i < 8; i++)
        {
            int nx = x + dx[i];
            int nz = z + dz[i];
            if (nx >= 0 && nx < map.GetLength(0) && nz >= 0 && nz < map.GetLength(1))
            {
                if (map[nx, nz] == MacroRegion.Wall || map[nx, nz] == MacroRegion.Platform)
                    count++;
            }
        }
        return count;
    }

    private MacroRegion[,] InitializeMap(int width, int length)
    {
        var map = new MacroRegion[width, length];
        for (int x = 0; x < width; x++)
            for (int z = 0; z < length; z++)
                map[x, z] = MacroRegion.Wall;
        return map;
    }

    private float EvaluateInfrastructure(MacroRegion[,] map)
    {
        int W = map.GetLength(0);
        int L = map.GetLength(1);
        int total = W * L;
        int wallCount = 0;
        int structuralCount = 0;

        Vector2Int actualSpawn = new Vector2Int(-1, -1);
        Vector2Int actualExit = new Vector2Int(-1, -1);

        for (int x = 0; x < W; x++)
        {
            for (int z = 0; z < L; z++)
            {
                // Platforms and Walls count towards structural coverage
                if (map[x, z] == MacroRegion.Wall || map[x, z] == MacroRegion.Platform)
                {
                    wallCount++;
                    if (IsStructural(map, x, z)) structuralCount++;
                }
                if (map[x, z] == MacroRegion.Spawn) actualSpawn = new Vector2Int(x, z);
                if (map[x, z] == MacroRegion.ExitPit && actualExit.x == -1) actualExit = new Vector2Int(x, z);
            }
        }

        float wallRatio = wallCount / (float)total;
        float structuralCoverage = structuralCount / (float)total;
        
        bool reachable = false;
        if (actualSpawn.x != -1 && actualExit.x != -1)
            reachable = IsReachable(map, actualSpawn, actualExit);

        float pathScore = reachable ? 1f : 0f;
        float wallScore = 1f - Mathf.Clamp01(Mathf.Abs(wallRatio - targetWallRatio) / targetWallRatio);
        float structuralScore = Mathf.Clamp01(structuralCoverage / targetStructuralCoverage);

        return (pathScore * 0.5f) + (wallScore * 0.25f) + (structuralScore * 0.25f);
    }

    private bool IsStructural(MacroRegion[,] map, int x, int z)
    {
        int openNeighbors = 0;
        int[] dx = { 1, -1, 0, 0 };
        int[] dz = { 0, 0, 1, -1 };
        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx[i];
            int nz = z + dz[i];
            if (nx >= 0 && nx < map.GetLength(0) && nz >= 0 && nz < map.GetLength(1))
            {
                MacroRegion r = map[nx, nz];
                // Non-blockers
                if (r != MacroRegion.Wall && r != MacroRegion.Platform && r != MacroRegion.Hill) openNeighbors++;
            }
        }
        return openNeighbors >= 2;
    }

    private bool IsReachable(MacroRegion[,] map, Vector2Int start, Vector2Int goal)
    {
        int W = map.GetLength(0);
        int L = map.GetLength(1);
        var visited = new bool[W, L];
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        visited[start.x, start.y] = true;

        int[] dx = { 1, -1, 0, 0 };
        int[] dz = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            if (curr == goal) return true;

            for (int i = 0; i < 4; i++)
            {
                int nx = curr.x + dx[i];
                int nz = curr.y + dz[i];
                if (nx >= 0 && nx < W && nz >= 0 && nz < L && !visited[nx, nz])
                {
                    MacroRegion r = map[nx, nz];
                    // Walkable regions (including elevated ones as they provide floors)
                    if (r != MacroRegion.Wall && r != MacroRegion.Platform && r != MacroRegion.Pit && r != MacroRegion.Hill)
                    {
                        visited[nx, nz] = true;
                        queue.Enqueue(new Vector2Int(nx, nz));
                    }
                }
            }
        }
        return false;
    }

    private void CarveRect(MacroRegion[,] map, int xMin, int zMin, int xMax, int zMax, MacroRegion region, bool forceCarve = false)
    {
        for (int x = Mathf.Max(0, xMin); x <= Mathf.Min(xMax, map.GetLength(0) - 1); x++)
        {
            for (int z = Mathf.Max(0, zMin); z <= Mathf.Min(zMax, map.GetLength(1) - 1); z++)
            {
                // If forceCarve, overwrite everything (used for initial arena carving)
                // Otherwise, only overwrite Open cells (preserve Bridge, Wall, Spawn, ExitPit, etc.)
                if (forceCarve || map[x, z] == MacroRegion.Open)
                    map[x, z] = region;
            }
        }
    }
}
