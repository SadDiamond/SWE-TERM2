using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    [NonSerialized] public int debugLastRuntimeConnectivityRepairs;
    [NonSerialized] public int debugLastRuntimeConnectivityCulls;
    [NonSerialized] public int debugLastReconfigureDistricts;
    [NonSerialized] public string debugLastFastMovementAudit;
    [NonSerialized] public bool debugLastFastMovementAuditPassed;
    [NonSerialized] public float debugLastRouteCoverage;
    [NonSerialized] public float debugLastElevatedCoverage;
    [NonSerialized] public float debugLastRouteRecoveryCoverage;

    [Header("Performance")]
    [Range(0f, 1f)] public float decorativeDensity = 0.18f;
    [Range(0f, 1f)] public float microDetailDensity = 0.035f;

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
    [Range(0, 8)] public int reconfigureDistrictCount = 5;

    [Header("Playability")]
    [Range(0f, 0.25f)] public float outerGapChance = 0.08f;
    [Range(0f, 0.20f)] public float hazardChance = 0.05f;
    [Range(0f, 0.20f)] public float coverChance = 0.055f;
    [Range(0f, 0.20f)] public float itemChance = 0.08f;
    [Min(0)] public int safeRadiusAroundSpawn = 4;
    [Min(0)] public int safeRadiusAroundExit = 4;

    [Header("Fast Movement Layout")]
    public bool prioritizeFastMovementFlow = true;
    [Range(0.6f, 2f)] public float primaryRouteWidthScale = 1.35f;
    [Range(0f, 1f)] public float clutterSuppression = 0.82f;
    [Range(0f, 1f)] public float verticalTraversalBias = 0.72f;

    [Header("Debug")]
    public bool debugDrawFastRouteBands = true;
    public bool debugDrawTraversalConnectors = true;
    public bool debugDrawRecoveryPoints = true;

    [Header("Fast Movement Audit")]
    [Range(0.1f, 0.8f)] public float auditMinRouteCoverage = 0.26f;
    [Range(0.1f, 0.8f)] public float auditMinElevatedCoverage = 0.22f;
    [Range(0f, 1f)] public float auditMinRouteRecoveryCoverage = 0.45f;
    [Min(0)] public int auditMaxRouteClutter = 0;
    [Min(0)] public int auditMinConnectorEdges = 12;
    [Min(0)] public int auditMinFlankWalkables = 8;

    [Header("References")]
    public Transform playerToPlace;
    public float playerSpawnHeight = 3.1f;
    public string generatedRootName = "_Arena";

    [Header("Enemy Spawning")]
    public GameObject enemyPrefab;
    [Min(1f)] public float enemyHealthMultiplier = 1f;
    [Min(0)] public int enemyCountBonus;
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
    private readonly HashSet<long> stairVisualEdges = new HashSet<long>();
    private readonly Dictionary<int, Transform> districtRoots = new Dictionary<int, Transform>();
    private readonly Dictionary<int, Material> shopDisplayMaterialCache = new Dictionary<int, Material>();
    private int[,] currentDistrictMap;

    public Transform CurrentArenaRoot { get; private set; }
    public bool IsGenerating { get; private set; }
    private CellKind[,] lastCells;
    private Vector2Int lastSpawnCell;
    private Vector2Int lastExitCell;
    private float effectiveDecorativeDensity;
    private float effectiveMicroDetailDensity;
    private Volume environmentVolume;
    private Material skyboxMaterial;
    [NonSerialized] public bool skipPlayerPlacementOnce;
    private Coroutine generationRoutine;
    private Vector2Int lastLayoutCenter;
    private int lastSideLaneOffset;
    private int lastPrimaryLaneHalfWidth;
    private static Shader cachedLitShader;
    private static Shader cachedUnlitShader;

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
        public float fogDensity;
        public float shellHeight;
        public float shellInsetScale;
        public float skylineHeight;
        public float skylineDensity;
        public Color structureGlowColor;
    }

    private struct StairVisualCandidate
    {
        public int x;
        public int z;
        public int dx;
        public int dz;
        public float low;
        public float high;
        public int score;
    }

    private struct ArenaDiagnostics
    {
        public int walkable;
        public int elevated;
        public int upperPlatforms;
        public int routeCells;
        public int routeClutter;
        public int flankWalkables;
        public int recoveryCount;
        public int routeRecovery;
        public int connectorEdges;
        public int unreachable;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Start()
    {
        if (generateOnStart)
        {
            if (!PersistentLoadingScreen.IsActive)
                GenerateArena();
        }
    }

    [ContextMenu("Generate Arena")]
    public void GenerateArena()
    {
        if (generationRoutine != null)
        {
            StopCoroutine(generationRoutine);
            generationRoutine = null;
        }

        IsGenerating = true;
        var generationTimer = System.Diagnostics.Stopwatch.StartNew();
        long layoutMs;
        long cellsMs;
        long detailMs;
        long gameplayMs;
        if (clearBeforeGenerate)
            ClearArena();

        bridgeLevel = Mathf.Clamp(bridgeLevel, 1, platformLevel - 1);
        platformLevel = Mathf.Clamp(platformLevel, bridgeLevel + 1, crownLevel - 1);
        crownLevel = Mathf.Max(platformLevel + 1, crownLevel);
        recoveryPoints.Clear();
        traversalConnectors.Clear();
        stairVisualEdges.Clear();
        districtRoots.Clear();
        currentDistrictMap = null;
        RefreshEffectiveDensity();
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
        FinalizeLayoutConnectivity(cells);
        lastCells = cells;
        RepairRuntimePathConnectivity(cells);
        lastCells = cells;
        lastSpawnCell = FindFirst(cells, CellKind.Spawn);
        lastExitCell = FindFirst(cells, CellKind.Exit);
        BuildDistrictRoots(root, cells);
        layoutMs = generationTimer.ElapsedMilliseconds;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                SpawnCell(root, cells[x, z], x, z);
            }
        }
        cellsMs = generationTimer.ElapsedMilliseconds - layoutMs;

        SpawnBoundaryFrame(root);
        SpawnUndersidePillars(root, cells);
        SpawnRecoveryDecks(root);
        SpawnFloatingTrim(root, cells);
        SpawnHeightChangeFascia(root, cells);
        SpawnArchitecturalContent(root, cells, rng);
        detailMs = generationTimer.ElapsedMilliseconds - layoutMs - cellsMs;
        RegisterArenaRecoveryPoints(cells);
        SpawnGameplayContent(root, cells, rng);
        gameplayMs = generationTimer.ElapsedMilliseconds - layoutMs - cellsMs - detailMs;
        SpawnArenaLighting(root);
        SpawnStructuralShell(root, rng);
        SpawnAtmosphereFX(root, rng);
        ApplyEnvironmentFX();
        if (!skipPlayerPlacementOnce)
            PlacePlayer(cells);
        else
            skipPlayerPlacementOnce = false;

        generationTimer.Stop();
        int objectCount = root != null ? root.GetComponentsInChildren<Transform>(true).Length : 0;
        Debug.Log($"[Arena] Generated {width}x{length} seed {actualSeed} in {generationTimer.ElapsedMilliseconds}ms " +
                  $"(layout {layoutMs}ms, cells {cellsMs}ms, detail {detailMs}ms, gameplay {gameplayMs}ms, objects {objectCount}).");
        DebugAuditFastMovementLayout();
        IsGenerating = false;
    }

    public void BeginGenerateArenaAsync()
    {
        if (!gameObject.activeInHierarchy)
        {
            GenerateArena();
            return;
        }

        if (generationRoutine != null)
            StopCoroutine(generationRoutine);
        generationRoutine = StartCoroutine(RunGenerationSafely());
    }

    private IEnumerator RunGenerationSafely()
    {
        IEnumerator routine = GenerateArenaRoutine();
        System.Exception failure = null;

        while (failure == null)
        {
            bool hasNext = false;
            object yielded = null;
            try
            {
                hasNext = routine.MoveNext();
                if (hasNext)
                    yielded = routine.Current;
            }
            catch (System.Exception exception)
            {
                failure = exception;
            }

            if (!hasNext)
                break;

            yield return yielded;
        }

        if (failure != null)
            Debug.LogException(failure, this);

        IsGenerating = false;
        generationRoutine = null;
    }

    private IEnumerator GenerateArenaRoutine()
    {
        IsGenerating = true;
        var generationTimer = System.Diagnostics.Stopwatch.StartNew();
        long layoutMs;
        long cellsMs;
        long detailMs;
        long gameplayMs;
        if (clearBeforeGenerate)
            ClearArena();

        bridgeLevel = Mathf.Clamp(bridgeLevel, 1, platformLevel - 1);
        platformLevel = Mathf.Clamp(platformLevel, bridgeLevel + 1, crownLevel - 1);
        crownLevel = Mathf.Max(platformLevel + 1, crownLevel);
        recoveryPoints.Clear();
        traversalConnectors.Clear();
        stairVisualEdges.Clear();
        districtRoots.Clear();
        currentDistrictMap = null;
        RefreshEffectiveDensity();
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
        FinalizeLayoutConnectivity(cells);
        lastCells = cells;
        RepairRuntimePathConnectivity(cells);
        lastCells = cells;
        lastSpawnCell = FindFirst(cells, CellKind.Spawn);
        lastExitCell = FindFirst(cells, CellKind.Exit);
        BuildDistrictRoots(root, cells);
        layoutMs = generationTimer.ElapsedMilliseconds;
        yield return null;

        int spawnedCellsThisFrame = 0;
        const int maxCellsPerFrame = 18;
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                SpawnCell(root, cells[x, z], x, z);
                spawnedCellsThisFrame++;
                if (spawnedCellsThisFrame >= maxCellsPerFrame)
                {
                    spawnedCellsThisFrame = 0;
                    yield return null;
                }
            }
        }
        cellsMs = generationTimer.ElapsedMilliseconds - layoutMs;
        yield return null;

        SpawnBoundaryFrame(root);
        yield return null;
        SpawnUndersidePillars(root, cells);
        yield return null;
        SpawnRecoveryDecks(root);
        yield return null;
        SpawnFloatingTrim(root, cells);
        yield return null;
        SpawnHeightChangeFascia(root, cells);
        yield return null;
        SpawnArchitecturalContent(root, cells, rng);
        detailMs = generationTimer.ElapsedMilliseconds - layoutMs - cellsMs;
        yield return null;

        RegisterArenaRecoveryPoints(cells);
        yield return null;
        SpawnGameplayContent(root, cells, rng);
        gameplayMs = generationTimer.ElapsedMilliseconds - layoutMs - cellsMs - detailMs;
        yield return null;
        SpawnArenaLighting(root);
        yield return null;
        SpawnStructuralShell(root, rng);
        yield return null;
        SpawnAtmosphereFX(root, rng);
        yield return null;
        ApplyEnvironmentFX();
        if (!skipPlayerPlacementOnce)
            PlacePlayer(cells);
        else
            skipPlayerPlacementOnce = false;

        generationTimer.Stop();
        int objectCount = root != null ? root.GetComponentsInChildren<Transform>(true).Length : 0;
        Debug.Log($"[Arena] Generated {width}x{length} seed {actualSeed} in {generationTimer.ElapsedMilliseconds}ms " +
                  $"(layout {layoutMs}ms, cells {cellsMs}ms, detail {detailMs}ms, gameplay {gameplayMs}ms, objects {objectCount}).");
        DebugAuditFastMovementLayout();
        IsGenerating = false;
        generationRoutine = null;
    }

    private void FinalizeLayoutConnectivity(CellKind[,] cells)
    {
        for (int i = 0; i < 3; i++)
        {
            EnsureElevatedCellsHaveExits(cells);
            RepairLayoutConnectivity(cells);
        }

        EnsureElevatedCellsHaveExits(cells);
        RepairLayoutConnectivity(cells);
        EnsureElevatedCellsHaveExits(cells);
        RegisterAllAdjacentHeightConnectors(cells);
    }

    private void RepairRuntimePathConnectivity(CellKind[,] cells)
    {
        if (cells == null) return;
        debugLastRuntimeConnectivityRepairs = 0;
        debugLastRuntimeConnectivityCulls = 0;

        Vector2Int spawn = FindFirst(cells, CellKind.Spawn);
        Vector2Int exit = FindFirst(cells, CellKind.Exit);
        if (!InBounds(spawn.x, spawn.y))
            return;

        RegisterAllAdjacentHeightConnectors(cells);
        for (int pass = 0; pass < 48; pass++)
        {
            lastCells = cells;
            Vector2Int unreachable = FindFirstRuntimeUnreachableWalkable(cells, spawn);
            if (!InBounds(unreachable.x, unreachable.y))
                break;

            Vector2Int anchor = FindNearestRuntimeReachableCell(cells, spawn, unreachable);
            if (!InBounds(anchor.x, anchor.y))
                break;

            StampBridgeLine(cells, unreachable, anchor, 0);
            StampSafeZone(cells, spawn, safeRadiusAroundSpawn);
            StampSafeZone(cells, exit, safeRadiusAroundExit);
            cells[spawn.x, spawn.y] = CellKind.Spawn;
            cells[exit.x, exit.y] = CellKind.Exit;
            EnsureElevatedCellsHaveExits(cells);
            RegisterAllAdjacentHeightConnectors(cells);
            lastCells = cells;
            List<Vector2Int> repairedPath = FindPath(spawn, unreachable);
            if (repairedPath == null || repairedPath.Count == 0)
                cells[unreachable.x, unreachable.y] = CellKind.Void;
            debugLastRuntimeConnectivityRepairs++;
        }

        CullRuntimeUnreachableWalkablePockets(cells, spawn, exit);
    }

    private void CullRuntimeUnreachableWalkablePockets(CellKind[,] cells, Vector2Int spawn, Vector2Int exit)
    {
        lastCells = cells;
        HashSet<Vector2Int> reachable = CollectRuntimeReachable(spawn);
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                Vector2Int cell = new Vector2Int(x, z);
                if (cell == spawn || cell == exit || !IsLayoutWalkable(cells[x, z]) || reachable.Contains(cell)) continue;
                cells[x, z] = CellKind.Void;
                debugLastRuntimeConnectivityCulls++;
            }
        }

        cells[spawn.x, spawn.y] = CellKind.Spawn;
        cells[exit.x, exit.y] = CellKind.Exit;
        lastCells = cells;
    }

    private void RegisterAllAdjacentHeightConnectors(CellKind[,] cells)
    {
        if (cells == null) return;

        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                if (!IsLayoutWalkable(cells[x, z])) continue;
                TryRegisterAdjacentHeightConnector(cells, new Vector2Int(x, z), new Vector2Int(x + 1, z));
                TryRegisterAdjacentHeightConnector(cells, new Vector2Int(x, z), new Vector2Int(x, z + 1));
            }
        }
    }

    private void TryRegisterAdjacentHeightConnector(CellKind[,] cells, Vector2Int a, Vector2Int b)
    {
        if (!InBounds(a.x, a.y) || !InBounds(b.x, b.y)) return;
        if (!IsLayoutWalkable(cells[a.x, a.y]) || !IsLayoutWalkable(cells[b.x, b.y])) return;

        float heightA = GetCellHeight(cells[a.x, a.y]);
        float heightB = GetCellHeight(cells[b.x, b.y]);
        float difference = Mathf.Abs(heightA - heightB);
        if (difference < 0.1f || difference > levelHeight + 0.75f) return;

        RegisterCellHeightConnector(cells, a, b);
    }

    private Vector2Int FindFirstRuntimeUnreachableWalkable(CellKind[,] cells, Vector2Int spawn)
    {
        lastCells = cells;
        HashSet<Vector2Int> reachable = CollectRuntimeReachable(spawn);
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                if (!IsLayoutWalkable(cells[x, z])) continue;
                if (!reachable.Contains(new Vector2Int(x, z)))
                    return new Vector2Int(x, z);
            }
        }

        return new Vector2Int(-1, -1);
    }

    private Vector2Int FindNearestRuntimeReachableCell(CellKind[,] cells, Vector2Int spawn, Vector2Int from)
    {
        lastCells = cells;
        HashSet<Vector2Int> reachable = CollectRuntimeReachable(spawn);
        Vector2Int best = new Vector2Int(-1, -1);
        int bestDistance = int.MaxValue;
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                if (!IsLayoutWalkable(cells[x, z])) continue;
                if (!reachable.Contains(new Vector2Int(x, z))) continue;

                int distance = Mathf.Abs(from.x - x) + Mathf.Abs(from.y - z);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = new Vector2Int(x, z);
            }
        }

        return best;
    }

    private HashSet<Vector2Int> CollectRuntimeReachable(Vector2Int start)
    {
        HashSet<Vector2Int> reachable = new HashSet<Vector2Int>();
        if (!InBounds(start.x, start.y) || !IsWalkableForContentCell(start.x, start.y)) return reachable;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        reachable.Add(start);
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            foreach (Vector2Int neighbor in GetNeighbors(current))
            {
                if (!InBounds(neighbor.x, neighbor.y) || reachable.Contains(neighbor)) continue;
                if (!IsWalkableForContentCell(neighbor.x, neighbor.y) || !CanTraverseCells(current, neighbor)) continue;
                reachable.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }
        return reachable;
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
        environmentVolume = null;

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
        debugLastReconfigureDistricts = 0;
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

        if (arenaMode == ArenaMode.Boss)
        {
            BuildBossLayout(cells, spawn, exit);
            return cells;
        }

        BuildFastCombatLayout(cells, rng, center, spawn, exit);
        StampSafeZone(cells, spawn, safeRadiusAroundSpawn);
        StampSafeZone(cells, exit, safeRadiusAroundExit);
        cells[spawn.x, spawn.y] = CellKind.Spawn;
        cells[exit.x, exit.y] = CellKind.Exit;

        return cells;
    }

    private void BuildFlatShopLayout(CellKind[,] cells, Vector2Int spawn, Vector2Int exit)
    {
        lastLayoutCenter = new Vector2Int(width / 2, length / 2);
        lastSideLaneOffset = 0;
        lastPrimaryLaneHalfWidth = 0;
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

    private void BuildBossLayout(CellKind[,] cells, Vector2Int spawn, Vector2Int exit)
    {
        lastLayoutCenter = new Vector2Int(width / 2, length / 2);
        lastSideLaneOffset = 0;
        lastPrimaryLaneHalfWidth = Mathf.Max(1, mainBridgeHalfWidth);
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
        int arenaRadius = Mathf.Clamp(Mathf.Min(width, length) / 3, 7, 10);
        for (int x = borderInset; x < width - borderInset; x++)
        {
            for (int z = borderInset; z < length - borderInset; z++)
            {
                int dx = Mathf.Abs(x - center.x);
                int dz = Mathf.Abs(z - center.y);
                int dist = dx + dz;
                if (dist > arenaRadius + 4)
                {
                    cells[x, z] = CellKind.Void;
                    continue;
                }

                if (dx <= 3 && dz <= 3)
                    cells[x, z] = CellKind.Platform;
                else if ((dx <= 1 && dz <= arenaRadius) || (dz <= 1 && dx <= arenaRadius))
                    cells[x, z] = CellKind.Bridge;
                else
                    cells[x, z] = CellKind.Floor;
            }
        }

        StampRect(cells, center.x - 3, 2, center.x + 3, center.y - 4, CellKind.Bridge);
        StampRect(cells, center.x - 3, center.y + 4, center.x + 3, length - 3, CellKind.Bridge);
        StampRect(cells, center.x - arenaRadius, center.y - 1, center.x + arenaRadius, center.y + 1, CellKind.Bridge);
        StampRect(cells, center.x - 1, center.y - arenaRadius, center.x + 1, center.y + arenaRadius, CellKind.Bridge);
        StampRect(cells, center.x - arenaRadius, center.y - arenaRadius, center.x - arenaRadius + 2, center.y - arenaRadius + 2, CellKind.Platform);
        StampRect(cells, center.x + arenaRadius - 2, center.y - arenaRadius, center.x + arenaRadius, center.y - arenaRadius + 2, CellKind.Platform);
        StampRect(cells, center.x - arenaRadius, center.y + arenaRadius - 2, center.x - arenaRadius + 2, center.y + arenaRadius, CellKind.Platform);
        StampRect(cells, center.x + arenaRadius - 2, center.y + arenaRadius - 2, center.x + arenaRadius, center.y + arenaRadius, CellKind.Platform);
        StampRect(cells, center.x - 2, center.y - 2, center.x + 2, center.y + 2, CellKind.UpperPlatform);

        StampSafeZone(cells, spawn, safeRadiusAroundSpawn + 1);
        StampSafeZone(cells, exit, safeRadiusAroundExit + 1);
        cells[spawn.x, spawn.y] = CellKind.Spawn;
        cells[exit.x, exit.y] = CellKind.Exit;
    }

    private void BuildFastCombatLayout(CellKind[,] cells, System.Random rng, Vector2Int center, Vector2Int spawn, Vector2Int exit)
    {
        CarveVoidMoat(cells);

        int centerRadius = Mathf.Clamp(centralPlatformRadius + rng.Next(-1, 2), 3, Mathf.Max(3, Mathf.Min(width, length) / 4));
        int crownRadius = Mathf.Clamp(Mathf.RoundToInt(centerRadius * Mathf.Lerp(0.5f, 0.75f, verticalTraversalBias)), 2, centerRadius - 1);
        int primaryHalfWidth = Mathf.Clamp(Mathf.RoundToInt((mainBridgeHalfWidth + 1) * primaryRouteWidthScale), 2, 4);
        int sideLaneHalfWidth = Mathf.Clamp(primaryHalfWidth - 1, 1, 3);
        int sideLaneOffset = Mathf.Clamp(centerRadius + 4, 5, Mathf.Min(width, length) / 2 - 4);
        int loopInset = Mathf.Clamp(Mathf.Min(width, length) / 6, 4, 7);
        int cornerSize = Mathf.Clamp(cornerPlatformSize + 1, 4, 6);

        lastLayoutCenter = center;
        lastPrimaryLaneHalfWidth = primaryHalfWidth;
        lastSideLaneOffset = sideLaneOffset;

        StampRect(cells, center.x - centerRadius, center.y - centerRadius, center.x + centerRadius, center.y + centerRadius, CellKind.Platform);
        StampRect(cells, center.x - crownRadius, center.y - 2, center.x + crownRadius, center.y + 2, CellKind.UpperPlatform);
        StampRect(cells, center.x - 2, center.y - crownRadius, center.x + 2, center.y + crownRadius, CellKind.UpperPlatform);

        StampRect(cells, center.x - primaryHalfWidth, 1, center.x + primaryHalfWidth, length - 2, CellKind.Bridge);
        StampRect(cells, 1, center.y - primaryHalfWidth, width - 2, center.y + primaryHalfWidth, CellKind.Bridge);

        int northLaneZ = Mathf.Clamp(center.y + sideLaneOffset, 3, length - 4);
        int southLaneZ = Mathf.Clamp(center.y - sideLaneOffset, 3, length - 4);
        int eastLaneX = Mathf.Clamp(center.x + sideLaneOffset, 3, width - 4);
        int westLaneX = Mathf.Clamp(center.x - sideLaneOffset, 3, width - 4);
        StampRect(cells, 2, northLaneZ - sideLaneHalfWidth, width - 3, northLaneZ + sideLaneHalfWidth, CellKind.Bridge);
        StampRect(cells, 2, southLaneZ - sideLaneHalfWidth, width - 3, southLaneZ + sideLaneHalfWidth, CellKind.Bridge);
        StampRect(cells, eastLaneX - sideLaneHalfWidth, 2, eastLaneX + sideLaneHalfWidth, length - 3, CellKind.Bridge);
        StampRect(cells, westLaneX - sideLaneHalfWidth, 2, westLaneX + sideLaneHalfWidth, length - 3, CellKind.Bridge);

        StampRect(cells, loopInset, loopInset, width - loopInset - 1, loopInset + primaryHalfWidth, CellKind.Bridge);
        StampRect(cells, loopInset, length - loopInset - 1 - primaryHalfWidth, width - loopInset - 1, length - loopInset - 1, CellKind.Bridge);
        StampRect(cells, loopInset, loopInset, loopInset + primaryHalfWidth, length - loopInset - 1, CellKind.Bridge);
        StampRect(cells, width - loopInset - 1 - primaryHalfWidth, loopInset, width - loopInset - 1, length - loopInset - 1, CellKind.Bridge);

        StampTraversalTower(cells, new Vector2Int(3 + cornerSize / 2, 3 + cornerSize / 2), cornerSize);
        StampTraversalTower(cells, new Vector2Int(width - 4 - cornerSize / 2, 3 + cornerSize / 2), cornerSize);
        StampTraversalTower(cells, new Vector2Int(3 + cornerSize / 2, length - 4 - cornerSize / 2), cornerSize);
        StampTraversalTower(cells, new Vector2Int(width - 4 - cornerSize / 2, length - 4 - cornerSize / 2), cornerSize);

        StampBridgeLine(cells, new Vector2Int(4, 4), new Vector2Int(center.x - centerRadius - 1, southLaneZ), 1);
        StampBridgeLine(cells, new Vector2Int(width - 5, 4), new Vector2Int(center.x + centerRadius + 1, southLaneZ), 1);
        StampBridgeLine(cells, new Vector2Int(4, length - 5), new Vector2Int(center.x - centerRadius - 1, northLaneZ), 1);
        StampBridgeLine(cells, new Vector2Int(width - 5, length - 5), new Vector2Int(center.x + centerRadius + 1, northLaneZ), 1);
        StampBridgeLine(cells, new Vector2Int(westLaneX, 4), new Vector2Int(westLaneX, center.y - centerRadius - 1), 0);
        StampBridgeLine(cells, new Vector2Int(eastLaneX, 4), new Vector2Int(eastLaneX, center.y - centerRadius - 1), 0);
        StampBridgeLine(cells, new Vector2Int(westLaneX, length - 5), new Vector2Int(westLaneX, center.y + centerRadius + 1), 0);
        StampBridgeLine(cells, new Vector2Int(eastLaneX, length - 5), new Vector2Int(eastLaneX, center.y + centerRadius + 1), 0);

        StampFloatingIslands(cells, rng, center, spawn, exit);
        StampReconfigurableDistricts(cells, rng, center, spawn, exit);
        StampBraidedSecondaryRoutes(cells, rng, center, spawn, exit, centerRadius);
        WidenFastRouteLandings(cells, rng, center, spawn, exit);
        ClearHighSpeedRouteEnvelope(cells, spawn, exit);
        StampOuterDetail(cells, rng, center, spawn, exit);
    }

    private void StampTraversalTower(CellKind[,] cells, Vector2Int anchor, int size)
    {
        int half = Mathf.Max(1, size / 2);
        StampRect(cells, anchor.x - half - 1, anchor.y - half - 1, anchor.x + half + 1, anchor.y + half + 1, CellKind.Platform);
        StampRect(cells, anchor.x - half, anchor.y - half, anchor.x + half, anchor.y + half, CellKind.UpperPlatform);
    }

    private void ClearHighSpeedRouteEnvelope(CellKind[,] cells, Vector2Int spawn, Vector2Int exit)
    {
        for (int x = 2; x < width - 2; x++)
        {
            for (int z = 2; z < length - 2; z++)
            {
                if (!IsNearPrimaryMovementRoute(x, z))
                    continue;

                if (cells[x, z] == CellKind.CoverLow || cells[x, z] == CellKind.CoverHigh || cells[x, z] == CellKind.Hazard)
                    cells[x, z] = CellKind.Floor;

                if (cells[x, z] == CellKind.Void &&
                    DistanceManhattan(x, z, spawn) > safeRadiusAroundSpawn &&
                    DistanceManhattan(x, z, exit) > safeRadiusAroundExit)
                {
                    cells[x, z] = CellKind.Bridge;
                }
            }
        }
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
        while (repairPasses < 32)
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

    private void EnsureElevatedCellsHaveExits(CellKind[,] cells)
    {
        if (cells == null) return;

        for (int pass = 0; pass < 3; pass++)
        {
            bool changed = false;
            bool[,] visited = new bool[width, length];
            for (int x = 1; x < width - 1; x++)
            {
                for (int z = 1; z < length - 1; z++)
                {
                    if (visited[x, z] || !IsElevatedKind(cells[x, z]))
                        continue;

                    List<Vector2Int> component = GatherSameHeightComponent(cells, new Vector2Int(x, z), visited);
                    if (component.Count == 0 || ElevatedComponentHasLowerExit(cells, component))
                        continue;

                    changed |= CreateLowerExitForElevatedComponent(cells, component);
                }
            }

            if (!changed)
                break;
        }
    }

    private List<Vector2Int> GatherSameHeightComponent(CellKind[,] cells, Vector2Int start, bool[,] visited)
    {
        List<Vector2Int> component = new List<Vector2Int>();
        if (!InBounds(start.x, start.y) || !IsElevatedKind(cells[start.x, start.y]))
            return component;

        float height = GetCellHeight(cells[start.x, start.y]);
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        visited[start.x, start.y] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            component.Add(cell);

            foreach (Vector2Int neighbor in GetCardinalNeighbors(cell))
            {
                if (!InBounds(neighbor.x, neighbor.y) || visited[neighbor.x, neighbor.y]) continue;
                if (!IsElevatedKind(cells[neighbor.x, neighbor.y])) continue;
                if (Mathf.Abs(GetCellHeight(cells[neighbor.x, neighbor.y]) - height) > 0.1f) continue;

                visited[neighbor.x, neighbor.y] = true;
                queue.Enqueue(neighbor);
            }
        }

        return component;
    }

    private bool ElevatedComponentHasLowerExit(CellKind[,] cells, List<Vector2Int> component)
    {
        for (int i = 0; i < component.Count; i++)
        {
            Vector2Int cell = component[i];
            float cellHeight = GetCellHeight(cells[cell.x, cell.y]);
            foreach (Vector2Int neighbor in GetCardinalNeighbors(cell))
            {
                if (!InBounds(neighbor.x, neighbor.y) || !IsLayoutWalkable(cells[neighbor.x, neighbor.y])) continue;
                float neighborHeight = GetCellHeight(cells[neighbor.x, neighbor.y]);
                float drop = cellHeight - neighborHeight;
                if (drop > 0.1f && drop <= levelHeight + 0.75f)
                {
                    RegisterCellHeightConnector(cells, cell, neighbor);
                    return true;
                }
            }
        }

        return false;
    }

    private bool CreateLowerExitForElevatedComponent(CellKind[,] cells, List<Vector2Int> component)
    {
        Vector2Int center = new Vector2Int(width / 2, length / 2);
        Vector2Int bestCell = component[0];
        Vector2Int bestNeighbor = new Vector2Int(-1, -1);
        Vector2Int bestDirection = Vector2Int.zero;
        int bestScore = int.MaxValue;

        for (int i = 0; i < component.Count; i++)
        {
            Vector2Int cell = component[i];
            foreach (Vector2Int neighbor in GetCardinalNeighbors(cell))
            {
                if (!InBounds(neighbor.x, neighbor.y)) continue;
                if (neighbor.x <= 0 || neighbor.x >= width - 1 || neighbor.y <= 0 || neighbor.y >= length - 1) continue;
                if (cells[neighbor.x, neighbor.y] == CellKind.Spawn || cells[neighbor.x, neighbor.y] == CellKind.Exit) continue;
                if (IsElevatedKind(cells[neighbor.x, neighbor.y]) &&
                    Mathf.Abs(GetCellHeight(cells[neighbor.x, neighbor.y]) - GetCellHeight(cells[cell.x, cell.y])) < 0.1f)
                    continue;

                Vector2Int direction = neighbor - cell;
                Vector2Int landing = neighbor + direction;
                bool hasLanding = InBounds(landing.x, landing.y) &&
                                  landing.x > 0 && landing.x < width - 1 &&
                                  landing.y > 0 && landing.y < length - 1 &&
                                  cells[landing.x, landing.y] != CellKind.Spawn &&
                                  cells[landing.x, landing.y] != CellKind.Exit;
                int score = Mathf.Abs(neighbor.x - center.x) + Mathf.Abs(neighbor.y - center.y);
                if (hasLanding)
                    score -= 4;
                if (score >= bestScore) continue;

                bestScore = score;
                bestCell = cell;
                bestNeighbor = neighbor;
                bestDirection = direction;
            }
        }

        if (!InBounds(bestNeighbor.x, bestNeighbor.y))
            return false;

        float elevatedHeight = GetCellHeight(cells[bestCell.x, bestCell.y]);
        CellKind lowerKind = ResolveStepDownKind(elevatedHeight);
        cells[bestNeighbor.x, bestNeighbor.y] = lowerKind;
        RegisterCellHeightConnector(cells, bestCell, bestNeighbor);

        Vector2Int landingCell = bestNeighbor + bestDirection;
        if (InBounds(landingCell.x, landingCell.y) &&
            landingCell.x > 0 && landingCell.x < width - 1 &&
            landingCell.y > 0 && landingCell.y < length - 1 &&
            cells[landingCell.x, landingCell.y] != CellKind.Spawn &&
            cells[landingCell.x, landingCell.y] != CellKind.Exit)
        {
            cells[landingCell.x, landingCell.y] = lowerKind;
            RegisterCellHeightConnector(cells, bestNeighbor, landingCell);
        }

        return true;
    }

    private CellKind ResolveStepDownKind(float elevatedHeight)
    {
        float crownHeight = crownLevel * levelHeight;
        float platformHeight = platformLevel * levelHeight;
        float bridgeHeight = bridgeLevel * levelHeight;

        if (elevatedHeight >= crownHeight - 0.1f)
            return CellKind.Platform;
        if (elevatedHeight >= platformHeight - 0.1f)
            return CellKind.Bridge;
        if (elevatedHeight >= bridgeHeight - 0.1f)
            return CellKind.Floor;
        return CellKind.Floor;
    }

    private void RegisterCellHeightConnector(CellKind[,] cells, Vector2Int from, Vector2Int to)
    {
        if (!InBounds(from.x, from.y) || !InBounds(to.x, to.y)) return;
        if (!IsLayoutWalkable(cells[from.x, from.y]) || !IsLayoutWalkable(cells[to.x, to.y])) return;

        float fromY = GetCellHeight(cells[from.x, from.y]);
        float toY = GetCellHeight(cells[to.x, to.y]);
        if (Mathf.Abs(fromY - toY) < 0.1f) return;
        if (Mathf.Abs(fromY - toY) > levelHeight + 0.75f) return;

        Vector2Int delta = to - from;
        Vector3 travelDir = new Vector3(Mathf.Clamp(delta.x, -1, 1), 0f, Mathf.Clamp(delta.y, -1, 1));
        if (travelDir.sqrMagnitude < 0.25f)
            return;

        travelDir.Normalize();
        float edgeInset = tileSize * 0.42f;
        float connectorYBias = 0.16f;

        Vector3 start = CellCenter(from.x, from.y, fromY + connectorYBias);
        Vector3 end = CellCenter(to.x, to.y, toY + connectorYBias);
        Vector3 fromLip = start + travelDir * edgeInset;
        Vector3 toLip = end - travelDir * edgeInset;
        Vector3 verticalStep = new Vector3(fromLip.x, toY + connectorYBias, fromLip.z);
        Vector3 settleStep = new Vector3(toLip.x, toY + connectorYBias, toLip.z);

        var points = new List<Vector3>(5)
        {
            start,
            fromLip,
            verticalStep,
            settleStep,
            end
        };

        RegisterTraversalConnector(from, to, points);
    }

    private bool IsElevatedKind(CellKind kind)
    {
        return kind == CellKind.Bridge || kind == CellKind.Platform || kind == CellKind.UpperPlatform;
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
        if (Mathf.Abs(fromY - toY) < 0.1f)
            return true;

        return TryGetTraversalConnector(from, to, out _);
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
        if (prioritizeFastMovementFlow)
            islandCount = Mathf.Max(2, Mathf.RoundToInt(islandCount * Mathf.Lerp(0.72f, 0.38f, clutterSuppression)));
        for (int i = 0; i < islandCount; i++)
        {
            int x = rng.Next(4, width - 4);
            int z = rng.Next(4, length - 4);
            if (DistanceManhattan(x, z, center) < centralPlatformRadius + 3) continue;
            if (IsNearPrimaryMovementRoute(x, z)) continue;
            if (DistanceManhattan(x, z, spawn) < safeRadiusAroundSpawn + 2) continue;
            if (DistanceManhattan(x, z, exit) < safeRadiusAroundExit + 2) continue;

            int rx = rng.Next(1, 3);
            int rz = rng.Next(1, 3);
            CellKind kind = rng.NextDouble() < 0.65 ? CellKind.Platform : CellKind.Bridge;
            StampRect(cells, x - rx, z - rz, x + rx, z + rz, kind);
            ConnectIslandToMainRoute(cells, center, new Vector2Int(x, z), kind, rng);
        }
    }

    private void StampReconfigurableDistricts(CellKind[,] cells, System.Random rng, Vector2Int center, Vector2Int spawn, Vector2Int exit)
    {
        debugLastReconfigureDistricts = 0;
        int targetCount = Mathf.Clamp(reconfigureDistrictCount + ResolveThemeProfile(themeIndex).extraIslands / 2, 0, 10);
        if (prioritizeFastMovementFlow)
            targetCount = Mathf.Max(1, Mathf.RoundToInt(targetCount * Mathf.Lerp(0.72f, 0.35f, clutterSuppression)));
        if (targetCount <= 0) return;

        int attempts = targetCount * 9;
        for (int i = 0; i < attempts && debugLastReconfigureDistricts < targetCount; i++)
        {
            int x = rng.Next(4, width - 4);
            int z = rng.Next(4, length - 4);
            if (DistanceManhattan(x, z, center) < centralPlatformRadius + 1) continue;
            if (IsNearPrimaryMovementRoute(x, z)) continue;
            if (DistanceManhattan(x, z, spawn) < safeRadiusAroundSpawn + 3) continue;
            if (DistanceManhattan(x, z, exit) < safeRadiusAroundExit + 3) continue;

            int rx = rng.Next(2, 5);
            int rz = rng.Next(2, 5);
            if (!CanStampDistrict(cells, x, z, rx, rz, spawn, exit)) continue;

            double roll = rng.NextDouble();
            CellKind districtKind = roll < 0.38
                ? CellKind.Bridge
                : roll < 0.78
                    ? CellKind.Platform
                    : CellKind.UpperPlatform;

            StampDistrictModule(cells, new Vector2Int(x, z), rx, rz, districtKind, center, spawn, exit);
            debugLastReconfigureDistricts++;
        }
    }

    private bool CanStampDistrict(CellKind[,] cells, int x, int z, int rx, int rz, Vector2Int spawn, Vector2Int exit)
    {
        for (int ix = x - rx - 1; ix <= x + rx + 1; ix++)
        {
            for (int iz = z - rz - 1; iz <= z + rz + 1; iz++)
            {
                if (!InBounds(ix, iz)) return false;
                if (ix <= 1 || iz <= 1 || ix >= width - 2 || iz >= length - 2) return false;
                if (DistanceManhattan(ix, iz, spawn) <= safeRadiusAroundSpawn + 1) return false;
                if (DistanceManhattan(ix, iz, exit) <= safeRadiusAroundExit + 1) return false;
                if (cells[ix, iz] == CellKind.Spawn || cells[ix, iz] == CellKind.Exit) return false;
            }
        }

        return true;
    }

    private void StampDistrictModule(CellKind[,] cells, Vector2Int anchor, int rx, int rz, CellKind kind, Vector2Int center, Vector2Int spawn, Vector2Int exit)
    {
        if (kind == CellKind.UpperPlatform)
        {
            StampProtectedRect(cells, anchor.x - rx - 1, anchor.y - rz - 1, anchor.x + rx + 1, anchor.y + rz + 1, CellKind.Platform, spawn, exit);
            StampProtectedRect(cells, anchor.x - rx, anchor.y - rz, anchor.x + rx, anchor.y + rz, CellKind.UpperPlatform, spawn, exit);
        }
        else if (kind == CellKind.Platform)
        {
            StampProtectedRect(cells, anchor.x - rx - 1, anchor.y - rz - 1, anchor.x + rx + 1, anchor.y + rz + 1, CellKind.Bridge, spawn, exit);
            StampProtectedRect(cells, anchor.x - rx, anchor.y - rz, anchor.x + rx, anchor.y + rz, CellKind.Platform, spawn, exit);
        }
        else
        {
            StampProtectedRect(cells, anchor.x - rx, anchor.y - rz, anchor.x + rx, anchor.y + rz, CellKind.Bridge, spawn, exit);
        }

        Vector2Int routeTarget = PickDistrictRouteTarget(anchor, center, rx, rz);
        CellKind routeKind = kind == CellKind.UpperPlatform ? CellKind.Platform : CellKind.Bridge;
        StampLine(cells, anchor, routeTarget, routeKind, 0);
        if (kind == CellKind.UpperPlatform)
            StampProtectedRect(cells, anchor.x - 1, anchor.y - 1, anchor.x + 1, anchor.y + 1, CellKind.UpperPlatform, spawn, exit);
        else if (kind == CellKind.Platform)
            StampProtectedRect(cells, anchor.x - 1, anchor.y - 1, anchor.x + 1, anchor.y + 1, CellKind.Platform, spawn, exit);
    }

    private Vector2Int PickDistrictRouteTarget(Vector2Int anchor, Vector2Int center, int rx, int rz)
    {
        bool routeHorizontally = Mathf.Abs(anchor.x - center.x) > Mathf.Abs(anchor.y - center.y);
        if (routeHorizontally)
        {
            int x = anchor.x < center.x ? anchor.x + rx + 1 : anchor.x - rx - 1;
            return new Vector2Int(Mathf.Clamp(x, 2, width - 3), Mathf.Clamp(center.y, 2, length - 3));
        }

        int z = anchor.y < center.y ? anchor.y + rz + 1 : anchor.y - rz - 1;
        return new Vector2Int(Mathf.Clamp(center.x, 2, width - 3), Mathf.Clamp(z, 2, length - 3));
    }

    private void StampProtectedRect(CellKind[,] cells, int xMin, int zMin, int xMax, int zMax, CellKind kind, Vector2Int spawn, Vector2Int exit)
    {
        for (int x = Mathf.Clamp(xMin, 1, width - 2); x <= Mathf.Clamp(xMax, 1, width - 2); x++)
        {
            for (int z = Mathf.Clamp(zMin, 1, length - 2); z <= Mathf.Clamp(zMax, 1, length - 2); z++)
            {
                if (DistanceManhattan(x, z, spawn) <= safeRadiusAroundSpawn) continue;
                if (DistanceManhattan(x, z, exit) <= safeRadiusAroundExit) continue;
                if (cells[x, z] == CellKind.Spawn || cells[x, z] == CellKind.Exit) continue;
                cells[x, z] = kind;
            }
        }
    }

    private void StampLine(CellKind[,] cells, Vector2Int start, Vector2Int end, CellKind kind, int extraHalfWidth)
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
            StampRect(cells, x - extraHalfWidth, z - extraHalfWidth, x + extraHalfWidth, z + extraHalfWidth, kind);
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

    private void StampBraidedSecondaryRoutes(CellKind[,] cells, System.Random rng, Vector2Int center, Vector2Int spawn, Vector2Int exit, int centerRadius)
    {
        int routeCount = Mathf.Clamp((width + length) / 18, 2, 4);
        if (prioritizeFastMovementFlow)
            routeCount = Mathf.Clamp(routeCount + 1, 3, 5);
        int inner = Mathf.Clamp(centerRadius + 2, 4, Mathf.Min(width, length) / 3);
        int outerX = Mathf.Clamp(width / 2 - 4, inner + 2, width / 2);
        int outerZ = Mathf.Clamp(length / 2 - 4, inner + 2, length / 2);

        for (int i = 0; i < routeCount; i++)
        {
            bool horizontal = (i & 1) == 0;
            int side = rng.NextDouble() < 0.5 ? -1 : 1;
            int offset = horizontal
                ? rng.Next(inner, Mathf.Max(inner + 1, outerZ + 1))
                : rng.Next(inner, Mathf.Max(inner + 1, outerX + 1));

            Vector2Int a;
            Vector2Int b;
            Vector2Int c;
            if (horizontal)
            {
                int z = Mathf.Clamp(center.y + side * offset, 3, length - 4);
                a = new Vector2Int(3, z);
                b = new Vector2Int(Mathf.Clamp(center.x + rng.Next(-2, 3), 4, width - 5), Mathf.Clamp(z + rng.Next(-1, 2), 3, length - 4));
                c = new Vector2Int(width - 4, Mathf.Clamp(z + rng.Next(-2, 3), 3, length - 4));
            }
            else
            {
                int x = Mathf.Clamp(center.x + side * offset, 3, width - 4);
                a = new Vector2Int(x, 3);
                b = new Vector2Int(Mathf.Clamp(x + rng.Next(-1, 2), 3, width - 4), Mathf.Clamp(center.y + rng.Next(-2, 3), 4, length - 5));
                c = new Vector2Int(Mathf.Clamp(x + rng.Next(-2, 3), 3, width - 4), length - 4);
            }

            StampBridgeLine(cells, a, b, rng.NextDouble() < 0.35 ? 1 : 0);
            StampBridgeLine(cells, b, c, rng.NextDouble() < 0.35 ? 1 : 0);

            if (DistanceManhattan(b.x, b.y, spawn) > safeRadiusAroundSpawn + 2 &&
                DistanceManhattan(b.x, b.y, exit) > safeRadiusAroundExit + 2)
            {
                CellKind nodeKind = rng.NextDouble() < 0.45 ? CellKind.Platform : CellKind.UpperPlatform;
                StampRect(cells, b.x - 1, b.y - 1, b.x + 1, b.y + 1, nodeKind);
            }
        }
    }

    private void WidenFastRouteLandings(CellKind[,] cells, System.Random rng, Vector2Int center, Vector2Int spawn, Vector2Int exit)
    {
        List<Vector2Int> bays = new List<Vector2Int>();
        for (int x = 3; x < width - 3; x++)
        {
            for (int z = 3; z < length - 3; z++)
            {
                if (cells[x, z] != CellKind.Bridge) continue;
                if (DistanceManhattan(x, z, spawn) < safeRadiusAroundSpawn + 1) continue;
                if (DistanceManhattan(x, z, exit) < safeRadiusAroundExit + 1) continue;

                bool north = IsSameLayoutHeight(cells, x, z, x, z + 1);
                bool south = IsSameLayoutHeight(cells, x, z, x, z - 1);
                bool east = IsSameLayoutHeight(cells, x, z, x + 1, z);
                bool west = IsSameLayoutHeight(cells, x, z, x - 1, z);
                int degree = (north ? 1 : 0) + (south ? 1 : 0) + (east ? 1 : 0) + (west ? 1 : 0);
                if (degree < 2) continue;

                bool nearCoreRoute = Mathf.Abs(x - center.x) <= centralPlatformRadius + 3 ||
                                     Mathf.Abs(z - center.y) <= centralPlatformRadius + 3;
                int hash = Mathf.Abs(x * 42589 ^ z * 91733 ^ lastGeneratedSeed);
                if (!nearCoreRoute && hash % 5 != 0) continue;
                if (nearCoreRoute && hash % 3 != 0) continue;

                bays.Add(new Vector2Int(x, z));
            }
        }

        Shuffle(bays, rng);
        int maxBays = Mathf.Clamp((width * length) / 95, 5, 12);
        for (int i = 0; i < bays.Count && i < maxBays; i++)
            StampFastRouteBay(cells, bays[i], spawn, exit);
    }

    private void StampFastRouteBay(CellKind[,] cells, Vector2Int cell, Vector2Int spawn, Vector2Int exit)
    {
        bool northSouth = IsSameLayoutHeight(cells, cell.x, cell.y, cell.x, cell.y + 1) ||
                          IsSameLayoutHeight(cells, cell.x, cell.y, cell.x, cell.y - 1);
        bool eastWest = IsSameLayoutHeight(cells, cell.x, cell.y, cell.x + 1, cell.y) ||
                        IsSameLayoutHeight(cells, cell.x, cell.y, cell.x - 1, cell.y);

        if (northSouth)
        {
            TryPromoteRouteShoulder(cells, cell.x + 1, cell.y, spawn, exit);
            TryPromoteRouteShoulder(cells, cell.x - 1, cell.y, spawn, exit);
        }

        if (eastWest)
        {
            TryPromoteRouteShoulder(cells, cell.x, cell.y + 1, spawn, exit);
            TryPromoteRouteShoulder(cells, cell.x, cell.y - 1, spawn, exit);
        }

        if (northSouth && eastWest)
        {
            TryPromoteRouteShoulder(cells, cell.x + 1, cell.y + 1, spawn, exit);
            TryPromoteRouteShoulder(cells, cell.x - 1, cell.y - 1, spawn, exit);
        }
    }

    private void TryPromoteRouteShoulder(CellKind[,] cells, int x, int z, Vector2Int spawn, Vector2Int exit)
    {
        if (x <= 1 || z <= 1 || x >= width - 2 || z >= length - 2) return;
        if (DistanceManhattan(x, z, spawn) <= safeRadiusAroundSpawn) return;
        if (DistanceManhattan(x, z, exit) <= safeRadiusAroundExit) return;
        if (cells[x, z] == CellKind.Spawn || cells[x, z] == CellKind.Exit) return;
        if (cells[x, z] != CellKind.Floor && cells[x, z] != CellKind.Void) return;

        cells[x, z] = CellKind.Bridge;
    }

    private bool IsSameLayoutHeight(CellKind[,] cells, int x, int z, int nx, int nz)
    {
        if (!InBounds(x, z) || !InBounds(nx, nz)) return false;
        if (!IsLayoutWalkable(cells[x, z]) || !IsLayoutWalkable(cells[nx, nz])) return false;
        return Mathf.Abs(GetCellHeight(cells[x, z]) - GetCellHeight(cells[nx, nz])) < 0.1f;
    }

    private bool IsNearPrimaryMovementRoute(int x, int z)
    {
        int routePad = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, 3f, clutterSuppression)), 1, 3);
        if (Mathf.Abs(x - lastLayoutCenter.x) <= lastPrimaryLaneHalfWidth + routePad)
            return true;
        if (Mathf.Abs(z - lastLayoutCenter.y) <= lastPrimaryLaneHalfWidth + routePad)
            return true;
        if (lastSideLaneOffset > 0)
        {
            if (Mathf.Abs(z - (lastLayoutCenter.y + lastSideLaneOffset)) <= routePad + 1)
                return true;
            if (Mathf.Abs(z - (lastLayoutCenter.y - lastSideLaneOffset)) <= routePad + 1)
                return true;
            if (Mathf.Abs(x - (lastLayoutCenter.x + lastSideLaneOffset)) <= routePad + 1)
                return true;
            if (Mathf.Abs(x - (lastLayoutCenter.x - lastSideLaneOffset)) <= routePad + 1)
                return true;
        }

        return false;
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
        if (prioritizeFastMovementFlow)
        {
            adjustedOuterGapChance *= Mathf.Lerp(0.7f, 0.25f, clutterSuppression);
            adjustedHazardChance *= Mathf.Lerp(0.65f, 0.2f, clutterSuppression);
            adjustedCoverChance *= Mathf.Lerp(0.58f, 0.18f, clutterSuppression);
        }

        for (int x = 2; x < width - 2; x++)
        {
            for (int z = 2; z < length - 2; z++)
            {
                if (cells[x, z] != CellKind.Floor) continue;
                if (prioritizeFastMovementFlow && IsNearPrimaryMovementRoute(x, z)) continue;
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

    private void BuildDistrictRoots(Transform root, CellKind[,] cells)
    {
        districtRoots.Clear();
        currentDistrictMap = new int[width, length];
        if (root == null || cells == null) return;

        int districtSize = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(16, width * length)) * 0.28f), 5, 8);
        int districtIndex = 0;
        for (int x0 = 1; x0 < width - 1; x0 += districtSize)
        {
            for (int z0 = 1; z0 < length - 1; z0 += districtSize)
            {
                int x1 = Mathf.Min(width - 2, x0 + districtSize - 1);
                int z1 = Mathf.Min(length - 2, z0 + districtSize - 1);
                if (!TryCreateDistrictRoot(root, cells, x0, z0, x1, z1, districtIndex, out Transform districtRoot))
                    continue;

                for (int x = x0; x <= x1; x++)
                {
                    for (int z = z0; z <= z1; z++)
                    {
                        if (!IsLayoutWalkable(cells[x, z])) continue;
                        currentDistrictMap[x, z] = districtIndex + 1;
                    }
                }

                districtRoots[districtIndex + 1] = districtRoot;
                districtIndex++;
            }
        }
    }

    private bool TryCreateDistrictRoot(Transform root, CellKind[,] cells, int x0, int z0, int x1, int z1, int index, out Transform districtRoot)
    {
        districtRoot = null;
        int walkableCount = 0;
        float heightSum = 0f;
        int centerX = (x0 + x1) / 2;
        int centerZ = (z0 + z1) / 2;

        for (int x = x0; x <= x1; x++)
        {
            for (int z = z0; z <= z1; z++)
            {
                if (!IsLayoutWalkable(cells[x, z])) continue;
                walkableCount++;
                heightSum += GetCellHeight(cells[x, z]);
            }
        }

        if (walkableCount < 4)
            return false;

        float averageHeight = heightSum / walkableCount;
        GameObject district = new GameObject($"ArenaDistrict_{centerX}_{centerZ}_{index}");
        district.transform.SetParent(root, false);
        district.transform.position = transform.position + CellCenter(centerX, centerZ, averageHeight);
        districtRoot = district.transform;
        return true;
    }

    private Transform GetDistrictParent(Transform fallbackRoot, int x, int z)
    {
        if (currentDistrictMap == null || !InBounds(x, z)) return fallbackRoot;
        int key = currentDistrictMap[x, z];
        if (key == 0) return fallbackRoot;
        return districtRoots.TryGetValue(key, out Transform districtRoot) && districtRoot != null
            ? districtRoot
            : fallbackRoot;
    }

    private void SpawnCell(Transform root, CellKind kind, int x, int z)
    {
        if (kind == CellKind.Void) return;

        Transform parent = GetDistrictParent(root, x, z);
        float y = GetCellHeight(kind);
        Material mat = GetCellMaterial(kind);
        GameObject floor = CreateCube(parent, $"{kind}_{x}_{z}", CellCenter(x, z, y), new Vector3(tileSize, floorThickness, tileSize), mat);

        if (kind == CellKind.Hazard)
        {
            CreateCube(parent, $"HazardInset_{x}_{z}", CellCenter(x, z, y + 0.08f), new Vector3(tileSize * 0.72f, 0.05f, tileSize * 0.72f), hazardMaterial);
        }
        else if (kind == CellKind.Exit)
        {
            SpawnExitBeacon(parent, x, z, y);
        }
        else if (kind == CellKind.CoverLow || kind == CellKind.CoverHigh)
        {
            float h = kind == CellKind.CoverLow ? 0.9f : 1.85f;
            CreateCube(parent, $"Cover_{x}_{z}", CellCenter(x, z, y + (h * 0.5f) + floorThickness), new Vector3(tileSize * 0.72f, h, tileSize * 0.22f), darkMaterial);
        }

        AddTilePanel(parent, kind, x, z, y);
        floor.isStatic = false;
    }

    private void AddTilePanel(Transform root, CellKind kind, int x, int z, float y)
    {
        if (kind == CellKind.Hazard) return;

        float panel = tileSize * 0.62f;
        float ay = y + floorThickness * 0.5f + 0.025f;
        Material mat = kind == CellKind.Spawn || kind == CellKind.Exit ? GetCellMaterial(kind) : darkMaterial;

        int hash = Mathf.Abs(x * 17 + z * 31 + lastGeneratedSeed);
        if (PassesDensity(hash, effectiveMicroDetailDensity, 4, 14) || kind == CellKind.Spawn || kind == CellKind.Exit)
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
                int hash = Mathf.Abs(x * 17389 ^ z * 28411 ^ lastGeneratedSeed);
                if (!PassesDensity(hash, effectiveDecorativeDensity, 1, 3)) continue;
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

    private void SpawnHeightChangeFascia(Transform root, CellKind[,] cells)
    {
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                if (!IsLayoutWalkable(cells[x, z])) continue;

                TryCreateHeightFascia(root, cells, new Vector2Int(x, z), new Vector2Int(x, z + 1), 0);
                TryCreateHeightFascia(root, cells, new Vector2Int(x, z), new Vector2Int(x, z - 1), 1);
                TryCreateHeightFascia(root, cells, new Vector2Int(x, z), new Vector2Int(x + 1, z), 2);
                TryCreateHeightFascia(root, cells, new Vector2Int(x, z), new Vector2Int(x - 1, z), 3);
            }
        }
    }

    private void TryCreateHeightFascia(Transform root, CellKind[,] cells, Vector2Int from, Vector2Int to, int direction)
    {
        if (!InBounds(to.x, to.y)) return;
        if (!IsLayoutWalkable(cells[to.x, to.y])) return;

        float fromY = GetCellHeight(cells[from.x, from.y]);
        float toY = GetCellHeight(cells[to.x, to.y]);
        float drop = fromY - toY;
        if (drop < levelHeight * 0.45f) return;
        if (HasTraversalConnectorBetween(from, to)) return;

        bool northSouth = direction == 0 || direction == 1;
        float sign = direction == 0 || direction == 2 ? 1f : -1f;
        float fasciaHeight = Mathf.Clamp(drop * 0.62f, 1.4f, levelHeight * 0.72f);
        float centerY = fromY - fasciaHeight * 0.5f - 0.12f;
        Vector3 edgeOffset = northSouth
            ? new Vector3(0f, 0f, sign * tileSize * 0.505f)
            : new Vector3(sign * tileSize * 0.505f, 0f, 0f);
        Vector3 scale = northSouth
            ? new Vector3(tileSize * 0.88f, fasciaHeight, 0.12f)
            : new Vector3(0.12f, fasciaHeight, tileSize * 0.88f);

        CreateCube(root, $"HeightFascia_{from.x}_{from.y}_{direction}", CellCenter(from.x, from.y, centerY) + edgeOffset, scale, darkMaterial, false);

        int hash = Mathf.Abs(from.x * 61343 ^ from.y * 12289 ^ direction * 81173 ^ lastGeneratedSeed);
        if (!PassesDensity(hash, effectiveMicroDetailDensity, 3, 11)) return;

        Vector3 glowScale = northSouth
            ? new Vector3(tileSize * 0.62f, 0.045f, 0.055f)
            : new Vector3(0.055f, 0.045f, tileSize * 0.62f);
        Vector3 glowPos = CellCenter(from.x, from.y, fromY - 0.38f) + edgeOffset * 1.01f;
        CreateCube(root, $"HeightFasciaGlow_{from.x}_{from.y}_{direction}", glowPos, glowScale, accentMaterial, false);
    }

    private void SpawnArenaLighting(Transform root)
    {
        ThemeProfile profile = ResolveThemeProfile(themeIndex);
        RenderSettings.ambientLight = new Color(0.070f, 0.078f, 0.088f) + Color.white * profile.ambientBoost;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = profile.skyTint * 1.18f + Color.white * 0.040f;
        RenderSettings.ambientEquatorColor = profile.skyTint * 0.78f + Color.white * 0.028f;
        RenderSettings.ambientGroundColor = profile.fogColor * 1.05f + Color.white * 0.018f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = profile.fogColor;
        RenderSettings.fogDensity = profile.fogDensity;

        GameObject key = new GameObject("ArenaKeyLight");
        key.transform.SetParent(root, false);
        key.transform.position = new Vector3(width * tileSize * 0.5f, 18f, length * tileSize * 0.5f);
        Light light = key.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.Lerp(new Color(0.72f, 0.82f, 1f), profile.skyTint, 0.58f);
        light.intensity = 1.5f + profile.ambientBoost * 4.5f;
        light.shadows = LightShadows.None;
        key.transform.rotation = Quaternion.Euler(48f + profile.skylineDensity * 5f, -32f, 0f);

        SpawnArenaFillLight(root, "ArenaCenterFill", new Vector3(width * tileSize * 0.5f, 13f, length * tileSize * 0.5f), profile.skyTint, 0.65f + profile.ambientBoost * 2.5f, tileSize * 14f);
        SpawnArenaFillLight(root, "ArenaSpawnFill", new Vector3(width * tileSize * 0.5f, 9f, tileSize * 4.5f), profile.bloomTint, 0.34f + profile.skylineDensity * 0.08f, tileSize * 7.5f);
        SpawnArenaFillLight(root, "ArenaExitFill", new Vector3(width * tileSize * 0.5f, 10f, length * tileSize - tileSize * 4.5f), profile.bloomTint, 0.4f + profile.skylineDensity * 0.08f, tileSize * 8f);

        CreateCube(root, "AbyssFogPlane", new Vector3((width - 1) * tileSize * 0.5f, killPlaneY - 8f, (length - 1) * tileSize * 0.5f), new Vector3(width * tileSize * 2.2f, 1f, length * tileSize * 2.2f), darkMaterial, false);
    }

    private void SpawnArenaFillLight(Transform root, string name, Vector3 position, Color color, float intensity, float range)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = transform.position + position;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Color.Lerp(Color.white, color, 0.72f);
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    private void SpawnStructuralShell(Transform root, System.Random rng)
    {
        ThemeProfile profile = ResolveThemeProfile(themeIndex);
        float arenaSpanX = width * tileSize;
        float arenaSpanZ = length * tileSize;
        Vector3 center = new Vector3((width - 1) * tileSize * 0.5f, 0f, (length - 1) * tileSize * 0.5f);
        float shellInset = tileSize * Mathf.Lerp(3.2f, 5.1f, profile.shellInsetScale);
        float shellRadiusX = arenaSpanX * 0.5f + shellInset;
        float shellRadiusZ = arenaSpanZ * 0.5f + shellInset;
        float lowerDeckY = -6.5f;
        float lowerDeckThickness = 0.8f;
        float wallHeight = profile.shellHeight;

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
            if (Mathf.Abs(i) <= 1)
            {
                CreateCube(root, $"ShellNorthGlow_{i}", center + new Vector3(offsetX, 8.8f, shellRadiusZ - tileSize * 0.88f), new Vector3(tileSize * 0.055f, 7.4f, tileSize * 0.055f), accentMaterial, false);
                CreateCube(root, $"ShellSouthGlow_{i}", center + new Vector3(offsetX, 8.8f, -shellRadiusZ + tileSize * 0.88f), new Vector3(tileSize * 0.055f, 7.4f, tileSize * 0.055f), accentMaterial, false);
            }
        }

        for (int i = -2; i <= 2; i++)
        {
            float offsetZ = i * tileSize * 4.4f;
            CreateCube(root, $"ShellEastRib_{i}", center + new Vector3(shellRadiusX - tileSize * 0.4f, 7.5f, offsetZ), new Vector3(tileSize * 0.44f, 17f + Mathf.Abs(i) * 1.8f, tileSize * 0.36f), darkMaterial, false);
            CreateCube(root, $"ShellWestRib_{i}", center + new Vector3(-shellRadiusX + tileSize * 0.4f, 7.5f, offsetZ), new Vector3(tileSize * 0.44f, 17f + Mathf.Abs(i) * 1.8f, tileSize * 0.36f), darkMaterial, false);
            if (Mathf.Abs(i) <= 1)
            {
                CreateCube(root, $"ShellEastGlow_{i}", center + new Vector3(shellRadiusX - tileSize * 0.88f, 8.8f, offsetZ), new Vector3(tileSize * 0.055f, 7.4f, tileSize * 0.055f), accentMaterial, false);
                CreateCube(root, $"ShellWestGlow_{i}", center + new Vector3(-shellRadiusX + tileSize * 0.88f, 8.8f, offsetZ), new Vector3(tileSize * 0.055f, 7.4f, tileSize * 0.055f), accentMaterial, false);
            }
        }

        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            float ringX = Mathf.Cos(angle) * (shellRadiusX - tileSize * 1.8f);
            float ringZ = Mathf.Sin(angle) * (shellRadiusZ - tileSize * 1.8f);
            float towerHeight = 8f + (float)rng.NextDouble() * 5f;
            CreateCube(root, $"ShellTower_{i}", center + new Vector3(ringX, lowerDeckY + towerHeight * 0.5f + 0.2f, ringZ), new Vector3(tileSize * 0.82f, towerHeight, tileSize * 0.82f), darkMaterial, false);
            CreateCube(root, $"ShellTowerCap_{i}", center + new Vector3(ringX, lowerDeckY + towerHeight + 0.45f, ringZ), new Vector3(tileSize * 1.2f, 0.24f, tileSize * 1.2f), darkMaterial, false);
            if ((i & 1) == 0)
                CreateCube(root, $"ShellTowerCapInset_{i}", center + new Vector3(ringX, lowerDeckY + towerHeight + 0.58f, ringZ), new Vector3(tileSize * 0.72f, 0.06f, tileSize * 0.72f), accentMaterial, false);
        }

        if (prioritizeFastMovementFlow && arenaMode == ArenaMode.Combat)
            SpawnShellTraversalLedges(root, center, shellRadiusX, shellRadiusZ, wallHeight);

        SpawnMegastructureBackdrop(root, rng, profile, center, arenaSpanX, arenaSpanZ, shellRadiusX, shellRadiusZ, wallHeight);
    }

    private void SpawnMegastructureBackdrop(Transform root, System.Random rng, ThemeProfile profile, Vector3 center, float arenaSpanX, float arenaSpanZ, float shellRadiusX, float shellRadiusZ, float wallHeight)
    {
        int sideSegments = Mathf.Clamp(Mathf.RoundToInt(4f + profile.skylineDensity * 5f), 5, 9);
        float towerHeightBase = profile.skylineHeight;
        float ringOffset = tileSize * Mathf.Lerp(7f, 11f, profile.shellInsetScale);
        float outerRadiusX = shellRadiusX + ringOffset;
        float outerRadiusZ = shellRadiusZ + ringOffset;
        Material glowMaterial = accentMaterial != null ? accentMaterial : itemMaterial;

        for (int i = 0; i < sideSegments; i++)
        {
            float lane = sideSegments <= 1 ? 0f : i / (float)(sideSegments - 1);
            float xOffset = Mathf.Lerp(-arenaSpanX * 0.52f, arenaSpanX * 0.52f, lane);
            float zOffset = Mathf.Lerp(-arenaSpanZ * 0.52f, arenaSpanZ * 0.52f, lane);
            float jitter = ((float)rng.NextDouble() - 0.5f) * tileSize * 1.4f;
            float towerHeight = towerHeightBase + (float)rng.NextDouble() * (8f + profile.skylineDensity * 10f);

            SpawnBackdropTowerCluster(root, $"NorthBackdrop_{i}", center + new Vector3(xOffset + jitter, 0f, outerRadiusZ), towerHeight, glowMaterial, rng, true, profile);
            SpawnBackdropTowerCluster(root, $"SouthBackdrop_{i}", center + new Vector3(xOffset - jitter, 0f, -outerRadiusZ), towerHeight * 0.96f, glowMaterial, rng, true, profile);
            SpawnBackdropTowerCluster(root, $"EastBackdrop_{i}", center + new Vector3(outerRadiusX, 0f, zOffset + jitter), towerHeight * 1.04f, glowMaterial, rng, false, profile);
            SpawnBackdropTowerCluster(root, $"WestBackdrop_{i}", center + new Vector3(-outerRadiusX, 0f, zOffset - jitter), towerHeight * 0.98f, glowMaterial, rng, false, profile);
        }

        float bridgeY = wallHeight + Mathf.Lerp(6f, 12f, profile.skylineDensity);
        CreateCube(root, "BackdropNorthBridge", center + new Vector3(0f, bridgeY, outerRadiusZ - tileSize * 0.8f), new Vector3(arenaSpanX + tileSize * 10f, 0.28f, 0.5f), darkMaterial, false);
        CreateCube(root, "BackdropSouthBridge", center + new Vector3(0f, bridgeY + 1.2f, -outerRadiusZ + tileSize * 0.8f), new Vector3(arenaSpanX + tileSize * 8.5f, 0.22f, 0.42f), darkMaterial, false);
        CreateCube(root, "BackdropEastBridge", center + new Vector3(outerRadiusX - tileSize * 0.8f, bridgeY - 0.8f, 0f), new Vector3(0.5f, 0.24f, arenaSpanZ + tileSize * 10f), darkMaterial, false);
        CreateCube(root, "BackdropWestBridge", center + new Vector3(-outerRadiusX + tileSize * 0.8f, bridgeY + 0.6f, 0f), new Vector3(0.42f, 0.2f, arenaSpanZ + tileSize * 8.5f), darkMaterial, false);

        AddBackdropGlowStrip(root, "BackdropNorthGlow", center + new Vector3(0f, bridgeY + 0.1f, outerRadiusZ - tileSize * 1.25f), new Vector3(arenaSpanX * 0.82f, 0.08f, 0.08f), profile.structureGlowColor);
        AddBackdropGlowStrip(root, "BackdropSouthGlow", center + new Vector3(0f, bridgeY + 1.32f, -outerRadiusZ + tileSize * 1.25f), new Vector3(arenaSpanX * 0.76f, 0.08f, 0.08f), profile.structureGlowColor);
        AddBackdropGlowStrip(root, "BackdropEastGlow", center + new Vector3(outerRadiusX - tileSize * 1.25f, bridgeY - 0.7f, 0f), new Vector3(0.08f, 0.08f, arenaSpanZ * 0.82f), profile.structureGlowColor);
        AddBackdropGlowStrip(root, "BackdropWestGlow", center + new Vector3(-outerRadiusX + tileSize * 1.25f, bridgeY + 0.72f, 0f), new Vector3(0.08f, 0.08f, arenaSpanZ * 0.76f), profile.structureGlowColor);
    }

    private void SpawnBackdropTowerCluster(Transform root, string name, Vector3 baseCenter, float towerHeight, Material glowMaterial, System.Random rng, bool northSouth, ThemeProfile profile)
    {
        float bodyWidth = tileSize * Mathf.Lerp(0.9f, 1.45f, profile.skylineDensity);
        float bodyDepth = tileSize * Mathf.Lerp(1.2f, 1.9f, profile.shellInsetScale);
        float lateralOffset = tileSize * 1.35f;
        Vector3 lateral = northSouth ? Vector3.right * lateralOffset : Vector3.forward * lateralOffset;
        Vector3 towerScale = northSouth
            ? new Vector3(bodyWidth, towerHeight, bodyDepth)
            : new Vector3(bodyDepth, towerHeight, bodyWidth);

        CreateCube(root, $"{name}_Core", baseCenter + Vector3.up * (towerHeight * 0.5f - 4f), towerScale, darkMaterial, false);
        CreateCube(root, $"{name}_ShoulderA", baseCenter + lateral + Vector3.up * (towerHeight * 0.42f - 3f), Vector3.Scale(towerScale, new Vector3(0.58f, 0.84f, 0.58f)), darkMaterial, false);
        CreateCube(root, $"{name}_ShoulderB", baseCenter - lateral + Vector3.up * (towerHeight * 0.36f - 3f), Vector3.Scale(towerScale, new Vector3(0.48f, 0.72f, 0.48f)), darkMaterial, false);
        CreateCube(root, $"{name}_Cap", baseCenter + Vector3.up * (towerHeight - 7.8f), new Vector3(towerScale.x * 1.22f, 0.28f, towerScale.z * 1.22f), darkMaterial, false);

        AddBackdropGlowStrip(root, $"{name}_GlowA", baseCenter + lateral * 0.38f + Vector3.up * Mathf.Lerp(6f, towerHeight - 10f, 0.42f), northSouth ? new Vector3(0.08f, towerHeight * 0.58f, 0.08f) : new Vector3(0.08f, towerHeight * 0.58f, 0.08f), profile.structureGlowColor);
        AddBackdropGlowStrip(root, $"{name}_GlowB", baseCenter - lateral * 0.38f + Vector3.up * Mathf.Lerp(8f, towerHeight - 8f, 0.55f), northSouth ? new Vector3(0.06f, towerHeight * 0.42f, 0.06f) : new Vector3(0.06f, towerHeight * 0.42f, 0.06f), profile.structureGlowColor * 0.9f);

        if ((Mathf.Abs(themeIndex) % 4) == 2)
        {
            float ventY = Mathf.Lerp(10f, towerHeight - 12f, 0.78f);
            CreateCube(root, $"{name}_VentBand", baseCenter + Vector3.up * ventY, northSouth ? new Vector3(towerScale.x * 1.05f, 0.18f, towerScale.z * 0.52f) : new Vector3(towerScale.x * 0.52f, 0.18f, towerScale.z * 1.05f), glowMaterial, false);
        }
        else if ((Mathf.Abs(themeIndex) % 4) == 1)
        {
            float mastHeight = towerHeight * 0.32f;
            CreateCube(root, $"{name}_Mast", baseCenter + Vector3.up * (towerHeight + mastHeight * 0.5f - 8f), new Vector3(0.18f, mastHeight, 0.18f), darkMaterial, false);
            AddBackdropGlowStrip(root, $"{name}_MastGlow", baseCenter + Vector3.up * (towerHeight + mastHeight - 8.8f), new Vector3(0.14f, 0.64f, 0.14f), profile.structureGlowColor);
        }
    }

    private void AddBackdropGlowStrip(Transform root, string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject glow = CreateCube(root, name, position, scale, accentMaterial, false);
        if (glow == null)
            return;

        ArenaPulseFx pulse = glow.AddComponent<ArenaPulseFx>();
        pulse.baseScale = scale;
        pulse.scalePulse = 0.035f;
        pulse.pulseSpeed = 1.2f;
        pulse.emissionColor = color;
        pulse.emissionStrength = 1.3f;
        pulse.emissionPulse = 0.22f;
    }

    private void SpawnShellTraversalLedges(Transform root, Vector3 center, float shellRadiusX, float shellRadiusZ, float wallHeight)
    {
        int laneOffset = Mathf.Max(0, lastSideLaneOffset);
        int[] xCells =
        {
            Mathf.Clamp(lastLayoutCenter.x, 2, width - 3),
            Mathf.Clamp(lastLayoutCenter.x - laneOffset, 2, width - 3),
            Mathf.Clamp(lastLayoutCenter.x + laneOffset, 2, width - 3)
        };
        int[] zCells =
        {
            Mathf.Clamp(lastLayoutCenter.y, 2, length - 3),
            Mathf.Clamp(lastLayoutCenter.y - laneOffset, 2, length - 3),
            Mathf.Clamp(lastLayoutCenter.y + laneOffset, 2, length - 3)
        };

        float[] heights =
        {
            levelHeight * 1.35f,
            levelHeight * 2.1f,
            Mathf.Min(wallHeight - 3.5f, levelHeight * 3.1f)
        };

        for (int i = 0; i < xCells.Length; i++)
        {
            float xWorld = xCells[i] * tileSize;
            for (int h = 0; h < heights.Length; h++)
            {
                float ledgeY = heights[h];
                CreateCube(root, $"ShellNorthTraverseLedge_{i}_{h}", new Vector3(xWorld, ledgeY, center.z + shellRadiusZ - tileSize * 0.86f), new Vector3(tileSize * 2.1f, 0.16f, 0.34f), darkMaterial, true);
                CreateCube(root, $"ShellSouthTraverseLedge_{i}_{h}", new Vector3(xWorld, ledgeY, center.z - shellRadiusZ + tileSize * 0.86f), new Vector3(tileSize * 2.1f, 0.16f, 0.34f), darkMaterial, true);
                CreateCube(root, $"ShellNorthTraverseGlow_{i}_{h}", new Vector3(xWorld, ledgeY + 0.05f, center.z + shellRadiusZ - tileSize * 0.58f), new Vector3(tileSize * 1.4f, 0.04f, 0.06f), accentMaterial, false);
                CreateCube(root, $"ShellSouthTraverseGlow_{i}_{h}", new Vector3(xWorld, ledgeY + 0.05f, center.z - shellRadiusZ + tileSize * 0.58f), new Vector3(tileSize * 1.4f, 0.04f, 0.06f), accentMaterial, false);
            }
        }

        for (int i = 0; i < zCells.Length; i++)
        {
            float zWorld = zCells[i] * tileSize;
            for (int h = 0; h < heights.Length; h++)
            {
                float ledgeY = heights[h];
                CreateCube(root, $"ShellEastTraverseLedge_{i}_{h}", new Vector3(center.x + shellRadiusX - tileSize * 0.86f, ledgeY, zWorld), new Vector3(0.34f, 0.16f, tileSize * 2.1f), darkMaterial, true);
                CreateCube(root, $"ShellWestTraverseLedge_{i}_{h}", new Vector3(center.x - shellRadiusX + tileSize * 0.86f, ledgeY, zWorld), new Vector3(0.34f, 0.16f, tileSize * 2.1f), darkMaterial, true);
                CreateCube(root, $"ShellEastTraverseGlow_{i}_{h}", new Vector3(center.x + shellRadiusX - tileSize * 0.58f, ledgeY + 0.05f, zWorld), new Vector3(0.06f, 0.04f, tileSize * 1.4f), accentMaterial, false);
                CreateCube(root, $"ShellWestTraverseGlow_{i}_{h}", new Vector3(center.x - shellRadiusX + tileSize * 0.58f, ledgeY + 0.05f, zWorld), new Vector3(0.06f, 0.04f, tileSize * 1.4f), accentMaterial, false);
            }
        }

        for (int i = 0; i < xCells.Length; i++)
        {
            float xWorld = xCells[i] * tileSize;
            CreateCube(root, $"ShellNorthTraverseBrace_{i}", new Vector3(xWorld, wallHeight * 0.42f - 1.1f, center.z + shellRadiusZ - tileSize * 0.44f), new Vector3(0.22f, wallHeight * 0.62f, 0.22f), darkMaterial, true);
            CreateCube(root, $"ShellSouthTraverseBrace_{i}", new Vector3(xWorld, wallHeight * 0.42f - 1.1f, center.z - shellRadiusZ + tileSize * 0.44f), new Vector3(0.22f, wallHeight * 0.62f, 0.22f), darkMaterial, true);
        }

        for (int i = 0; i < zCells.Length; i++)
        {
            float zWorld = zCells[i] * tileSize;
            CreateCube(root, $"ShellEastTraverseBrace_{i}", new Vector3(center.x + shellRadiusX - tileSize * 0.44f, wallHeight * 0.42f - 1.1f, zWorld), new Vector3(0.22f, wallHeight * 0.62f, 0.22f), darkMaterial, true);
            CreateCube(root, $"ShellWestTraverseBrace_{i}", new Vector3(center.x - shellRadiusX + tileSize * 0.44f, wallHeight * 0.42f - 1.1f, zWorld), new Vector3(0.22f, wallHeight * 0.62f, 0.22f), darkMaterial, true);
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
        ResolveSkyboxTuning(out float exposure, out float atmosphereThickness, out float sunSize, out float sunConvergence);
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
            skyboxMaterial.SetFloat("_Exposure", exposure);
        if (skyboxMaterial.HasProperty("_AtmosphereThickness"))
            skyboxMaterial.SetFloat("_AtmosphereThickness", atmosphereThickness);
        if (skyboxMaterial.HasProperty("_SunSize"))
            skyboxMaterial.SetFloat("_SunSize", sunSize);
        if (skyboxMaterial.HasProperty("_SunSizeConvergence"))
            skyboxMaterial.SetFloat("_SunSizeConvergence", sunConvergence);
        RenderSettings.skybox = skyboxMaterial;
    }

    private void EnsureVolume()
    {
        ThemeProfile themeProfile = ResolveThemeProfile(themeIndex);
        ResolveVolumeTuning(out float bloomIntensity, out float bloomThreshold, out float postExposure, out float contrast, out float saturation, out float grainIntensity, out float temperature, out float tint);
        if (environmentVolume == null)
        {
            Transform existing = transform.Find("ArenaEnvironmentVolume");
            if (existing != null)
                environmentVolume = existing.GetComponent<Volume>();

            if (environmentVolume == null)
            {
                GameObject volumeObject = new GameObject("ArenaEnvironmentVolume");
                volumeObject.transform.SetParent(transform, false);
                environmentVolume = volumeObject.AddComponent<Volume>();
            }
        }

        RemoveDuplicateEnvironmentVolumes(environmentVolume);
        environmentVolume.isGlobal = true;
        environmentVolume.priority = 20f;
        if (environmentVolume.profile == null)
            environmentVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

        if (environmentVolume == null || environmentVolume.profile == null) return;

        VolumeProfile volProfile = environmentVolume.profile;
        if (!volProfile.TryGet(out Bloom bloom))
            bloom = volProfile.Add<Bloom>(true);
        bloom.active = true;
        bloom.intensity.Override(bloomIntensity);
        bloom.threshold.Override(bloomThreshold);
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
        colorAdjustments.postExposure.Override(postExposure);
        colorAdjustments.contrast.Override(contrast);
        colorAdjustments.saturation.Override(saturation);
        colorAdjustments.colorFilter.Override(themeProfile.colorFilter);

        if (!volProfile.TryGet(out FilmGrain filmGrain))
            filmGrain = volProfile.Add<FilmGrain>(true);
        filmGrain.active = true;
        filmGrain.intensity.Override(grainIntensity);
        filmGrain.type.Override(FilmGrainLookup.Thin1);

        if (!volProfile.TryGet(out WhiteBalance whiteBalance))
            whiteBalance = volProfile.Add<WhiteBalance>(true);
        whiteBalance.active = true;
        whiteBalance.temperature.Override(temperature);
        whiteBalance.tint.Override(tint);
    }

    private void ResolveSkyboxTuning(out float exposure, out float atmosphereThickness, out float sunSize, out float sunConvergence)
    {
        switch (Mathf.Abs(themeIndex) % 4)
        {
            case 1:
                exposure = 1.18f;
                atmosphereThickness = 0.92f;
                sunSize = 0.032f;
                sunConvergence = 6f;
                break;
            case 2:
                exposure = 0.98f;
                atmosphereThickness = 0.72f;
                sunSize = 0.048f;
                sunConvergence = 4f;
                break;
            case 3:
                exposure = 1.04f;
                atmosphereThickness = 0.84f;
                sunSize = 0.028f;
                sunConvergence = 7f;
                break;
            default:
                exposure = 1.1f;
                atmosphereThickness = 0.8f;
                sunSize = 0.036f;
                sunConvergence = 5f;
                break;
        }
    }

    private void ResolveVolumeTuning(out float bloomIntensity, out float bloomThreshold, out float postExposure, out float contrast, out float saturation, out float grainIntensity, out float temperature, out float tint)
    {
        switch (Mathf.Abs(themeIndex) % 4)
        {
            case 1:
                bloomIntensity = 2.45f;
                bloomThreshold = 0.72f;
                postExposure = 0.42f;
                contrast = 16f;
                saturation = 10f;
                grainIntensity = 0.16f;
                temperature = -9f;
                tint = 2f;
                break;
            case 2:
                bloomIntensity = 1.95f;
                bloomThreshold = 0.84f;
                postExposure = 0.18f;
                contrast = 22f;
                saturation = 8f;
                grainIntensity = 0.2f;
                temperature = 12f;
                tint = 6f;
                break;
            case 3:
                bloomIntensity = 2.15f;
                bloomThreshold = 0.76f;
                postExposure = 0.28f;
                contrast = 18f;
                saturation = 14f;
                grainIntensity = 0.17f;
                temperature = -2f;
                tint = -4f;
                break;
            default:
                bloomIntensity = 2.2f;
                bloomThreshold = 0.78f;
                postExposure = 0.35f;
                contrast = 18f;
                saturation = 12f;
                grainIntensity = 0.18f;
                temperature = -5f;
                tint = 3f;
                break;
        }
    }

    private void RemoveDuplicateEnvironmentVolumes(Volume keep)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || child.name != "ArenaEnvironmentVolume") continue;
            Volume volume = child.GetComponent<Volume>();
            if (volume == null || volume == keep) continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private void SpawnGameplayContent(Transform root, CellKind[,] cells, System.Random rng)
    {
        ThemeProfile profile = ResolveThemeProfile(themeIndex);
        Vector2Int spawn = FindFirst(cells, CellKind.Spawn);
        Vector2Int exit = FindFirst(cells, CellKind.Exit);
        List<Vector2Int> candidates = new List<Vector2Int>();
        List<Vector2Int> routeCandidates = new List<Vector2Int>();

        for (int x = 2; x < width - 2; x++)
        {
            for (int z = 2; z < length - 2; z++)
            {
                if (!IsSafePuzzleCell(cells, x, z)) continue;
                if (DistanceManhattan(x, z, spawn) < safeRadiusAroundSpawn + 2) continue;
                if (DistanceManhattan(x, z, exit) < safeRadiusAroundExit + 1) continue;
                Vector2Int cell = new Vector2Int(x, z);
                if (prioritizeFastMovementFlow && arenaMode == ArenaMode.Combat && IsNearPrimaryMovementRoute(x, z))
                    routeCandidates.Add(cell);
                else
                    candidates.Add(cell);
            }
        }

        Shuffle(candidates, rng);
        Shuffle(routeCandidates, rng);
        if (routeCandidates.Count > 0)
            candidates.AddRange(routeCandidates);

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
                if (!HasGroundPathBetween(new Vector2Int(x, z), exit, 3)) continue;
                if (DistanceManhattan(x, z, spawn) < minEnemyDistanceFromSpawn) continue;
                if (DistanceManhattan(x, z, exit) < safeRadiusAroundExit) continue;
                enemyCells.Add(new Vector2Int(x, z));
            }
        }

        enemyCells.Sort((a, b) =>
        {
            int scoreB = ScoreEnemySpawnCell(cells, b, spawn, exit);
            int scoreA = ScoreEnemySpawnCell(cells, a, spawn, exit);
            if (scoreA != scoreB)
                return scoreB.CompareTo(scoreA);

            int tieA = Mathf.Abs(a.x * 92821 ^ a.y * 68917 ^ lastGeneratedSeed);
            int tieB = Mathf.Abs(b.x * 92821 ^ b.y * 68917 ^ lastGeneratedSeed);
            return tieA.CompareTo(tieB);
        });

        int min = (arenaMode == ArenaMode.Boss ? bossEnemyMin : combatEnemyMin) + enemyCountBonus;
        int max = (arenaMode == ArenaMode.Boss ? bossEnemyMax : combatEnemyMax) + enemyCountBonus;
        int targetCount = Mathf.Clamp(rng.Next(Mathf.Min(min, max), Mathf.Max(min, max) + 1), 0, enemyCells.Count);

        List<Vector2Int> selectedCells = new List<Vector2Int>();
        List<Vector2Int> usedCells = new List<Vector2Int>();
        int minimumEnemySpacing = enemyCountBonus >= 8 ? 2 : enemyCountBonus >= 4 ? 3 : 4;
        for (int i = 0; i < enemyCells.Count && selectedCells.Count < targetCount; i++)
        {
            Vector2Int cell = enemyCells[i];
            bool tooCloseToExisting = false;
            for (int usedIndex = 0; usedIndex < usedCells.Count; usedIndex++)
            {
                Vector2Int used = usedCells[usedIndex];
                if (DistanceManhattan(cell.x, cell.y, used) < minimumEnemySpacing)
                {
                    tooCloseToExisting = true;
                    break;
                }
            }
            if (tooCloseToExisting)
                continue;

            usedCells.Add(cell);
            selectedCells.Add(cell);
        }

        List<BasicEnemyAI.EnemyType> encounterPool = BuildEncounterEnemyTypes(rng, arenaMode, selectedCells.Count, true);
        List<BasicEnemyAI.EnemyType> encounterTypes = LayoutEncounterTypesForCells(cells, selectedCells, encounterPool, spawn, exit);
        for (int spawned = 0; spawned < selectedCells.Count; spawned++)
        {
            Vector2Int cell = selectedCells[spawned];

            float y = GetCellHeight(cells[cell.x, cell.y]);
            Vector3 spawnPos = transform.position + CellCenter(cell.x, cell.y, y + 0.15f);

            GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity, root);
            enemy.name = $"Enemy_{spawned + 1}";

            BasicEnemyAI ai = enemy.GetComponent<BasicEnemyAI>();
            if (ai != null)
            {
                ai.enemyType = encounterTypes[Mathf.Clamp(spawned, 0, encounterTypes.Count - 1)];
                ai.maxHealth *= Mathf.Max(1f, enemyHealthMultiplier);
                if (ai.enemyType == BasicEnemyAI.EnemyType.Flying)
                    enemy.transform.position += Vector3.up * Mathf.Max(2.2f, ai.hoverHeight + 0.65f);
                ai.autoBuildTypeModel = true;
            }
        }
    }

    private int ScoreEnemySpawnCell(CellKind[,] cells, Vector2Int cell, Vector2Int spawn, Vector2Int exit)
    {
        int score = 0;
        int spawnDistance = DistanceManhattan(cell.x, cell.y, spawn);
        int exitDistance = DistanceManhattan(cell.x, cell.y, exit);
        score += Mathf.Clamp(spawnDistance * 3, 0, 36);
        score += Mathf.Clamp(exitDistance, 0, 14);

        float height = GetCellHeight(cells[cell.x, cell.y]);
        if (cells[cell.x, cell.y] == CellKind.UpperPlatform)
            score += 20;
        else if (cells[cell.x, cell.y] == CellKind.Platform)
            score += 12;
        else if (cells[cell.x, cell.y] == CellKind.Bridge)
            score += 6;

        if (prioritizeFastMovementFlow)
        {
            if (IsNearPrimaryMovementRoute(cell.x, cell.y))
                score -= 18;
            else
                score += 12;

            int dx = Mathf.Abs(cell.x - lastLayoutCenter.x);
            int dz = Mathf.Abs(cell.y - lastLayoutCenter.y);
            bool flankBand = dx >= lastPrimaryLaneHalfWidth + 2 || dz >= lastPrimaryLaneHalfWidth + 2;
            if (flankBand)
                score += 10;
        }

        int accessibleNeighbors = 0;
        foreach (Vector2Int neighbor in GetNeighbors(cell))
        {
            if (!InBounds(neighbor.x, neighbor.y)) continue;
            if (!IsWalkableForContent(cells[neighbor.x, neighbor.y])) continue;
            if (!CanTraverseCells(cell, neighbor)) continue;
            accessibleNeighbors++;
        }
        score += accessibleNeighbors * 2;

        return score;
    }

    private int ScoreJumpPadCell(CellKind[,] cells, Vector2Int cell)
    {
        int score = 0;
        CellKind kind = cells[cell.x, cell.y];
        if (kind == CellKind.UpperPlatform)
            score += 18;
        else if (kind == CellKind.Platform)
            score += 12;
        else if (kind == CellKind.Bridge)
            score += 6;

        if (IsRouteConnectorCell(cells, cell))
            score += 18;

        if (prioritizeFastMovementFlow)
        {
            if (IsNearPrimaryMovementRoute(cell.x, cell.y))
                score += 14;

            int dx = Mathf.Abs(cell.x - lastLayoutCenter.x);
            int dz = Mathf.Abs(cell.y - lastLayoutCenter.y);
            bool sideBand = dx >= lastPrimaryLaneHalfWidth + 1 || dz >= lastPrimaryLaneHalfWidth + 1;
            if (sideBand)
                score += 10;
        }

        int accessibleNeighbors = 0;
        foreach (Vector2Int neighbor in GetNeighbors(cell))
        {
            if (!InBounds(neighbor.x, neighbor.y)) continue;
            if (!IsWalkableForContent(cells[neighbor.x, neighbor.y])) continue;
            accessibleNeighbors++;
        }
        score += accessibleNeighbors * 2;

        return score;
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
        Vector2Int exitCell = InBounds(lastExitCell.x, lastExitCell.y)
            ? lastExitCell
            : new Vector2Int(Mathf.Clamp(lastLayoutCenter.x, 2, width - 3), Mathf.Clamp(length - 3, 2, length - 3));
        candidates.Sort((a, b) =>
        {
            int scoreB = ScoreEnemySpawnCell(lastCells, b, origin, exitCell);
            int scoreA = ScoreEnemySpawnCell(lastCells, a, origin, exitCell);
            if (scoreA != scoreB)
                return scoreB.CompareTo(scoreA);

            int tieA = Mathf.Abs(a.x * 73471 ^ a.y * 29311 ^ lastGeneratedSeed);
            int tieB = Mathf.Abs(b.x * 73471 ^ b.y * 29311 ^ lastGeneratedSeed);
            return tieA.CompareTo(tieB);
        });

        List<Vector2Int> usedCells = new List<Vector2Int>();
        List<Vector2Int> selectedCells = new List<Vector2Int>();
        for (int i = 0; i < candidates.Count && selectedCells.Count < count; i++)
        {
            Vector2Int cell = candidates[i];
            bool tooCloseToExisting = false;
            for (int usedIndex = 0; usedIndex < usedCells.Count; usedIndex++)
            {
                if (DistanceManhattan(cell.x, cell.y, usedCells[usedIndex]) < 4)
                {
                    tooCloseToExisting = true;
                    break;
                }
            }
            if (tooCloseToExisting)
                continue;

            usedCells.Add(cell);
            selectedCells.Add(cell);
        }

        List<BasicEnemyAI.EnemyType> encounterPool = BuildEncounterEnemyTypes(rng, arenaMode, selectedCells.Count, false);
        List<BasicEnemyAI.EnemyType> encounterTypes = LayoutEncounterTypesForCells(lastCells, selectedCells, encounterPool, origin, exitCell);
        int spawnedCount = 0;
        for (int i = 0; i < selectedCells.Count; i++)
        {
            Vector2Int cell = selectedCells[i];

            Vector3 spawnPos = GetNavigationPointForCell(cell) + Vector3.up * 0.05f;
            GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity, CurrentArenaRoot);
            enemy.name = $"PressureEnemy_{spawnedCount + 1}";

            BasicEnemyAI ai = enemy.GetComponent<BasicEnemyAI>();
            if (ai != null)
            {
                ai.enemyType = encounterTypes[Mathf.Clamp(spawnedCount, 0, encounterTypes.Count - 1)];
                ai.maxHealth *= Mathf.Max(1f, enemyHealthMultiplier);
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
        if (ai.enemyType == BasicEnemyAI.EnemyType.Flying)
            boss.transform.position += Vector3.up * Mathf.Max(3.2f, ai.hoverHeight + 1.2f);
        ai.isBoss = true;
        string themeLabel = GetThemeLabel(themeIndex);
        ai.displayName = ai.bossArchetype == BasicEnemyAI.BossArchetype.Sentinel
            ? $"{themeLabel} Aerial Sentinel"
            : ai.bossArchetype == BasicEnemyAI.BossArchetype.Striker
                ? $"{themeLabel} Raze Striker"
                : $"{themeLabel} Obelisk Warden";
        ai.maxHealth = (74f + (themeIndex * 18f)) * Mathf.Max(1f, enemyHealthMultiplier);
        ai.moveSpeed = ai.bossArchetype == BasicEnemyAI.BossArchetype.Striker ? 5.3f : ai.bossArchetype == BasicEnemyAI.BossArchetype.Sentinel ? 4.6f : 3.9f;
        ai.fireRate = ai.bossArchetype == BasicEnemyAI.BossArchetype.Striker ? 1.48f : 1.2f;
        ai.meleeDamage = ai.bossArchetype == BasicEnemyAI.BossArchetype.Striker ? 8.5f : ai.bossArchetype == BasicEnemyAI.BossArchetype.Sentinel ? 7.5f : 9f;
        ai.stoppingDistance = ai.bossArchetype == BasicEnemyAI.BossArchetype.Striker ? 8.8f : ai.bossArchetype == BasicEnemyAI.BossArchetype.Sentinel ? 14.5f : 12f;
        ai.autoBuildTypeModel = true;
    }

    private List<BasicEnemyAI.EnemyType> BuildEncounterEnemyTypes(System.Random rng, ArenaMode mode, int count, bool allowFlying)
    {
        List<BasicEnemyAI.EnemyType> result = new List<BasicEnemyAI.EnemyType>(Mathf.Max(0, count));
        if (count <= 0)
            return result;

        for (int i = 0; i < count; i++)
        {
            BasicEnemyAI.EnemyType rolled = RollEnemyType(rng, mode);
            if (!allowFlying && rolled == BasicEnemyAI.EnemyType.Flying)
                rolled = BasicEnemyAI.EnemyType.Shooter;
            result.Add(rolled);
        }

        if (result.Count >= 3)
        {
            EnsureEncounterType(result, BasicEnemyAI.EnemyType.Shooter, 0);
            if (!ContainsAnyEncounterType(result, BasicEnemyAI.EnemyType.Grunt, BasicEnemyAI.EnemyType.Tank))
                EnsureEncounterType(result, BasicEnemyAI.EnemyType.Grunt, result.Count - 1);
        }

        if (result.Count >= 4)
            EnsureEncounterType(result, BasicEnemyAI.EnemyType.Tank, Mathf.Clamp(result.Count / 2, 0, result.Count - 1));

        ThemeProfile profile = ResolveThemeProfile(themeIndex);
        if (allowFlying && result.Count >= 5 && profile.flyingWeight > 0 && !result.Contains(BasicEnemyAI.EnemyType.Flying))
        {
            int replacementIndex = result.FindIndex(t => t == BasicEnemyAI.EnemyType.Shooter || t == BasicEnemyAI.EnemyType.Grunt);
            if (replacementIndex >= 0)
                result[replacementIndex] = BasicEnemyAI.EnemyType.Flying;
        }

        if (result.Count >= 6)
        {
            EnsureEncounterType(result, BasicEnemyAI.EnemyType.Grunt, 1);
            EnsureEncounterType(result, BasicEnemyAI.EnemyType.Shooter, Mathf.Clamp(result.Count - 2, 0, result.Count - 1));
        }

        return result;
    }

    private List<BasicEnemyAI.EnemyType> LayoutEncounterTypesForCells(
        CellKind[,] cells,
        List<Vector2Int> selectedCells,
        List<BasicEnemyAI.EnemyType> encounterPool,
        Vector2Int spawn,
        Vector2Int exit)
    {
        List<BasicEnemyAI.EnemyType> ordered = new List<BasicEnemyAI.EnemyType>(selectedCells.Count);
        if (selectedCells == null || encounterPool == null || selectedCells.Count == 0 || encounterPool.Count == 0)
            return ordered;

        List<BasicEnemyAI.EnemyType> remaining = new List<BasicEnemyAI.EnemyType>(encounterPool);
        for (int i = 0; i < selectedCells.Count; i++)
        {
            Vector2Int cell = selectedCells[i];
            int bestIndex = 0;
            float bestScore = float.MinValue;
            for (int j = 0; j < remaining.Count; j++)
            {
                float score = ScoreEncounterTypeForCell(cells, cell, remaining[j], spawn, exit);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = j;
                }
            }

            ordered.Add(remaining[bestIndex]);
            remaining.RemoveAt(bestIndex);
            if (remaining.Count == 0)
                break;
        }

        return ordered;
    }

    private float ScoreEncounterTypeForCell(
        CellKind[,] cells,
        Vector2Int cell,
        BasicEnemyAI.EnemyType type,
        Vector2Int spawn,
        Vector2Int exit)
    {
        CellKind kind = cells[cell.x, cell.y];
        int dx = Mathf.Abs(cell.x - lastLayoutCenter.x);
        int dz = Mathf.Abs(cell.y - lastLayoutCenter.y);
        bool flankBand = dx >= lastPrimaryLaneHalfWidth + 2 || dz >= lastPrimaryLaneHalfWidth + 2;
        bool routeCell = prioritizeFastMovementFlow && IsNearPrimaryMovementRoute(cell.x, cell.y);
        float spawnDistance = DistanceManhattan(cell.x, cell.y, spawn);
        float exitDistance = DistanceManhattan(cell.x, cell.y, exit);

        float score = ScoreEnemySpawnCell(cells, cell, spawn, exit) * 0.15f;
        switch (type)
        {
            case BasicEnemyAI.EnemyType.Flying:
                score += kind == CellKind.UpperPlatform ? 26f : kind == CellKind.Platform ? 16f : 8f;
                score += flankBand ? 10f : 0f;
                score += Mathf.Clamp(spawnDistance * 0.8f, 0f, 10f);
                break;
            case BasicEnemyAI.EnemyType.Shooter:
                score += kind == CellKind.UpperPlatform ? 18f : kind == CellKind.Platform ? 11f : 4f;
                score += flankBand ? 8f : 0f;
                score += routeCell ? -6f : 5f;
                score += Mathf.Clamp(exitDistance * 0.45f, 0f, 6f);
                break;
            case BasicEnemyAI.EnemyType.Tank:
                score += kind == CellKind.UpperPlatform ? -8f : kind == CellKind.Platform ? -2f : 12f;
                score += routeCell ? 10f : 2f;
                score += flankBand ? -4f : 6f;
                break;
            case BasicEnemyAI.EnemyType.Grunt:
                score += kind == CellKind.UpperPlatform ? -4f : kind == CellKind.Platform ? 3f : 11f;
                score += routeCell ? 14f : 1f;
                score += flankBand ? -3f : 5f;
                score += Mathf.Clamp(18f - spawnDistance, 0f, 10f) * 0.5f;
                break;
        }

        return score;
    }

    private static bool ContainsAnyEncounterType(List<BasicEnemyAI.EnemyType> types, params BasicEnemyAI.EnemyType[] matches)
    {
        for (int i = 0; i < types.Count; i++)
        {
            for (int j = 0; j < matches.Length; j++)
            {
                if (types[i] == matches[j])
                    return true;
            }
        }

        return false;
    }

    private static void EnsureEncounterType(List<BasicEnemyAI.EnemyType> types, BasicEnemyAI.EnemyType desiredType, int preferredIndex)
    {
        if (types.Contains(desiredType))
            return;

        if (types.Count == 0)
            return;

        int index = Mathf.Clamp(preferredIndex, 0, types.Count - 1);
        types[index] = desiredType;
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
        int cardinalAccessibleNeighbors = 0;
        Vector2Int cell = new Vector2Int(x, z);
        foreach (Vector2Int neighbor in GetNeighbors(cell))
        {
            if (!InBounds(neighbor.x, neighbor.y)) continue;
            if (!IsWalkableForContent(cells[neighbor.x, neighbor.y])) continue;
            if (!CanTraverseCells(cell, neighbor)) continue;
            accessibleNeighbors++;
            if (neighbor.x == x || neighbor.y == z)
                cardinalAccessibleNeighbors++;
        }

        if (cardinalAccessibleNeighbors <= 0)
            return false;

        if (cells[x, z] == CellKind.UpperPlatform)
            return cardinalAccessibleNeighbors >= 2;

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
        SpawnStairsAndParkour(root, cells);
        SpawnTraversalSpines(root, cells);
        SpawnReentryJumpPads(root, cells);
        SpawnBridgeRailings(root, cells);
        SpawnTraversalOpeningMarkers(root, cells);
        SpawnTraversalRouteFrames(root, cells);
        SpawnTraversalLandingMarkers(root, cells);
        SpawnModularSurfaceDetail(root, cells, rng);
        SpawnRouteMarkings(root, cells);
        SpawnModularDistrictMasses(root, cells, rng);
        SpawnModeFloorPresentation(root, cells);
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
        int pylonCount = Mathf.RoundToInt((Mathf.Clamp((width * length) / 145, 6, 16) + profile.extraPylons) * Mathf.Lerp(0.45f, 1f, effectiveDecorativeDensity));
        pylonCount = Mathf.Clamp(pylonCount, 3, 16 + profile.extraPylons);
        for (int i = 0; i < platforms.Count && pylonCount > 0; i += 5)
        {
            Vector2Int cell = platforms[i];
            if (cells[cell.x, cell.y] == CellKind.Spawn || cells[cell.x, cell.y] == CellKind.Exit) continue;
            if (prioritizeFastMovementFlow && IsNearPrimaryMovementRoute(cell.x, cell.y)) continue;
            float y = GetCellHeight(cells[cell.x, cell.y]);
            CreateCube(root, $"ArenaPylon_{cell.x}_{cell.y}_{pylonCount}", CellCenter(cell.x, cell.y, y + 2.6f), new Vector3(0.95f, 5.2f, 0.95f), darkMaterial);
            CreateCube(root, $"PylonGlow_{cell.x}_{cell.y}_{pylonCount}", CellCenter(cell.x, cell.y, y + 5.25f), new Vector3(1.65f, 0.14f, 1.65f), accentMaterial, false);
            CreateCube(root, $"PylonCore_{cell.x}_{cell.y}_{pylonCount}", CellCenter(cell.x, cell.y, y + 2.6f), new Vector3(0.28f, 5.0f, 0.28f), accentMaterial, false);
            pylonCount--;
        }

        int jumpPadCount = Mathf.Clamp((width * length) / 260, 2, 6) + profile.extraJumpPads;
        platforms.Sort((a, b) =>
        {
            int scoreB = ScoreJumpPadCell(cells, b);
            int scoreA = ScoreJumpPadCell(cells, a);
            if (scoreA != scoreB)
                return scoreB.CompareTo(scoreA);

            int tieA = Mathf.Abs(a.x * 11887 ^ a.y * 30971 ^ lastGeneratedSeed);
            int tieB = Mathf.Abs(b.x * 11887 ^ b.y * 30971 ^ lastGeneratedSeed);
            return tieA.CompareTo(tieB);
        });
        List<Vector2Int> usedJumpPads = new List<Vector2Int>();
        for (int i = 0; i < platforms.Count && jumpPadCount > 0; i++)
        {
            Vector2Int cell = platforms[i];
            if (cells[cell.x, cell.y] == CellKind.Spawn || cells[cell.x, cell.y] == CellKind.Exit) continue;
            bool tooClose = false;
            for (int usedIndex = 0; usedIndex < usedJumpPads.Count; usedIndex++)
            {
                if (DistanceManhattan(cell.x, cell.y, usedJumpPads[usedIndex]) < 5)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            float y = GetCellHeight(cells[cell.x, cell.y]);
            GameObject pad = CreateCube(root, $"JumpPad_{cell.x}_{cell.y}_{jumpPadCount}", CellCenter(cell.x, cell.y, y + 0.1f), new Vector3(tileSize * 0.55f, 0.16f, tileSize * 0.55f), accentMaterial);
            JumpPad jumpPad = pad.AddComponent<JumpPad>();
            jumpPad.launchHeight = prioritizeFastMovementFlow ? 13.5f : 11f;
            jumpPad.forwardMomentumBoost = prioritizeFastMovementFlow ? 9f : 7f;
            Collider trigger = pad.GetComponent<Collider>();
            if (trigger != null) trigger.isTrigger = true;
            usedJumpPads.Add(cell);
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

        ParticleSystem haze = CreateAtmosphereEmitter(fxRoot.transform, "UpperHaze", 12f + profile.skylineDensity * 10f, 0.5f, new Color(profile.fogColor.r, profile.fogColor.g, profile.fogColor.b, 0.08f), new Vector3(width * tileSize * 1.2f, 3.4f, length * tileSize * 1.2f), true);
        haze.transform.localPosition = new Vector3(0f, 15f + profile.shellHeight * 0.2f, 0f);

        if (arenaMode == ArenaMode.Boss)
        {
            ParticleSystem sparks = CreateAtmosphereEmitter(fxRoot.transform, "BossSparks", 8f, 0.12f, profile.sparkColor, new Vector3(8f, 1.4f, 8f), false);
            sparks.transform.localPosition = Vector3.zero;
            SpawnBossAtmosphereFX(fxRoot.transform, profile);
        }
        else if (arenaMode == ArenaMode.Shop)
        {
            SpawnShopAtmosphereFX(fxRoot.transform, profile);
        }
        else
        {
            ParticleSystem embers = CreateAtmosphereEmitter(fxRoot.transform, "ShellGlints", 4f + profile.skylineDensity * 4f, 0.18f, new Color(profile.structureGlowColor.r, profile.structureGlowColor.g, profile.structureGlowColor.b, 0.18f), new Vector3(width * tileSize, 9f, length * tileSize), false);
            embers.transform.localPosition = new Vector3(0f, profile.shellHeight * 0.45f, 0f);
        }
    }

    private void SpawnShopAtmosphereFX(Transform parent, ThemeProfile profile)
    {
        switch (Mathf.Abs(themeIndex) % 4)
        {
            case 1:
                ParticleSystem lift = CreateAtmosphereEmitter(parent, "ShopLiftMotes", 8f, 0.36f, new Color(profile.structureGlowColor.r, profile.structureGlowColor.g, profile.structureGlowColor.b, 0.16f), new Vector3(width * tileSize * 0.55f, 2.8f, length * tileSize * 0.4f), true);
                lift.transform.localPosition = new Vector3(0f, 5.2f, 0f);
                break;
            case 2:
                ParticleSystem heat = CreateAtmosphereEmitter(parent, "ShopHeatCinders", 10f, 0.2f, new Color(profile.sparkColor.r, profile.sparkColor.g, profile.sparkColor.b, 0.2f), new Vector3(width * tileSize * 0.6f, 2.2f, length * tileSize * 0.32f), false);
                heat.transform.localPosition = new Vector3(0f, 3.4f, -tileSize * 3f);
                break;
            case 3:
                ParticleSystem signal = CreateAtmosphereEmitter(parent, "ShopSignalDust", 7f, 0.32f, new Color(profile.structureGlowColor.r, profile.structureGlowColor.g, profile.structureGlowColor.b, 0.14f), new Vector3(width * tileSize * 0.52f, 2.4f, length * tileSize * 0.36f), true);
                signal.transform.localPosition = new Vector3(0f, 4.4f, tileSize * 1.5f);
                break;
            default:
                ParticleSystem scan = CreateAtmosphereEmitter(parent, "ShopScanGlints", 6f, 0.22f, new Color(profile.structureGlowColor.r, profile.structureGlowColor.g, profile.structureGlowColor.b, 0.14f), new Vector3(width * tileSize * 0.5f, 2.2f, length * tileSize * 0.28f), false);
                scan.transform.localPosition = new Vector3(0f, 4.8f, 0f);
                break;
        }
    }

    private void SpawnBossAtmosphereFX(Transform parent, ThemeProfile profile)
    {
        switch (Mathf.Abs(themeIndex) % 4)
        {
            case 1:
                ParticleSystem rise = CreateAtmosphereEmitter(parent, "BossHighRiseMotes", 9f, 0.42f, new Color(profile.structureGlowColor.r, profile.structureGlowColor.g, profile.structureGlowColor.b, 0.18f), new Vector3(10f, 5.5f, 10f), true);
                rise.transform.localPosition = new Vector3(0f, 6.8f, 0f);
                break;
            case 2:
                ParticleSystem embers = CreateAtmosphereEmitter(parent, "BossHeatAsh", 12f, 0.18f, new Color(profile.sparkColor.r, profile.sparkColor.g, profile.sparkColor.b, 0.22f), new Vector3(9f, 2.6f, 9f), false);
                embers.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                break;
            case 3:
                ParticleSystem staticDust = CreateAtmosphereEmitter(parent, "BossSignalMotes", 8f, 0.35f, new Color(profile.structureGlowColor.r, profile.structureGlowColor.g, profile.structureGlowColor.b, 0.16f), new Vector3(10f, 4.4f, 10f), true);
                staticDust.transform.localPosition = new Vector3(0f, 5.6f, 0f);
                break;
            default:
                ParticleSystem crossfire = CreateAtmosphereEmitter(parent, "BossCrossfireShards", 7f, 0.24f, new Color(profile.structureGlowColor.r, profile.structureGlowColor.g, profile.structureGlowColor.b, 0.14f), new Vector3(8.5f, 3.2f, 8.5f), false);
                crossfire.transform.localPosition = new Vector3(0f, 4.6f, 0f);
                break;
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
            // This shader is also listed in GraphicsSettings so standalone builds do
            // not strip it. Keep the fallback for projects using a different pipeline.
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = FindUrpShader(true);
            if (shader != null)
            {
                Material mat = new Material(shader) { name = $"{name} Material" };
                if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
                if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
                renderer.material = mat;
            }
        }

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = particleLifetime;
        main.startSpeed = drifting ? 0.35f : 0.12f;
        main.startSize = drifting ? 0.14f : 0.08f;
        main.startColor = color;
        main.maxParticles = Mathf.Clamp(Mathf.RoundToInt(rate * particleLifetime * 18f), 48, drifting ? 260 : 120);

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
                float widthScale = cells[x, z] == CellKind.Platform || cells[x, z] == CellKind.Bridge
                    ? tileSize * 1.08f
                    : tileSize * 1.04f;
                CreateCube(root, $"ModularDepthPillar_{x}_{z}", CellCenter(x, z, topY), new Vector3(widthScale, pillarDepth, widthScale), darkMaterial, true);
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

        if (prioritizeFastMovementFlow && arenaMode == ArenaMode.Combat)
        {
            RegisterHighSpeedRecoveryBands(cells);
        }
    }

    private void RegisterHighSpeedRecoveryBands(CellKind[,] cells)
    {
        int[] xs =
        {
            Mathf.Clamp(lastLayoutCenter.x - lastSideLaneOffset, 2, width - 3),
            Mathf.Clamp(lastLayoutCenter.x + lastSideLaneOffset, 2, width - 3),
            Mathf.Clamp(lastLayoutCenter.x, 2, width - 3)
        };
        int[] zs =
        {
            Mathf.Clamp(lastLayoutCenter.y - lastSideLaneOffset, 2, length - 3),
            Mathf.Clamp(lastLayoutCenter.y + lastSideLaneOffset, 2, length - 3),
            Mathf.Clamp(lastLayoutCenter.y, 2, length - 3)
        };

        for (int i = 0; i < xs.Length; i++)
        {
            AddRecoveryPointForColumn(cells, xs[i], Mathf.Clamp(length / 4, 4, 7));
        }

        for (int i = 0; i < zs.Length; i++)
        {
            AddRecoveryPointForRow(cells, zs[i], Mathf.Clamp(width / 4, 4, 7));
        }
    }

    private void AddRecoveryPointForColumn(CellKind[,] cells, int x, int stride)
    {
        for (int z = 3; z < length - 3; z += Mathf.Max(1, stride))
        {
            if (!InBounds(x, z) || !IsWalkableForContentCell(x, z))
                continue;
            AddRecoveryPointForCell(cells, new Vector2Int(x, z));
        }
    }

    private void AddRecoveryPointForRow(CellKind[,] cells, int z, int stride)
    {
        for (int x = 3; x < width - 3; x += Mathf.Max(1, stride))
        {
            if (!InBounds(x, z) || !IsWalkableForContentCell(x, z))
                continue;
            AddRecoveryPointForCell(cells, new Vector2Int(x, z));
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
        List<StairVisualCandidate> candidates = CollectStairVisualCandidates(cells);
        SpawnSelectedStairVisuals(root, cells, candidates);

        Vector2Int center = new Vector2Int(width / 2, length / 2);
        if (prioritizeFastMovementFlow)
        {
            int offset = Mathf.Clamp(lastSideLaneOffset, 4, 8);
            SpawnParkourCluster(root, new Vector2Int(center.x - offset, center.y - offset));
            SpawnParkourCluster(root, new Vector2Int(center.x + offset, center.y + offset));
        }
        else
        {
            SpawnParkourCluster(root, center + new Vector2Int(-5, -4));
            SpawnParkourCluster(root, center + new Vector2Int(5, 4));
        }
    }

    private void SpawnReentryJumpPads(Transform root, CellKind[,] cells)
    {
        if (arenaMode != ArenaMode.Combat || !prioritizeFastMovementFlow)
            return;

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = 2; x < width - 2; x++)
        {
            for (int z = 2; z < length - 2; z++)
            {
                if (!IsReliableEnemyCell(cells, x, z))
                    continue;
                if (!IsNearPrimaryMovementRoute(x, z))
                    continue;
                if (cells[x, z] == CellKind.UpperPlatform)
                    continue;

                int dx = Mathf.Abs(x - lastLayoutCenter.x);
                int dz = Mathf.Abs(z - lastLayoutCenter.y);
                bool sideBand = dx >= lastPrimaryLaneHalfWidth + 1 || dz >= lastPrimaryLaneHalfWidth + 1;
                if (!sideBand)
                    continue;

                candidates.Add(new Vector2Int(x, z));
            }
        }

        int stride = Mathf.Clamp(Mathf.Min(width, length) / 4, 4, 7);
        int made = 0;
        for (int i = 0; i < candidates.Count; i += stride)
        {
            Vector2Int cell = candidates[i];
            float y = GetCellHeight(cells[cell.x, cell.y]);
            GameObject pad = CreateCube(root, $"ReentryJumpPad_{cell.x}_{cell.y}_{made}", CellCenter(cell.x, cell.y, y + 0.1f), new Vector3(tileSize * 0.62f, 0.16f, tileSize * 0.62f), accentMaterial);
            JumpPad jumpPad = pad.AddComponent<JumpPad>();
            jumpPad.launchHeight = (platformLevel * levelHeight) + 8f;
            jumpPad.forwardMomentumBoost = 8.5f;
            Collider trigger = pad.GetComponent<Collider>();
            if (trigger != null)
                trigger.isTrigger = true;

            CreateCube(root, $"ReentryJumpPadGlow_{cell.x}_{cell.y}_{made}", CellCenter(cell.x, cell.y, y + 0.18f), new Vector3(tileSize * 0.42f, 0.05f, tileSize * 0.42f), spawnMaterial, false);
            made++;
            if (made >= 4)
                break;
        }
    }

    private void SpawnTraversalSpines(Transform root, CellKind[,] cells)
    {
        if (arenaMode != ArenaMode.Combat || !prioritizeFastMovementFlow)
            return;

        int[] laneXs =
        {
            Mathf.Clamp(lastLayoutCenter.x - lastSideLaneOffset, 2, width - 3),
            Mathf.Clamp(lastLayoutCenter.x + lastSideLaneOffset, 2, width - 3)
        };
        int[] laneZs =
        {
            Mathf.Clamp(lastLayoutCenter.y - lastSideLaneOffset, 2, length - 3),
            Mathf.Clamp(lastLayoutCenter.y + lastSideLaneOffset, 2, length - 3)
        };

        for (int i = 0; i < laneXs.Length; i++)
            SpawnTraversalSpineLine(root, cells, true, laneXs[i], Mathf.Clamp(length / 5, 4, 7), i);

        for (int i = 0; i < laneZs.Length; i++)
            SpawnTraversalSpineLine(root, cells, false, laneZs[i], Mathf.Clamp(width / 5, 4, 7), i + 2);
    }

    private void SpawnTraversalSpineLine(Transform root, CellKind[,] cells, bool verticalLine, int fixedCoord, int stride, int variant)
    {
        if (stride <= 0)
            return;

        int start = 3;
        int end = verticalLine ? length - 3 : width - 3;
        for (int walker = start; walker < end; walker += stride)
        {
            int x = verticalLine ? fixedCoord : walker;
            int z = verticalLine ? walker : fixedCoord;
            if (!InBounds(x, z) || !IsWalkableForContentCell(x, z))
                continue;
            if (!IsNearPrimaryMovementRoute(x, z))
                continue;

            float y = GetCellHeight(cells[x, z]);
            float height = Mathf.Lerp(levelHeight * 1.8f, levelHeight * 2.7f, ((walker + variant) & 1) == 0 ? 0.35f : 0.8f);
            Vector3 center = CellCenter(x, z, y + height * 0.5f);
            Vector3 coreScale = verticalLine
                ? new Vector3(tileSize * 0.18f, height, tileSize * 0.9f)
                : new Vector3(tileSize * 0.9f, height, tileSize * 0.18f);
            Vector3 glowScale = verticalLine
                ? new Vector3(tileSize * 0.055f, height * 0.72f, tileSize * 0.62f)
                : new Vector3(tileSize * 0.62f, height * 0.72f, tileSize * 0.055f);

            CreateCube(root, $"Wall_TraversalSpine_{x}_{z}_{variant}", center, coreScale, darkMaterial, true);
            CreateCube(root, $"TraversalSpineGlow_{x}_{z}_{variant}", center + Vector3.up * (height * 0.08f), glowScale, accentMaterial, false);
            CreateCube(root, $"TraversalSpineCap_{x}_{z}_{variant}", center + Vector3.up * (height * 0.5f + 0.14f), verticalLine ? new Vector3(tileSize * 0.42f, 0.12f, tileSize * 1.08f) : new Vector3(tileSize * 1.08f, 0.12f, tileSize * 0.42f), darkMaterial, false);
        }
    }

    private List<StairVisualCandidate> CollectStairVisualCandidates(CellKind[,] cells)
    {
        List<StairVisualCandidate> candidates = new List<StairVisualCandidate>();
        for (int x = 2; x < width - 2; x++)
        {
            for (int z = 2; z < length - 2; z++)
            {
                if (!IsWalkableForContentCell(x, z)) continue;
                TryAddStairVisualCandidate(candidates, cells, x, z, 1, 0);
                TryAddStairVisualCandidate(candidates, cells, x, z, 0, 1);
            }
        }

        candidates.Sort((a, b) => b.score.CompareTo(a.score));
        return candidates;
    }

    private void TryAddStairVisualCandidate(List<StairVisualCandidate> candidates, CellKind[,] cells, int x, int z, int dx, int dz)
    {
        int ex = x + dx;
        int ez = z + dz;
        if (!InBounds(ex, ez)) return;
        if (!IsWalkableForContentCell(ex, ez)) return;

        float fromY = GetCellHeight(cells[x, z]);
        float toY = GetCellHeight(cells[ex, ez]);
        if (Mathf.Abs(fromY - toY) <= 1f) return;
        if (Mathf.Abs(fromY - toY) > levelHeight + 0.75f) return;
        if (HasTraversalConnectorBetween(new Vector2Int(x, z), new Vector2Int(ex, ez)) == false)
            return;

        bool toHigher = toY > fromY;
        int score = 0;
        score += Mathf.RoundToInt(Mathf.Abs(toY - fromY) * 10f);
        Vector2Int focusCenter = prioritizeFastMovementFlow ? lastLayoutCenter : new Vector2Int(width / 2, length / 2);
        score -= Mathf.Abs(x - focusCenter.x) + Mathf.Abs(z - focusCenter.y);
        if (prioritizeFastMovementFlow && IsNearPrimaryMovementRoute(x, z))
            score += 8;
        if (Mathf.Abs((toY - fromY) - levelHeight) < 0.4f || Mathf.Abs((fromY - toY) - levelHeight) < 0.4f)
            score += 18;
        score += (cells[x, z] == CellKind.Platform || cells[ex, ez] == CellKind.Platform) ? 4 : 0;
        score += (cells[x, z] == CellKind.UpperPlatform || cells[ex, ez] == CellKind.UpperPlatform) ? 6 : 0;
        score += Mathf.Abs(x * 92821 ^ z * 68917 ^ dx * 2713 ^ dz * 3821 ^ lastGeneratedSeed) % 5;

        candidates.Add(new StairVisualCandidate
        {
            x = toHigher ? x : ex,
            z = toHigher ? z : ez,
            dx = toHigher ? dx : -dx,
            dz = toHigher ? dz : -dz,
            low = Mathf.Min(fromY, toY),
            high = Mathf.Max(fromY, toY),
            score = score
        });
    }

    private int SpawnSelectedStairVisuals(Transform root, CellKind[,] cells, List<StairVisualCandidate> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return 0;

        int targetCount = arenaMode == ArenaMode.Shop
            ? 1
            : arenaMode == ArenaMode.Boss
                ? Mathf.Clamp(Mathf.RoundToInt((width + length) / 20f), 2, 4)
                : Mathf.Clamp(Mathf.RoundToInt((width + length) / 8f), 5, 10);

        int stairsMade = 0;
        HashSet<long> usedEdges = new HashSet<long>();
        List<Vector2Int> usedAnchors = new List<Vector2Int>();
        int[,] elevatedSurfaceMap = BuildElevatedSurfaceMap(cells);
        List<StairVisualCandidate> primaryCandidates = new List<StairVisualCandidate>();
        List<StairVisualCandidate> secondaryCandidates = new List<StairVisualCandidate>();
        for (int i = 0; i < candidates.Count; i++)
        {
            StairVisualCandidate candidate = candidates[i];
            bool firstTierConnector = Mathf.Abs((candidate.high - candidate.low) - levelHeight) < 0.4f;
            bool routeCandidate = IsNearPrimaryMovementRoute(candidate.x, candidate.z) ||
                                  IsNearPrimaryMovementRoute(candidate.x + candidate.dx, candidate.z + candidate.dz);
            if (firstTierConnector || routeCandidate)
                primaryCandidates.Add(candidate);
            else
                secondaryCandidates.Add(candidate);
        }

        stairsMade += TrySpawnGuaranteedFirstTierSurfaceStairs(root, primaryCandidates, elevatedSurfaceMap, usedEdges, usedAnchors);
        stairsMade += TrySpawnGuaranteedSurfaceStairs(root, primaryCandidates, elevatedSurfaceMap, usedEdges, usedAnchors);
        stairsMade += TrySpawnGuaranteedSurfaceStairs(root, secondaryCandidates, elevatedSurfaceMap, usedEdges, usedAnchors);
        stairsMade += TrySpawnStairCandidates(root, primaryCandidates, usedEdges, usedAnchors, targetCount - stairsMade);
        if (stairsMade < targetCount)
            stairsMade += TrySpawnStairCandidates(root, secondaryCandidates, usedEdges, usedAnchors, targetCount - stairsMade);

        return stairsMade;
    }

    private int[,] BuildElevatedSurfaceMap(CellKind[,] cells)
    {
        int[,] surfaceMap = new int[width, length];
        int nextSurfaceId = 1;
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                if (surfaceMap[x, z] != 0 || !IsWalkableForContentCell(x, z))
                    continue;
                if (GetCellHeight(cells[x, z]) <= 0.5f)
                    continue;

                surfaceMap[x, z] = nextSurfaceId;
                queue.Enqueue(new Vector2Int(x, z));
                while (queue.Count > 0)
                {
                    Vector2Int cell = queue.Dequeue();
                    Vector2Int[] neighbors =
                    {
                        new Vector2Int(cell.x + 1, cell.y),
                        new Vector2Int(cell.x - 1, cell.y),
                        new Vector2Int(cell.x, cell.y + 1),
                        new Vector2Int(cell.x, cell.y - 1)
                    };

                    for (int i = 0; i < neighbors.Length; i++)
                    {
                        Vector2Int neighbor = neighbors[i];
                        if (!InBounds(neighbor.x, neighbor.y) || surfaceMap[neighbor.x, neighbor.y] != 0)
                            continue;
                        if (!IsWalkableForContentCell(neighbor.x, neighbor.y))
                            continue;
                        if (!IsSameElevatedSurface(cells, cell.x, cell.y, neighbor.x, neighbor.y))
                            continue;

                        surfaceMap[neighbor.x, neighbor.y] = nextSurfaceId;
                        queue.Enqueue(neighbor);
                    }
                }

                nextSurfaceId++;
            }
        }

        return surfaceMap;
    }

    private int TrySpawnGuaranteedFirstTierSurfaceStairs(
        Transform root,
        List<StairVisualCandidate> candidates,
        int[,] elevatedSurfaceMap,
        HashSet<long> usedEdges,
        List<Vector2Int> usedAnchors)
    {
        if (candidates == null || candidates.Count == 0 || elevatedSurfaceMap == null)
            return 0;

        Dictionary<int, StairVisualCandidate> bestBySurface = new Dictionary<int, StairVisualCandidate>();
        for (int i = 0; i < candidates.Count; i++)
        {
            StairVisualCandidate candidate = candidates[i];
            if (Mathf.Abs((candidate.high - candidate.low) - levelHeight) >= 0.4f)
                continue;

            int surfaceId = elevatedSurfaceMap[candidate.x, candidate.z];
            if (surfaceId <= 0)
                continue;

            if (!bestBySurface.TryGetValue(surfaceId, out StairVisualCandidate existing) || candidate.score > existing.score)
                bestBySurface[surfaceId] = candidate;
        }

        if (bestBySurface.Count == 0)
            return 0;

        List<StairVisualCandidate> guaranteed = new List<StairVisualCandidate>(bestBySurface.Values);
        guaranteed.Sort((a, b) => b.score.CompareTo(a.score));

        int stairsMade = 0;
        for (int i = 0; i < guaranteed.Count; i++)
        {
            StairVisualCandidate candidate = guaranteed[i];
            int ex = candidate.x + candidate.dx;
            int ez = candidate.z + candidate.dz;
            long edgeKey = EncodeTraversalKey(new Vector2Int(candidate.x, candidate.z), new Vector2Int(ex, ez));
            if (usedEdges.Contains(edgeKey))
                continue;

            CreateStairVisual(root, candidate);
            usedEdges.Add(edgeKey);
            stairVisualEdges.Add(edgeKey);
            stairVisualEdges.Add(EncodeTraversalKey(new Vector2Int(ex, ez), new Vector2Int(candidate.x, candidate.z)));
            usedAnchors.Add(new Vector2Int(candidate.x, candidate.z));
            usedAnchors.Add(new Vector2Int(ex, ez));
            stairsMade++;
        }

        return stairsMade;
    }

    private int TrySpawnGuaranteedSurfaceStairs(
        Transform root,
        List<StairVisualCandidate> candidates,
        int[,] elevatedSurfaceMap,
        HashSet<long> usedEdges,
        List<Vector2Int> usedAnchors)
    {
        if (candidates == null || candidates.Count == 0 || elevatedSurfaceMap == null)
            return 0;

        Dictionary<int, StairVisualCandidate> bestBySurface = new Dictionary<int, StairVisualCandidate>();
        for (int i = 0; i < candidates.Count; i++)
        {
            StairVisualCandidate candidate = candidates[i];
            int surfaceId = elevatedSurfaceMap[candidate.x, candidate.z];
            if (surfaceId <= 0)
                continue;

            if (!bestBySurface.TryGetValue(surfaceId, out StairVisualCandidate existing) || candidate.score > existing.score)
                bestBySurface[surfaceId] = candidate;
        }

        if (bestBySurface.Count == 0)
            return 0;

        List<StairVisualCandidate> guaranteed = new List<StairVisualCandidate>(bestBySurface.Values);
        guaranteed.Sort((a, b) => b.score.CompareTo(a.score));
        return TrySpawnStairCandidates(root, guaranteed, usedEdges, usedAnchors, guaranteed.Count);
    }

    private int TrySpawnStairCandidates(
        Transform root,
        List<StairVisualCandidate> candidates,
        HashSet<long> usedEdges,
        List<Vector2Int> usedAnchors,
        int remaining)
    {
        if (remaining <= 0 || candidates == null || candidates.Count == 0)
            return 0;

        int stairsMade = 0;
        for (int i = 0; i < candidates.Count && stairsMade < remaining; i++)
        {
            StairVisualCandidate candidate = candidates[i];
            int ex = candidate.x + candidate.dx;
            int ez = candidate.z + candidate.dz;
            long edgeKey = EncodeTraversalKey(new Vector2Int(candidate.x, candidate.z), new Vector2Int(ex, ez));
            if (usedEdges.Contains(edgeKey))
                continue;
            if (IsNearExistingStairAnchor(usedAnchors, candidate.x, candidate.z, ex, ez))
                continue;

            CreateStairVisual(root, candidate);
            usedEdges.Add(edgeKey);
            stairVisualEdges.Add(edgeKey);
            stairVisualEdges.Add(EncodeTraversalKey(new Vector2Int(ex, ez), new Vector2Int(candidate.x, candidate.z)));
            usedAnchors.Add(new Vector2Int(candidate.x, candidate.z));
            usedAnchors.Add(new Vector2Int(ex, ez));
            stairsMade++;
        }

        return stairsMade;
    }

    private bool IsNearExistingStairAnchor(List<Vector2Int> anchors, int x, int z, int ex, int ez)
    {
        if (anchors == null || anchors.Count == 0)
            return false;

        Vector2Int a = new Vector2Int(x, z);
        Vector2Int b = new Vector2Int(ex, ez);
        for (int i = 0; i < anchors.Count; i++)
        {
            if (Mathf.Abs(anchors[i].x - a.x) + Mathf.Abs(anchors[i].y - a.y) <= 1)
                return true;
            if (Mathf.Abs(anchors[i].x - b.x) + Mathf.Abs(anchors[i].y - b.y) <= 1)
                return true;
        }

        return false;
    }

    private void CreateStairVisual(Transform root, StairVisualCandidate candidate)
    {
        int x = candidate.x;
        int z = candidate.z;
        int dx = candidate.dx;
        int dz = candidate.dz;
        float low = candidate.low;
        float high = candidate.high;

        int steps = Mathf.Clamp(Mathf.RoundToInt((high - low) / Mathf.Max(0.01f, levelHeight) * 5f), 5, 7);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)(steps + 1);
            Vector3 pos = CellCenter(x, z, Mathf.Lerp(low, high, t) + 0.12f);
            pos += new Vector3(dx * tileSize * t, 0f, dz * tileSize * t);
            Vector3 scale = new Vector3(dx == 0 ? tileSize * 0.82f : tileSize * 0.58f, 0.2f, dz == 0 ? tileSize * 0.82f : tileSize * 0.58f);
            CreateCube(root, $"Step_{x}_{z}_{i}", pos, scale, darkMaterial);
        }
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
                bool northRail = ShouldPlaceRailOnEdge(cells, new Vector2Int(x, z), new Vector2Int(x, z + 1));
                bool southRail = ShouldPlaceRailOnEdge(cells, new Vector2Int(x, z), new Vector2Int(x, z - 1));
                bool eastRail = ShouldPlaceRailOnEdge(cells, new Vector2Int(x, z), new Vector2Int(x + 1, z));
                bool westRail = ShouldPlaceRailOnEdge(cells, new Vector2Int(x, z), new Vector2Int(x - 1, z));

                float railY = y + 0.75f;
                if (northRail)
                    CreateCube(root, $"RailN_{x}_{z}", CellCenter(x, z, railY) + new Vector3(0f, 0f, tileSize * 0.48f), new Vector3(tileSize, 0.36f, 0.16f), darkMaterial);
                if (southRail)
                    CreateCube(root, $"RailS_{x}_{z}", CellCenter(x, z, railY) - new Vector3(0f, 0f, tileSize * 0.48f), new Vector3(tileSize, 0.36f, 0.16f), darkMaterial);
                if (eastRail)
                    CreateCube(root, $"RailE_{x}_{z}", CellCenter(x, z, railY) + new Vector3(tileSize * 0.48f, 0f, 0f), new Vector3(0.16f, 0.36f, tileSize), darkMaterial);
                if (westRail)
                    CreateCube(root, $"RailW_{x}_{z}", CellCenter(x, z, railY) - new Vector3(tileSize * 0.48f, 0f, 0f), new Vector3(0.16f, 0.36f, tileSize), darkMaterial);
            }
        }
    }

    private bool ShouldPlaceRailOnEdge(CellKind[,] cells, Vector2Int from, Vector2Int to)
    {
        if (!InBounds(from.x, from.y))
            return false;
        if (!InBounds(to.x, to.y))
            return true;
        if (!IsLayoutWalkable(cells[to.x, to.y]))
            return true;
        if (IsSameElevatedSurface(cells, from.x, from.y, to.x, to.y))
            return false;
        if (HasStairVisualBetween(from, to))
            return false;

        return true;
    }

    private void SpawnTraversalOpeningMarkers(Transform root, CellKind[,] cells)
    {
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                CellKind kind = cells[x, z];
                if (kind != CellKind.Bridge && kind != CellKind.Platform && kind != CellKind.UpperPlatform) continue;

                Vector2Int from = new Vector2Int(x, z);
                TryCreateTraversalOpening(root, cells, from, new Vector2Int(x, z + 1), 0);
                TryCreateTraversalOpening(root, cells, from, new Vector2Int(x, z - 1), 1);
                TryCreateTraversalOpening(root, cells, from, new Vector2Int(x + 1, z), 2);
                TryCreateTraversalOpening(root, cells, from, new Vector2Int(x - 1, z), 3);
            }
        }
    }

    private void TryCreateTraversalOpening(Transform root, CellKind[,] cells, Vector2Int from, Vector2Int to, int direction)
    {
        if (!InBounds(to.x, to.y)) return;
        if (!IsLayoutWalkable(cells[to.x, to.y])) return;
        if (IsSameElevatedSurface(cells, from.x, from.y, to.x, to.y)) return;
        if (!HasTraversalConnectorBetween(from, to)) return;
        if (ShouldPlaceRailOnEdge(cells, from, to)) return;

        float y = GetCellHeight(cells[from.x, from.y]);
        float toY = GetCellHeight(cells[to.x, to.y]);
        float drop = y - toY;
        if (drop <= levelHeight * 0.45f) return;

        float railY = y + 0.75f;
        bool northSouth = direction == 0 || direction == 1;
        float sign = direction == 0 || direction == 2 ? 1f : -1f;
        Vector3 edgeOffset = northSouth
            ? new Vector3(0f, 0f, sign * tileSize * 0.48f)
            : new Vector3(sign * tileSize * 0.48f, 0f, 0f);
        Vector3 center = CellCenter(from.x, from.y, railY) + edgeOffset;
        Vector3 stubAOffset = northSouth ? new Vector3(-tileSize * 0.36f, 0f, 0f) : new Vector3(0f, 0f, -tileSize * 0.36f);
        Vector3 stubBOffset = -stubAOffset;
        Vector3 stubScale = northSouth
            ? new Vector3(tileSize * 0.24f, 0.36f, 0.16f)
            : new Vector3(0.16f, 0.36f, tileSize * 0.24f);

        CreateCube(root, $"BrokenRailA_{from.x}_{from.y}_{direction}", center + stubAOffset, stubScale, darkMaterial);
        CreateCube(root, $"BrokenRailB_{from.x}_{from.y}_{direction}", center + stubBOffset, stubScale, darkMaterial);

        Vector3 glowCenter = CellCenter(from.x, from.y, y + floorThickness * 0.5f + 0.09f) + edgeOffset * 0.42f;
        Vector3 glowScale = northSouth
            ? new Vector3(tileSize * 0.42f, 0.035f, 0.14f)
            : new Vector3(0.14f, 0.035f, tileSize * 0.42f);
        int hash = Mathf.Abs(from.x * 57193 ^ from.y * 83401 ^ direction * 26561 ^ lastGeneratedSeed);
        if (PassesDensity(hash, effectiveMicroDetailDensity, 3, 9))
            CreateCube(root, $"TraversalGapGlow_{from.x}_{from.y}_{direction}", glowCenter, glowScale, accentMaterial, false);
    }

    private void SpawnTraversalRouteFrames(Transform root, CellKind[,] cells)
    {
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                if (!IsElevatedKind(cells[x, z])) continue;

                Vector2Int from = new Vector2Int(x, z);
                TryCreateTraversalRouteFrame(root, cells, from, new Vector2Int(x, z + 1), 0);
                TryCreateTraversalRouteFrame(root, cells, from, new Vector2Int(x, z - 1), 1);
                TryCreateTraversalRouteFrame(root, cells, from, new Vector2Int(x + 1, z), 2);
                TryCreateTraversalRouteFrame(root, cells, from, new Vector2Int(x - 1, z), 3);
            }
        }
    }

    private void TryCreateTraversalRouteFrame(Transform root, CellKind[,] cells, Vector2Int from, Vector2Int to, int direction)
    {
        if (!InBounds(to.x, to.y)) return;
        if (!IsLayoutWalkable(cells[to.x, to.y])) return;
        if (!HasTraversalConnectorBetween(from, to)) return;

        float y = GetCellHeight(cells[from.x, from.y]);
        float toY = GetCellHeight(cells[to.x, to.y]);
        if (y <= toY + levelHeight * 0.45f) return;
        int cadence = Mathf.RoundToInt(Mathf.Lerp(7f, 3f, effectiveDecorativeDensity));
        if (!ShouldUseStrongRouteMarker(from, direction, cadence)) return;

        bool northSouth = direction == 0 || direction == 1;
        float sign = direction == 0 || direction == 2 ? 1f : -1f;
        Vector3 edgeOffset = northSouth
            ? new Vector3(0f, 0f, sign * tileSize * 0.43f)
            : new Vector3(sign * tileSize * 0.43f, 0f, 0f);
        Vector3 center = CellCenter(from.x, from.y, y + floorThickness * 0.5f + 0.07f) + edgeOffset;
        Vector3 lipScale = northSouth
            ? new Vector3(tileSize * 0.68f, 0.045f, 0.18f)
            : new Vector3(0.18f, 0.045f, tileSize * 0.68f);
        CreateCube(root, $"RouteLip_{from.x}_{from.y}_{direction}", center, lipScale, accentMaterial, false);

        Vector3 sideOffset = northSouth
            ? new Vector3(tileSize * 0.38f, 0f, 0f)
            : new Vector3(0f, 0f, tileSize * 0.38f);
        Vector3 postScale = new Vector3(0.18f, 0.82f, 0.18f);
        CreateCube(root, $"RoutePostA_{from.x}_{from.y}_{direction}", center + sideOffset + Vector3.up * 0.42f, postScale, darkMaterial, false);
        CreateCube(root, $"RoutePostB_{from.x}_{from.y}_{direction}", center - sideOffset + Vector3.up * 0.42f, postScale, darkMaterial, false);

        int openingHash = Mathf.Abs(from.x * 92837111 ^ from.y * 689287499 ^ direction * 283923481 ^ lastGeneratedSeed);
        if (openingHash % 18 == 0)
        {
            Vector3 beamScale = northSouth
                ? new Vector3(tileSize * 0.92f, 0.12f, 0.16f)
                : new Vector3(0.16f, 0.12f, tileSize * 0.92f);
            CreateCube(root, $"RouteOverhead_{from.x}_{from.y}_{direction}", center + Vector3.up * 1.02f, beamScale, darkMaterial, false);
        }
    }

    private void SpawnTraversalLandingMarkers(Transform root, CellKind[,] cells)
    {
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                if (!IsElevatedKind(cells[x, z])) continue;

                Vector2Int from = new Vector2Int(x, z);
                TryCreateTraversalLandingMarker(root, cells, from, new Vector2Int(x, z + 1), 0);
                TryCreateTraversalLandingMarker(root, cells, from, new Vector2Int(x, z - 1), 1);
                TryCreateTraversalLandingMarker(root, cells, from, new Vector2Int(x + 1, z), 2);
                TryCreateTraversalLandingMarker(root, cells, from, new Vector2Int(x - 1, z), 3);
            }
        }
    }

    private void TryCreateTraversalLandingMarker(Transform root, CellKind[,] cells, Vector2Int from, Vector2Int to, int direction)
    {
        if (!InBounds(to.x, to.y)) return;
        if (!IsLayoutWalkable(cells[to.x, to.y])) return;
        if (!HasTraversalConnectorBetween(from, to)) return;

        float highY = GetCellHeight(cells[from.x, from.y]);
        float lowY = GetCellHeight(cells[to.x, to.y]);
        if (highY <= lowY + levelHeight * 0.45f) return;
        int cadence = Mathf.RoundToInt(Mathf.Lerp(7f, 3f, effectiveDecorativeDensity));
        if (!ShouldUseStrongRouteMarker(from, direction, cadence)) return;

        bool northSouth = direction == 0 || direction == 1;
        float sign = direction == 0 || direction == 2 ? -1f : 1f;
        Vector3 edgeOffset = northSouth
            ? new Vector3(0f, 0f, sign * tileSize * 0.32f)
            : new Vector3(sign * tileSize * 0.32f, 0f, 0f);
        Vector3 center = CellCenter(to.x, to.y, lowY + floorThickness * 0.5f + 0.085f) + edgeOffset;

        Vector3 padScale = northSouth
            ? new Vector3(tileSize * 0.54f, 0.035f, tileSize * 0.28f)
            : new Vector3(tileSize * 0.28f, 0.035f, tileSize * 0.54f);
        Vector3 slashScale = northSouth
            ? new Vector3(tileSize * 0.22f, 0.04f, 0.055f)
            : new Vector3(0.055f, 0.04f, tileSize * 0.22f);

        CreateCube(root, $"RouteLandingPad_{from.x}_{from.y}_{direction}", center, padScale, accentMaterial, false);
        Vector3 sideOffset = northSouth
            ? new Vector3(tileSize * 0.18f, 0f, 0f)
            : new Vector3(0f, 0f, tileSize * 0.18f);
        CreateCube(root, $"RouteLandingTickA_{from.x}_{from.y}_{direction}", center + sideOffset + Vector3.up * 0.028f, slashScale, darkMaterial, false);
        CreateCube(root, $"RouteLandingTickB_{from.x}_{from.y}_{direction}", center - sideOffset + Vector3.up * 0.028f, slashScale, darkMaterial, false);
    }

    private bool ShouldUseStrongRouteMarker(Vector2Int from, int direction, int cadence)
    {
        int hash = Mathf.Abs(from.x * 92837111 ^ from.y * 689287499 ^ direction * 283923481 ^ lastGeneratedSeed);
        return cadence <= 1 || hash % cadence == 0;
    }

    private bool PassesDensity(int hash, float density, int denseCadence, int sparseCadence)
    {
        if (density <= 0f) return false;
        int cadence = Mathf.RoundToInt(Mathf.Lerp(Mathf.Max(1, sparseCadence), Mathf.Max(1, denseCadence), Mathf.Clamp01(density)));
        return cadence <= 1 || Mathf.Abs(hash) % cadence == 0;
    }

    private void SpawnModularSurfaceDetail(Transform root, CellKind[,] cells, System.Random rng)
    {
        for (int x = 2; x < width - 2; x++)
        {
            for (int z = 2; z < length - 2; z++)
            {
                if (!IsWalkableForContent(cells[x, z])) continue;
                if (cells[x, z] == CellKind.Spawn || cells[x, z] == CellKind.Exit) continue;
                if (IsRouteConnectorCell(cells, new Vector2Int(x, z))) continue;
                if (prioritizeFastMovementFlow && IsNearPrimaryMovementRoute(x, z)) continue;
                int hash = Mathf.Abs(x * 37 + z * 19 + lastGeneratedSeed);
                if (!PassesDensity(hash, effectiveMicroDetailDensity * 0.72f, 8, 24)) continue;

                float y = GetCellHeight(cells[x, z]) + floorThickness * 0.5f + 0.055f;
                bool longX = ((x + z + lastGeneratedSeed) & 1) == 0;
                Vector3 ribScale = longX
                    ? new Vector3(tileSize * 0.58f, 0.035f, 0.08f)
                    : new Vector3(0.08f, 0.035f, tileSize * 0.58f);
                Vector3 ribOffset = longX
                    ? new Vector3(0f, 0f, tileSize * Mathf.Lerp(-0.18f, 0.18f, (float)rng.NextDouble()))
                    : new Vector3(tileSize * Mathf.Lerp(-0.18f, 0.18f, (float)rng.NextDouble()), 0f, 0f);

                CreateCube(root, $"ServiceRib_{x}_{z}", CellCenter(x, z, y) + ribOffset, ribScale, darkMaterial, false);

                int chipHash = Mathf.Abs(x * 11 + z * 7 + lastGeneratedSeed);
                if (PassesDensity(chipHash, effectiveMicroDetailDensity * 0.48f, 18, 42))
                {
                    Vector3 chipScale = new Vector3(tileSize * 0.14f, 0.03f, tileSize * 0.14f);
                    Vector3 chipOffset = new Vector3(tileSize * 0.22f, 0f, tileSize * -0.22f);
                    CreateCube(root, $"ServiceGlowChip_{x}_{z}", CellCenter(x, z, y + 0.025f) + chipOffset, chipScale, accentMaterial, false);
                }
            }
        }
    }

    private bool IsRouteConnectorCell(CellKind[,] cells, Vector2Int cell)
    {
        foreach (Vector2Int neighbor in GetCardinalNeighbors(cell))
        {
            if (!InBounds(neighbor.x, neighbor.y)) continue;
            if (!IsLayoutWalkable(cells[neighbor.x, neighbor.y])) continue;
            if (HasTraversalConnectorBetween(cell, neighbor))
                return true;
        }

        return false;
    }

    private void SpawnRouteMarkings(Transform root, CellKind[,] cells)
    {
        for (int x = 2; x < width - 2; x++)
        {
            for (int z = 2; z < length - 2; z++)
            {
                CellKind kind = cells[x, z];
                if (kind != CellKind.Bridge && kind != CellKind.Platform && kind != CellKind.UpperPlatform) continue;

                bool important = IsRouteConnectorCell(cells, new Vector2Int(x, z)) ||
                                 (prioritizeFastMovementFlow
                                     ? IsNearPrimaryMovementRoute(x, z)
                                     : Mathf.Abs(x - width / 2) <= mainBridgeHalfWidth ||
                                       Mathf.Abs(z - length / 2) <= mainBridgeHalfWidth);
                int hash = Mathf.Abs(x * 21961 ^ z * 48611 ^ lastGeneratedSeed);
                if (important && !PassesDensity(hash, effectiveDecorativeDensity * (prioritizeFastMovementFlow ? 0.52f : 0.72f), 3, 7)) continue;
                if (!important && !PassesDensity(hash, effectiveMicroDetailDensity * 0.34f, 16, 32)) continue;

                bool eastWest = IsSameLayoutHeight(cells, x, z, x + 1, z) || IsSameLayoutHeight(cells, x, z, x - 1, z);
                bool northSouth = IsSameLayoutHeight(cells, x, z, x, z + 1) || IsSameLayoutHeight(cells, x, z, x, z - 1);
                if (!eastWest && !northSouth && !important) continue;

                bool connector = IsRouteConnectorCell(cells, new Vector2Int(x, z));
                if (eastWest && northSouth && !connector)
                {
                    bool preferX = Mathf.Abs(x - width / 2) > Mathf.Abs(z - length / 2);
                    eastWest = preferX;
                    northSouth = !preferX;
                }

                float y = GetCellHeight(kind) + floorThickness * 0.5f + 0.075f;
                Material stripeMaterial = connector || important ? accentMaterial : darkMaterial;
                if (eastWest)
                {
                    Vector3 scale = new Vector3(tileSize * (important ? 0.48f : 0.32f), 0.03f, connector ? 0.06f : 0.045f);
                    CreateCube(root, $"RouteStripeX_{x}_{z}", CellCenter(x, z, y), scale, stripeMaterial, false);
                }

                if (northSouth)
                {
                    Vector3 scale = new Vector3(connector ? 0.06f : 0.045f, 0.03f, tileSize * (important ? 0.48f : 0.32f));
                    CreateCube(root, $"RouteStripeZ_{x}_{z}", CellCenter(x, z, y + 0.01f), scale, stripeMaterial, false);
                }
            }
        }
    }

    private void SpawnModularDistrictMasses(Transform root, CellKind[,] cells, System.Random rng)
    {
        if (arenaMode == ArenaMode.Shop) return;

        bool[,] used = new bool[width, length];
        int targetCount = Mathf.RoundToInt(Mathf.Clamp((width * length) / 95, 5, 13) * Mathf.Lerp(0.45f, 1f, effectiveDecorativeDensity));
        targetCount = Mathf.Clamp(targetCount, 3, 13);
        if (prioritizeFastMovementFlow)
            targetCount = Mathf.Max(1, Mathf.RoundToInt(targetCount * Mathf.Lerp(0.55f, 0.18f, clutterSuppression)));
        int made = 0;

        for (int z = 2; z < length - 3 && made < targetCount; z++)
        {
            for (int x = 2; x < width - 3 && made < targetCount; x++)
            {
                if (used[x, z]) continue;
                if (!IsDistrictAnchorCell(cells, x, z)) continue;
                int hash = Mathf.Abs(x * 3571 ^ z * 1597 ^ lastGeneratedSeed);
                if (hash % 4 == 0) continue;

                Vector2Int size = PickDistrictFootprint(cells, used, x, z, rng);
                if (size.x <= 0 || size.y <= 0) continue;

                MarkDistrictUsed(used, x, z, size.x, size.y);
                CreateDistrictModule(root, cells, x, z, size.x, size.y, made, rng);
                made++;
            }
        }
    }

    private bool IsDistrictAnchorCell(CellKind[,] cells, int x, int z)
    {
        if (!InBounds(x, z)) return false;
        CellKind kind = cells[x, z];
        if (!IsWalkableForContent(kind)) return false;
        if (kind == CellKind.Spawn || kind == CellKind.Exit) return false;
        if (kind == CellKind.Bridge &&
            (prioritizeFastMovementFlow
                ? IsNearPrimaryMovementRoute(x, z)
                : (x == width / 2 || z == length / 2)))
            return false;
        if (prioritizeFastMovementFlow && IsNearPrimaryMovementRoute(x, z)) return false;
        if (IsRouteConnectorCell(cells, new Vector2Int(x, z))) return false;
        return true;
    }

    private Vector2Int PickDistrictFootprint(CellKind[,] cells, bool[,] used, int x, int z, System.Random rng)
    {
        Vector2Int[] options =
        {
            new Vector2Int(4, 3),
            new Vector2Int(3, 4),
            new Vector2Int(3, 3),
            new Vector2Int(4, 2),
            new Vector2Int(2, 4),
            new Vector2Int(2, 3),
            new Vector2Int(3, 2)
        };

        int offset = rng.Next(options.Length);
        for (int i = 0; i < options.Length; i++)
        {
            Vector2Int size = options[(i + offset) % options.Length];
            if (CanPlaceDistrictFootprint(cells, used, x, z, size.x, size.y))
                return size;
        }

        return Vector2Int.zero;
    }

    private bool CanPlaceDistrictFootprint(CellKind[,] cells, bool[,] used, int x, int z, int sx, int sz)
    {
        if (x + sx >= width - 1 || z + sz >= length - 1) return false;
        float height = GetCellHeight(cells[x, z]);
        int walkable = 0;

        for (int ix = x; ix < x + sx; ix++)
        {
            for (int iz = z; iz < z + sz; iz++)
            {
                if (used[ix, iz]) return false;
                if (!IsDistrictAnchorCell(cells, ix, iz)) return false;
                if (Mathf.Abs(GetCellHeight(cells[ix, iz]) - height) > 0.1f) return false;
                walkable++;
            }
        }

        return walkable >= sx * sz;
    }

    private void MarkDistrictUsed(bool[,] used, int x, int z, int sx, int sz)
    {
        for (int ix = x; ix < x + sx; ix++)
            for (int iz = z; iz < z + sz; iz++)
                used[ix, iz] = true;
    }

    private void CreateDistrictModule(Transform root, CellKind[,] cells, int x, int z, int sx, int sz, int index, System.Random rng)
    {
        float topY = GetCellHeight(cells[x, z]);
        float centerX = (x + (sx - 1) * 0.5f) * tileSize;
        float centerZ = (z + (sz - 1) * 0.5f) * tileSize;
        float spanX = sx * tileSize;
        float spanZ = sz * tileSize;
        Vector3 center = new Vector3(centerX, topY, centerZ);

        float slabY = topY - 0.44f;
        CreateCube(root, $"DistrictPlate_{x}_{z}_{index}", center + Vector3.down * 0.13f, new Vector3(spanX * 0.94f, 0.18f, spanZ * 0.94f), darkMaterial, false);
        CreateCube(root, $"DistrictUndercarriage_{x}_{z}_{index}", new Vector3(centerX, slabY - 0.42f, centerZ), new Vector3(spanX * 0.78f, 0.62f, spanZ * 0.78f), darkMaterial, false);

        bool longX = spanX >= spanZ;
        Vector3 seamScale = longX
            ? new Vector3(spanX * 0.72f, 0.035f, 0.085f)
            : new Vector3(0.085f, 0.035f, spanZ * 0.72f);
        Vector3 seamOffset = longX
            ? new Vector3(0f, floorThickness * 0.5f + 0.125f, spanZ * 0.22f * (((index & 1) == 0) ? 1f : -1f))
            : new Vector3(spanX * 0.22f * (((index & 1) == 0) ? 1f : -1f), floorThickness * 0.5f + 0.125f, 0f);
        if (((x + z + index) & 1) == 0)
            CreateCube(root, $"DistrictSeamGlow_{x}_{z}_{index}", center + seamOffset, seamScale, accentMaterial, false);

        int conduitCount = Mathf.Clamp(Mathf.RoundToInt((sx + sz) * 0.45f), 2, 4);
        for (int i = 0; i < conduitCount; i++)
        {
            float t = conduitCount == 1 ? 0.5f : i / (float)(conduitCount - 1);
            float side = ((i + index) & 1) == 0 ? -1f : 1f;
            Vector3 conduitPos;
            Vector3 conduitScale;
            if (longX)
            {
                conduitPos = new Vector3(centerX + Mathf.Lerp(-spanX * 0.34f, spanX * 0.34f, t), slabY - 0.82f, centerZ + side * spanZ * 0.42f);
                conduitScale = new Vector3(tileSize * 0.18f, 1.25f + (float)rng.NextDouble() * 0.85f, tileSize * 0.16f);
            }
            else
            {
                conduitPos = new Vector3(centerX + side * spanX * 0.42f, slabY - 0.82f, centerZ + Mathf.Lerp(-spanZ * 0.34f, spanZ * 0.34f, t));
                conduitScale = new Vector3(tileSize * 0.16f, 1.25f + (float)rng.NextDouble() * 0.85f, tileSize * 0.18f);
            }

            CreateCube(root, $"DistrictActuator_{x}_{z}_{index}_{i}", conduitPos, conduitScale, darkMaterial, false);
            if (((i + index) & 3) == 0)
                CreateCube(root, $"DistrictActuatorGlow_{x}_{z}_{index}_{i}", conduitPos + Vector3.up * (conduitScale.y * 0.18f), new Vector3(conduitScale.x * 0.3f, conduitScale.y * 0.28f, conduitScale.z * 0.3f), accentMaterial, false);
        }

        if (sx >= 3 && sz >= 3)
        {
            Vector3 railScaleA = new Vector3(spanX * 0.82f, 0.085f, 0.10f);
            Vector3 railScaleB = new Vector3(0.10f, 0.085f, spanZ * 0.82f);
            float y = topY + floorThickness * 0.5f + 0.16f;
            CreateCube(root, $"DistrictCornerLineA_{x}_{z}_{index}", new Vector3(centerX, y, centerZ - spanZ * 0.36f), railScaleA, darkMaterial, false);
            CreateCube(root, $"DistrictCornerLineB_{x}_{z}_{index}", new Vector3(centerX - spanX * 0.36f, y + 0.01f, centerZ), railScaleB, darkMaterial, false);
        }
    }

    private void SpawnModeFloorPresentation(Transform root, CellKind[,] cells)
    {
        if (arenaMode == ArenaMode.Shop)
        {
            SpawnShopFloorPresentation(root, cells);
            return;
        }

        if (arenaMode == ArenaMode.Boss)
            SpawnBossFloorPresentation(root, cells);
    }

    private void SpawnShopFloorPresentation(Transform root, CellKind[,] cells)
    {
        Vector2Int center = new Vector2Int(width / 2, length / 2);
        float y = GetCellHeight(cells[center.x, center.y]) + floorThickness * 0.5f + 0.105f;

        CreateCube(root, "ShopAisleLine", CellCenter(center.x, center.y - 2, y), new Vector3(0.08f, 0.03f, tileSize * 8.2f), darkMaterial, false);
        CreateCube(root, "ShopCounterLine", CellCenter(center.x, center.y + 1, y + 0.01f), new Vector3(tileSize * 7.2f, 0.03f, 0.08f), darkMaterial, false);
        CreateCube(root, "ShopExitLine", CellCenter(center.x, center.y + 5, y + 0.02f), new Vector3(tileSize * 3.6f, 0.03f, 0.08f), exitMaterial, false);
        CreateCube(root, "ShopAisleGlow", CellCenter(center.x, center.y - 1, y + 0.024f), new Vector3(0.04f, 0.02f, tileSize * 9.8f), accentMaterial, false);
        CreateCube(root, "ShopCounterGlow", CellCenter(center.x, center.y + 1, y + 0.024f), new Vector3(tileSize * 6.2f, 0.02f, 0.04f), accentMaterial, false);

        Vector2Int[] stationCells =
        {
            new Vector2Int(center.x - 3, center.y),
            new Vector2Int(center.x, center.y),
            new Vector2Int(center.x + 3, center.y),
            new Vector2Int(center.x - 5, center.y - 2),
            new Vector2Int(center.x + 4, center.y - 2),
            new Vector2Int(center.x + 6, center.y - 2),
            new Vector2Int(center.x, center.y + 3)
        };

        for (int i = 0; i < stationCells.Length; i++)
        {
            Vector2Int cell = stationCells[i];
            if (!InBounds(cell.x, cell.y) || !IsLayoutWalkable(cells[cell.x, cell.y])) continue;
            float padY = GetCellHeight(cells[cell.x, cell.y]) + floorThickness * 0.5f + 0.095f;
            Vector3 centerPos = CellCenter(cell.x, cell.y, padY);
            CreateCube(root, $"ShopStationPad_{i}", centerPos, new Vector3(tileSize * 0.8f, 0.042f, tileSize * 0.6f), darkMaterial, false);
            CreateCube(root, $"ShopStationPadInset_{i}", centerPos + Vector3.up * 0.028f, new Vector3(tileSize * 0.58f, 0.02f, tileSize * 0.38f), i == 3 ? spawnMaterial : accentMaterial, false);
        }
    }

    private void SpawnBossFloorPresentation(Transform root, CellKind[,] cells)
    {
        ThemeProfile profile = ResolveThemeProfile(themeIndex);
        Vector2Int center = new Vector2Int(width / 2, length / 2);
        float y = GetCellHeight(cells[center.x, center.y]) + floorThickness * 0.5f + 0.11f;

        CreateCube(root, "BossLaneNorthSouth", CellCenter(center.x, center.y, y), new Vector3(0.14f, 0.04f, tileSize * 13.5f), accentMaterial, false);
        CreateCube(root, "BossLaneEastWest", CellCenter(center.x, center.y, y + 0.012f), new Vector3(tileSize * 13.5f, 0.04f, 0.14f), accentMaterial, false);
        CreateCube(root, "BossOuterRingA", CellCenter(center.x, center.y + 5, y + 0.02f), new Vector3(tileSize * 8.8f, 0.035f, 0.12f), hazardMaterial, false);
        CreateCube(root, "BossOuterRingB", CellCenter(center.x, center.y - 5, y + 0.02f), new Vector3(tileSize * 8.8f, 0.035f, 0.12f), hazardMaterial, false);
        CreateCube(root, "BossOuterRingC", CellCenter(center.x + 5, center.y, y + 0.02f), new Vector3(0.12f, 0.035f, tileSize * 8.8f), hazardMaterial, false);
        CreateCube(root, "BossOuterRingD", CellCenter(center.x - 5, center.y, y + 0.02f), new Vector3(0.12f, 0.035f, tileSize * 8.8f), hazardMaterial, false);
        CreateCube(root, "BossInnerCrossGlowA", CellCenter(center.x, center.y, y + 0.038f), new Vector3(tileSize * 8.8f, 0.02f, 0.06f), accentMaterial, false);
        CreateCube(root, "BossInnerCrossGlowB", CellCenter(center.x, center.y, y + 0.038f), new Vector3(0.06f, 0.02f, tileSize * 8.8f), accentMaterial, false);

        float monolithY = y + 5.6f;
        for (int side = -1; side <= 1; side += 2)
        {
            CreateCube(root, $"BossNorthMonolith_{side}", CellCenter(center.x + side * 6, center.y + 8, monolithY), new Vector3(1.2f, 8.4f, 0.64f), darkMaterial, false);
            CreateCube(root, $"BossSouthMonolith_{side}", CellCenter(center.x + side * 6, center.y - 8, monolithY - 0.6f), new Vector3(1.0f, 7.2f, 0.56f), darkMaterial, false);
            CreateCube(root, $"BossNorthMonolithGlow_{side}", CellCenter(center.x + side * 6, center.y + 8, monolithY + 0.42f), new Vector3(0.12f, 5.8f, 0.12f), accentMaterial, false);
            CreateCube(root, $"BossSouthMonolithGlow_{side}", CellCenter(center.x + side * 6, center.y - 8, monolithY - 0.16f), new Vector3(0.12f, 5.0f, 0.12f), accentMaterial, false);
        }

        float bridgeY = y + 8.8f + profile.skylineDensity * 1.4f;
        CreateCube(root, "BossNorthBridge", CellCenter(center.x, center.y + 8, bridgeY), new Vector3(tileSize * 9.4f, 0.24f, 0.42f), darkMaterial, false);
        CreateCube(root, "BossSouthBridge", CellCenter(center.x, center.y - 8, bridgeY + 0.8f), new Vector3(tileSize * 8.4f, 0.2f, 0.38f), darkMaterial, false);
        CreateCube(root, "BossEastBridge", CellCenter(center.x + 8, center.y, bridgeY - 0.6f), new Vector3(0.42f, 0.2f, tileSize * 9.2f), darkMaterial, false);
        CreateCube(root, "BossWestBridge", CellCenter(center.x - 8, center.y, bridgeY + 0.4f), new Vector3(0.38f, 0.2f, tileSize * 8.2f), darkMaterial, false);
        CreateCube(root, "BossBridgeGlowA", CellCenter(center.x, center.y + 8, bridgeY + 0.08f), new Vector3(tileSize * 7.8f, 0.06f, 0.06f), accentMaterial, false);
        CreateCube(root, "BossBridgeGlowB", CellCenter(center.x, center.y - 8, bridgeY + 0.88f), new Vector3(tileSize * 6.8f, 0.06f, 0.06f), accentMaterial, false);

        switch (Mathf.Abs(themeIndex) % 4)
        {
            case 1:
                AddBackdropGlowStrip(root, "BossHighRiseHalo", CellCenter(center.x, center.y, bridgeY + 2.6f), new Vector3(tileSize * 5.2f, 0.08f, tileSize * 5.2f), profile.structureGlowColor);
                break;
            case 2:
                CreateCube(root, "BossHeatSpineNorth", CellCenter(center.x, center.y + 9, y + 7.4f), new Vector3(tileSize * 4.6f, 0.26f, 0.8f), accentMaterial, false);
                CreateCube(root, "BossHeatSpineSouth", CellCenter(center.x, center.y - 9, y + 6.8f), new Vector3(tileSize * 4.6f, 0.26f, 0.8f), accentMaterial, false);
                break;
            case 3:
                AddBackdropGlowStrip(root, "BossSignalAxisA", CellCenter(center.x, center.y, y + 6.9f), new Vector3(tileSize * 7.2f, 0.06f, 0.06f), profile.structureGlowColor);
                AddBackdropGlowStrip(root, "BossSignalAxisB", CellCenter(center.x, center.y, y + 6.9f), new Vector3(0.06f, 0.06f, tileSize * 7.2f), profile.structureGlowColor);
                break;
            default:
                CreateCube(root, "BossCrossfireSpineL", CellCenter(center.x - 9, center.y, y + 7.2f), new Vector3(0.22f, 6.8f, 0.22f), darkMaterial, false);
                CreateCube(root, "BossCrossfireSpineR", CellCenter(center.x + 9, center.y, y + 7.2f), new Vector3(0.22f, 6.8f, 0.22f), darkMaterial, false);
                AddBackdropGlowStrip(root, "BossCrossfireSpineGlowL", CellCenter(center.x - 9, center.y, y + 8.4f), new Vector3(0.08f, 1.4f, 0.08f), profile.structureGlowColor);
                AddBackdropGlowStrip(root, "BossCrossfireSpineGlowR", CellCenter(center.x + 9, center.y, y + 8.4f), new Vector3(0.08f, 1.4f, 0.08f), profile.structureGlowColor);
                break;
        }

        for (int i = 0; i < 4; i++)
        {
            Vector2Int cell = i switch
            {
                0 => new Vector2Int(center.x + 7, center.y + 7),
                1 => new Vector2Int(center.x - 7, center.y + 7),
                2 => new Vector2Int(center.x + 7, center.y - 7),
                _ => new Vector2Int(center.x - 7, center.y - 7)
            };
            if (!InBounds(cell.x, cell.y) || !IsLayoutWalkable(cells[cell.x, cell.y])) continue;
            float padY = GetCellHeight(cells[cell.x, cell.y]) + floorThickness * 0.5f + 0.095f;
            CreateCube(root, $"BossCornerRoutePad_{i}", CellCenter(cell.x, cell.y, padY), new Vector3(tileSize * 0.78f, 0.035f, tileSize * 0.78f), accentMaterial, false);
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

    private bool HasStairVisualBetween(Vector2Int from, Vector2Int to)
    {
        if (!InBounds(from.x, from.y) || !InBounds(to.x, to.y))
            return false;

        return stairVisualEdges.Contains(EncodeTraversalKey(from, to)) ||
               stairVisualEdges.Contains(EncodeTraversalKey(to, from));
    }

    private void SpawnGateFrames(Transform root, CellKind[,] cells)
    {
        Vector2Int center = prioritizeFastMovementFlow ? lastLayoutCenter : new Vector2Int(width / 2, length / 2);
        float h = prioritizeFastMovementFlow ? 10.5f : 8.5f;
        float span = tileSize * Mathf.Clamp((prioritizeFastMovementFlow ? lastPrimaryLaneHalfWidth * 2 + 3 : centralPlatformRadius * 2 + 1), 7, 15);

        if (prioritizeFastMovementFlow && arenaMode == ArenaMode.Combat)
        {
            int laneOffset = Mathf.Clamp(lastSideLaneOffset, 4, Mathf.Min(width, length) / 2 - 3);
            CreateGateAtCell(root, cells, "NorthLaneGate", center.x, Mathf.Clamp(center.y + laneOffset, 2, length - 3), span, h, true);
            CreateGateAtCell(root, cells, "SouthLaneGate", center.x, Mathf.Clamp(center.y - laneOffset, 2, length - 3), span, h, true);
            CreateGateAtCell(root, cells, "EastLaneGate", Mathf.Clamp(center.x + laneOffset, 2, width - 3), center.y, span, h, false);
            CreateGateAtCell(root, cells, "WestLaneGate", Mathf.Clamp(center.x - laneOffset, 2, width - 3), center.y, span, h, false);
            return;
        }

        CreateGateAtCell(root, cells, "NorthGate", center.x, center.y + centralPlatformRadius + 2, span, h, true);
        CreateGateAtCell(root, cells, "SouthGate", center.x, center.y - centralPlatformRadius - 2, span, h, true);
        CreateGateAtCell(root, cells, "EastGate", center.x + centralPlatformRadius + 2, center.y, span, h, false);
        CreateGateAtCell(root, cells, "WestGate", center.x - centralPlatformRadius - 2, center.y, span, h, false);
    }

    private void CreateGateAtCell(Transform root, CellKind[,] cells, string name, int x, int z, float span, float height, bool horizontal)
    {
        x = Mathf.Clamp(x, 1, width - 2);
        z = Mathf.Clamp(z, 1, length - 2);
        if (!CanPlaceGateAtCell(cells, x, z, horizontal))
            return;

        float y = GetCellHeight(cells[x, z]) + 0.04f;
        CreateGate(root, name, CellCenter(x, z, y), span, height, horizontal);
    }

    private bool CanPlaceGateAtCell(CellKind[,] cells, int x, int z, bool horizontal)
    {
        if (!InBounds(x, z) || !IsWalkableForContent(cells[x, z]))
            return false;

        float y = GetCellHeight(cells[x, z]);
        int halfSpanCells = Mathf.Clamp(centralPlatformRadius + 1, 3, 6);
        Vector2Int a = horizontal ? new Vector2Int(x - halfSpanCells, z) : new Vector2Int(x, z - halfSpanCells);
        Vector2Int b = horizontal ? new Vector2Int(x + halfSpanCells, z) : new Vector2Int(x, z + halfSpanCells);
        if (!InBounds(a.x, a.y) || !InBounds(b.x, b.y))
            return false;
        if (!IsWalkableForContent(cells[a.x, a.y]) || !IsWalkableForContent(cells[b.x, b.y]))
            return false;

        int start = horizontal ? a.x : a.y;
        int end = horizontal ? b.x : b.y;
        for (int i = start; i <= end; i++)
        {
            int cx = horizontal ? i : x;
            int cz = horizontal ? z : i;
            if (!InBounds(cx, cz) || !IsWalkableForContent(cells[cx, cz]))
                return false;
            if (Mathf.Abs(GetCellHeight(cells[cx, cz]) - y) > 0.1f)
                return false;
        }

        return true;
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
            if (prioritizeFastMovementFlow && IsNearPrimaryMovementRoute(a.x, a.y)) continue;

            float y = GetCellHeight(cells[a.x, a.y]);
            float pillarHeight = 10f + (float)rng.NextDouble() * 4f;
            CreateCube(root, $"MegaPillarCore_{a.x}_{a.y}_{i}", CellCenter(a.x, a.y, y + pillarHeight * 0.5f), new Vector3(tileSize * 0.88f, pillarHeight, tileSize * 0.88f), darkMaterial);
            CreateCube(root, $"MegaPillarCrown_{a.x}_{a.y}_{i}", CellCenter(a.x, a.y, y + pillarHeight + 0.35f), new Vector3(tileSize * 1.55f, 0.7f, tileSize * 1.55f), darkMaterial);
            CreateCube(root, $"MegaPillarGlowA_{a.x}_{a.y}_{i}", CellCenter(a.x, a.y, y + pillarHeight * 0.5f) + new Vector3(tileSize * 0.54f, 0f, 0f), new Vector3(0.1f, pillarHeight * 0.82f, 0.1f), accentMaterial, false);
            CreateCube(root, $"MegaPillarGlowB_{a.x}_{a.y}_{i}", CellCenter(a.x, a.y, y + pillarHeight * 0.5f) - new Vector3(tileSize * 0.54f, 0f, 0f), new Vector3(0.1f, pillarHeight * 0.82f, 0.1f), accentMaterial, false);
        }
    }

    private void SpawnShopStalls(Transform root, CellKind[,] cells)
    {
        Vector2Int center = new Vector2Int(width / 2, length / 2);
        Vector2Int entrance = FindFirst(cells, CellKind.Spawn);
        CybergrindRunState runState = CybergrindRunState.GetOrCreate();
        int[] refitPresets = { 1, 2, 3 };
        string[] refitLabels = { "TRIDENT", "KILN", "LODESTAR" };

        for (int i = 0; i < refitPresets.Length; i++)
        {
            Vector2Int cell = new Vector2Int(center.x + ((i - 1) * 3), center.y);
            float y = GetCellHeight(cells[cell.x, cell.y]);
            int presetIndex = refitPresets[i];
            GameObject stall = CreateCube(root, $"ShopDisplay_{presetIndex}", CellCenter(cell.x, cell.y, y + 0.65f), new Vector3(2.4f, 1.3f, 1.0f), itemMaterial);
            OrientShopStation(stall.transform, cell, entrance);
            CybergrindShopStation shop = stall.AddComponent<CybergrindShopStation>();
            shop.service = CybergrindShopStation.ShopService.Refit;
            shop.presetIndex = presetIndex;
            shop.cost = i == 0 ? 2 : 4;
            shop.displayRenderer = stall.GetComponent<Renderer>();

            BuildShopStationModel(stall.transform, CybergrindShopStation.ShopService.Refit, refitLabels[i]);
            BuildShopWeaponDisplay(stall.transform, presetIndex, refitLabels[i]);
            CreateShopDescriptionLabel(stall.transform, refitLabels[i], shop.cost <= 0 ? "FREE" : $"{shop.cost}C", new Color(0.76f, 0.88f, 1f));
        }

        float serviceY = GetCellHeight(cells[center.x, center.y]);
        GameObject repair = CreateCube(root, "ShopRepairStation", CellCenter(center.x - 5, center.y - 2, serviceY + 0.7f), new Vector3(1.8f, 1.4f, 1.2f), spawnMaterial);
        OrientShopStation(repair.transform, new Vector2Int(center.x - 5, center.y - 2), entrance);
        CybergrindShopStation repairStation = repair.AddComponent<CybergrindShopStation>();
        repairStation.service = CybergrindShopStation.ShopService.Repair;
        repairStation.cost = 3 + Mathf.Min(2, runState.bossesClearedThisRun);
        repairStation.healAmount = 45;
        repairStation.displayRenderer = repair.GetComponent<Renderer>();
        BuildShopStationModel(repair.transform, CybergrindShopStation.ShopService.Repair, "HEAL");
        CreateShopDescriptionLabel(repair.transform, "HEAL", $"+{repairStation.healAmount} HP  {repairStation.cost}C", new Color(0.70f, 1f, 0.84f));

        SpawnModStation(root, center + new Vector2Int(4, -2), cells, serviceY, 0, runState);
        SpawnModStation(root, center + new Vector2Int(6, -2), cells, serviceY, 1, runState);

        GameObject surge = CreateCube(root, "ShopSurgeStation", CellCenter(center.x, center.y + 3, serviceY + 0.7f), new Vector3(1.8f, 1.4f, 1.2f), accentMaterial);
        OrientShopStation(surge.transform, new Vector2Int(center.x, center.y + 3), entrance);
        CybergrindShopStation surgeStation = surge.AddComponent<CybergrindShopStation>();
        surgeStation.service = CybergrindShopStation.ShopService.Surge;
        surgeStation.cost = 0;
        surgeStation.moveSpeedBonus = 1.8f;
        surgeStation.dashBonus = 4.5f;
        surgeStation.jumpBonus = 0.35f;
        surgeStation.displayRenderer = surge.GetComponent<Renderer>();
        BuildShopStationModel(surge.transform, CybergrindShopStation.ShopService.Surge, "MOVE");
        CreateShopDescriptionLabel(surge.transform, "MOVE", "SPEED  DASH  FREE", new Color(0.92f, 0.78f, 1f));

    }

    private void SpawnModStation(Transform root, Vector2Int cell, CellKind[,] cells, float fallbackY, int index, CybergrindRunState runState)
    {
        cell.x = Mathf.Clamp(cell.x, 2, width - 3);
        cell.y = Mathf.Clamp(cell.y, 2, length - 3);
        float y = InBounds(cell.x, cell.y) ? GetCellHeight(cells[cell.x, cell.y]) : fallbackY;
        Vector2Int entrance = FindFirst(cells, CellKind.Spawn);

        GameObject overclock = CreateCube(root, $"ShopModStation_{index}", CellCenter(cell.x, cell.y, y + 0.7f), new Vector3(1.7f, 1.35f, 1.15f), puzzleMaterial);
        OrientShopStation(overclock.transform, cell, entrance);
        CybergrindShopStation station = overclock.AddComponent<CybergrindShopStation>();
        station.service = CybergrindShopStation.ShopService.Overclock;
        station.cost = 4 + Mathf.Min(2, runState.bossesClearedThisRun) + index;
        station.healAmount = 18;
        station.displayRenderer = overclock.GetComponent<Renderer>();

        int roll = Mathf.Abs(themeIndex + index + runState.floorsClearedThisRun) % 3;
        station.passiveMod = roll switch
        {
            0 => Gun.PassiveMod.RapidFeed,
            1 => Gun.PassiveMod.SharpenedRounds,
            _ => Gun.PassiveMod.Stabilizer
        };
        station.altFireMod = ((themeIndex + index) & 1) == 0 ? Gun.AltFireMod.QuickCharge : Gun.AltFireMod.Overload;
        station.fireRateBoostPercent = station.passiveMod == Gun.PassiveMod.RapidFeed ? 0.18f : 0.08f;
        station.damageBoostPercent = station.passiveMod == Gun.PassiveMod.SharpenedRounds ? 0.16f : 0.08f;
        station.altCooldownBoostPercent = station.altFireMod == Gun.AltFireMod.QuickCharge ? 0.22f : 0.08f;

        string label = station.passiveMod switch
        {
            Gun.PassiveMod.SharpenedRounds => "DAMAGE",
            Gun.PassiveMod.Stabilizer => "FOCUS",
            _ => "RATE"
        };
        BuildShopStationModel(overclock.transform, CybergrindShopStation.ShopService.Overclock, label);
        BuildShopModifierDisplay(overclock.transform, station.passiveMod, station.altFireMod, label);
        CreateShopDescriptionLabel(overclock.transform, label, $"{BuildShortModLabel(station.passiveMod, station.altFireMod)}  {station.cost}C", new Color(1f, 0.82f, 0.62f));
    }

    private void OrientShopStation(Transform station, Vector2Int stationCell, Vector2Int entranceCell)
    {
        if (station == null) return;

        Vector3 toEntrance = new Vector3(entranceCell.x - stationCell.x, 0f, entranceCell.y - stationCell.y);
        if (toEntrance.sqrMagnitude < 0.01f)
            toEntrance = Vector3.back;

        station.localRotation = Quaternion.LookRotation(toEntrance.normalized, Vector3.up);
    }

    private string BuildShortModLabel(Gun.PassiveMod passive, Gun.AltFireMod alt)
    {
        string passiveText = passive switch
        {
            Gun.PassiveMod.SharpenedRounds => "damage",
            Gun.PassiveMod.Stabilizer => "spread",
            Gun.PassiveMod.RapidFeed => "rate",
            _ => "clean"
        };
        string altText = alt switch
        {
            Gun.AltFireMod.Overload => "hard special",
            Gun.AltFireMod.QuickCharge => "quick special",
            _ => "special"
        };
        return $"{passiveText} + {altText}";
    }

    private void SpawnBossArenaMarkers(Transform root, CellKind[,] cells)
    {
        ThemeProfile profile = ResolveThemeProfile(themeIndex);
        Vector2Int center = new Vector2Int(width / 2, length / 2);
        float y = GetCellHeight(cells[center.x, center.y]);
        Vector3 platformCenter = CellCenter(center.x, center.y, y + 0.22f);
        CreateCube(root, $"BossArenaDais_{center.x}_{center.y}", platformCenter, new Vector3(6.4f, 0.42f, 6.4f), hazardMaterial, false);
        CreateCube(root, $"BossArenaDaisInset_{center.x}_{center.y}", platformCenter + Vector3.up * 0.18f, new Vector3(4.2f, 0.1f, 4.2f), darkMaterial, false);
        CreateCube(root, $"BossArenaInnerPad_{center.x}_{center.y}", platformCenter + Vector3.up * 0.28f, new Vector3(2.5f, 0.08f, 2.5f), accentMaterial, false);
        CreateCube(root, $"BossArenaNorthArch_{center.x}_{center.y}", platformCenter + new Vector3(0f, 2.4f, 3.4f), new Vector3(5.2f, 0.22f, 0.28f), darkMaterial, false);
        CreateCube(root, $"BossArenaSouthArch_{center.x}_{center.y}", platformCenter + new Vector3(0f, 2.4f, -3.4f), new Vector3(5.2f, 0.22f, 0.28f), darkMaterial, false);
        CreateCube(root, $"BossArenaEastArch_{center.x}_{center.y}", platformCenter + new Vector3(3.4f, 2.4f, 0f), new Vector3(0.28f, 0.22f, 5.2f), darkMaterial, false);
        CreateCube(root, $"BossArenaWestArch_{center.x}_{center.y}", platformCenter + new Vector3(-3.4f, 2.4f, 0f), new Vector3(0.28f, 0.22f, 5.2f), darkMaterial, false);
        CreateCube(root, $"BossArenaGlowRingA_{center.x}_{center.y}", platformCenter + Vector3.up * 0.26f, new Vector3(6.9f, 0.08f, 0.24f), accentMaterial, false);
        CreateCube(root, $"BossArenaGlowRingB_{center.x}_{center.y}", platformCenter + Vector3.up * 0.26f, new Vector3(0.24f, 0.08f, 6.9f), accentMaterial, false);
        CreateCube(root, $"BossArenaCrown_{center.x}_{center.y}", platformCenter + Vector3.up * 5.8f, new Vector3(4.8f, 0.14f, 4.8f), darkMaterial, false);
        CreateCube(root, $"BossArenaCrownGlowA_{center.x}_{center.y}", platformCenter + Vector3.up * 5.68f, new Vector3(4.1f, 0.05f, 0.08f), accentMaterial, false);
        CreateCube(root, $"BossArenaCrownGlowB_{center.x}_{center.y}", platformCenter + Vector3.up * 5.68f, new Vector3(0.08f, 0.05f, 4.1f), accentMaterial, false);
        for (int i = 0; i < 4; i++)
        {
            Vector3 offset = i switch
            {
                0 => new Vector3(2.85f, 0f, 2.85f),
                1 => new Vector3(-2.85f, 0f, 2.85f),
                2 => new Vector3(2.85f, 0f, -2.85f),
                _ => new Vector3(-2.85f, 0f, -2.85f)
            };
            CreateCube(root, $"BossArenaPylon_{center.x}_{center.y}_{i}", platformCenter + offset + Vector3.up * 1.45f, new Vector3(0.42f, 2.9f, 0.42f), darkMaterial, false);
            CreateCube(root, $"BossArenaPylonGlow_{center.x}_{center.y}_{i}", platformCenter + offset + Vector3.up * 2.45f, new Vector3(0.16f, 0.78f, 0.16f), accentMaterial, false);
            CreateCube(root, $"BossArenaSuspend_{center.x}_{center.y}_{i}", platformCenter + offset * 0.74f + Vector3.up * 4.0f, new Vector3(0.12f, 3.2f, 0.12f), darkMaterial, false);
        }

        switch (Mathf.Abs(themeIndex) % 4)
        {
            case 1:
                AddBackdropGlowStrip(root, $"BossArenaSkySpine_{center.x}_{center.y}", platformCenter + Vector3.up * 7.2f, new Vector3(0.08f, 1.6f, 0.08f), profile.structureGlowColor);
                break;
            case 2:
                CreateCube(root, $"BossArenaVentBand_{center.x}_{center.y}", platformCenter + Vector3.up * 3.8f, new Vector3(5.6f, 0.18f, 1.1f), accentMaterial, false);
                break;
            case 3:
                AddBackdropGlowStrip(root, $"BossArenaSignalRing_{center.x}_{center.y}", platformCenter + Vector3.up * 4.8f, new Vector3(5.8f, 0.06f, 5.8f), profile.structureGlowColor);
                break;
            default:
                CreateCube(root, $"BossArenaCrossfireMast_{center.x}_{center.y}", platformCenter + Vector3.up * 6.8f, new Vector3(0.18f, 2.2f, 0.18f), darkMaterial, false);
                AddBackdropGlowStrip(root, $"BossArenaCrossfireGlow_{center.x}_{center.y}", platformCenter + Vector3.up * 7.8f, new Vector3(0.08f, 0.9f, 0.08f), profile.structureGlowColor);
                break;
        }
    }

    private void BuildShopStationModel(Transform parent, CybergrindShopStation.ShopService service, string label)
    {
        parent = GetShopVisualRoot(parent);
        Renderer legacyShell = parent.parent != null ? parent.parent.GetComponent<Renderer>() : null;
        if (legacyShell != null)
            legacyShell.enabled = false;

        Material glow = accentMaterial != null ? accentMaterial : itemMaterial;
        Material body = darkMaterial != null ? darkMaterial : floorMaterial;
        CreateChildCube(parent, $"{label}_Foot", new Vector3(0f, -0.58f, 0f), new Vector3(1.78f, 0.18f, 1.24f), body, false);
        CreateChildCube(parent, $"{label}_Column", new Vector3(0f, -0.29f, -0.02f), new Vector3(1.2f, 0.52f, 0.82f), body, false);
        CreateChildCube(parent, $"{label}_Top", new Vector3(0f, 0.02f, 0f), new Vector3(1.5f, 0.12f, 1.02f), body, false);
        Renderer insetRenderer = CreateChildCube(parent, $"{label}_TopInset", new Vector3(0f, 0.09f, 0.01f), new Vector3(1.12f, 0.025f, 0.72f), glow, false).GetComponent<Renderer>();
        Renderer statusRenderer = CreateChildCube(parent, $"{label}_Status", new Vector3(0f, -0.23f, 0.405f), new Vector3(0.66f, 0.055f, 0.025f), glow, false).GetComponent<Renderer>();

        ShopStationPresentation presentation = parent.GetComponent<ShopStationPresentation>();
        if (presentation == null)
            presentation = parent.gameObject.AddComponent<ShopStationPresentation>();
        presentation.accentRenderers = new[] { insetRenderer, statusRenderer };
        CreateShopDisplayLight(parent, glow != null ? glow.color : Color.cyan);

        GameObject serviceDisplay = null;
        switch (service)
        {
            case CybergrindShopStation.ShopService.Repair:
                serviceDisplay = new GameObject("RepairModule");
                serviceDisplay.transform.SetParent(parent, false);
                serviceDisplay.transform.localPosition = new Vector3(0f, 0.38f, 0f);
                CreateChildCube(serviceDisplay.transform, "RepairHousing", Vector3.zero, new Vector3(0.58f, 0.48f, 0.42f), body, false);
                CreateChildCube(serviceDisplay.transform, "RepairCrossH", new Vector3(0f, 0f, 0.23f), new Vector3(0.46f, 0.1f, 0.04f), glow, false);
                CreateChildCube(serviceDisplay.transform, "RepairCrossV", new Vector3(0f, 0f, 0.23f), new Vector3(0.1f, 0.42f, 0.04f), glow, false);
                break;
            case CybergrindShopStation.ShopService.Overclock:
                break;
            case CybergrindShopStation.ShopService.Surge:
                serviceDisplay = new GameObject("SurgeModule");
                serviceDisplay.transform.SetParent(parent, false);
                serviceDisplay.transform.localPosition = new Vector3(0f, 0.38f, 0f);
                CreateChildCube(serviceDisplay.transform, "SurgeHousing", Vector3.zero, new Vector3(0.62f, 0.38f, 0.46f), body, false);
                CreateChildCube(serviceDisplay.transform, "SurgeArrow", new Vector3(0f, 0.08f, 0.25f), new Vector3(0.34f, 0.34f, 0.04f), glow, false).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                CreateChildCube(serviceDisplay.transform, "SurgeStem", new Vector3(0f, -0.2f, 0.25f), new Vector3(0.1f, 0.32f, 0.04f), glow, false);
                break;
        }

        if (serviceDisplay != null)
            presentation.productRoot = serviceDisplay.transform;
    }

    private void BuildShopWeaponDisplay(Transform parent, int presetIndex, string label)
    {
        parent = GetShopVisualRoot(parent);
        Color color = GetShopWeaponAccent(presetIndex);
        Material accent = BuildShopDisplayMaterial(color);
        Material body = darkMaterial != null ? darkMaterial : floorMaterial;
        GameObject displayRoot = new GameObject($"{label}_WeaponDisplay");
        displayRoot.transform.SetParent(parent, false);
        displayRoot.transform.localPosition = new Vector3(0f, 0.36f, 0.02f);
        displayRoot.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        displayRoot.transform.localScale = Vector3.one * 0.62f;
        BuildShopDisplayWeapon(displayRoot.transform, presetIndex, body, accent);
        ShopStationPresentation presentation = parent.GetComponent<ShopStationPresentation>();
        if (presentation != null) presentation.productRoot = displayRoot.transform;
    }

    private void BuildShopModifierDisplay(Transform parent, Gun.PassiveMod passive, Gun.AltFireMod alt, string label)
    {
        parent = GetShopVisualRoot(parent);
        Color color = passive switch
        {
            Gun.PassiveMod.SharpenedRounds => new Color(1f, 0.52f, 0.28f, 0.88f),
            Gun.PassiveMod.Stabilizer => new Color(0.58f, 0.92f, 1f, 0.88f),
            Gun.PassiveMod.RapidFeed => new Color(0.78f, 1f, 0.5f, 0.88f),
            _ => new Color(1f, 0.82f, 0.62f, 0.88f)
        };
        Material accent = BuildShopDisplayMaterial(color);
        Material body = darkMaterial != null ? darkMaterial : floorMaterial;

        GameObject root = new GameObject($"{label}_ModDisplay");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0f, 0.42f, 0.02f);
        root.transform.localScale = Vector3.one * 0.9f;

        BuildShopDisplayModifier(root.transform, passive, alt, body, accent);
        ShopStationPresentation presentation = parent.GetComponent<ShopStationPresentation>();
        if (presentation != null) presentation.productRoot = root.transform;
    }

    private void BuildShopDisplayModifier(Transform root, Gun.PassiveMod passive, Gun.AltFireMod alt, Material body, Material accent)
    {
        AddDisplayPart(root, "ModifierHousing", Vector3.zero, new Vector3(0.7f, 0.3f, 0.56f), Vector3.zero, body);
        AddDisplayPart(root, "ModifierMount", new Vector3(0f, -0.2f, 0f), new Vector3(0.86f, 0.12f, 0.42f), Vector3.zero, body);
        AddDisplayPart(root, "ModifierCore", new Vector3(0f, 0.02f, 0.3f), new Vector3(0.34f, 0.16f, 0.08f), Vector3.zero, accent);

        switch (passive)
        {
            case Gun.PassiveMod.SharpenedRounds:
                AddDisplayPart(root, "DamageBladeL", new Vector3(-0.24f, 0.15f, 0.02f), new Vector3(0.1f, 0.44f, 0.32f), new Vector3(0f, 0f, -16f), accent);
                AddDisplayPart(root, "DamageBladeR", new Vector3(0.24f, 0.15f, 0.02f), new Vector3(0.1f, 0.44f, 0.32f), new Vector3(0f, 0f, 16f), accent);
                break;
            case Gun.PassiveMod.Stabilizer:
                AddDisplayPart(root, "StabilizerRailL", new Vector3(-0.42f, 0f, 0f), new Vector3(0.12f, 0.2f, 0.72f), Vector3.zero, accent);
                AddDisplayPart(root, "StabilizerRailR", new Vector3(0.42f, 0f, 0f), new Vector3(0.12f, 0.2f, 0.72f), Vector3.zero, accent);
                break;
            case Gun.PassiveMod.RapidFeed:
                AddDisplayPart(root, "FeedCellL", new Vector3(-0.22f, -0.02f, -0.28f), new Vector3(0.18f, 0.38f, 0.22f), Vector3.zero, accent);
                AddDisplayPart(root, "FeedCellR", new Vector3(0.22f, -0.02f, -0.28f), new Vector3(0.18f, 0.38f, 0.22f), Vector3.zero, accent);
                break;
        }

        if (alt == Gun.AltFireMod.Overload)
        {
            AddDisplayPart(root, "OverloadSpine", new Vector3(0f, 0.28f, -0.04f), new Vector3(0.18f, 0.42f, 0.2f), Vector3.zero, accent);
            AddDisplayPart(root, "OverloadCap", new Vector3(0f, 0.5f, -0.04f), new Vector3(0.5f, 0.08f, 0.38f), Vector3.zero, body);
        }
        else if (alt == Gun.AltFireMod.QuickCharge)
        {
            AddDisplayPart(root, "ChargeCoilL", new Vector3(-0.17f, 0.3f, -0.04f), new Vector3(0.1f, 0.4f, 0.16f), Vector3.zero, accent);
            AddDisplayPart(root, "ChargeCoilR", new Vector3(0.17f, 0.3f, -0.04f), new Vector3(0.1f, 0.4f, 0.16f), Vector3.zero, accent);
        }
    }

    private Color GetShopWeaponAccent(int presetIndex)
    {
        return presetIndex < 3
            ? new Color(0.72f, 0.95f, 1f, 0.86f)
            : presetIndex < 6
                ? new Color(1f, 0.72f, 0.42f, 0.86f)
                : new Color(0.9f, 0.62f, 1f, 0.86f);
    }

    private void BuildShopDisplayWeapon(Transform root, int presetIndex, Material body, Material accent)
    {
        int displayModelIndex = presetIndex switch
        {
            1 => 2,
            2 => 3,
            3 => 4,
            _ => 0
        };
        switch (displayModelIndex)
        {
            case 0:
                AddDisplayPart(root, "VesperGrip", new Vector3(-0.04f, -0.38f, 0.05f), new Vector3(0.24f, 0.72f, 0.24f), new Vector3(-14f, 0f, 0f), body);
                AddDisplayPart(root, "VesperFrame", new Vector3(0f, -0.08f, 0.48f), new Vector3(0.46f, 0.28f, 0.92f), Vector3.zero, body);
                AddDisplayPart(root, "VesperSuppressor", new Vector3(0f, 0f, 1.18f), new Vector3(0.22f, 0.22f, 0.82f), Vector3.zero, body);
                AddDisplayPart(root, "VesperSight", new Vector3(0f, 0.22f, 0.58f), new Vector3(0.06f, 0.18f, 0.5f), new Vector3(-6f, 0f, 0f), accent);
                AddDisplayPart(root, "VesperRailL", new Vector3(-0.25f, 0.04f, 0.82f), new Vector3(0.06f, 0.1f, 0.72f), Vector3.zero, accent);
                AddDisplayPart(root, "VesperRailR", new Vector3(0.25f, 0.04f, 0.82f), new Vector3(0.06f, 0.1f, 0.72f), Vector3.zero, accent);
                break;
            case 1:
                AddDisplayPart(root, "RedlineGrip", new Vector3(0f, -0.42f, 0.1f), new Vector3(0.3f, 0.76f, 0.28f), new Vector3(-10f, 0f, 0f), body);
                AddDisplayPart(root, "RedlineBreech", new Vector3(0f, -0.02f, 0.5f), new Vector3(0.68f, 0.4f, 0.72f), Vector3.zero, body);
                AddDisplayPart(root, "RedlineCore", new Vector3(0f, 0.05f, 0.9f), new Vector3(0.26f, 0.26f, 1.35f), Vector3.zero, accent);
                AddDisplayPart(root, "RedlineForkL", new Vector3(-0.28f, 0.04f, 1.48f), new Vector3(0.1f, 0.16f, 0.62f), new Vector3(0f, 5f, 0f), body);
                AddDisplayPart(root, "RedlineForkR", new Vector3(0.28f, 0.04f, 1.48f), new Vector3(0.1f, 0.16f, 0.62f), new Vector3(0f, -5f, 0f), body);
                AddDisplayPart(root, "RedlineSpine", new Vector3(0f, 0.22f, 0.78f), new Vector3(0.12f, 0.22f, 1.12f), Vector3.zero, body);
                break;
            case 2:
                AddDisplayPart(root, "TridentGrip", new Vector3(0f, -0.4f, 0.12f), new Vector3(0.26f, 0.7f, 0.25f), new Vector3(-12f, 0f, 0f), body);
                AddDisplayPart(root, "TridentCell", new Vector3(0f, -0.05f, 0.52f), new Vector3(0.58f, 0.4f, 0.7f), Vector3.zero, body);
                AddDisplayPart(root, "TridentCenter", new Vector3(0f, 0.12f, 1.15f), new Vector3(0.1f, 0.1f, 1.1f), Vector3.zero, accent);
                AddDisplayPart(root, "TridentLeft", new Vector3(-0.24f, -0.04f, 1.1f), new Vector3(0.1f, 0.1f, 1.0f), new Vector3(0f, 0f, -4f), accent);
                AddDisplayPart(root, "TridentRight", new Vector3(0.24f, -0.04f, 1.1f), new Vector3(0.1f, 0.1f, 1.0f), new Vector3(0f, 0f, 4f), accent);
                break;
            case 3:
                AddDisplayPart(root, "KilnStock", new Vector3(0f, -0.2f, 0.05f), new Vector3(0.5f, 0.52f, 0.62f), new Vector3(8f, 0f, 0f), body);
                AddDisplayPart(root, "KilnChamber", new Vector3(0f, 0.18f, 0.56f), new Vector3(0.74f, 0.48f, 0.62f), Vector3.zero, accent);
                AddDisplayPart(root, "KilnVentL", new Vector3(-0.42f, 0.06f, 0.72f), new Vector3(0.12f, 0.58f, 0.48f), new Vector3(0f, 0f, -12f), body);
                AddDisplayPart(root, "KilnVentR", new Vector3(0.42f, 0.06f, 0.72f), new Vector3(0.12f, 0.58f, 0.48f), new Vector3(0f, 0f, 12f), body);
                AddDisplayPart(root, "KilnMuzzle", new Vector3(0f, 0.02f, 1.55f), new Vector3(0.42f, 0.42f, 0.72f), Vector3.zero, body);
                break;
            case 4:
                AddDisplayPart(root, "LodestarBody", new Vector3(0f, -0.08f, 0.62f), new Vector3(0.58f, 0.42f, 1.1f), Vector3.zero, body);
                AddDisplayPart(root, "LodestarGrip", new Vector3(0f, -0.5f, 0.3f), new Vector3(0.28f, 0.64f, 0.26f), new Vector3(-9f, 0f, 0f), body);
                AddDisplayPart(root, "LodestarArmL", new Vector3(-0.48f, 0.12f, 1.2f), new Vector3(0.14f, 0.48f, 1.28f), new Vector3(0f, -8f, -10f), body);
                AddDisplayPart(root, "LodestarArmR", new Vector3(0.48f, 0.12f, 1.2f), new Vector3(0.14f, 0.48f, 1.28f), new Vector3(0f, 8f, 10f), body);
                AddDisplayPart(root, "LodestarLens", new Vector3(0f, 0.1f, 1.74f), new Vector3(0.28f, 0.28f, 0.2f), Vector3.zero, accent);
                break;
            case 5:
                AddDisplayPart(root, "BreachBlock", new Vector3(0f, -0.02f, 0.72f), new Vector3(0.86f, 0.58f, 1.3f), Vector3.zero, body);
                AddDisplayPart(root, "BreachGrip", new Vector3(0f, -0.56f, 0.24f), new Vector3(0.32f, 0.7f, 0.3f), new Vector3(-8f, 0f, 0f), body);
                AddDisplayPart(root, "BreachRam", new Vector3(0f, 0.06f, 1.48f), new Vector3(0.34f, 0.34f, 1.22f), Vector3.zero, accent);
                AddDisplayPart(root, "BreachMuzzle", new Vector3(0f, 0.04f, 1.98f), new Vector3(1.0f, 0.62f, 0.34f), Vector3.zero, body);
                break;
            case 6:
                AddDisplayPart(root, "CinderRear", new Vector3(0f, -0.08f, 0.3f), new Vector3(0.7f, 0.52f, 0.82f), Vector3.zero, body);
                AddDisplayPart(root, "CinderDrum", new Vector3(0f, -0.02f, 0.9f), new Vector3(0.68f, 0.42f, 0.62f), Vector3.zero, accent);
                AddDisplayPart(root, "CinderTube", new Vector3(0f, 0.06f, 1.5f), new Vector3(0.3f, 0.3f, 1.28f), Vector3.zero, body);
                AddDisplayPart(root, "CinderCageL", new Vector3(-0.48f, 0.12f, 1.28f), new Vector3(0.12f, 0.72f, 1.28f), new Vector3(0f, 0f, -8f), accent);
                AddDisplayPart(root, "CinderCageR", new Vector3(0.48f, 0.12f, 1.28f), new Vector3(0.12f, 0.72f, 1.28f), new Vector3(0f, 0f, 8f), accent);
                break;
            case 7:
                AddDisplayPart(root, "DriverRear", new Vector3(0f, -0.04f, 0.38f), new Vector3(0.82f, 0.48f, 0.86f), Vector3.zero, body);
                AddDisplayPart(root, "DriverRam", new Vector3(0f, -0.04f, 1.45f), new Vector3(0.42f, 0.42f, 1.72f), Vector3.zero, body);
                AddDisplayPart(root, "DriverForkL", new Vector3(-0.18f, 0.02f, 1.82f), new Vector3(0.08f, 0.08f, 0.48f), Vector3.zero, accent);
                AddDisplayPart(root, "DriverForkR", new Vector3(0.18f, 0.02f, 1.82f), new Vector3(0.08f, 0.08f, 0.48f), Vector3.zero, accent);
                AddDisplayPart(root, "DriverBrace", new Vector3(0f, -0.34f, 0.74f), new Vector3(1.0f, 0.16f, 0.5f), Vector3.zero, accent);
                break;
            default:
                AddDisplayPart(root, "TempestReactor", new Vector3(0f, 0.02f, 0.72f), new Vector3(0.72f, 0.62f, 0.82f), Vector3.zero, accent);
                AddDisplayPart(root, "TempestHousing", new Vector3(0f, -0.12f, 0.48f), new Vector3(0.82f, 0.42f, 0.92f), Vector3.zero, body);
                AddDisplayPart(root, "TempestProngL", new Vector3(-0.42f, 0.18f, 1.48f), new Vector3(0.16f, 0.2f, 1.52f), new Vector3(0f, -8f, 0f), accent);
                AddDisplayPart(root, "TempestProngR", new Vector3(0.42f, 0.18f, 1.48f), new Vector3(0.16f, 0.2f, 1.52f), new Vector3(0f, 8f, 0f), accent);
                AddDisplayPart(root, "TempestGrip", new Vector3(0f, -0.56f, 0.34f), new Vector3(0.3f, 0.68f, 0.3f), new Vector3(-8f, 0f, 0f), body);
                break;
        }
    }

    private void AddDisplayPart(Transform root, string name, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Material material)
    {
        GameObject part = CreateChildCube(root, name, localPosition, localScale, material, false);
        part.transform.localRotation = Quaternion.Euler(localEuler);
    }

    private void CreateShopDescriptionLabel(Transform parent, string title, string detail, Color color)
    {
        parent = GetShopVisualRoot(parent);
        CreateShopNameplate(parent, title, detail, color);
    }

    private Transform GetShopVisualRoot(Transform station)
    {
        if (station == null) return null;

        Transform existing = station.Find("ShopVisualRoot");
        if (existing != null) return existing;

        GameObject rootObject = new GameObject("ShopVisualRoot");
        Transform root = rootObject.transform;
        root.SetParent(station, false);
        Vector3 scale = station.localScale;
        root.localScale = new Vector3(
            Mathf.Abs(scale.x) > 0.0001f ? 1f / scale.x : 1f,
            Mathf.Abs(scale.y) > 0.0001f ? 1f / scale.y : 1f,
            Mathf.Abs(scale.z) > 0.0001f ? 1f / scale.z : 1f);
        return root;
    }

    private void CreateShopNameplate(Transform parent, string title, string detail, Color color)
    {
        Material body = darkMaterial != null ? darkMaterial : floorMaterial;
        Material accent = BuildShopDisplayMaterial(color);
        CreateChildCube(parent, $"{title}_NameplateBack", new Vector3(0f, -0.23f, 0.45f), new Vector3(1.04f, 0.28f, 0.04f), body, false);
        CreateChildCube(parent, $"{title}_NameplateHeader", new Vector3(0f, -0.075f, 0.475f), new Vector3(1.04f, 0.025f, 0.02f), accent, false);

        GameObject labelObject = new GameObject($"{title}_ShopNameplate");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = new Vector3(0f, -0.24f, 0.48f);
        labelObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        TextMeshPro text = labelObject.AddComponent<TextMeshPro>();
        ProjectStructureUIRoot.ApplyDefaultFont(text);
        text.text = $"<b>{title}</b>\n<size=65%>{detail}</size>";
        text.fontSize = 1.45f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.rectTransform.sizeDelta = new Vector2(0.94f, 0.24f);
        text.sortingOrder = 4;
        text.outlineWidth = 0.14f;
        text.outlineColor = new Color(0f, 0f, 0f, 0.85f);
    }

    private Material BuildShopDisplayMaterial(Color color)
    {
        int key = color.GetHashCode();
        if (shopDisplayMaterialCache.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        Material mat = new Material(FindUrpShader(false));
        mat.name = $"ShopDisplayAccent_{key}";
        Color baseColor = Color.Lerp(color, new Color(0.12f, 0.14f, 0.16f, 1f), 0.62f);
        mat.color = baseColor;
        if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, baseColor);
        if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, baseColor);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 0.8f);
        }
        shopDisplayMaterialCache[key] = mat;
        return mat;
    }

    private void CreateShopDisplayLight(Transform parent, Color color)
    {
        GameObject lightObject = new GameObject("DisplayLight");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = new Vector3(0f, 1.35f, 0.25f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = 2.4f;
        light.intensity = 0.75f;
        light.shadows = LightShadows.None;
        ShopStationPresentation presentation = parent.GetComponent<ShopStationPresentation>();
        if (presentation != null)
            presentation.displayLight = light;
    }

    private GameObject CreateChildCube(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = scale;

        Renderer renderer = go.GetComponent<Renderer>();
        ConfigureGeneratedRenderer(renderer, material, collider);

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
        mesh.fontSize = 48;
        mesh.characterSize = 0.064f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        ArenaWorldBillboard billboard = go.AddComponent<ArenaWorldBillboard>();
        billboard.keepUpright = true;
    }

    private void RefreshEffectiveDensity()
    {
        float arenaArea = Mathf.Max(1f, width * length);
        float arenaScale = Mathf.InverseLerp(121f, 625f, arenaArea);
        float densityScale = Mathf.Lerp(0.82f, 0.36f, arenaScale);
        if (arenaMode == ArenaMode.Shop)
            densityScale *= 0.58f;
        else if (arenaMode == ArenaMode.Boss)
            densityScale *= 0.76f;
        else if (prioritizeFastMovementFlow)
            densityScale *= Mathf.Lerp(0.78f, 0.42f, clutterSuppression);

        effectiveDecorativeDensity = Mathf.Clamp01(decorativeDensity * densityScale);
        effectiveMicroDetailDensity = Mathf.Clamp01(microDetailDensity * Mathf.Lerp(0.72f, 0.28f, arenaScale));
        if (prioritizeFastMovementFlow && arenaMode == ArenaMode.Combat)
            effectiveMicroDetailDensity *= Mathf.Lerp(0.86f, 0.4f, clutterSuppression);
    }

    private void ConfigureGeneratedRenderer(Renderer renderer, Material material, bool collider)
    {
        if (renderer == null)
            return;

        renderer.sharedMaterial = material;

        bool disableShadows = !collider ||
                              material == accentMaterial ||
                              material == itemMaterial ||
                              material == puzzleMaterial ||
                              material == spawnMaterial ||
                              material == exitMaterial;

        if (!disableShadows)
            return;

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
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
        return new Vector2Int(Mathf.Clamp(lastLayoutCenter.x, 1, width - 2), 2);
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

    public string DebugDescribeCell(int x, int z)
    {
        if (lastCells == null || !InBounds(x, z))
            return $"{x},{z}: out";

        CellKind kind = lastCells[x, z];
        return $"{x},{z}: {kind} h={GetCellHeight(kind):0.0} walk={IsWalkableForContentCell(x, z)}";
    }

    public bool DebugCanTraverseCells(Vector2Int from, Vector2Int to)
    {
        return CanTraverseCells(from, to);
    }

    public int DebugPathCellCount(Vector2Int start, Vector2Int goal)
    {
        List<Vector2Int> path = FindPath(start, goal);
        return path != null ? path.Count : 0;
    }

#if UNITY_EDITOR
    public Vector3 DebugGetSpawnWorldPosition()
    {
        if (lastCells == null || !InBounds(lastSpawnCell.x, lastSpawnCell.y))
            return transform.position;

        return GetNavigationPointForCell(lastSpawnCell);
    }

    public List<Vector3> DebugGetEnemyPathProbePoints(int maxPoints)
    {
        List<Vector3> points = new List<Vector3>();
        if (lastCells == null)
            return points;

        int limit = Mathf.Max(1, maxPoints);
        int stride = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(1, width * length) / (float)limit)));
        for (int x = 2; x < width - 2 && points.Count < limit; x += stride)
        {
            for (int z = 2; z < length - 2 && points.Count < limit; z += stride)
            {
                if (!IsReliableEnemyCell(lastCells, x, z)) continue;
                points.Add(GetNavigationPointForCell(new Vector2Int(x, z)));
            }
        }

        if (points.Count >= limit)
            return points;

        for (int x = 2; x < width - 2 && points.Count < limit; x++)
        {
            for (int z = 2; z < length - 2 && points.Count < limit; z++)
            {
                if (!IsReliableEnemyCell(lastCells, x, z)) continue;
                Vector3 point = GetNavigationPointForCell(new Vector2Int(x, z));
                bool duplicate = false;
                for (int i = 0; i < points.Count; i++)
                {
                    if (Vector3.SqrMagnitude(points[i] - point) < 0.01f)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                    points.Add(point);
            }
        }

        return points;
    }
#endif

    public int DebugCountUnreachableWalkablesFromSpawn()
    {
        if (lastCells == null)
            return -1;

        Vector2Int spawn = FindFirst(lastCells, CellKind.Spawn);
        int unreachable = 0;
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                if (!IsWalkableForContentCell(x, z)) continue;
                List<Vector2Int> path = FindPath(spawn, new Vector2Int(x, z));
                if (path == null || path.Count == 0)
                    unreachable++;
            }
        }

        return unreachable;
    }

    public Vector2Int DebugFindFirstUnreachableWalkableFromSpawn()
    {
        if (lastCells == null)
            return new Vector2Int(-1, -1);

        Vector2Int spawn = FindFirst(lastCells, CellKind.Spawn);
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                if (!IsWalkableForContentCell(x, z)) continue;
                List<Vector2Int> path = FindPath(spawn, new Vector2Int(x, z));
                if (path == null || path.Count == 0)
                    return new Vector2Int(x, z);
            }
        }

        return new Vector2Int(-1, -1);
    }

    public string DebugSummarizeArenaLayout()
    {
        if (lastCells == null)
            return "Arena: no generated layout.";

        return FormatArenaDiagnostics(CollectArenaDiagnostics());
    }

    [ContextMenu("Debug/Log Arena Summary")]
    private void DebugLogArenaSummary()
    {
        Debug.Log(DebugSummarizeArenaLayout());
    }

    public string DebugAuditFastMovementLayout()
    {
        if (lastCells == null)
        {
            debugLastFastMovementAuditPassed = false;
            debugLastFastMovementAudit = "Arena audit: no generated layout.";
            return debugLastFastMovementAudit;
        }

        ArenaDiagnostics diagnostics = CollectArenaDiagnostics();
        float routeCoverage = diagnostics.walkable > 0 ? diagnostics.routeCells / (float)diagnostics.walkable : 0f;
        float elevatedCoverage = diagnostics.walkable > 0 ? diagnostics.elevated / (float)diagnostics.walkable : 0f;
        float routeRecoveryCoverage = diagnostics.recoveryCount > 0 ? diagnostics.routeRecovery / (float)diagnostics.recoveryCount : 0f;
        debugLastRouteCoverage = routeCoverage;
        debugLastElevatedCoverage = elevatedCoverage;
        debugLastRouteRecoveryCoverage = routeRecoveryCoverage;

        List<string> issues = new List<string>();
        if (diagnostics.unreachable > 0)
            issues.Add($"unreachable walkables={diagnostics.unreachable}");
        if (routeCoverage < auditMinRouteCoverage)
            issues.Add($"route coverage low ({routeCoverage * 100f:0.#}%)");
        if (elevatedCoverage < auditMinElevatedCoverage)
            issues.Add($"verticality low ({elevatedCoverage * 100f:0.#}%)");
        if (diagnostics.routeClutter > auditMaxRouteClutter)
            issues.Add($"route clutter remains ({diagnostics.routeClutter})");
        if (diagnostics.connectorEdges < auditMinConnectorEdges && arenaMode == ArenaMode.Combat)
            issues.Add($"traversal connectors low ({diagnostics.connectorEdges})");
        if (routeRecoveryCoverage < auditMinRouteRecoveryCoverage && arenaMode == ArenaMode.Combat)
            issues.Add($"route recovery low ({routeRecoveryCoverage * 100f:0.#}%)");
        if (diagnostics.flankWalkables < Mathf.Max(auditMinFlankWalkables, diagnostics.routeCells / 3) && arenaMode == ArenaMode.Combat)
            issues.Add($"flank space low ({diagnostics.flankWalkables})");

        string summary = FormatArenaDiagnostics(diagnostics);
        debugLastFastMovementAuditPassed = issues.Count == 0;
        debugLastFastMovementAudit = debugLastFastMovementAuditPassed
            ? $"[Arena Audit] PASS. {summary}"
            : $"[Arena Audit] WARN. {summary} Issues: {string.Join("; ", issues)}.";

        if (debugLastFastMovementAuditPassed)
            Debug.Log(debugLastFastMovementAudit);
        else
            Debug.LogWarning(debugLastFastMovementAudit);

        return debugLastFastMovementAudit;
    }

    [ContextMenu("Debug/Run Fast Movement Audit")]
    private void DebugRunFastMovementAuditMenu()
    {
        DebugAuditFastMovementLayout();
    }

    private ArenaDiagnostics CollectArenaDiagnostics()
    {
        ArenaDiagnostics diagnostics = new ArenaDiagnostics();
        if (lastCells == null)
            return diagnostics;

        Vector2Int spawn = FindFirst(lastCells, CellKind.Spawn);
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                CellKind kind = lastCells[x, z];
                bool route = IsNearPrimaryMovementRoute(x, z);
                if (route && (kind == CellKind.Hazard || kind == CellKind.CoverLow || kind == CellKind.CoverHigh || kind == CellKind.Void))
                    diagnostics.routeClutter++;

                if (!IsWalkableForContent(kind))
                    continue;

                diagnostics.walkable++;
                if (kind == CellKind.Platform || kind == CellKind.UpperPlatform || kind == CellKind.Bridge)
                    diagnostics.elevated++;
                if (kind == CellKind.UpperPlatform)
                    diagnostics.upperPlatforms++;
                if (route)
                    diagnostics.routeCells++;
                else
                    diagnostics.flankWalkables++;

                List<Vector2Int> path = FindPath(spawn, new Vector2Int(x, z));
                if (path == null || path.Count == 0)
                    diagnostics.unreachable++;
            }
        }

        diagnostics.recoveryCount = recoveryPoints.Count;
        for (int i = 0; i < recoveryPoints.Count; i++)
        {
            Vector2Int recoveryCell = WorldToCell(recoveryPoints[i]);
            if (InBounds(recoveryCell.x, recoveryCell.y) && IsNearPrimaryMovementRoute(recoveryCell.x, recoveryCell.y))
                diagnostics.routeRecovery++;
        }

        diagnostics.connectorEdges = traversalConnectors.Count / 2;
        return diagnostics;
    }

    private string FormatArenaDiagnostics(ArenaDiagnostics diagnostics)
    {
        float routeCoverage = diagnostics.walkable > 0 ? (diagnostics.routeCells / (float)diagnostics.walkable) * 100f : 0f;
        float elevatedCoverage = diagnostics.walkable > 0 ? (diagnostics.elevated / (float)diagnostics.walkable) * 100f : 0f;
        return $"Arena summary: walkable={diagnostics.walkable}, elevated={diagnostics.elevated} ({elevatedCoverage:0.#}%), upper={diagnostics.upperPlatforms}, routeCells={diagnostics.routeCells} ({routeCoverage:0.#}%), routeClutter={diagnostics.routeClutter}, flankWalkables={diagnostics.flankWalkables}, recovery={diagnostics.recoveryCount}, routeRecovery={diagnostics.routeRecovery}, connectors={diagnostics.connectorEdges}, unreachable={diagnostics.unreachable}, repairs={debugLastRuntimeConnectivityRepairs}, culls={debugLastRuntimeConnectivityCulls}, districts={debugLastReconfigureDistricts}.";
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
    }

    private bool CanTraverseCells(Vector2Int from, Vector2Int to)
    {
        if (!InBounds(from.x, from.y) || !InBounds(to.x, to.y) || lastCells == null)
            return false;

        bool diagonal = from.x != to.x && from.y != to.y;
        if (diagonal)
        {
            Vector2Int sideA = new Vector2Int(from.x, to.y);
            Vector2Int sideB = new Vector2Int(to.x, from.y);
            bool routeA = IsWalkableForContentCell(sideA.x, sideA.y) &&
                          CanTraverseOrthogonalCells(from, sideA) &&
                          CanTraverseOrthogonalCells(sideA, to);
            bool routeB = IsWalkableForContentCell(sideB.x, sideB.y) &&
                          CanTraverseOrthogonalCells(from, sideB) &&
                          CanTraverseOrthogonalCells(sideB, to);
            return routeA || routeB;
        }

        return CanTraverseOrthogonalCells(from, to);
    }

    private bool CanTraverseOrthogonalCells(Vector2Int from, Vector2Int to)
    {
        if (!InBounds(from.x, from.y) || !InBounds(to.x, to.y) || lastCells == null)
            return false;
        if (from.x != to.x && from.y != to.y)
            return false;
        if (!IsWalkableForContentCell(from.x, from.y) || !IsWalkableForContentCell(to.x, to.y))
            return false;

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
        ConfigureGeneratedRenderer(renderer, material, collider);

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
                return arenaMode == ArenaMode.Shop ? 0f : platformLevel * levelHeight;
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
            floor = new Color(0.115f, 0.123f, 0.135f),
            dark = new Color(0.052f, 0.060f, 0.072f),
            accent = new Color(0.060f, 0.42f, 0.50f),
            accentEmission = new Color(0.0f, 0.50f, 0.66f),
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
                    floor = new Color(0.102f, 0.114f, 0.142f),
                    dark = new Color(0.050f, 0.058f, 0.084f),
                    accent = new Color(0.14f, 0.21f, 0.47f),
                    accentEmission = new Color(0.13f, 0.32f, 0.76f),
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
                    floor = new Color(0.126f, 0.104f, 0.096f),
                    dark = new Color(0.070f, 0.050f, 0.044f),
                    accent = new Color(0.40f, 0.15f, 0.12f),
                    accentEmission = new Color(0.70f, 0.24f, 0.16f),
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
                    floor = new Color(0.106f, 0.126f, 0.104f),
                    dark = new Color(0.050f, 0.068f, 0.052f),
                    accent = new Color(0.14f, 0.38f, 0.16f),
                    accentEmission = new Color(0.16f, 0.64f, 0.24f),
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
                    directiveTitle = "CROSSFIRE GRID",
                    directiveDetail = "Tight lanes and more shooters.",
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
                    ambientBoost = 0f,
                    fogDensity = 0.0135f,
                    shellHeight = 30f,
                    shellInsetScale = 0.48f,
                    skylineHeight = 22f,
                    skylineDensity = 0.46f,
                    structureGlowColor = new Color(0.34f, 0.82f, 1f)
                };
            case 1:
                return new ThemeProfile
                {
                    directiveTitle = "HIGH RISE",
                    directiveDetail = "More vertical routes and flying enemies.",
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
                    ambientBoost = 0.015f,
                    fogDensity = 0.0148f,
                    shellHeight = 34f,
                    shellInsetScale = 0.7f,
                    skylineHeight = 30f,
                    skylineDensity = 0.78f,
                    structureGlowColor = new Color(0.48f, 0.84f, 1f)
                };
            case 2:
                return new ThemeProfile
                {
                    directiveTitle = "HEAT SINK",
                    directiveDetail = "More heavy enemies near the centre.",
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
                    ambientBoost = 0.008f,
                    fogDensity = 0.0115f,
                    shellHeight = 32f,
                    shellInsetScale = 0.58f,
                    skylineHeight = 26f,
                    skylineDensity = 0.62f,
                    structureGlowColor = new Color(1f, 0.52f, 0.22f)
                };
            default:
                return new ThemeProfile
                {
                    directiveTitle = "GREEN STATIC",
                    directiveDetail = "More cover and side routes.",
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
                    ambientBoost = 0.012f,
                    fogDensity = 0.0128f,
                    shellHeight = 31f,
                    shellInsetScale = 0.62f,
                    skylineHeight = 24f,
                    skylineDensity = 0.56f,
                    structureGlowColor = new Color(0.64f, 1f, 0.6f)
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
        if (unlit)
        {
            if (cachedUnlitShader == null)
            {
                cachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (cachedUnlitShader == null) cachedUnlitShader = Shader.Find("Sprites/Default");
            }
            return cachedUnlitShader;
        }

        if (cachedLitShader == null)
        {
            cachedLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (cachedLitShader == null) cachedLitShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (cachedLitShader == null) cachedLitShader = Shader.Find("Sprites/Default");
        }
        return cachedLitShader;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!prioritizeFastMovementFlow)
            return;

        if (debugDrawFastRouteBands)
            DrawFastRouteBandGizmos();
        if (debugDrawTraversalConnectors)
            DrawTraversalConnectorGizmos();
        if (debugDrawRecoveryPoints)
            DrawRecoveryPointGizmos();
    }

    private void DrawFastRouteBandGizmos()
    {
        int halfWidth = Mathf.Max(1, lastPrimaryLaneHalfWidth);
        int laneOffset = Mathf.Max(0, lastSideLaneOffset);
        Vector2Int center = lastLayoutCenter == Vector2Int.zero
            ? new Vector2Int(width / 2, length / 2)
            : lastLayoutCenter;

        Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.22f);
        DrawBandRect(center.x - halfWidth, 1, center.x + halfWidth, length - 2, 0.1f);
        DrawBandRect(1, center.y - halfWidth, width - 2, center.y + halfWidth, 0.1f);

        if (laneOffset > 0)
        {
            Gizmos.color = new Color(1f, 0.65f, 0.18f, 0.18f);
            DrawBandRect(1, center.y + laneOffset - 1, width - 2, center.y + laneOffset + 1, 0.14f);
            DrawBandRect(1, center.y - laneOffset - 1, width - 2, center.y - laneOffset + 1, 0.14f);
            DrawBandRect(center.x + laneOffset - 1, 1, center.x + laneOffset + 1, length - 2, 0.14f);
            DrawBandRect(center.x - laneOffset - 1, 1, center.x - laneOffset + 1, length - 2, 0.14f);
        }
    }

    private void DrawBandRect(int xMin, int zMin, int xMax, int zMax, float yOffset)
    {
        xMin = Mathf.Clamp(xMin, 1, width - 2);
        xMax = Mathf.Clamp(xMax, 1, width - 2);
        zMin = Mathf.Clamp(zMin, 1, length - 2);
        zMax = Mathf.Clamp(zMax, 1, length - 2);
        float spanX = (xMax - xMin + 1) * tileSize;
        float spanZ = (zMax - zMin + 1) * tileSize;
        Vector3 center = transform.position + CellCenter((xMin + xMax) / 2, (zMin + zMax) / 2, yOffset);
        Gizmos.DrawCube(center, new Vector3(spanX, 0.06f, spanZ));
    }

    private void DrawTraversalConnectorGizmos()
    {
        if (traversalConnectors == null || traversalConnectors.Count == 0)
            return;

        Gizmos.color = new Color(0.9f, 0.2f, 1f, 0.9f);
        HashSet<string> drawn = new HashSet<string>();
        foreach (KeyValuePair<long, List<Vector3>> pair in traversalConnectors)
        {
            if (pair.Value == null || pair.Value.Count < 2)
                continue;

            Vector3 first = pair.Value[0];
            Vector3 last = pair.Value[pair.Value.Count - 1];
            string keyA = $"{Mathf.RoundToInt(first.x * 100f)}:{Mathf.RoundToInt(first.y * 100f)}:{Mathf.RoundToInt(first.z * 100f)}";
            string keyB = $"{Mathf.RoundToInt(last.x * 100f)}:{Mathf.RoundToInt(last.y * 100f)}:{Mathf.RoundToInt(last.z * 100f)}";
            string edgeKey = string.CompareOrdinal(keyA, keyB) <= 0 ? $"{keyA}|{keyB}" : $"{keyB}|{keyA}";
            if (!drawn.Add(edgeKey))
                continue;

            for (int i = 1; i < pair.Value.Count; i++)
                Gizmos.DrawLine(transform.position + pair.Value[i - 1], transform.position + pair.Value[i]);
        }
    }

    private void DrawRecoveryPointGizmos()
    {
        if (recoveryPoints == null || recoveryPoints.Count == 0)
            return;

        for (int i = 0; i < recoveryPoints.Count; i++)
        {
            Vector2Int cell = WorldToCell(recoveryPoints[i]);
            Gizmos.color = IsNearPrimaryMovementRoute(cell.x, cell.y)
                ? new Color(0.2f, 1f, 0.45f, 0.95f)
                : new Color(1f, 0.95f, 0.22f, 0.95f);
            Gizmos.DrawSphere(recoveryPoints[i], 0.24f);
        }
    }
#endif
}

public class ArenaWorldBillboard : MonoBehaviour
{
    public bool keepUpright = true;
    private Camera targetCamera;

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        Vector3 toCamera = targetCamera.transform.position - transform.position;
        if (keepUpright)
            toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
    }
}
