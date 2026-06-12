using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CybergrindArenaGenerator : MonoBehaviour
{
    private enum CellKind
    {
        Void,
        Floor,
        Bridge,
        Platform,
        UpperPlatform,
        Hazard,
        CoverLow,
        CoverHigh,
        Spawn,
        Exit
    }

    [Header("Arena")]
    [Min(9)] public int width = 25;
    [Min(9)] public int length = 25;
    public float tileSize = 4f;
    public float floorThickness = 0.16f;
    public float pillarDepth = 42f;
    public float killPlaneY = -24f;
    public int seed = 0;
    public bool randomizeSeedEachGeneration = true;
    public int lastGeneratedSeed;
    public bool generateOnStart = true;
    public bool clearBeforeGenerate = true;
    public ArenaMode arenaMode = ArenaMode.Combat;
    [Min(0)] public int themeIndex = 0;
    public bool useThemePaletteVariants = true;

    public enum ArenaMode
    {
        Combat,
        Shop,
        Boss
    }

    public string GetThemeLabel()
    {
        return GetThemeLabel(themeIndex);
    }

    public static string GetThemeLabel(int index)
    {
        switch (Math.Abs(index) % 4)
        {
            case 0: return "Null Sector";
            case 1: return "Blue Wake";
            case 2: return "Ember Vault";
            default: return "Verdant Static";
        }
    }

    public string GetThemeDirectiveTitle()
    {
        return GetThemeDirectiveTitle(themeIndex);
    }

    public static string GetThemeDirectiveTitle(int index)
    {
        return ResolveThemeProfile(index).directiveTitle;
    }

    public string GetThemeDirectiveDetail()
    {
        return GetThemeDirectiveDetail(themeIndex);
    }

    public static string GetThemeDirectiveDetail(int index)
    {
        return ResolveThemeProfile(index).directiveDetail;
    }

    [Header("Floating Layout")]
    [Range(1, 4)] public int bridgeLevel = 1;
    [Range(2, 5)] public int platformLevel = 2;
    [Range(3, 6)] public int crownLevel = 3;
    public float levelHeight = 5.4f;
    [Range(1, 3)] public int mainBridgeHalfWidth = 1;
    [Range(2, 6)] public int centralPlatformRadius = 4;
    [Range(2, 5)] public int cornerPlatformSize = 4;

    [Header("Playability")]
    [Range(0f, 0.25f)] public float outerGapChance = 0.08f;
    [Range(0f, 0.20f)] public float hazardChance = 0.05f;
    [Range(0f, 0.20f)] public float coverChance = 0.08f;
    [Range(0f, 0.20f)] public float itemChance = 0.08f;
    [Min(0)] public int safeRadiusAroundSpawn = 4;
    [Min(0)] public int safeRadiusAroundExit = 4;

    [Header("References")]
    public Transform playerToPlace;
    public float playerSpawnHeight = 3.1f;
    public string generatedRootName = "_Arena";

    [Header("Enemy Spawning")]
    public GameObject enemyPrefab;
    [Min(0)] public int combatEnemyMin = 4;
    [Min(0)] public int combatEnemyMax = 8;
    [Min(0)] public int bossEnemyMin = 10;
    [Min(0)] public int bossEnemyMax = 14;
    [Min(0)] public int minEnemyDistanceFromSpawn = 7;
    public bool spawnBossChampion = true;

    [Header("Materials")]
    public Material floorMaterial;
    public Material darkMaterial;
    public Material accentMaterial;
    public Material hazardMaterial;
    public Material spawnMaterial;
    public Material exitMaterial;
    public Material itemMaterial;
    public Material puzzleMaterial;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<Vector3> recoveryPoints = new List<Vector3>();
    private readonly Dictionary<long, List<Vector3>> traversalConnectors = new Dictionary<long, List<Vector3>>();

    public Transform CurrentArenaRoot { get; private set; }
    private CellKind[,] lastCells;
    private Vector2Int lastSpawnCell;
    private Vector2Int lastExitCell;
    private Volume environmentVolume;
    private Material skyboxMaterial;
    [NonSerialized] public bool skipPlayerPlacementOnce;

    private struct ThemePalette
    {
        public Color floor;
        public Color dark;
        public Color accent;
        public Color accentEmission;
        public Color hazard;
        public Color hazardEmission;
        public Color spawn;
        public Color spawnEmission;
        public Color exit;
        public Color exitEmission;
        public Color item;
        public Color itemEmission;
        public Color puzzle;
        public Color puzzleEmission;
    }

    private struct ThemeProfile
    {
        public string directiveTitle;
        public string directiveDetail;
        public float outerGapMultiplier;
        public float hazardMultiplier;
        public float coverMultiplier;
        public float itemMultiplier;
        public int extraIslands;
        public int extraJumpPads;
        public int extraPylons;
        public int terminalBonus;
        public int shooterWeight;
        public int gruntWeight;
        public int tankWeight;
        public int flyingWeight;
        public Color fogColor;
        public Color skyTint;
        public Color bloomTint;
        public Color colorFilter;
        public Color dustColor;
        public Color sparkColor;
        public float ambientBoost;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Start()
    {
        if (generateOnStart)
            GenerateArena();
    }

    [ContextMenu("Generate Arena")]
    public void GenerateArena()
    {
        if (clearBeforeGenerate)
            ClearArena();

        bridgeLevel = Mathf.Clamp(bridgeLevel, 1, platformLevel - 1);
        platformLevel = Mathf.Clamp(platformLevel, bridgeLevel + 1, crownLevel - 1);
        crownLevel = Mathf.Max(platformLevel + 1, crownLevel);
        recoveryPoints.Clear();
        traversalConnectors.Clear();
        EnsureMaterials();

        Transform root = new GameObject(generatedRootName).transform;
        root.SetParent(transform, false);
        spawned.Add(root.gameObject);
        CurrentArenaRoot = root;

        int actualSeed = (randomizeSeedEachGeneration || seed == 0)
            ? unchecked(System.Environment.TickCount ^ (int)(Time.realtimeSinceStartup * 100000f) ^ UnityEngine.Random.Range(int.MinValue, int.MaxValue))
            : seed;
        lastGeneratedSeed = actualSeed;
        var rng = new System.Random(actualSeed);
        CellKind[,] cells = BuildLayout(rng);
        RepairLayoutConnectivity(cells);
        lastCells = cells;
        lastSpawnCell = FindFirst(cells, CellKind.Spawn);
        lastExitCell = FindFirst(cells, CellKind.Exit);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                SpawnCell(root, cells[x, z], x, z);
            }
        }

        SpawnBoundaryFrame(root);
        SpawnUndersidePillars(root, cells);
        SpawnRecoveryDecks(root);
        SpawnFloatingTrim(root, cells);
        SpawnArchitecturalContent(root, cells, rng);
        RegisterArenaRecoveryPoints(cells);
        SpawnGameplayContent(root, cells, rng);
        SpawnArenaLighting(root);
        SpawnStructuralShell(root, rng);
        SpawnAtmosphereFX(root, rng);
        ApplyEnvironmentFX();
        if (!skipPlayerPlacementOnce)
            PlacePlayer(cells);
        else
            skipPlayerPlacementOnce = false;

        Debug.Log($"[Arena] Generated {width}x{length} arena with seed {actualSeed}.");
    }

    [ContextMenu("Clear Arena")]
    public void ClearArena()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] == null) continue;
            spawned[i].SetActive(false);
            if (Application.isPlaying)
                Destroy(spawned[i]);
            else
                DestroyImmediate(spawned[i]);
        }

        spawned.Clear();
        recoveryPoints.Clear();
        traversalConnectors.Clear();
        CurrentArenaRoot = null;

        Transform old = transform.Find(generatedRootName);
        if (old != null)
        {
            old.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(old.gameObject);
            else
                DestroyImmediate(old.gameObject);
        }
    }

    private CellKind[,] BuildLayout(System.Random rng)
    {
        var cells = new CellKind[width, length];
        int centerJitterX = Mathf.Clamp(width / 10, 1, 4);
        int centerJitterZ = Mathf.Clamp(length / 10, 1, 4);
        Vector2Int center = new Vector2Int(
            Mathf.Clamp((width / 2) + rng.Next(-centerJitterX, centerJitterX + 1), 6, width - 7),
            Mathf.Clamp((length / 2) + rng.Next(-centerJitterZ, centerJitterZ + 1), 6, length - 7));
        Vector2Int spawn = new Vector2Int(width / 2, 2);
        Vector2Int exit = new Vector2Int(width / 2, length - 3);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                bool border = x == 0 || z == 0 || x == width - 1 || z == length - 1;
                cells[x, z] = border ? CellKind.Void : CellKind.Floor;
            }
        }

        if (arenaMode == ArenaMode.Shop)
        {
            BuildFlatShopLayout(cells, spawn, exit);
            return cells;
        }

        CarveVoidMoat(cells);
        int centerRadius = Mathf.Clamp(centralPlatformRadius + rng.Next(-1, 2), 2, Mathf.Max(2, Mathf.Min(width, length) / 5));
        StampRect(cells, center.x - centerRadius, center.y - centerRadius, center.x + centerRadius, center.y + centerRadius, CellKind.Platform);
        int crownRadius = Mathf.Clamp(centerRadius - 2, 2, 4);
        StampRect(cells, center.x - crownRadius, center.y - 1, center.x + crownRadius, center.y + 1, CellKind.UpperPlatform);
        StampRect(cells, center.x - 1, center.y - crownRadius, center.x + 1, center.y + crownRadius, CellKind.UpperPlatform);

        StampRect(cells, center.x - mainBridgeHalfWidth, 1, center.x + mainBridgeHalfWidth, length - 2, CellKind.Bridge);
        StampRect(cells, 1, center.y - mainBridgeHalfWidth, width - 2, center.y + mainBridgeHalfWidth, CellKind.Bridge);

        int diagonalCount = Mathf.Clamp((width + length) / 14, 4, 8);
        for (int i = 0; i < diagonalCount; i++)
        {
            Vector2Int a = new Vector2Int(rng.Next(3, width - 3), rng.Next(3, length - 3));
            Vector2Int b = new Vector2Int(rng.Next(3, width - 3), rng.Next(3, length - 3));
            StampBridgeLine(cells, a, b, rng.NextDouble() < 0.55 ? 1 : 0);
        }

        int ringInset = Mathf.Clamp(Mathf.Min(width, length) / 5, 4, 8);
        StampRect(cells, ringInset, ringInset, width - ringInset - 1, ringInset + 1, CellKind.Bridge);
        StampRect(cells, ringInset, length - ringInset - 2, width - ringInset - 1, length - ringInset - 1, CellKind.Bridge);
        StampRect(cells, ringInset, ringInset, ringInset + 1, length - ringInset - 1, CellKind.Bridge);
        StampRect(cells, width - ringInset - 2, ringInset, width - ringInset - 1, length - ringInset - 1, CellKind.Bridge);

        int s = cornerPlatformSize;
        StampRect(cells, 2, 2, 1 + s, 1 + s, CellKind.Platform);
        StampRect(cells, width - 2 - s, 2, width - 3, 1 + s, CellKind.Platform);
        StampRect(cells, 2, length - 2 - s, 1 + s, length - 3, CellKind.Platform);
        StampRect(cells, width - 2 - s, length - 2 - s, width - 3, length - 3, CellKind.Platform);
        StampRect(cells, 3, 3, 4, 4, CellKind.UpperPlatform);
        StampRect(cells, width - 5, 3, width - 4, 4, CellKind.UpperPlatform);
        StampRect(cells, 3, length - 5, 4, length - 4, CellKind.UpperPlatform);
        StampRect(cells, width - 5, length - 5, width - 4, length - 4, CellKind.UpperPlatform);

        StampBridgeLine(cells, new Vector2Int(4, 4), new Vector2Int(center.x - centerRadius - 1, center.y - centerRadius - 1), 0);
        StampBridgeLine(cells, new Vector2Int(width - 5, 4), new Vector2Int(center.x + centerRadius + 1, center.y - centerRadius - 1), 0);
        StampBridgeLine(cells, new Vector2Int(4, length - 5), new Vector2Int(center.x - centerRadius - 1, center.y + centerRadius + 1), 0);
        StampBridgeLine(cells, new Vector2Int(width - 5, length - 5), new Vector2Int(center.x + centerRadius + 1, center.y + centerRadius + 1), 0);
        StampBridgeLine(cells, new Vector2Int(center.x - centerRadius - 1, center.y), new Vector2Int(4, center.y), 0);
        StampBridgeLine(cells, new Vector2Int(center.x + centerRadius + 1, center.y), new Vector2Int(width - 5, center.y), 0);
        StampBridgeLine(cells, new Vector2Int(center.x, center.y - centerRadius - 1), new Vector2Int(center.x, 4), 0);
        StampBridgeLine(cells, new Vector2Int(center.x, center.y + centerRadius + 1), new Vector2Int(center.x, length - 5), 0);

        StampOuterDetail(cells, rng, center, spawn, exit);
        StampFloatingIslands(cells, rng, center, spawn, exit);
        StampSafeZone(cells, spawn, safeRadiusAroundSpawn);
        StampSafeZone(cells, exit, safeRadiusAroundExit);
        cells[spawn.x, spawn.y] = CellKind.Spawn;
        cells[exit.x, exit.y] = CellKind.Exit;

        return cells;
    }

    private void BuildFlatShopLayout(CellKind[,] cells, Vector2Int spawn, Vector2Int exit)
    {
        int borderInset = 2;
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                bool outside = x < borderInset || z < borderInset || x >= width - borderInset || z >= length - borderInset;
                cells[x, z] = outside ? CellKind.Void : CellKind.Floor;
            }
        }

        Vector2Int center = new Vector2Int(width / 2, length / 2);
        StampRect(cells, center.x - 6, center.y - 5, center.x + 6, center.y + 4, CellKind.Floor);
        StampRect(cells, center.x - 2, 2, center.x + 2, center.y - 5, CellKind.Floor);
        StampRect(cells, center.x - 2, center.y + 5, center.x + 2, length - 3, CellKind.Floor);
        StampSafeZone(cells, spawn, safeRadiusAroundSpawn + 1);
        StampSafeZone(cells, exit, safeRadiusAroundExit + 1);
        cells[spawn.x, spawn.y] = CellKind.Spawn;
        cells[exit.x, exit.y] = CellKind.Exit;
    }

    private void RepairLayoutConnectivity(CellKind[,] cells)
    {
        if (cells == null) return;

        Vector2Int spawn = FindFirst(cells, CellKind.Spawn);
        Vector2Int exit = FindFirst(cells, CellKind.Exit);
        if (!InBounds(spawn.x, spawn.y))
            return;

        bool[,] reachable = FloodReachableCells(cells, spawn);
        int repairPasses = 0;
        while (repairPasses < 8)
        {
            Vector2Int unreachable = FindFirstUnreachableWalkable(cells, reachable);
            if (!InBounds(unreachable.x, unreachable.y))
                break;

            Vector2Int anchor = FindNearestReachableCell(cells, reachable, unreachable);
            if (!InBounds(anchor.x, anchor.y))
                break;

            StampBridgeLine(cells, unreachable, anchor, 0);
            StampSafeZone(cells, spawn, safeRadiusAroundSpawn);
            StampSafeZone(cells, exit, safeRadiusAroundExit);
            cells[spawn.x, spawn.y] = CellKind.Spawn;
            cells[exit.x, exit.y] = CellKind.Exit;
            reachable = FloodReachableCells(cells, spawn);
            repairPasses++;
        }

        if (!FloodReachableCells(cells, spawn)[exit.x, exit.y])
        {
            StampBridgeLine(cells, spawn, exit, Mathf.Max(0, mainBridgeHalfWidth - 1));
            cells[spawn.x, spawn.y] = CellKind.Spawn;
            cells[exit.x, exit.y] = CellKind.Exit;
        }
    }

    private bool[,] FloodReachableCells(CellKind[,] cells, Vector2Int start)
    {
        bool[,] reachable = new bool[width, length];
        if (!InBounds(start.x, start.y) || !IsLayoutWalkable(cells[start.x, start.y]))
            return reachable;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        reachable[start.x, start.y] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            foreach (Vector2Int neighbor in GetCardinalNeighbors(cell))
            {
                if (!InBounds(neighbor.x, neighbor.y)) continue;
                if (reachable[neighbor.x, neighbor.y]) continue;
                if (!CanTraverseCellsForLayout(cells, cell, neighbor)) continue;
                reachable[neighbor.x, neighbor.y] = true;
                queue.Enqueue(neighbor);
            }
        }

        return reachable;
    }

    private Vector2Int FindFirstUnreachableWalkable(CellKind[,] cells, bool[,] reachable)
    {
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                if (!IsLayoutWalkable(cells[x, z])) continue;
                if (!reachable[x, z])
                    return new Vector2Int(x, z);
            }
        }

        return new Vector2Int(-1, -1);
    }

    private Vector2Int FindNearestReachableCell(CellKind[,] cells, bool[,] reachable, Vector2Int from)
    {
        Vector2Int best = new Vector2Int(-1, -1);
        int bestDistance = int.MaxValue;
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                if (!reachable[x, z] || !IsLayoutWalkable(cells[x, z])) continue;
                int distance = Mathf.Abs(from.x - x) + Mathf.Abs(from.y - z);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = new Vector2Int(x, z);
            }
        }

        return best;
    }

    private bool CanTraverseCellsForLayout(CellKind[,] cells, Vector2Int from, Vector2Int to)
    {
        if (!InBounds(from.x, from.y) || !InBounds(to.x, to.y))
            return false;
        if (!IsLayoutWalkable(cells[from.x, from.y]) || !IsLayoutWalkable(cells[to.x, to.y]))
            return false;

        float fromY = GetCellHeight(cells[from.x, from.y]);
        float toY = GetCellHeight(cells[to.x, to.y]);
        return Mathf.Abs(fromY - toY) <= levelHeight + 0.1f;
    }

    private bool IsLayoutWalkable(CellKind kind)
    {
        return kind == CellKind.Floor ||
               kind == CellKind.Bridge ||
               kind == CellKind.Platform ||
               kind == CellKind.UpperPlatform ||
               kind == CellKind.Spawn ||
               kind == CellKind.Exit;
    }

    private IEnumerable<Vector2Int> GetCardinalNeighbors(Vector2Int cell)
    {
        yield return new Vector2Int(cell.x + 1, cell.y);
        yield return new Vector2Int(cell.x - 1, cell.y);
        yield return new Vector2Int(cell.x, cell.y + 1);
        yield return new Vector2Int(cell.x, cell.y - 1);
    }

    private void StampBridgeLine(CellKind[,] cells, Vector2Int start, Vector2Int end, int extraHalfWidth)
    {
        int x = start.x;
        int z = start.y;
        int dx = Mathf.Abs(end.x - start.x);
        int dz = Mathf.Abs(end.y - start.y);
        int sx = start.x < end.x ? 1 : -1;
        int sz = start.y < end.y ? 1 : -1;
        int err = dx - dz;

        while (true)
        {
            StampRect(cells, x - extraHalfWidth, z - extraHalfWidth, x + extraHalfWidth, z + extraHalfWidth, CellKind.Bridge);
            if (x == end.x && z == end.y) break;
            int e2 = 2 * err;
            if (e2 > -dz)
            {
                err -= dz;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                z += sz;
            }
        }
    }

    private void StampFloatingIslands(CellKind[,] cells, System.Random rng, Vector2Int center, Vector2Int spawn, Vector2Int exit)
    {
        ThemeProfile profile = ResolveThemeProfile(themeIndex);
        int islandCount = Mathf.Clamp((width * length) / 130, 3, 12) + profile.extraIslands;
        for (int i = 0; i < islandCount; i++)
        {
            int x = rng.Next(4, width - 4);
            int z = rng.Next(4, length - 4);
            if (DistanceManhattan(x, z, center) < centralPlatformRadius + 3) continue;
            if (DistanceManhattan(x, z, spawn) < safeRadiusAroundSpawn + 2) continue;
            if (DistanceManhattan(x, z, exit) < safeRadiusAroundExit + 2) continue;

            int rx = rng.Next(1, 3);
            int rz = rng.Next(1, 3);
            CellKind kind = rng.NextDouble() < 0.65 ? CellKind.Platform : CellKind.Bridge;
            StampRect(cells, x - rx, z - rz, x + rx, z + rz, kind);
            ConnectIslandToMainRoute(cells, center, new Vector2Int(x, z), kind, rng);
        }
    }

    private void ConnectIslandToMainRoute(CellKind[,] cells, Vector2Int center, Vector2Int islandCenter, CellKind islandKind, System.Random rng)
    {
        Vector2Int target;
        if (Mathf.Abs(islandCenter.x - center.x) > Mathf.Abs(islandCenter.y - center.y))
        {
            int targetX = islandCenter.x < center.x ? center.x - (centralPlatformRadius + 1) : center.x + (centralPlatformRadius + 1);
            target = new Vector2Int(Mathf.Clamp(targetX, 2, width - 3), Mathf.Clamp(islandCenter.y, 2, length - 3));
        }
        else
        {
            int targetZ = islandCenter.y < center.y ? center.y - (centralPlatformRadius + 1) : center.y + (centralPlatformRadius + 1);
            target = new Vector2Int(Mathf.Clamp(islandCenter.x, 2, width - 3), Mathf.Clamp(targetZ, 2, length - 3));
        }

        int extraWidth = islandKind == CellKind.Platform && rng.NextDouble() < 0.45 ? 1 : 0;
        StampBridgeLine(cells, islandCenter, target, extraWidth);

        if (islandKind == CellKind.Platform)
        {
            StampRect(cells, islandCenter.x - 1, islandCenter.y - 1, islandCenter.x + 1, islandCenter.y + 1, CellKind.Platform);
        }
    }

    private void CarveVoidMoat(CellKind[,] cells)
    {
        int inset = 1;
        for (int x = inset; x < width - inset; x++)
        {
            cells[x, inset] = CellKind.Void;
            cells[x, length - inset - 1] = CellKind.Void;
        }

        for (int z = inset; z < length - inset; z++)
        {
            cells[inset, z] = CellKind.Void;
            cells[width - inset - 1, z] = CellKind.Void;
        }
    }

    private void StampOuterDetail(CellKind[,] cells, System.Random rng, Vector2Int center, Vector2Int spawn, Vector2Int exit)
    {
        ThemeProfile profile = ResolveThemeProfile(themeIndex);
        float adjustedOuterGapChance = Mathf.Clamp01(outerGapChance * profile.outerGapMultiplier);
        float adjustedHazardChance = Mathf.Clamp01(hazardChance * profile.hazardMultiplier);
        float adjustedCoverChance = Mathf.Clamp01(coverChance * profile.coverMultiplier);

        for (int x = 2; x < width - 2; x++)
        {
            for (int z = 2; z < length - 2; z++)
            {
                if (cells[x, z] != CellKind.Floor) continue;
                if (DistanceManhattan(x, z, spawn) <= safeRadiusAroundSpawn) continue;
                if (DistanceManhattan(x, z, exit) <= safeRadiusAroundExit) continue;
                if (Mathf.Abs(x - center.x) <= centralPlatformRadius + 2) continue;
                if (Mathf.Abs(z - center.y) <= centralPlatformRadius + 2) continue;

                double roll = rng.NextDouble();
                if (roll < adjustedOuterGapChance)
                {
                    cells[x, z] = CellKind.Void;
                }
                else if (roll < adjustedOuterGapChance + adjustedHazardChance)
                {
                    cells[x, z] = CellKind.Hazard;
                }
                else if (roll < adjustedOuterGapChance + adjustedHazardChance + adjustedCoverChance)
                {
                    cells[x, z] = rng.NextDouble() < 0.65 ? CellKind.CoverLow : CellKind.CoverHigh;
                }
            }
        }
    }

    private void StampSafeZone(CellKind[,] cells, Vector2Int point, int radius)
    {
        for (int x = point.x - radius; x <= point.x + radius; x++)
        {
            for (int z = point.y - radius; z <= point.y + radius; z++)
            {
                if (!InBounds(x, z)) continue;
                if (DistanceManhattan(x, z, point) > radius) continue;
                if (cells[x, z] == CellKind.Void || cells[x, z] == CellKind.Hazard)
                    cells[x, z] = CellKind.Floor;
            }
        }
    }

    private void StampRect(CellKind[,] cells, int xMin, int zMin, int xMax, int zMax, CellKind kind)
    {
        for (int x = Mathf.Clamp(xMin, 0, width - 1); x <= Mathf.Clamp(xMax, 0, width - 1); x++)
        {
            for (int z = Mathf.Clamp(zMin, 0, length - 1); z <= Mathf.Clamp(zMax, 0, length - 1); z++)
            {
                if (x == 0 || z == 0 || x == width - 1 || z == length - 1) continue;
                cells[x, z] = kind;
            }
        }
    }

    private void SpawnCell(Transform root, CellKind kind, int x, int z)
    {
        if (kind == CellKind.Void) return;

        float y = GetCellHeight(kind);
        Material mat = GetCellMaterial(kind);
        GameObject floor = CreateCube(root, $"{kind}_{x}_{z}", CellCenter(x, z, y), new Vector3(tileSize, floorThickness, tileSize), mat);

        if (kind == CellKind.Hazard)
        {
            CreateCube(root, $"HazardInset_{x}_{z}", CellCenter(x, z, y + 0.08f), new Vector3(tileSize * 0.72f, 0.05f, tileSize * 0.72f), hazardMaterial);
        }
        else if (kind == CellKind.Exit)
        {
            SpawnExitBeacon(root, x, z, y);
        }
        else if (kind == CellKind.CoverLow || kind == CellKind.CoverHigh)
        {
            float h = kind == CellKind.CoverLow ? 0.9f : 1.85f;
            CreateCube(root, $"Cover_{x}_{z}", CellCenter(x, z, y + (h * 0.5f) + floorThickness), new Vector3(tileSize * 0.72f, h, tileSize * 0.22f), darkMaterial);
        }

        AddTilePanel(root, kind, x, z, y);
        floor.isStatic = true;
    }

    private void AddTilePanel(Transform root, CellKind kind, int x, int z, float y)
    {
        if (kind == CellKind.Hazard) return;

        float panel = tileSize * 0.62f;
        float ay = y + floorThickness * 0.5f + 0.025f;
        Material mat = kind == CellKind.Spawn || kind == CellKind.Exit ? GetCellMaterial(kind) : darkMaterial;

        if (((x * 17 + z * 31 + lastGeneratedSeed) & 3) == 0 || kind == CellKind.Spawn || kind == CellKind.Exit)
            CreateCube(root, $"SurfacePanel_{x}_{z}", CellCenter(x, z, ay), new Vector3(panel, 0.03f, panel), mat, false);
    }

    private void SpawnExitBeacon(Transform root, int x, int z, float y)
    {
        Vector3 center = CellCenter(x, z, y + floorThickness * 0.5f + 0.045f);
        GameObject pad = CreateCube(root, $"ExitBeaconPad_{x}_{z}", center, new Vector3(tileSize * 0.72f, 0.05f, tileSize * 0.72f), exitMaterial, false);
        ArenaPulseFx padPulse = pad.AddComponent<ArenaPulseFx>();
        padPulse.SetBaseScale(pad.transform.localScale);
        padPulse.scalePulse = 0.09f;
        padPulse.pulseSpeed = 2.2f;
        padPulse.emissionColor = new Color(1f, 0.65f, 0.22f);
        padPulse.emissionStrength = 0.8f;

        GameObject crossA = CreateCube(root, $"ExitBeaconLineA_{x}_{z}", center + Vector3.up * 0.04f, new Vector3(tileSize * 0.88f, 0.045f, 0.13f), accentMaterial, false);
        GameObject crossB = CreateCube(root, $"ExitBeaconLineB_{x}_{z}", center + Vector3.up * 0.045f, new Vector3(0.13f, 0.045f, tileSize * 0.88f), accentMaterial, false);
        ArenaPulseFx linePulseA = crossA.AddComponent<ArenaPulseFx>();
        linePulseA.SetBaseScale(crossA.transform.localScale);
        linePulseA.scalePulse = 0.14f;
        linePulseA.pulseSpeed = 3.1f;
        linePulseA.emissionColor = new Color(0.95f, 0.72f, 0.32f);
        linePulseA.emissionStrength = 0.9f;
        ArenaPulseFx linePulseB = crossB.AddComponent<ArenaPulseFx>();
        linePulseB.SetBaseScale(crossB.transform.localScale);
        linePulseB.scalePulse = 0.14f;
        linePulseB.pulseSpeed = 3.1f;
        linePulseB.emissionColor = new Color(0.95f, 0.72f, 0.32f);
        linePulseB.emissionStrength = 0.9f;

        GameObject beam = CreateCube(root, $"ExitBeaconBeam_{x}_{z}", CellCenter(x, z, y + 2.2f), new Vector3(0.16f, 4.2f, 0.16f), accentMaterial, false);
        ArenaPulseFx beamPulse = beam.AddComponent<ArenaPulseFx>();
        beamPulse.SetBaseScale(beam.transform.localScale);
        beamPulse.scalePulse = 0.28f;
        beamPulse.pulseSpeed = 2.7f;
        beamPulse.rotationDegreesPerSecond = new Vector3(0f, 42f, 0f);
        beamPulse.emissionColor = new Color(1f, 0.68f, 0.24f);
        beamPulse.emissionStrength = 1.2f;
    }

    private void SpawnBoundaryFrame(Transform root)
    {
        float y = levelHeight * crownLevel + 0.15f;
        Vector3 center = new Vector3((width - 1) * tileSize * 0.5f, y, (length - 1) * tileSize * 0.5f);
        CreateCube(root, "NorthFloatingFrame", center + new Vector3(0f, 0f, (length * tileSize) * 0.5f), new Vector3(width * tileSize, 0.35f, 0.35f), darkMaterial);
        CreateCube(root, "SouthFloatingFrame", center + new Vector3(0f, 0f, -(length * tileSize) * 0.5f), new Vector3(width * tileSize, 0.35f, 0.35f), darkMaterial);
        CreateCube(root, "EastFloatingFrame", center + new Vector3((width * tileSize) * 0.5f, 0f, 0f), new Vector3(0.35f, 0.35f, length * tileSize), darkMaterial);
        CreateCube(root, "WestFloatingFrame", center + new Vector3(-(width * tileSize) * 0.5f, 0f, 0f), new Vector3(0.35f, 0.35f, length * tileSize), darkMaterial);
    }

    private void SpawnFloatingTrim(Transform root, CellKind[,] cells)
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                if (cells[x, z] == CellKind.Void) continue;
                float y = GetCellHeight(cells[x, z]) - 0.35f;
                if (!InBounds(x + 1, z) || cells[x + 1, z] == CellKind.Void)
                    CreateCube(root, $"EdgeE_{x}_{z}", CellCenter(x, z, y) + new Vector3(tileSize * 0.5f, 0f, 0f), new Vector3(0.16f, 0.42f, tileSize), darkMaterial, false);
                if (!InBounds(x - 1, z) || cells[x - 1, z] == CellKind.Void)
                    CreateCube(root, $"EdgeW_{x}_{z}", CellCenter(x, z, y) - new Vector3(tileSize * 0.5f, 0f, 0f), new Vector3(0.16f, 0.42f, tileSize), darkMaterial, false);
                if (!InBounds(x, z + 1) || cells[x, z + 1] == CellKind.Void)
                    CreateCube(root, $"EdgeN_{x}_{z}", CellCenter(x, z, y) + new Vector3(0f, 0f, tileSize * 0.5f), new Vector3(tileSize, 0.42f, 0.16f), darkMaterial, false);
                if (!InBounds(x, z - 1) || cells[x, z - 1] == CellKind.Void)
                    CreateCube(root, $"EdgeS_{x}_{z}", CellCenter(x, z, y) - new Vector3(0f, 0f, tileSize * 0.5f), new Vector3(tileSize, 0.42f, 0.16f), darkMaterial, false);
            }
        }
    }

    private void SpawnArenaLighting(Transform root)
    {
        ThemeProfile profile = ResolveThemeProfile(themeIndex);
        RenderSettings.ambientLight = new Color(0.035f, 0.04f, 0.048f) + Color.white * profile.ambientBoost;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = profile.skyTint * 0.68f;
        RenderSettings.ambientEquatorColor = profile.skyTint * 0.38f;
        RenderSettings.ambientGroundColor = profile.fogColor * 0.95f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = profile.fogColor;
        RenderSettings.fogDensity = themeIndex % 4 == 1 ? 0.014f : themeIndex % 4 == 2 ? 0.012f : 0.013f;

        GameObject key = new GameObject("ArenaKeyLight");
        key.transform.SetParent(root, false);
        key.transform.position = new Vector3(width * tileSize * 0.5f, 18f, length * tileSize * 0.5f);
        Light light = key.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.Lerp(new Color(0.72f, 0.82f, 1f), profile.skyTint, 0.45f);
        light.intensity = 0.95f + profile.ambientBoost * 4f;
        key.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

        CreateCube(root, "AbyssFogPlane", new Vector3((width - 1) * tileSize * 0.5f, killPlaneY - 8f, (length - 1) * tileSize * 0.5f), new Vector3(width * tileSize * 2.2f, 1f, length * tileSize * 2.2f), darkMaterial, false);
    }

    private void SpawnStructuralShell(Transform root, System.Random rng)
    {
        float arenaSpanX = width * tileSize;
        float arenaSpanZ = length * tileSize;
        Vector3 center = new Vector3((width - 1) * tileSize * 0.5f, 0f, (length - 1) * tileSize * 0.5f);
        float shellInset = tileSize * 3.4f;
        float shellRadiusX = arenaSpanX * 0.5f + shellInset;
        float shellRadiusZ = arenaSpanZ * 0.5f + shellInset;
        float lowerDeckY = -6.5f;
        float lowerDeckThickness = 0.8f;
        float wallHeight = 28f;

        CreateCube(root, "ShellNorthDeck", center + new Vector3(0f, lowerDeckY, shellRadiusZ - tileSize * 1.4f), new Vector3(arenaSpanX + tileSize * 7f, lowerDeckThickness, tileSize * 4.2f), darkMaterial, false);
        CreateCube(root, "ShellSouthDeck", center + new Vector3(0f, lowerDeckY, -(shellRadiusZ - tileSize * 1.4f)), new Vector3(arenaSpanX + tileSize * 7f, lowerDeckThickness, tileSize * 4.2f), darkMaterial, false);
        CreateCube(root, "ShellEastDeck", center + new Vector3(shellRadiusX - tileSize * 1.4f, lowerDeckY, 0f), new Vector3(tileSize * 4.2f, lowerDeckThickness, arenaSpanZ + tileSize * 7f), darkMaterial, false);
        CreateCube(root, "ShellWestDeck", center + new Vector3(-(shellRadiusX - tileSize * 1.4f), lowerDeckY, 0f), new Vector3(tileSize * 4.2f, lowerDeckThickness, arenaSpanZ + tileSize * 7f), darkMaterial, false);

        CreateCube(root, "ShellNorthWall", center + new Vector3(0f, wallHeight * 0.5f - 5f, shellRadiusZ), new Vector3(arenaSpanX + tileSize * 8.5f, wallHeight, tileSize * 1.1f), darkMaterial, false);
        CreateCube(root, "ShellSouthWall", center + new Vector3(0f, wallHeight * 0.5f - 5f, -shellRadiusZ), new Vector3(arenaSpanX + tileSize * 8.5f, wallHeight, tileSize * 1.1f), darkMaterial, false);
        CreateCube(root, "ShellEastWall", center + new Vector3(shellRadiusX, wallHeight * 0.5f - 5f, 0f), new Vector3(tileSize * 1.1f, wallHeight, arenaSpanZ + tileSize * 8.5f), darkMaterial, false);
        CreateCube(root, "ShellWestWall", center + new Vector3(-shellRadiusX, wallHeight * 0.5f - 5f, 0f), new Vector3(tileSize * 1.1f, wallHeight, arenaSpanZ + tileSize * 8.5f), darkMaterial, false);

        for (int i = -2; i <= 2; i++)
        {
            float offsetX = i * tileSize * 4.4f;
            CreateCube(root, $"ShellNorthRib_{i}", center + new Vector3(offsetX, 7.5f, shellRadiusZ - tileSize * 0.4f), new Vector3(tileSize * 0.36f, 17f + Mathf.Abs(i) * 1.8f, tileSize * 0.44f), darkMaterial, false);
            CreateCube(root, $"ShellSouthRib_{i}", center + new Vector3(offsetX, 7.5f, -shellRadiusZ + tileSize * 0.4f), new Vector3(tileSize * 0.36f, 17f + Mathf.Abs(i) * 1.8f, tileSize * 0.44f), darkMaterial, false);
            CreateCube(root, $"ShellNorthGlow_{i}", center + new Vector3(offsetX, 8.8f, shellRadiusZ - tileSize * 0.88f), new Vector3(tileSize * 0.08f, 11f, tileSize * 0.08f), accentMaterial, false);
            CreateCube(root, $"ShellSouthGlow_{i}", center + new Vector3(offsetX, 8.8f, -shellRadiusZ + tileSize * 0.88f), new Vector3(tileSize * 0.08f, 11f, tileSize * 0.08f), accentMaterial, false);
        }

        for (int i = -2; i <= 2; i++)
        {
            float offsetZ = i * tileSize * 4.4f;
            CreateCube(root, $"ShellEastRib_{i}", center + new Vector3(shellRadiusX - tileSize * 0.4f, 7.5f, offsetZ), new Vector3(tileSize * 0.44f, 17f + Mathf.Abs(i) * 1.8f, tileSize * 0.36f), darkMaterial, false);
            CreateCube(root, $"ShellWestRib_{i}", center + new Vector3(-shellRadiusX + tileSize * 0.4f, 7.5f, offsetZ), new Vector3(tileSize * 0.44f, 17f + Mathf.Abs(i) * 1.8f, tileSize * 0.36f), darkMaterial, false);
            CreateCube(root, $"ShellEastGlow_{i}", center + new Vector3(shellRadiusX - tileSize * 0.88f, 8.8f, offsetZ), new Vector3(tileSize * 0.08f, 11f, tileSize * 0.08f), accentMaterial, false);
            CreateCube(root, $"ShellWestGlow_{i}", center + new Vector3(-shellRadiusX + tileSize * 0.88f, 8.8f, offsetZ), new Vector3(tileSize * 0.08f, 11f, tileSize * 0.08f), accentMaterial, false);
        }

        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            float ringX = Mathf.Cos(angle) * (shellRadiusX - tileSize * 1.8f);
            float ringZ = Mathf.Sin(angle) * (shellRadiusZ - tileSize * 1.8f);
            float towerHeight = 8f + (float)rng.NextDouble() * 5f;
            CreateCube(root, $"ShellTower_{i}", center + new Vector3(ringX, lowerDeckY + towerHeight * 0.5f + 0.2f, ringZ), new Vector3(tileSize * 0.82f, towerHeight, tileSize * 0.82f), darkMaterial, false);
            CreateCube(root, $"ShellTowerCap_{i}", center + new Vector3(ringX, lowerDeckY + towerHeight + 0.45f, ringZ), new Vector3(tileSize * 1.2f, 0.24f, tileSize * 1.2f), accentMaterial, false);
        }
    }

    private void ApplyEnvironmentFX()
    {
        EnsureSkybox();
        EnsureVolume();
    }

    private void EnsureSkybox()
    {
        ThemeProfile profile = ResolveThemeProfile(themeIndex);
        if (skyboxMaterial == null)
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null) shader = Shader.Find("Skybox/6 Sided");
            if (shader == null) shader = Shader.Find("Standard");
        skyboxMaterial = new Material(shader);
        skyboxMaterial.name = "Arena Sky";
        }

        if (skyboxMaterial == null) return;

        if (skyboxMaterial.HasProperty("_SkyTint"))
            skyboxMaterial.SetColor("_SkyTint", profile.skyTint);
        if (skyboxMaterial.HasProperty("_GroundColor"))
            skyboxMaterial.SetColor("_GroundColor", profile.fogColor * 0.85f);
        if (skyboxMaterial.HasProperty("_Exposure"))
            skyboxMaterial.SetFloat("_Exposure", 1.1f + profile.ambientBoost * 2.5f);
        RenderSettings.skybox = skyboxMaterial;
    }

    private void EnsureVolume()
    {
        ThemeProfile themeProfile = ResolveThemeProfile(themeIndex);
        if (environmentVolume == null)
        {
            GameObject volumeObject = new GameObject("ArenaEnvironmentVolume");
            volumeObject.transform.SetParent(transform, false);
            environmentVolume = volumeObject.AddComponent<Volume>();
            environmentVolume.isGlobal = true;
            environmentVolume.priority = 20f;
            environmentVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        }

        if (environmentVolume == null || environmentVolume.profile == null) return;

        VolumeProfile volProfile = environmentVolume.profile;
        if (!volProfile.TryGet(out Bloom bloom))
            bloom = volProfile.Add<Bloom>(true);
        bloom.active = true;
        bloom.intensity.Override(2.2f);
        bloom.threshold.Override(0.78f);
        bloom.scatter.Override(0.72f);
        bloom.tint.Override(themeProfile.bloomTint);

        if (!volProfile.TryGet(out Vignette vignette))
            vignette = volProfile.Add<Vignette>(true);
        vignette.active = true;
        vignette.intensity.Override(0.28f);
        vignette.smoothness.Override(0.62f);
        vignette.color.Override(new Color(0.02f, 0.02f, 0.03f));

        if (!volProfile.TryGet(out ColorAdjustments colorAdjustments))
            colorAdjustments = volProfile.Add<ColorAdjustments>(true);
        colorAdjustments.active = true;
        colorAdjustments.postExposure.Override(0.35f);
        colorAdjustments.contrast.Override(18f);
        colorAdjustments.saturation.Override(12f);
        colorAdjustments.colorFilter.Override(themeProfile.colorFilter);

        if (!volProfile.TryGet(out FilmGrain filmGrain))
            filmGrain = volProfile.Add<FilmGrain>(true);
        filmGrain.active = true;
        filmGrain.intensity.Override(0.18f);
        filmGrain.type.Override(FilmGrainLookup.Thin1);

        if (!volProfile.TryGet(out WhiteBalance whiteBalance))
            whiteBalance = volProfile.Add<WhiteBalance>(true);
        whiteBalance.active = true;
        whiteBalance.temperature.Override(-5f);
        whiteBalance.tint.Override(3f);
    }

    private void SpawnGameplayContent(Transform root, CellKind[,] cells, System.Random rng)
    {
        ThemeProfile profile = ResolveThemeProfile(themeIndex);
        Vector2Int spawn = FindFirst(cells, CellKind.Spawn);
        Vector2Int exit = FindFirst(cells, CellKind.Exit);
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 2; x < width - 2; x++)
        {
            for (int z = 2; z < length - 2; z++)
            {
                if (!IsSafePuzzleCell(cells, x, z)) continue;
                if (DistanceManhattan(x, z, spawn) < safeRadiusAroundSpawn + 2) continue;
                if (DistanceManhattan(x, z, exit) < safeRadiusAroundExit + 1) continue;
                candidates.Add(new Vector2Int(x, z));
            }
        }

        Shuffle(candidates, rng);

        int terminalCount = arenaMode == ArenaMode.Shop
            ? 0
            : arenaMode == ArenaMode.Boss
                ? 0
                : Mathf.Clamp(1 + Mathf.Max(0, profile.terminalBonus), 1, 2);
        int placed = 0;
        for (int i = 0; i < candidates.Count && placed < terminalCount; i++)
        {
            Vector2Int cell = candidates[i];
            SpawnPuzzleTerminal(root, cells, cell, placed, rng);
            placed++;
        }

        int itemCount = arenaMode == ArenaMode.Shop
            ? Mathf.Clamp(Mathf.RoundToInt(10 * profile.itemMultiplier), 8, 14)
            : arenaMode == ArenaMode.Boss
                ? Mathf.Clamp(Mathf.RoundToInt(4 * profile.itemMultiplier), 3, 8)
                : Mathf.Clamp(Mathf.RoundToInt(width * length * itemChance * profile.itemMultiplier), 6, 20);
        for (int i = terminalCount; i < candidates.Count && itemCount > 0; i++)
        {
            Vector2Int cell = candidates[i];
            if (rng.NextDouble() > 0.65) continue;
            SpawnPickup(root, cells, cell, rng.NextDouble() < 0.62 ? CybergrindPickup.PickupType.Health : CybergrindPickup.PickupType.Coin);
            itemCount--;
        }

        if (arenaMode == ArenaMode.Shop)
            SpawnShopStalls(root, cells);
        else if (arenaMode == ArenaMode.Boss)
            SpawnBossArenaMarkers(root, cells);

        SpawnEnemies(root, cells, rng, spawn, exit);
    }

    private void SpawnEnemies(Transform root, CellKind[,] cells, System.Random rng, Vector2Int spawn, Vector2Int exit)
    {
        if (arenaMode == ArenaMode.Shop) return;
        if (enemyPrefab == null) return;

        if (arenaMode == ArenaMode.Boss)
        {
            SpawnBossChampion(root, cells, rng);
            return;
        }

        List<Vector2Int> enemyCells = new List<Vector2Int>();
        for (int x = 2; x < width - 2; x++)
        {
            for (int z = 2; z < length - 2; z++)
            {
                if (!IsWalkableForContent(cells[x, z])) continue;
                if (!IsReliableEnemyCell(cells, x, z)) continue;
                if (!HasGroundPathBetween(spawn, new Vector2Int(x, z), 3)) continue;
                if (DistanceManhattan(x, z, spawn) < minEnemyDistanceFromSpawn) continue;
                if (DistanceManhattan(x, z, exit) < safeRadiusAroundExit) continue;
                enemyCells.Add(new Vector2Int(x, z));
            }
        }

        Shuffle(enemyCells, rng);

        int min = arenaMode == ArenaMode.Boss ? bossEnemyMin : combatEnemyMin;
        int max = arenaMode == ArenaMode.Boss ? bossEnemyMax : combatEnemyMax;
        int targetCount = Mathf.Clamp(rng.Next(Mathf.Min(min, max), Mathf.Max(min, max) + 1), 0, enemyCells.Count);

        int startIndex = 0;
        for (int i = startIndex; i < enemyCells.Count && i - startIndex < targetCount; i++)
        {
            Vector2Int cell = enemyCells[i];
            float y = GetCellHeight(cells[cell.x, cell.y]);
            Vector3 spawnPos = transform.position + CellCenter(cell.x, cell.y, y + 0.15f);

            GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity, root);
            enemy.name = $"Enemy_{i + 1}";

            BasicEnemyAI ai = enemy.GetComponent<BasicEnemyAI>();
            if (ai != null)
            {
                ai.enemyType = RollEnemyType(rng, arenaMode);
                ai.autoBuildTypeModel = true;
            }
        }
    }

    public int SpawnPressureEnemiesNear(Vector3 worldPosition, int count)
    {
        if (enemyPrefab == null || CurrentArenaRoot == null || lastCells == null)
            return 0;
        if (arenaMode != ArenaMode.Combat)
            return 0;

        Vector2Int origin = FindNearestWalkable(WorldToCell(worldPosition));
        var candidates = new List<Vector2Int>();
        for (int radius = 3; radius <= 9; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;
                    Vector2Int cell = new Vector2Int(origin.x + dx, origin.y + dz);
                    if (!InBounds(cell.x, cell.y)) continue;
                    if (!IsReliableEnemyCell(lastCells, cell.x, cell.y)) continue;
                    if (!TryBuildGroundPath(worldPosition, GetNavigationPointForCell(cell), out List<Vector3> path) || path == null || path.Count < 2) continue;
                    candidates.Add(cell);
                }
            }

            if (candidates.Count >= count * 2)
                break;
        }

        if (candidates.Count == 0)
            return 0;

        var rng = new System.Random(unchecked(lastGeneratedSeed ^ Mathf.RoundToInt(Time.time * 1000f) ^ count * 131));
        Shuffle(candidates, rng);

        int spawnedCount = 0;
        for (int i = 0; i < candidates.Count && spawnedCount < count; i++)
        {
            Vector2Int cell = candidates[i];
            Vector3 spawnPos = GetNavigationPointForCell(cell) + Vector3.up * 0.05f;
            GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity, CurrentArenaRoot);
            enemy.name = $"PressureEnemy_{spawnedCount + 1}";

            BasicEnemyAI ai = enemy.GetComponent<BasicEnemyAI>();
            if (ai != null)
            {
                ai.enemyType = RollEnemyType(rng, arenaMode);
                if (ai.enemyType == BasicEnemyAI.EnemyType.Flying)
                    ai.enemyType = BasicEnemyAI.EnemyType.Shooter;
                ai.autoBuildTypeModel = true;
            }

            spawnedCount++;
        }

        return spawnedCount;
    }

    private void SpawnBossChampion(Transform root, CellKind[,] cells, System.Random rng)
    {
        if (enemyPrefab == null) return;

        Vector2Int center = new Vector2Int(width / 2, length / 2);
        float y = GetCellHeight(cells[center.x, center.y]);
        Vector3 spawnPos = transform.position + CellCenter(center.x, center.y, y + 0.18f);

        GameObject boss = Instantiate(enemyPrefab, spawnPos, Quaternion.identity, root);
        boss.name = $"BossChampion_{GetThemeLabel(themeIndex).Replace(" ", string.Empty)}";

        BasicEnemyAI ai = boss.GetComponent<BasicEnemyAI>();
        if (ai == null) return;

        int bossRoll = rng.Next(3);
        ai.bossArchetype = (BasicEnemyAI.BossArchetype)(bossRoll + 1);
        ai.enemyType = ai.bossArchetype == BasicEnemyAI.BossArchetype.Sentinel
            ? BasicEnemyAI.EnemyType.Flying
            : ai.bossArchetype == BasicEnemyAI.BossArchetype.Striker
                ? BasicEnemyAI.EnemyType.Grunt
                : BasicEnemyAI.EnemyType.Tank;
        ai.isBoss = true;
        string themeLabel = GetThemeLabel(themeIndex);
        ai.displayName = ai.bossArchetype == BasicEnemyAI.BossArchetype.Sentinel
            ? $"{themeLabel} Aerial Sentinel"
            : ai.bossArchetype == BasicEnemyAI.BossArchetype.Striker
                ? $"{themeLabel} Raze Striker"
                : $"{themeLabel} Obelisk Warden";
        ai.maxHealth = 74f + (themeIndex * 18f);
        ai.moveSpeed = ai.bossArchetype == BasicEnemyAI.BossArchetype.Striker ? 5.3f : ai.bossArchetype == BasicEnemyAI.BossArchetype.Sentinel ? 4.6f : 3.9f;
        ai.fireRate = ai.bossArchetype == BasicEnemyAI.BossArchetype.Striker ? 1.48f : 1.2f;
        ai.meleeDamage = ai.bossArchetype == BasicEnemyAI.BossArchetype.Striker ? 8.5f : ai.bossArchetype == BasicEnemyAI.BossArchetype.Sentinel ? 7.5f : 9f;
        ai.stoppingDistance = ai.bossArchetype == BasicEnemyAI.BossArchetype.Striker ? 8.8f : ai.bossArchetype == BasicEnemyAI.BossArchetype.Sentinel ? 14.5f : 12f;
        ai.autoBuildTypeModel = true;
    }

    private BasicEnemyAI.EnemyType RollEnemyType(System.Random rng, ArenaMode mode)
    {
        ThemeProfile profile = ResolveThemeProfile(themeIndex);
        int total = Mathf.Max(1, profile.shooterWeight + profile.gruntWeight + profile.tankWeight + profile.flyingWeight);
        int roll = rng.Next(total);

        if (roll < profile.shooterWeight) return BasicEnemyAI.EnemyType.Shooter;
        roll -= profile.shooterWeight;
        if (roll < profile.gruntWeight) return BasicEnemyAI.EnemyType.Grunt;
        roll -= profile.gruntWeight;
        if (roll < profile.tankWeight) return BasicEnemyAI.EnemyType.Tank;
        return BasicEnemyAI.EnemyType.Flying;
    }

    private bool IsWalkableForContent(CellKind kind)
    {
        return kind == CellKind.Floor || kind == CellKind.Bridge || kind == CellKind.Platform || kind == CellKind.UpperPlatform;
    }

    private bool IsSafePuzzleCell(CellKind[,] cells, int x, int z)
    {
        if (!IsWalkableForContent(cells[x, z])) return false;
        int solid = 0;
        if (InBounds(x + 1, z) && IsWalkableForContent(cells[x + 1, z])) solid++;
        if (InBounds(x - 1, z) && IsWalkableForContent(cells[x - 1, z])) solid++;
        if (InBounds(x, z + 1) && IsWalkableForContent(cells[x, z + 1])) solid++;
        if (InBounds(x, z - 1) && IsWalkableForContent(cells[x, z - 1])) solid++;
        return solid >= 2;
    }

    private bool IsReliableEnemyCell(CellKind[,] cells, int x, int z)
    {
        if (!IsWalkableForContent(cells[x, z]))
            return false;

        int accessibleNeighbors = 0;
        Vector2Int cell = new Vector2Int(x, z);
        foreach (Vector2Int neighbor in GetNeighbors(cell))
        {
            if (!InBounds(neighbor.x, neighbor.y)) continue;
            if (!IsWalkableForContent(cells[neighbor.x, neighbor.y])) continue;
            if (!CanTraverseCells(cell, neighbor)) continue;
            accessibleNeighbors++;
        }

        if (cells[x, z] == CellKind.UpperPlatform)
            return accessibleNeighbors >= 2;

        if (cells[x, z] == CellKind.Platform || cells[x, z] == CellKind.Bridge)
            return accessibleNeighbors >= 1;

        return accessibleNeighbors >= 2;
    }

    private bool HasGroundPathBetween(Vector2Int start, Vector2Int end, int minPoints)
    {
        if (!InBounds(start.x, start.y) || !InBounds(end.x, end.y))
            return false;

        List<Vector2Int> path = FindPath(start, end);
        return path != null && path.Count >= Mathf.Max(1, minPoints);
    }

    private void SpawnPuzzleTerminal(Transform root, CellKind[,] cells, Vector2Int cell, int index, System.Random rng)
    {
        float y = GetCellHeight(cells[cell.x, cell.y]);
        Vector3 pos = CellCenter(cell.x, cell.y, y + 0.25f);
        GameObject terminal = CreateCube(root, $"PuzzleTerminal_{index + 1}", pos, new Vector3(1.05f, 1.55f, 0.62f), puzzleMaterial);
        terminal.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        CybergrindPuzzleTerminal t = terminal.AddComponent<CybergrindPuzzleTerminal>();
        t.terminalSeed = rng.Next();
        t.challengeMode = (CybergrindPuzzleTerminal.ChallengeMode)(arenaMode == ArenaMode.Shop ? 0 : Mathf.Abs((t.terminalSeed + index * 11) % 10));
        t.sequenceIndex = index;
        t.requiredPresses = Mathf.Clamp(2 + rng.Next(0, 5), 2, 7);
        t.timingWindow = Mathf.Lerp(0.18f, 0.45f, (float)rng.NextDouble());
        t.requiredDelay = Mathf.Lerp(0.45f, 1.2f, (float)rng.NextDouble());
        t.holdDuration = Mathf.Lerp(1.0f, 3.0f, (float)rng.NextDouble());
        t.pulseSpeed = Mathf.Lerp(2.0f, 4.0f, (float)rng.NextDouble());
        t.calibrationDelay = Mathf.Lerp(0.45f, 1.2f, (float)rng.NextDouble());
        t.overridePrompt = GetTerminalPrompt(t.challengeMode, index);
        t.highlightRenderer = terminal.GetComponent<Renderer>();
    }

    private void SpawnPickup(Transform root, CellKind[,] cells, Vector2Int cell, CybergrindPickup.PickupType pickupType)
    {
        float y = GetCellHeight(cells[cell.x, cell.y]);
        string pickupName = pickupType == CybergrindPickup.PickupType.Health ? "Health Pickup" : "Coin Pickup";
        PrimitiveType shape = pickupType == CybergrindPickup.PickupType.Health ? PrimitiveType.Cube : PrimitiveType.Cylinder;
        GameObject pickup = CreatePrimitive(root, pickupName, shape, CellCenter(cell.x, cell.y, y + 0.85f), new Vector3(0.62f, 0.62f, 0.62f), itemMaterial, true);
        CybergrindPickup pickupComponent = pickup.AddComponent<CybergrindPickup>();
        pickupComponent.pickupType = pickupType;
        Collider trigger = pickup.GetComponent<Collider>();
        if (trigger != null) trigger.isTrigger = true;

        if (pickupType == CybergrindPickup.PickupType.Health)
        {
            CreateCube(pickup.transform, "Health Cross H", CellCenter(cell.x, cell.y, y + 0.86f), new Vector3(0.86f, 0.12f, 0.12f), spawnMaterial, false);
            CreateCube(pickup.transform, "Health Cross V", CellCenter(cell.x, cell.y, y + 0.86f), new Vector3(0.12f, 0.86f, 0.12f), spawnMaterial, false);
        }
    }

    private void SpawnArchitecturalContent(Transform root, CellKind[,] cells, System.Random rng)
    {
        ThemeProfile profile = ResolveThemeProfile(themeIndex);
        SpawnBridgeRailings(root, cells);
        SpawnStairsAndParkour(root, cells);
        SpawnGateFrames(root, cells);
        SpawnMegaPillars(root, cells, rng);

        List<Vector2Int> platforms = new List<Vector2Int>();
        for (int x = 2; x < width - 2; x++)
        {
            for (int z = 2; z < length - 2; z++)
            {
                if (cells[x, z] == CellKind.Platform || cells[x, z] == CellKind.Bridge || cells[x, z] == CellKind.UpperPlatform)
                    platforms.Add(new Vector2Int(x, z));
            }
        }

        Shuffle(platforms, rng);
        int pylonCount = Mathf.Clamp((width * length) / 145, 6, 16) + profile.extraPylons;
        for (int i = 0; i < platforms.Count && pylonCount > 0; i += 5)
        {
            Vector2Int cell = platforms[i];
            if (cells[cell.x, cell.y] == CellKind.Spawn || cells[cell.x, cell.y] == CellKind.Exit) continue;
            float y = GetCellHeight(cells[cell.x, cell.y]);
            CreateCube(root, $"ArenaPylon_{pylonCount}", CellCenter(cell.x, cell.y, y + 2.6f), new Vector3(0.95f, 5.2f, 0.95f), darkMaterial);
            CreateCube(root, $"PylonGlow_{pylonCount}", CellCenter(cell.x, cell.y, y + 5.25f), new Vector3(1.65f, 0.14f, 1.65f), accentMaterial, false);
            CreateCube(root, $"PylonCore_{pylonCount}", CellCenter(cell.x, cell.y, y + 2.6f), new Vector3(0.28f, 5.0f, 0.28f), accentMaterial, false);
            pylonCount--;
        }

        int jumpPadCount = Mathf.Clamp((width * length) / 260, 2, 6) + profile.extraJumpPads;
        for (int i = 2; i < platforms.Count && jumpPadCount > 0; i += 7)
        {
            Vector2Int cell = platforms[i];
            float y = GetCellHeight(cells[cell.x, cell.y]);
            GameObject pad = CreateCube(root, $"JumpPad_{jumpPadCount}", CellCenter(cell.x, cell.y, y + 0.1f), new Vector3(tileSize * 0.55f, 0.16f, tileSize * 0.55f), accentMaterial);
            JumpPad jumpPad = pad.AddComponent<JumpPad>();
            jumpPad.launchHeight = 11f;
            jumpPad.forwardMomentumBoost = 7f;
            Collider trigger = pad.GetComponent<Collider>();
            if (trigger != null) trigger.isTrigger = true;
            jumpPadCount--;
        }
    }

    private void SpawnAtmosphereFX(Transform root, System.Random rng)
    {
        ThemeProfile profile = ResolveThemeProfile(themeIndex);
        GameObject fxRoot = new GameObject("ArenaAtmosphereFX");
        fxRoot.transform.SetParent(root, false);
        fxRoot.transform.localPosition = new Vector3(width * tileSize * 0.5f, 4f, length * tileSize * 0.5f);

        ParticleSystem dust = CreateAtmosphereEmitter(fxRoot.transform, "DustField", 22f, 0.22f, profile.dustColor, new Vector3(width * tileSize * 0.85f, 1.2f, length * tileSize * 0.85f), true);
        dust.transform.localPosition = new Vector3(0f, 3.2f, 0f);

        if (arenaMode == ArenaMode.Boss)
        {
            ParticleSystem sparks = CreateAtmosphereEmitter(fxRoot.transform, "BossSparks", 8f, 0.12f, profile.sparkColor, new Vector3(8f, 1.4f, 8f), false);
            sparks.transform.localPosition = Vector3.zero;
        }
    }

    private ParticleSystem CreateAtmosphereEmitter(Transform parent, string name, float rate, float particleLifetime, Color color, Vector3 boxSize, bool drifting)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.color = color;
            renderer.material = mat;
        }

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = particleLifetime;
        main.startSpeed = drifting ? 0.35f : 0.12f;
        main.startSize = drifting ? 0.14f : 0.08f;
        main.startColor = color;
        main.maxParticles = 1200;

        var emission = ps.emission;
        emission.rateOverTime = rate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = boxSize;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = drifting;
        if (drifting)
        {
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        }

        var noise = ps.noise;
        noise.enabled = drifting;
        noise.strength = drifting ? 0.35f : 0.08f;
        noise.frequency = 0.18f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(new Color(color.r, color.g, color.b, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(color.a, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ps.Play();
        return ps;
    }

    private string GenerateTerminalCode(System.Random rng, int index)
    {
        int value = rng.Next(1000, 9999);
        value = (value + (themeIndex * 173) + (index * 41)) % 9000 + 1000;
        return value.ToString();
    }

    private bool[] GenerateSwitchPattern(System.Random rng, int switchCount)
    {
        bool[] pattern = new bool[Mathf.Clamp(switchCount, 3, 5)];
        for (int i = 0; i < pattern.Length; i++)
            pattern[i] = rng.NextDouble() > 0.5;

        bool allSame = true;
        for (int i = 1; i < pattern.Length; i++)
        {
            if (pattern[i] != pattern[0])
            {
                allSame = false;
                break;
            }
        }

        if (allSame)
            pattern[pattern.Length - 1] = !pattern[0];

        return pattern;
    }

    private string GetTerminalPrompt(CybergrindPuzzleTerminal.ChallengeMode mode, int index)
    {
        switch (mode)
        {
            case CybergrindPuzzleTerminal.ChallengeMode.Relay:
                return $"Relay latch {index + 1}";
            case CybergrindPuzzleTerminal.ChallengeMode.Burst:
                return $"Burst-lock terminal {index + 1}";
            case CybergrindPuzzleTerminal.ChallengeMode.Rhythm:
                return $"Beat-sync terminal {index + 1}";
            case CybergrindPuzzleTerminal.ChallengeMode.Delay:
                return $"Delay-lock node {index + 1}";
            case CybergrindPuzzleTerminal.ChallengeMode.DoubleTap:
                return $"Double-tap node {index + 1}";
            case CybergrindPuzzleTerminal.ChallengeMode.Hold:
                return $"Hold steady on node {index + 1}";
            case CybergrindPuzzleTerminal.ChallengeMode.Alternating:
                return $"Cadence lock {index + 1}";
            case CybergrindPuzzleTerminal.ChallengeMode.Calibration:
                return $"Calibrate node {index + 1}";
            case CybergrindPuzzleTerminal.ChallengeMode.Pulse:
                return $"Pulse sync node {index + 1}";
            case CybergrindPuzzleTerminal.ChallengeMode.Lockstep:
                return $"Lockstep node {index + 1}";
            default:
                return $"Machine-lock node {index + 1}";
        }
    }

    private void SpawnUndersidePillars(Transform root, CellKind[,] cells)
    {
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                if (cells[x, z] == CellKind.Void) continue;
                float topY = GetCellHeight(cells[x, z]) - (pillarDepth * 0.5f) - 0.2f;
                float widthScale = cells[x, z] == CellKind.Platform || cells[x, z] == CellKind.Bridge ? tileSize * 0.82f : tileSize * 0.72f;
                CreateCube(root, $"ModularDepthPillar_{x}_{z}", CellCenter(x, z, topY), new Vector3(widthScale, pillarDepth, widthScale), darkMaterial);
            }
        }
    }

    private void SpawnRecoveryDecks(Transform root)
    {
        float deckY = -Mathf.Max(6f, levelHeight * 1.4f);
        float centerX = (width - 1) * tileSize * 0.5f;
        float centerZ = (length - 1) * tileSize * 0.5f;
        Vector3[] anchors =
        {
            new Vector3(centerX, deckY, centerZ),
            new Vector3(centerX - tileSize * 4f, deckY - 0.75f, centerZ),
            new Vector3(centerX + tileSize * 4f, deckY - 0.75f, centerZ),
            new Vector3(centerX, deckY - 0.75f, centerZ - tileSize * 4f),
            new Vector3(centerX, deckY - 0.75f, centerZ + tileSize * 4f)
        };

        for (int i = 0; i < anchors.Length; i++)
        {
            Vector3 deckPosition = anchors[i];
            CreateCube(root, $"RecoveryDeck_{i}", deckPosition, new Vector3(tileSize * 2.2f, 0.4f, tileSize * 2.2f), darkMaterial);
            CreateCube(root, $"RecoveryGlow_{i}", deckPosition + Vector3.up * 0.28f, new Vector3(tileSize * 1.3f, 0.08f, tileSize * 1.3f), accentMaterial, false);

            GameObject pad = CreateCube(root, $"RecoveryPad_{i}", deckPosition + Vector3.up * 0.42f, new Vector3(tileSize * 0.85f, 0.18f, tileSize * 0.85f), spawnMaterial);
            JumpPad jumpPad = pad.AddComponent<JumpPad>();
            jumpPad.launchHeight = (platformLevel * levelHeight) + 6f;
            jumpPad.forwardMomentumBoost = 2.5f;
            Collider trigger = pad.GetComponent<Collider>();
            if (trigger != null)
                trigger.isTrigger = true;
        }
    }

    private void RegisterArenaRecoveryPoints(CellKind[,] cells)
    {
        recoveryPoints.Clear();
        Vector2Int spawn = FindFirst(cells, CellKind.Spawn);
        Vector2Int exit = FindFirst(cells, CellKind.Exit);
        AddRecoveryPointForCell(cells, spawn);
        AddRecoveryPointForCell(cells, exit);

        int stride = Mathf.Clamp(Mathf.Min(width, length) / 5, 3, 6);
        for (int x = 2; x < width - 2; x += stride)
        {
            for (int z = 2; z < length - 2; z += stride)
            {
                if (!IsReliableEnemyCell(cells, x, z)) continue;
                if (!HasGroundPathBetween(spawn, new Vector2Int(x, z), 2)) continue;
                AddRecoveryPointForCell(cells, new Vector2Int(x, z));
            }
        }
    }

    private void AddRecoveryPointForCell(CellKind[,] cells, Vector2Int cell)
    {
        if (!InBounds(cell.x, cell.y)) return;
        if (!IsWalkableForContentCell(cell.x, cell.y)) return;

        float y = GetCellHeight(cells[cell.x, cell.y]) + playerSpawnHeight;
        Vector3 point = transform.position + CellCenter(cell.x, cell.y, y);
        for (int i = 0; i < recoveryPoints.Count; i++)
        {
            if ((recoveryPoints[i] - point).sqrMagnitude < 1f)
                return;
        }

        recoveryPoints.Add(point);
    }

    private void SpawnStairsAndParkour(Transform root, CellKind[,] cells)
    {
        int stairsMade = 0;
        for (int x = 2; x < width - 2; x++)
        {
            for (int z = 2; z < length - 2; z++)
            {
                if (!IsWalkableForContentCell(x, z)) continue;
                TryCreateStairsTo(root, cells, x, z, 1, 0, ref stairsMade);
                TryCreateStairsTo(root, cells, x, z, -1, 0, ref stairsMade);
                TryCreateStairsTo(root, cells, x, z, 0, 1, ref stairsMade);
                TryCreateStairsTo(root, cells, x, z, 0, -1, ref stairsMade);
            }
        }

        Vector2Int center = new Vector2Int(width / 2, length / 2);
        SpawnParkourCluster(root, center + new Vector2Int(-5, -4));
        SpawnParkourCluster(root, center + new Vector2Int(5, 4));
    }

    private void TryCreateStairsTo(Transform root, CellKind[,] cells, int x, int z, int dx, int dz, ref int stairsMade)
    {
        int ex = x + dx;
        int ez = z + dz;
        if (!InBounds(ex, ez)) return;
        if (!IsWalkableForContentCell(ex, ez)) return;

        float low = GetCellHeight(cells[x, z]);
        float high = GetCellHeight(cells[ex, ez]);
        if (high <= low + 1f) return;

        int steps = 6;
        var connectorPoints = new List<Vector3>(steps + 1)
        {
            CellCenter(x, z, low + 0.12f)
        };
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)(steps + 1);
            Vector3 pos = CellCenter(x, z, Mathf.Lerp(low, high, t) + 0.12f);
            pos += new Vector3(dx * tileSize * t, 0f, dz * tileSize * t);
            Vector3 scale = new Vector3(dx == 0 ? tileSize * 0.62f : tileSize * 0.34f, 0.24f, dz == 0 ? tileSize * 0.62f : tileSize * 0.34f);
            CreateCube(root, $"Step_{x}_{z}_{i}", pos, scale, darkMaterial);
            connectorPoints.Add(pos + Vector3.up * 0.18f);
        }
        connectorPoints.Add(CellCenter(ex, ez, high + 0.12f));
        RegisterTraversalConnector(new Vector2Int(x, z), new Vector2Int(ex, ez), connectorPoints);
        stairsMade++;
    }

    private void SpawnParkourCluster(Transform root, Vector2Int around)
    {
        for (int i = 0; i < 5; i++)
        {
            int x = Mathf.Clamp(around.x + i - 2, 2, width - 3);
            int z = Mathf.Clamp(around.y + ((i & 1) == 0 ? 0 : 1), 2, length - 3);
            float y = Mathf.Lerp(0.9f, (crownLevel * levelHeight) - 0.8f, i / 4f);
            CreateCube(root, $"ParkourBlock_{around.x}_{around.y}_{i}", CellCenter(x, z, y), new Vector3(tileSize * 0.7f, 0.35f, tileSize * 0.7f), darkMaterial);
        }
    }

    private void SpawnBridgeRailings(Transform root, CellKind[,] cells)
    {
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                if (cells[x, z] != CellKind.Bridge && cells[x, z] != CellKind.Platform && cells[x, z] != CellKind.UpperPlatform) continue;

                float y = GetCellHeight(cells[x, z]);
                bool northOpen = !IsSameElevatedSurface(cells, x, z, x, z + 1) &&
                                 !HasTraversalConnectorBetween(new Vector2Int(x, z), new Vector2Int(x, z + 1));
                bool southOpen = !IsSameElevatedSurface(cells, x, z, x, z - 1) &&
                                 !HasTraversalConnectorBetween(new Vector2Int(x, z), new Vector2Int(x, z - 1));
                bool eastOpen = !IsSameElevatedSurface(cells, x, z, x + 1, z) &&
                                !HasTraversalConnectorBetween(new Vector2Int(x, z), new Vector2Int(x + 1, z));
                bool westOpen = !IsSameElevatedSurface(cells, x, z, x - 1, z) &&
                                !HasTraversalConnectorBetween(new Vector2Int(x, z), new Vector2Int(x - 1, z));

                float railY = y + 0.75f;
                if (northOpen)
                    CreateCube(root, $"RailN_{x}_{z}", CellCenter(x, z, railY) + new Vector3(0f, 0f, tileSize * 0.48f), new Vector3(tileSize, 0.36f, 0.16f), darkMaterial);
                if (southOpen)
                    CreateCube(root, $"RailS_{x}_{z}", CellCenter(x, z, railY) - new Vector3(0f, 0f, tileSize * 0.48f), new Vector3(tileSize, 0.36f, 0.16f), darkMaterial);
                if (eastOpen)
                    CreateCube(root, $"RailE_{x}_{z}", CellCenter(x, z, railY) + new Vector3(tileSize * 0.48f, 0f, 0f), new Vector3(0.16f, 0.36f, tileSize), darkMaterial);
                if (westOpen)
                    CreateCube(root, $"RailW_{x}_{z}", CellCenter(x, z, railY) - new Vector3(tileSize * 0.48f, 0f, 0f), new Vector3(0.16f, 0.36f, tileSize), darkMaterial);
            }
        }
    }

    private bool IsSameElevatedSurface(CellKind[,] cells, int x, int z, int nx, int nz)
    {
        if (!InBounds(nx, nz)) return false;
        if (cells[nx, nz] == CellKind.Void) return false;
        return Mathf.Abs(GetCellHeight(cells[x, z]) - GetCellHeight(cells[nx, nz])) < 0.1f &&
               (cells[nx, nz] == CellKind.Bridge || cells[nx, nz] == CellKind.Platform || cells[nx, nz] == CellKind.UpperPlatform || cells[nx, nz] == CellKind.Spawn || cells[nx, nz] == CellKind.Exit);
    }

    private bool HasTraversalConnectorBetween(Vector2Int from, Vector2Int to)
    {
        if (!InBounds(from.x, from.y) || !InBounds(to.x, to.y))
            return false;
        return traversalConnectors.ContainsKey(EncodeTraversalKey(from, to)) ||
               traversalConnectors.ContainsKey(EncodeTraversalKey(to, from));
    }

    private void SpawnGateFrames(Transform root, CellKind[,] cells)
    {
        Vector2Int center = new Vector2Int(width / 2, length / 2);
        float h = 8.5f;
        float span = tileSize * Mathf.Clamp(centralPlatformRadius * 2 + 1, 7, 13);

        CreateGateAtCell(root, cells, "NorthGate", center.x, center.y + centralPlatformRadius + 2, span, h, true);
        CreateGateAtCell(root, cells, "SouthGate", center.x, center.y - centralPlatformRadius - 2, span, h, true);
        CreateGateAtCell(root, cells, "EastGate", center.x + centralPlatformRadius + 2, center.y, span, h, false);
        CreateGateAtCell(root, cells, "WestGate", center.x - centralPlatformRadius - 2, center.y, span, h, false);
    }

    private void CreateGateAtCell(Transform root, CellKind[,] cells, string name, int x, int z, float span, float height, bool horizontal)
    {
        x = Mathf.Clamp(x, 1, width - 2);
        z = Mathf.Clamp(z, 1, length - 2);
        if (!IsWalkableForContent(cells[x, z]))
            return;

        float y = GetCellHeight(cells[x, z]) + 0.04f;
        CreateGate(root, name, CellCenter(x, z, y), span, height, horizontal);
    }

    private void CreateGate(Transform root, string name, Vector3 center, float span, float height, bool horizontal)
    {
        Vector3 sideOffset = horizontal ? new Vector3(span * 0.5f, 0f, 0f) : new Vector3(0f, 0f, span * 0.5f);
        Vector3 legScale = new Vector3(0.75f, height, 0.75f);
        Vector3 beamScale = horizontal ? new Vector3(span + 1.5f, 0.55f, 0.75f) : new Vector3(0.75f, 0.55f, span + 1.5f);

        CreateCube(root, $"{name}_LegA", center - sideOffset + Vector3.up * (height * 0.5f), legScale, darkMaterial);
        CreateCube(root, $"{name}_LegB", center + sideOffset + Vector3.up * (height * 0.5f), legScale, darkMaterial);
        CreateCube(root, $"{name}_TopBeam", center + Vector3.up * height, beamScale, darkMaterial);
        CreateCube(root, $"{name}_Glow", center + Vector3.up * (height - 0.35f), horizontal ? new Vector3(span, 0.12f, 0.12f) : new Vector3(0.12f, 0.12f, span), accentMaterial, false);
    }

    private void SpawnMegaPillars(Transform root, CellKind[,] cells, System.Random rng)
    {
        Vector2Int[] anchors =
        {
            new Vector2Int(Mathf.Max(3, width / 5), Mathf.Max(3, length / 5)),
            new Vector2Int(Mathf.Min(width - 4, width - width / 5), Mathf.Max(3, length / 5)),
            new Vector2Int(Mathf.Max(3, width / 5), Mathf.Min(length - 4, length - length / 5)),
            new Vector2Int(Mathf.Min(width - 4, width - width / 5), Mathf.Min(length - 4, length - length / 5))
        };

        for (int i = 0; i < anchors.Length; i++)
        {
            Vector2Int a = anchors[i];
            if (!InBounds(a.x, a.y) || cells[a.x, a.y] == CellKind.Void) continue;

            float y = GetCellHeight(cells[a.x, a.y]);
            float pillarHeight = 10f + (float)rng.NextDouble() * 4f;
            CreateCube(root, $"MegaPillar_{i}_Core", CellCenter(a.x, a.y, y + pillarHeight * 0.5f), new Vector3(tileSize * 0.88f, pillarHeight, tileSize * 0.88f), darkMaterial);
            CreateCube(root, $"MegaPillar_{i}_Crown", CellCenter(a.x, a.y, y + pillarHeight + 0.35f), new Vector3(tileSize * 1.55f, 0.7f, tileSize * 1.55f), darkMaterial);
            CreateCube(root, $"MegaPillar_{i}_VerticalGlowA", CellCenter(a.x, a.y, y + pillarHeight * 0.5f) + new Vector3(tileSize * 0.54f, 0f, 0f), new Vector3(0.1f, pillarHeight * 0.82f, 0.1f), accentMaterial, false);
            CreateCube(root, $"MegaPillar_{i}_VerticalGlowB", CellCenter(a.x, a.y, y + pillarHeight * 0.5f) - new Vector3(tileSize * 0.54f, 0f, 0f), new Vector3(0.1f, pillarHeight * 0.82f, 0.1f), accentMaterial, false);
        }
    }

    private void SpawnShopStalls(Transform root, CellKind[,] cells)
    {
        Vector2Int center = new Vector2Int(width / 2, length / 2);
        CybergrindRunState runState = CybergrindRunState.GetOrCreate();
        bool heavyAvailable = runState.shotgunUnlockedThisRun && (runState.heavyUnlockedThisRun || runState.floorsClearedThisRun >= 4 || runState.bossesClearedThisRun > 0);
        int[] refitPresets =
        {
            1 + Mathf.Abs(themeIndex % 2),
            runState.shotgunUnlockedThisRun ? 4 + Mathf.Abs(themeIndex % 2) : 3,
            heavyAvailable ? (runState.heavyUnlockedThisRun ? 7 + Mathf.Abs(themeIndex % 2) : 6) : (runState.shotgunUnlockedThisRun ? 5 : 3)
        };

        string[] refitLabels = { "PISTOL", "SHOTGUN", heavyAvailable ? "HEAVY" : "SHOTGUN ALT" };

        for (int i = 0; i < refitPresets.Length; i++)
        {
            Vector2Int cell = new Vector2Int(center.x + ((i - 1) * 3), center.y);
            float y = GetCellHeight(cells[cell.x, cell.y]);
            int presetIndex = Mathf.Clamp(refitPresets[i], 0, 8);
            GameObject stall = CreateCube(root, $"ShopDisplay_{presetIndex}", CellCenter(cell.x, cell.y, y + 0.65f), new Vector3(2.4f, 1.3f, 1.0f), itemMaterial);
            CybergrindShopStation shop = stall.AddComponent<CybergrindShopStation>();
            shop.service = CybergrindShopStation.ShopService.Refit;
            shop.presetIndex = presetIndex;
            shop.cost = i == 0 ? 2 : (i == 1 ? 4 : (heavyAvailable ? 6 : 4));
            shop.displayRenderer = stall.GetComponent<Renderer>();

            CreateCube(root, $"ShopBlade_{presetIndex}", CellCenter(cell.x, cell.y, y + 1.6f), new Vector3(0.18f, 1.4f, 0.18f), accentMaterial, false);
            BuildShopStationModel(stall.transform, CybergrindShopStation.ShopService.Refit, refitLabels[i]);
            BuildShopWeaponHologram(stall.transform, presetIndex, refitLabels[i]);
            CreateShopDescriptionLabel(stall.transform, refitLabels[i], shop.cost <= 0 ? "FREE" : $"{shop.cost} COINS", new Color(0.76f, 0.88f, 1f));
        }

        float serviceY = GetCellHeight(cells[center.x, center.y]);
        GameObject repair = CreateCube(root, "ShopRepairStation", CellCenter(center.x - 5, center.y - 2, serviceY + 0.7f), new Vector3(1.8f, 1.4f, 1.2f), spawnMaterial);
        CybergrindShopStation repairStation = repair.AddComponent<CybergrindShopStation>();
        repairStation.service = CybergrindShopStation.ShopService.Repair;
        repairStation.cost = 3 + Mathf.Min(2, runState.bossesClearedThisRun);
        repairStation.healAmount = 45;
        repairStation.displayRenderer = repair.GetComponent<Renderer>();
        BuildShopStationModel(repair.transform, CybergrindShopStation.ShopService.Repair, "HEAL");
        CreateShopDescriptionLabel(repair.transform, "HEAL", $"+{repairStation.healAmount} HP // {repairStation.cost} COINS", new Color(0.70f, 1f, 0.84f));

        GameObject overclock = CreateCube(root, "ShopOverclockStation", CellCenter(center.x + 5, center.y - 2, serviceY + 0.7f), new Vector3(1.8f, 1.4f, 1.2f), puzzleMaterial);
        CybergrindShopStation overclockStation = overclock.AddComponent<CybergrindShopStation>();
        overclockStation.service = CybergrindShopStation.ShopService.Overclock;
        overclockStation.cost = 4 + Mathf.Min(2, runState.bossesClearedThisRun);
        overclockStation.healAmount = 24;
        overclockStation.displayRenderer = overclock.GetComponent<Renderer>();
        BuildShopStationModel(overclock.transform, CybergrindShopStation.ShopService.Overclock, "BOOST");
        CreateShopDescriptionLabel(overclock.transform, "BOOST", $"+FIRE +DAMAGE // {overclockStation.cost} COINS", new Color(1f, 0.82f, 0.62f));

        GameObject surge = CreateCube(root, "ShopSurgeStation", CellCenter(center.x, center.y + 3, serviceY + 0.7f), new Vector3(1.8f, 1.4f, 1.2f), accentMaterial);
        CybergrindShopStation surgeStation = surge.AddComponent<CybergrindShopStation>();
        surgeStation.service = CybergrindShopStation.ShopService.Surge;
        surgeStation.cost = 0;
        surgeStation.moveSpeedBonus = 1.8f;
        surgeStation.dashBonus = 4.5f;
        surgeStation.jumpBonus = 0.35f;
        surgeStation.displayRenderer = surge.GetComponent<Renderer>();
        BuildShopStationModel(surge.transform, CybergrindShopStation.ShopService.Surge, "MOVE");
        CreateShopDescriptionLabel(surge.transform, "MOVE", "SPEED DASH JUMP // FREE", new Color(0.92f, 0.78f, 1f));

        CreateCube(root, "ShopCentralTable", CellCenter(center.x, center.y - 4, serviceY + 0.28f), new Vector3(8.2f, 0.24f, 2.4f), darkMaterial);
        CreateCube(root, "ShopSign", CellCenter(center.x, center.y - 4, serviceY + 2.2f), new Vector3(4.4f, 0.18f, 0.24f), accentMaterial, false);
        CreateCube(root, "ShopSignPole", CellCenter(center.x, center.y - 4, serviceY + 1.1f), new Vector3(0.18f, 2.2f, 0.18f), darkMaterial);
        CreateCube(root, "ShopSurgeSpire", CellCenter(center.x, center.y + 3, serviceY + 2.4f), new Vector3(0.28f, 3.4f, 0.28f), darkMaterial);
        CreateCube(root, "ShopSurgeHalo", CellCenter(center.x, center.y + 3, serviceY + 4.05f), new Vector3(1.8f, 0.08f, 1.8f), accentMaterial, false);
        for (int i = -1; i <= 1; i++)
        {
            CreateCube(root, $"ShopCanopy_{i}", CellCenter(center.x + i * 3, center.y + 1, serviceY + 3.2f), new Vector3(2.6f, 0.16f, 2.0f), darkMaterial);
            CreateCube(root, $"ShopCanopyGlow_{i}", CellCenter(center.x + i * 3, center.y + 1, serviceY + 3.36f), new Vector3(2.2f, 0.06f, 1.6f), accentMaterial, false);
        }
    }

    private void SpawnBossArenaMarkers(Transform root, CellKind[,] cells)
    {
        Vector2Int center = new Vector2Int(width / 2, length / 2);
        float y = GetCellHeight(CellKind.Platform);
        Vector3 platformCenter = CellCenter(center.x, center.y, y + 0.22f);
        CreateCube(root, "BossArenaDais", platformCenter, new Vector3(6.4f, 0.42f, 6.4f), hazardMaterial, false);
        CreateCube(root, "BossArenaDaisInset", platformCenter + Vector3.up * 0.18f, new Vector3(4.2f, 0.1f, 4.2f), darkMaterial, false);
        CreateCube(root, "BossArenaInnerPad", platformCenter + Vector3.up * 0.28f, new Vector3(2.5f, 0.08f, 2.5f), accentMaterial, false);
        CreateCube(root, "BossArenaNorthArch", platformCenter + new Vector3(0f, 2.4f, 3.4f), new Vector3(5.2f, 0.22f, 0.28f), darkMaterial, false);
        CreateCube(root, "BossArenaSouthArch", platformCenter + new Vector3(0f, 2.4f, -3.4f), new Vector3(5.2f, 0.22f, 0.28f), darkMaterial, false);
        CreateCube(root, "BossArenaEastArch", platformCenter + new Vector3(3.4f, 2.4f, 0f), new Vector3(0.28f, 0.22f, 5.2f), darkMaterial, false);
        CreateCube(root, "BossArenaWestArch", platformCenter + new Vector3(-3.4f, 2.4f, 0f), new Vector3(0.28f, 0.22f, 5.2f), darkMaterial, false);
        CreateCube(root, "BossArenaGlowRingA", platformCenter + Vector3.up * 0.26f, new Vector3(6.9f, 0.08f, 0.24f), accentMaterial, false);
        CreateCube(root, "BossArenaGlowRingB", platformCenter + Vector3.up * 0.26f, new Vector3(0.24f, 0.08f, 6.9f), accentMaterial, false);
        for (int i = 0; i < 4; i++)
        {
            Vector3 offset = i switch
            {
                0 => new Vector3(2.85f, 0f, 2.85f),
                1 => new Vector3(-2.85f, 0f, 2.85f),
                2 => new Vector3(2.85f, 0f, -2.85f),
                _ => new Vector3(-2.85f, 0f, -2.85f)
            };
            CreateCube(root, $"BossArenaPylon_{i}", platformCenter + offset + Vector3.up * 1.45f, new Vector3(0.42f, 2.9f, 0.42f), darkMaterial, false);
            CreateCube(root, $"BossArenaPylonGlow_{i}", platformCenter + offset + Vector3.up * 2.45f, new Vector3(0.16f, 0.78f, 0.16f), accentMaterial, false);
        }
    }

    private void BuildShopStationModel(Transform parent, CybergrindShopStation.ShopService service, string label)
    {
        Material glow = accentMaterial != null ? accentMaterial : itemMaterial;
        Material body = darkMaterial != null ? darkMaterial : floorMaterial;

        CreateChildCube(parent, $"{label}_FootA", new Vector3(0f, -0.62f, 0f), new Vector3(1.85f, 0.12f, 1.2f), body, false);
        CreateChildCube(parent, $"{label}_FootGlow", new Vector3(0f, -0.52f, 0f), new Vector3(1.52f, 0.05f, 0.86f), glow, false);
        CreateChildCube(parent, $"{label}_BaseTrim", Vector3.up * 0.82f, new Vector3(1.45f, 0.12f, 0.82f), body, false);
        CreateChildCube(parent, $"{label}_BackPlate", new Vector3(0f, 1.38f, -0.54f), new Vector3(1.32f, 1.42f, 0.12f), body, false);
        CreateChildCube(parent, $"{label}_BackGlow", new Vector3(0f, 1.38f, -0.46f), new Vector3(1.02f, 1.08f, 0.05f), glow, false);
        CreateChildCube(parent, $"{label}_SignPole", Vector3.up * 1.95f, new Vector3(0.12f, 1.2f, 0.12f), body, false);
        CreateChildCube(parent, $"{label}_Sign", Vector3.up * 2.7f, new Vector3(1.35f, 0.18f, 0.16f), glow, false);
        CreateChildCube(parent, $"{label}_LightColumn", new Vector3(0f, 1.42f, 0.08f), new Vector3(0.09f, 1.36f, 0.09f), glow, false);

        switch (service)
        {
            case CybergrindShopStation.ShopService.Repair:
                CreateChildCube(parent, "RepairCrossH", new Vector3(0f, 1.45f, 0.62f), new Vector3(0.82f, 0.12f, 0.12f), glow, false);
                CreateChildCube(parent, "RepairCrossV", new Vector3(0f, 1.45f, 0.62f), new Vector3(0.12f, 0.72f, 0.12f), glow, false);
                CreateChildCube(parent, "RepairVial", new Vector3(0.5f, 1.18f, 0.58f), new Vector3(0.18f, 0.62f, 0.18f), glow, false);
                CreateChildCube(parent, "RepairVialCap", new Vector3(0.5f, 1.54f, 0.58f), new Vector3(0.28f, 0.08f, 0.28f), body, false);
                break;
            case CybergrindShopStation.ShopService.Overclock:
                CreateChildCube(parent, "OverclockFinL", new Vector3(-0.42f, 1.55f, 0.54f), new Vector3(0.12f, 0.86f, 0.16f), glow, false);
                CreateChildCube(parent, "OverclockFinR", new Vector3(0.42f, 1.55f, 0.54f), new Vector3(0.12f, 0.86f, 0.16f), glow, false);
                CreateChildCube(parent, "OverclockCore", new Vector3(0f, 1.42f, 0.62f), new Vector3(0.48f, 0.48f, 0.12f), glow, false);
                CreateChildCube(parent, "OverclockNeedle", new Vector3(0f, 1.72f, 0.66f), new Vector3(0.08f, 0.72f, 0.08f), body, false);
                break;
            case CybergrindShopStation.ShopService.Surge:
                CreateChildCube(parent, "SurgeSpine", new Vector3(0f, 1.75f, 0.54f), new Vector3(0.18f, 1.2f, 0.18f), glow, false);
                CreateChildCube(parent, "SurgeHalo", new Vector3(0f, 2.18f, 0.54f), new Vector3(0.92f, 0.05f, 0.92f), glow, false);
                CreateChildCube(parent, "SurgeArrow", new Vector3(0f, 1.18f, 0.62f), new Vector3(0.36f, 0.36f, 0.1f), glow, false);
                CreateChildCube(parent, "SurgeArrowStem", new Vector3(0f, 0.95f, 0.62f), new Vector3(0.12f, 0.48f, 0.1f), glow, false);
                break;
            default:
                CreateChildCube(parent, "RefitBarrel", new Vector3(0f, 1.55f, 0.58f), new Vector3(0.22f, 0.82f, 0.22f), glow, false);
                CreateChildCube(parent, "RefitCradle", new Vector3(0f, 1.24f, 0.58f), new Vector3(0.92f, 0.12f, 0.28f), body, false);
                CreateChildCube(parent, "RefitGrip", new Vector3(-0.28f, 1.0f, 0.55f), new Vector3(0.16f, 0.48f, 0.16f), body, false);
                CreateChildCube(parent, "RefitSight", new Vector3(0.25f, 1.8f, 0.57f), new Vector3(0.32f, 0.08f, 0.14f), glow, false);
                break;
        }

        if (service != CybergrindShopStation.ShopService.Refit)
        {
            CreateWorldLabel(parent, $"{label}_Label", label, new Vector3(0f, 2.72f, 0.22f), service switch
            {
                CybergrindShopStation.ShopService.Repair => new Color(0.70f, 1f, 0.84f),
                CybergrindShopStation.ShopService.Overclock => new Color(1f, 0.82f, 0.62f),
                _ => new Color(0.92f, 0.78f, 1f)
            });
        }
    }

    private void BuildShopWeaponHologram(Transform parent, int presetIndex, string label)
    {
        Color color = presetIndex < 3
            ? new Color(0.72f, 0.95f, 1f, 0.86f)
            : presetIndex < 6
                ? new Color(1f, 0.72f, 0.42f, 0.86f)
                : new Color(0.9f, 0.62f, 1f, 0.86f);
        Material holo = BuildHologramMaterial(color);

        float length = presetIndex < 3 ? 0.95f : presetIndex < 6 ? 1.25f : 1.5f;
        float width = presetIndex < 3 ? 0.22f : presetIndex < 6 ? 0.36f : 0.42f;
        GameObject holoRoot = new GameObject($"{label}_HologramModel");
        holoRoot.transform.SetParent(parent, false);
        holoRoot.transform.localPosition = new Vector3(0f, 2.18f, 0.58f);
        holoRoot.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
        ArenaPulseFx pulse = holoRoot.AddComponent<ArenaPulseFx>();
        pulse.scalePulse = 0.035f;
        pulse.pulseSpeed = 2.2f;
        pulse.rotationDegreesPerSecond = new Vector3(0f, 38f, 0f);
        pulse.emissionColor = color;
        pulse.emissionStrength = 1.5f;
        pulse.emissionPulse = 0.45f;

        CreateChildCube(holoRoot.transform, $"{label}_HoloBody", new Vector3(0f, 0f, 0f), new Vector3(width, 0.2f, length), holo, false);
        CreateChildCube(holoRoot.transform, $"{label}_HoloBarrel", new Vector3(0f, 0.02f, length * 0.55f), new Vector3(width * 0.45f, 0.12f, 0.44f), holo, false);
        CreateChildCube(holoRoot.transform, $"{label}_HoloGrip", new Vector3(-width * 0.8f, -0.3f, -0.22f), new Vector3(0.16f, 0.48f, 0.16f), holo, false);
        CreateChildCube(holoRoot.transform, $"{label}_HoloCore", new Vector3(width * 0.95f, 0.02f, -0.16f), new Vector3(0.12f, 0.38f, 0.38f), holo, false);
        CreateChildCube(holoRoot.transform, $"{label}_HoloSight", new Vector3(0f, 0.24f, -length * 0.18f), new Vector3(width * 0.65f, 0.08f, 0.24f), holo, false);
        CreateChildCube(holoRoot.transform, $"{label}_HoloRing", new Vector3(0f, -0.52f, 0f), new Vector3(1.25f, 0.035f, 1.25f), holo, false);
    }

    private void CreateShopDescriptionLabel(Transform parent, string title, string detail, Color color)
    {
        CreateWorldLabel(parent, $"{title}_ShopDescription", $"{title}\n{detail}", new Vector3(0f, 3.15f, -0.55f), color);
    }

    private Material BuildHologramMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
        mat.name = "ShopWeaponHologram";
        mat.color = color;
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 2.4f);
        }
        return mat;
    }

    private GameObject CreateChildCube(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = scale;

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;

        if (!collider)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
        }

        return go;
    }

    private void CreateWorldLabel(Transform parent, string name, string text, Vector3 localPosition, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;

        TextMesh mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.fontSize = 56;
        mesh.characterSize = 0.08f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void Shuffle(List<Vector2Int> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Vector2Int temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private void PlacePlayer(CellKind[,] cells)
    {
        if (playerToPlace == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerToPlace = player.transform;
        }

        if (playerToPlace == null) return;

        Vector2Int spawn = FindFirst(cells, CellKind.Spawn);
        float y = GetCellHeight(CellKind.Spawn) + playerSpawnHeight;
        CharacterController controller = playerToPlace.GetComponent<CharacterController>();
        bool hadController = controller != null;
        if (hadController)
            controller.enabled = false;

        playerToPlace.position = transform.position + CellCenter(spawn.x, spawn.y, y);
        playerToPlace.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

        PlayerController playerController = playerToPlace.GetComponent<PlayerController>();
        if (playerController != null)
            playerController.NotifySpawnPlacement(playerToPlace.position);

        if (hadController)
            controller.enabled = true;
    }

    public void PlacePlayerAtSpawn()
    {
        if (lastCells != null)
            PlacePlayer(lastCells);
    }

    public bool TryGetRecoveryPosition(Vector3 fromWorld, out Vector3 recoveryPosition)
    {
        recoveryPosition = Vector3.zero;
        if (recoveryPoints.Count == 0)
        {
            if (lastCells == null || !InBounds(lastSpawnCell.x, lastSpawnCell.y))
                return false;

            float spawnY = GetCellHeight(lastCells[lastSpawnCell.x, lastSpawnCell.y]) + playerSpawnHeight;
            recoveryPosition = transform.position + CellCenter(lastSpawnCell.x, lastSpawnCell.y, spawnY);
            return true;
        }

        float bestDistance = float.MaxValue;
        for (int i = 0; i < recoveryPoints.Count; i++)
        {
            float distance = (recoveryPoints[i] - fromWorld).sqrMagnitude;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            recoveryPosition = recoveryPoints[i];
        }

        return true;
    }

    private Vector2Int FindFirst(CellKind[,] cells, CellKind kind)
    {
        for (int x = 0; x < width; x++)
            for (int z = 0; z < length; z++)
                if (cells[x, z] == kind)
                    return new Vector2Int(x, z);
        return new Vector2Int(width / 2, 2);
    }

    public bool TryBuildGroundPath(Vector3 startWorld, Vector3 endWorld, out List<Vector3> worldPath)
    {
        worldPath = new List<Vector3>();
        if (lastCells == null)
            return false;

        Vector2Int start = WorldToCell(startWorld);
        Vector2Int end = WorldToCell(endWorld);
        start = FindNearestWalkable(start);
        end = FindNearestWalkable(end);
        if (!InBounds(start.x, start.y) || !InBounds(end.x, end.y))
            return false;

        List<Vector2Int> cellsPath = FindPath(start, end);
        if (cellsPath == null || cellsPath.Count == 0)
            return false;

        AppendPathPoint(worldPath, GetNavigationPointForCell(cellsPath[0]));

        for (int i = 1; i < cellsPath.Count; i++)
        {
            Vector2Int from = cellsPath[i - 1];
            Vector2Int to = cellsPath[i];

            if (TryGetTraversalConnector(from, to, out List<Vector3> connector) && connector != null && connector.Count > 0)
            {
                for (int j = 0; j < connector.Count; j++)
                    AppendPathPoint(worldPath, transform.position + connector[j]);
            }
            else
            {
                AppendPathPoint(worldPath, GetNavigationPointForCell(to));
            }
        }

        if (worldPath.Count == 1)
            worldPath.Add(worldPath[0] + Vector3.forward * 0.01f);

        return true;
    }

    private Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 local = world - transform.position;
        return new Vector2Int(Mathf.RoundToInt(local.x / tileSize), Mathf.RoundToInt(local.z / tileSize));
    }

    private Vector2Int FindNearestWalkable(Vector2Int cell)
    {
        if (IsWalkableForContentCell(cell.x, cell.y))
            return cell;

        int searchRadius = Mathf.Max(width, length);
        for (int radius = 1; radius <= searchRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;
                    int x = cell.x + dx;
                    int z = cell.y + dz;
                    if (InBounds(x, z) && IsWalkableForContentCell(x, z))
                        return new Vector2Int(x, z);
                }
            }
        }

        return new Vector2Int(Mathf.Clamp(cell.x, 1, width - 2), Mathf.Clamp(cell.y, 1, length - 2));
    }

    private bool IsWalkableForContentCell(int x, int z)
    {
        if (!InBounds(x, z) || lastCells == null) return false;
        CellKind kind = lastCells[x, z];
        return kind == CellKind.Floor || kind == CellKind.Bridge || kind == CellKind.Platform || kind == CellKind.UpperPlatform || kind == CellKind.Spawn || kind == CellKind.Exit;
    }

    private List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        var open = new List<Vector2Int> { start };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, int> { [start] = 0 };
        var fScore = new Dictionary<Vector2Int, int> { [start] = Heuristic(start, goal) };

        while (open.Count > 0)
        {
            Vector2Int current = open[0];
            int bestScore = fScore.TryGetValue(current, out int currentScore) ? currentScore : int.MaxValue;
            for (int i = 1; i < open.Count; i++)
            {
                Vector2Int candidate = open[i];
                int candidateScore = fScore.TryGetValue(candidate, out int s) ? s : int.MaxValue;
                if (candidateScore < bestScore)
                {
                    current = candidate;
                    bestScore = candidateScore;
                }
            }

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            open.Remove(current);

            foreach (Vector2Int neighbor in GetNeighbors(current))
            {
                if (!IsWalkableForContentCell(neighbor.x, neighbor.y)) continue;
                if (!CanTraverseCells(current, neighbor)) continue;
                int tentative = gScore[current] + GetTraversalCost(current, neighbor);
                if (!gScore.TryGetValue(neighbor, out int neighborScore) || tentative < neighborScore)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentative;
                    fScore[neighbor] = tentative + Heuristic(neighbor, goal);
                    if (!open.Contains(neighbor))
                        open.Add(neighbor);
                }
            }
        }

        return null;
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Vector2Int> path = new List<Vector2Int> { current };
        while (cameFrom.TryGetValue(current, out Vector2Int previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private IEnumerable<Vector2Int> GetNeighbors(Vector2Int cell)
    {
        yield return new Vector2Int(cell.x + 1, cell.y);
        yield return new Vector2Int(cell.x - 1, cell.y);
        yield return new Vector2Int(cell.x, cell.y + 1);
        yield return new Vector2Int(cell.x, cell.y - 1);
        yield return new Vector2Int(cell.x + 1, cell.y + 1);
        yield return new Vector2Int(cell.x + 1, cell.y - 1);
        yield return new Vector2Int(cell.x - 1, cell.y + 1);
        yield return new Vector2Int(cell.x - 1, cell.y - 1);
    }

    private bool CanTraverseCells(Vector2Int from, Vector2Int to)
    {
        if (!InBounds(from.x, from.y) || !InBounds(to.x, to.y) || lastCells == null)
            return false;

        bool diagonal = from.x != to.x && from.y != to.y;
        if (diagonal)
        {
            if (!IsWalkableForContentCell(from.x, to.y) || !IsWalkableForContentCell(to.x, from.y))
                return false;
        }

        float fromY = GetCellHeight(lastCells[from.x, from.y]);
        float toY = GetCellHeight(lastCells[to.x, to.y]);
        if (Mathf.Abs(fromY - toY) < 0.1f)
            return true;

        return TryGetTraversalConnector(from, to, out _);
    }

    private int GetTraversalCost(Vector2Int from, Vector2Int to)
    {
        bool diagonal = from.x != to.x && from.y != to.y;
        float fromY = GetCellHeight(lastCells[from.x, from.y]);
        float toY = GetCellHeight(lastCells[to.x, to.y]);
        int baseCost = diagonal ? 14 : 10;
        int heightPenalty = Mathf.RoundToInt(Mathf.Abs(fromY - toY) / Mathf.Max(0.01f, levelHeight) * 8f);
        return baseCost + heightPenalty;
    }

    private int Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private GameObject CreateCube(Transform root, string name, Vector3 position, Vector3 scale, Material material, bool collider = true)
    {
        return CreatePrimitive(root, name, PrimitiveType.Cube, position, scale, material, collider);
    }

    private GameObject CreatePrimitive(Transform root, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(primitive);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.position = transform.position + position;
        go.transform.localScale = scale;

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;

        if (!collider)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
        }

        return go;
    }

    private Vector3 CellCenter(int x, int z, float y)
    {
        return new Vector3(x * tileSize, y, z * tileSize);
    }

    private Vector3 GetNavigationPointForCell(Vector2Int cell)
    {
        float y = GetCellHeight(lastCells[cell.x, cell.y]) + 0.12f;
        return transform.position + CellCenter(cell.x, cell.y, y);
    }

    private void AppendPathPoint(List<Vector3> path, Vector3 point)
    {
        if (path.Count > 0 && Vector3.Distance(path[path.Count - 1], point) < 0.05f)
            return;

        path.Add(point);
    }

    private void RegisterTraversalConnector(Vector2Int from, Vector2Int to, List<Vector3> localPoints)
    {
        if (localPoints == null || localPoints.Count == 0)
            return;

        traversalConnectors[EncodeTraversalKey(from, to)] = new List<Vector3>(localPoints);

        var reverse = new List<Vector3>(localPoints);
        reverse.Reverse();
        traversalConnectors[EncodeTraversalKey(to, from)] = reverse;
    }

    private bool TryGetTraversalConnector(Vector2Int from, Vector2Int to, out List<Vector3> connector)
    {
        return traversalConnectors.TryGetValue(EncodeTraversalKey(from, to), out connector);
    }

    private static long EncodeTraversalKey(Vector2Int from, Vector2Int to)
    {
        unchecked
        {
            return ((long)(ushort)from.x << 48) |
                   ((long)(ushort)from.y << 32) |
                   ((long)(ushort)to.x << 16) |
                   (ushort)to.y;
        }
    }

    private float GetCellHeight(CellKind kind)
    {
        switch (kind)
        {
            case CellKind.Bridge:
                return bridgeLevel * levelHeight;
            case CellKind.Platform:
                return platformLevel * levelHeight;
            case CellKind.UpperPlatform:
                return crownLevel * levelHeight;
            case CellKind.Spawn:
            case CellKind.Exit:
                return platformLevel * levelHeight;
            default:
                return 0f;
        }
    }

    private Material GetCellMaterial(CellKind kind)
    {
        switch (kind)
        {
            case CellKind.Bridge:
            case CellKind.Platform:
            case CellKind.UpperPlatform:
                return darkMaterial;
            case CellKind.Hazard:
                return hazardMaterial;
            case CellKind.Spawn:
                return spawnMaterial;
            case CellKind.Exit:
                return exitMaterial;
            default:
                return floorMaterial;
        }
    }

    private bool InBounds(int x, int z)
    {
        return x >= 0 && z >= 0 && x < width && z < length;
    }

    private int DistanceManhattan(int x, int z, Vector2Int point)
    {
        return Mathf.Abs(x - point.x) + Mathf.Abs(z - point.y);
    }

    private void EnsureMaterials()
    {
        ThemePalette palette = ResolveThemePalette();

        floorMaterial = EnsureMaterial(floorMaterial, "Arena Floor", palette.floor, Color.black, false);
        darkMaterial = EnsureMaterial(darkMaterial, "Arena Dark", palette.dark, Color.black, false);
        accentMaterial = EnsureMaterial(accentMaterial, "Arena Accent", palette.accent, palette.accentEmission, true);
        hazardMaterial = EnsureMaterial(hazardMaterial, "Arena Hazard", palette.hazard, palette.hazardEmission, true);
        spawnMaterial = EnsureMaterial(spawnMaterial, "Arena Spawn", palette.spawn, palette.spawnEmission, true);
        exitMaterial = EnsureMaterial(exitMaterial, "Arena Exit", palette.exit, palette.exitEmission, true);
        itemMaterial = EnsureMaterial(itemMaterial, "Arena Item", palette.item, palette.itemEmission, true);
        puzzleMaterial = EnsureMaterial(puzzleMaterial, "Arena Puzzle", palette.puzzle, palette.puzzleEmission, true);
    }

    private ThemePalette ResolveThemePalette()
    {
        ThemePalette basePalette = new ThemePalette
        {
            floor = new Color(0.055f, 0.06f, 0.066f),
            dark = new Color(0.012f, 0.014f, 0.017f),
            accent = new Color(0.025f, 0.23f, 0.28f),
            accentEmission = new Color(0.0f, 0.22f, 0.30f),
            hazard = new Color(0.30f, 0.035f, 0.018f),
            hazardEmission = new Color(0.38f, 0.04f, 0.0f),
            spawn = new Color(0.02f, 0.23f, 0.10f),
            spawnEmission = new Color(0.0f, 0.25f, 0.08f),
            exit = new Color(0.28f, 0.13f, 0.025f),
            exitEmission = new Color(0.32f, 0.12f, 0.0f),
            item = new Color(0.08f, 0.17f, 0.16f),
            itemEmission = new Color(0.0f, 0.20f, 0.16f),
            puzzle = new Color(0.09f, 0.07f, 0.13f),
            puzzleEmission = new Color(0.13f, 0.05f, 0.20f)
        };

        if (!useThemePaletteVariants)
            return basePalette;

        switch (Mathf.Abs(themeIndex) % 4)
        {
            case 0:
                return basePalette;
            case 1:
                return new ThemePalette
                {
                    floor = new Color(0.05f, 0.056f, 0.07f),
                    dark = new Color(0.010f, 0.013f, 0.020f),
                    accent = new Color(0.10f, 0.15f, 0.35f),
                    accentEmission = new Color(0.07f, 0.16f, 0.42f),
                    hazard = new Color(0.33f, 0.08f, 0.03f),
                    hazardEmission = new Color(0.40f, 0.10f, 0.02f),
                    spawn = new Color(0.04f, 0.22f, 0.17f),
                    spawnEmission = new Color(0.02f, 0.26f, 0.22f),
                    exit = new Color(0.34f, 0.17f, 0.05f),
                    exitEmission = new Color(0.38f, 0.18f, 0.05f),
                    item = new Color(0.08f, 0.18f, 0.22f),
                    itemEmission = new Color(0.03f, 0.23f, 0.28f),
                    puzzle = new Color(0.11f, 0.08f, 0.18f),
                    puzzleEmission = new Color(0.16f, 0.07f, 0.26f)
                };
            case 2:
                return new ThemePalette
                {
                    floor = new Color(0.063f, 0.054f, 0.052f),
                    dark = new Color(0.020f, 0.015f, 0.015f),
                    accent = new Color(0.30f, 0.11f, 0.09f),
                    accentEmission = new Color(0.38f, 0.12f, 0.08f),
                    hazard = new Color(0.36f, 0.10f, 0.02f),
                    hazardEmission = new Color(0.46f, 0.14f, 0.01f),
                    spawn = new Color(0.09f, 0.20f, 0.08f),
                    spawnEmission = new Color(0.10f, 0.26f, 0.07f),
                    exit = new Color(0.30f, 0.18f, 0.04f),
                    exitEmission = new Color(0.36f, 0.22f, 0.04f),
                    item = new Color(0.18f, 0.14f, 0.08f),
                    itemEmission = new Color(0.24f, 0.17f, 0.06f),
                    puzzle = new Color(0.14f, 0.08f, 0.09f),
                    puzzleEmission = new Color(0.20f, 0.10f, 0.11f)
                };
            default:
                return new ThemePalette
                {
                    floor = new Color(0.050f, 0.060f, 0.050f),
                    dark = new Color(0.010f, 0.018f, 0.012f),
                    accent = new Color(0.10f, 0.28f, 0.12f),
                    accentEmission = new Color(0.08f, 0.34f, 0.14f),
                    hazard = new Color(0.22f, 0.08f, 0.02f),
                    hazardEmission = new Color(0.28f, 0.10f, 0.02f),
                    spawn = new Color(0.02f, 0.22f, 0.16f),
                    spawnEmission = new Color(0.0f, 0.28f, 0.18f),
                    exit = new Color(0.22f, 0.19f, 0.03f),
                    exitEmission = new Color(0.28f, 0.24f, 0.02f),
                    item = new Color(0.08f, 0.20f, 0.10f),
                    itemEmission = new Color(0.05f, 0.25f, 0.12f),
                    puzzle = new Color(0.06f, 0.10f, 0.09f),
                    puzzleEmission = new Color(0.07f, 0.15f, 0.14f)
                };
        }
    }

    private static ThemeProfile ResolveThemeProfile(int index)
    {
        switch (Mathf.Abs(index) % 4)
        {
            case 0:
                return new ThemeProfile
                {
                    directiveTitle = "SUPPRESSIVE LATTICE",
                    directiveDetail = "Tighter lanes, more machine locks, and crossfire pressure from precision units.",
                    outerGapMultiplier = 0.8f,
                    hazardMultiplier = 0.8f,
                    coverMultiplier = 1.15f,
                    itemMultiplier = 1f,
                    extraIslands = 0,
                    extraJumpPads = 0,
                    extraPylons = 2,
                    terminalBonus = 1,
                    shooterWeight = 45,
                    gruntWeight = 25,
                    tankWeight = 20,
                    flyingWeight = 10,
                    fogColor = new Color(0.014f, 0.018f, 0.024f),
                    skyTint = new Color(0.06f, 0.09f, 0.14f),
                    bloomTint = new Color(0.70f, 0.92f, 1f),
                    colorFilter = new Color(0.92f, 0.98f, 1f),
                    dustColor = new Color(0.55f, 0.75f, 1f, 0.16f),
                    sparkColor = new Color(0.95f, 0.38f, 0.12f, 0.45f),
                    ambientBoost = 0f
                };
            case 1:
                return new ThemeProfile
                {
                    directiveTitle = "VERTICAL DRIFT",
                    directiveDetail = "Air routes open up here. Expect more flyers, jump pads, and broken elevations.",
                    outerGapMultiplier = 1.12f,
                    hazardMultiplier = 0.72f,
                    coverMultiplier = 0.92f,
                    itemMultiplier = 1.1f,
                    extraIslands = 3,
                    extraJumpPads = 2,
                    extraPylons = 1,
                    terminalBonus = 0,
                    shooterWeight = 22,
                    gruntWeight = 22,
                    tankWeight = 14,
                    flyingWeight = 42,
                    fogColor = new Color(0.012f, 0.020f, 0.030f),
                    skyTint = new Color(0.09f, 0.14f, 0.24f),
                    bloomTint = new Color(0.55f, 0.76f, 1f),
                    colorFilter = new Color(0.84f, 0.92f, 1f),
                    dustColor = new Color(0.48f, 0.68f, 1f, 0.18f),
                    sparkColor = new Color(0.46f, 0.88f, 1f, 0.45f),
                    ambientBoost = 0.015f
                };
            case 2:
                return new ThemeProfile
                {
                    directiveTitle = "HEAT SINK",
                    directiveDetail = "Heavy chassis and hazard lanes hold the center. Space is safer than greed.",
                    outerGapMultiplier = 0.9f,
                    hazardMultiplier = 1.6f,
                    coverMultiplier = 1.05f,
                    itemMultiplier = 0.92f,
                    extraIslands = 1,
                    extraJumpPads = 0,
                    extraPylons = 4,
                    terminalBonus = 0,
                    shooterWeight = 18,
                    gruntWeight = 22,
                    tankWeight = 44,
                    flyingWeight = 16,
                    fogColor = new Color(0.022f, 0.016f, 0.016f),
                    skyTint = new Color(0.13f, 0.07f, 0.05f),
                    bloomTint = new Color(1f, 0.65f, 0.42f),
                    colorFilter = new Color(1f, 0.90f, 0.84f),
                    dustColor = new Color(0.96f, 0.52f, 0.24f, 0.14f),
                    sparkColor = new Color(1f, 0.48f, 0.16f, 0.50f),
                    ambientBoost = 0.008f
                };
            default:
                return new ThemeProfile
                {
                    directiveTitle = "OVERGROWTH NOISE",
                    directiveDetail = "More cover, more pickups, and wider flanker pressure through messy sightlines.",
                    outerGapMultiplier = 1f,
                    hazardMultiplier = 0.86f,
                    coverMultiplier = 1.5f,
                    itemMultiplier = 1.35f,
                    extraIslands = 2,
                    extraJumpPads = 1,
                    extraPylons = 2,
                    terminalBonus = 1,
                    shooterWeight = 20,
                    gruntWeight = 34,
                    tankWeight = 14,
                    flyingWeight = 32,
                    fogColor = new Color(0.012f, 0.022f, 0.016f),
                    skyTint = new Color(0.06f, 0.12f, 0.09f),
                    bloomTint = new Color(0.72f, 1f, 0.70f),
                    colorFilter = new Color(0.90f, 1f, 0.92f),
                    dustColor = new Color(0.58f, 0.90f, 0.66f, 0.16f),
                    sparkColor = new Color(0.72f, 1f, 0.64f, 0.45f),
                    ambientBoost = 0.012f
                };
        }
    }

    private Material EnsureMaterial(Material material, string name, Color baseColor, Color emission, bool emissive)
    {
        if (material == null)
            material = new Material(FindUrpShader(false)) { name = name };

        if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, baseColor);
        if (material.HasProperty(ColorId)) material.SetColor(ColorId, baseColor);

        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty(EmissionColorId)) material.SetColor(EmissionColorId, emission);
        }

        return material;
    }

    private Shader FindUrpShader(bool unlit)
    {
        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader;
    }
}
