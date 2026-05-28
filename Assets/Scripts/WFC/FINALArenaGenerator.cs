using System.Collections.Generic;
using UnityEngine;

public class CybergrindArenaGenerator : MonoBehaviour
{
    private enum CellKind
    {
        Void,
        Floor,
        Bridge,
        Platform,
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

    public enum ArenaMode
    {
        Combat,
        Shop,
        Boss
    }

    [Header("Floating Layout")]
    [Range(1, 4)] public int bridgeLevel = 1;
    [Range(1, 5)] public int platformLevel = 1;
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
    public float playerSpawnHeight = 2.2f;
    public string generatedRootName = "_CybergrindArena";

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

        EnsureMaterials();

        Transform root = new GameObject(generatedRootName).transform;
        root.SetParent(transform, false);
        spawned.Add(root.gameObject);

        int actualSeed = (randomizeSeedEachGeneration || seed == 0)
            ? unchecked(System.Environment.TickCount ^ (int)(Time.realtimeSinceStartup * 100000f) ^ Random.Range(int.MinValue, int.MaxValue))
            : seed;
        lastGeneratedSeed = actualSeed;
        var rng = new System.Random(actualSeed);
        CellKind[,] cells = BuildLayout(rng);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                SpawnCell(root, cells[x, z], x, z);
            }
        }

        SpawnBoundaryFrame(root);
        SpawnUndersidePillars(root, cells);
        SpawnFloatingTrim(root, cells);
        SpawnArchitecturalContent(root, cells, rng);
        SpawnGameplayContent(root, cells, rng);
        SpawnArenaLighting(root);
        PlacePlayer(cells);

        Debug.Log($"[CybergrindArena] Generated {width}x{length} arena with seed {actualSeed}.");
    }

    [ContextMenu("Clear Arena")]
    public void ClearArena()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] == null) continue;
            if (Application.isPlaying)
                Destroy(spawned[i]);
            else
                DestroyImmediate(spawned[i]);
        }

        spawned.Clear();

        Transform old = transform.Find(generatedRootName);
        if (old != null)
        {
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

        CarveVoidMoat(cells);
        int centerRadius = Mathf.Clamp(centralPlatformRadius + rng.Next(-1, 2), 2, Mathf.Max(2, Mathf.Min(width, length) / 5));
        StampRect(cells, center.x - centerRadius, center.y - centerRadius, center.x + centerRadius, center.y + centerRadius, CellKind.Platform);

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

        StampOuterDetail(cells, rng, center, spawn, exit);
        StampFloatingIslands(cells, rng, center, spawn, exit);
        StampSafeZone(cells, spawn, safeRadiusAroundSpawn);
        StampSafeZone(cells, exit, safeRadiusAroundExit);
        cells[spawn.x, spawn.y] = CellKind.Spawn;
        cells[exit.x, exit.y] = CellKind.Exit;

        return cells;
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
        int islandCount = Mathf.Clamp((width * length) / 130, 3, 12);
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
                if (roll < outerGapChance)
                {
                    cells[x, z] = CellKind.Void;
                }
                else if (roll < outerGapChance + hazardChance)
                {
                    cells[x, z] = CellKind.Hazard;
                }
                else if (roll < outerGapChance + hazardChance + coverChance)
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

    private void SpawnBoundaryFrame(Transform root)
    {
        float y = levelHeight * bridgeLevel + 0.15f;
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
        RenderSettings.ambientLight = new Color(0.035f, 0.04f, 0.048f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.015f, 0.018f, 0.022f);
        RenderSettings.fogDensity = 0.022f;

        GameObject key = new GameObject("ArenaKeyLight");
        key.transform.SetParent(root, false);
        key.transform.position = new Vector3(width * tileSize * 0.5f, 18f, length * tileSize * 0.5f);
        Light light = key.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.72f, 0.82f, 1f);
        light.intensity = 0.95f;
        key.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

        CreateCube(root, "AbyssFogPlane", new Vector3((width - 1) * tileSize * 0.5f, killPlaneY - 8f, (length - 1) * tileSize * 0.5f), new Vector3(width * tileSize * 2.2f, 1f, length * tileSize * 2.2f), darkMaterial, false);
    }

    private void SpawnGameplayContent(Transform root, CellKind[,] cells, System.Random rng)
    {
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

        int terminalCount = arenaMode == ArenaMode.Shop ? 1 : arenaMode == ArenaMode.Boss ? 3 : Mathf.Clamp((width * length) / 220, 2, 4);
        int placed = 0;
        for (int i = 0; i < candidates.Count && placed < terminalCount; i++)
        {
            Vector2Int cell = candidates[i];
            SpawnPuzzleTerminal(root, cells, cell, placed);
            placed++;
        }

        int itemCount = arenaMode == ArenaMode.Shop ? 10 : Mathf.Clamp(Mathf.RoundToInt(width * length * itemChance), 6, 18);
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
    }

    private bool IsWalkableForContent(CellKind kind)
    {
        return kind == CellKind.Floor || kind == CellKind.Bridge || kind == CellKind.Platform;
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

    private void SpawnPuzzleTerminal(Transform root, CellKind[,] cells, Vector2Int cell, int index)
    {
        float y = GetCellHeight(cells[cell.x, cell.y]);
        Vector3 pos = CellCenter(cell.x, cell.y, y + 0.95f);
        GameObject terminal = CreateCube(root, $"PuzzleTerminal_{index + 1}", pos, new Vector3(1.15f, 1.9f, 0.55f), puzzleMaterial);
        terminal.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        CybergrindPuzzleTerminal t = terminal.AddComponent<CybergrindPuzzleTerminal>();
        t.sequenceIndex = index;
        t.overridePrompt = $"Press E to solve node {index + 1}";
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
        SpawnBridgeRailings(root, cells);
        SpawnStairsAndParkour(root, cells);
        SpawnGateFrames(root, cells);
        SpawnMegaPillars(root, cells, rng);

        List<Vector2Int> platforms = new List<Vector2Int>();
        for (int x = 2; x < width - 2; x++)
        {
            for (int z = 2; z < length - 2; z++)
            {
                if (cells[x, z] == CellKind.Platform || cells[x, z] == CellKind.Bridge)
                    platforms.Add(new Vector2Int(x, z));
            }
        }

        Shuffle(platforms, rng);
        int pylonCount = Mathf.Clamp((width * length) / 145, 6, 16);
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

        int jumpPadCount = Mathf.Clamp((width * length) / 260, 2, 6);
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

    private void SpawnStairsAndParkour(Transform root, CellKind[,] cells)
    {
        int stairsMade = 0;
        for (int x = 2; x < width - 2 && stairsMade < 12; x++)
        {
            for (int z = 2; z < length - 2 && stairsMade < 12; z++)
            {
                if (cells[x, z] != CellKind.Floor) continue;
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
        if (cells[ex, ez] != CellKind.Bridge && cells[ex, ez] != CellKind.Platform) return;

        float low = GetCellHeight(cells[x, z]);
        float high = GetCellHeight(cells[ex, ez]);
        if (high <= low + 1f) return;

        int steps = 6;
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)(steps + 1);
            Vector3 pos = CellCenter(x, z, Mathf.Lerp(low, high, t) + 0.12f);
            pos += new Vector3(dx * tileSize * t, 0f, dz * tileSize * t);
            Vector3 scale = new Vector3(dx == 0 ? tileSize * 0.62f : tileSize * 0.34f, 0.24f, dz == 0 ? tileSize * 0.62f : tileSize * 0.34f);
            CreateCube(root, $"Step_{x}_{z}_{i}", pos, scale, darkMaterial);
        }
        stairsMade++;
    }

    private void SpawnParkourCluster(Transform root, Vector2Int around)
    {
        for (int i = 0; i < 5; i++)
        {
            int x = Mathf.Clamp(around.x + i - 2, 2, width - 3);
            int z = Mathf.Clamp(around.y + ((i & 1) == 0 ? 0 : 1), 2, length - 3);
            float y = Mathf.Lerp(0.9f, levelHeight - 0.8f, i / 4f);
            CreateCube(root, $"ParkourBlock_{around.x}_{around.y}_{i}", CellCenter(x, z, y), new Vector3(tileSize * 0.7f, 0.35f, tileSize * 0.7f), darkMaterial);
        }
    }

    private void SpawnBridgeRailings(Transform root, CellKind[,] cells)
    {
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < length - 1; z++)
            {
                if (cells[x, z] != CellKind.Bridge && cells[x, z] != CellKind.Platform) continue;

                float y = GetCellHeight(cells[x, z]);
                bool northOpen = !IsSameElevatedSurface(cells, x, z, x, z + 1);
                bool southOpen = !IsSameElevatedSurface(cells, x, z, x, z - 1);
                bool eastOpen = !IsSameElevatedSurface(cells, x, z, x + 1, z);
                bool westOpen = !IsSameElevatedSurface(cells, x, z, x - 1, z);

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
               (cells[nx, nz] == CellKind.Bridge || cells[nx, nz] == CellKind.Platform || cells[nx, nz] == CellKind.Spawn || cells[nx, nz] == CellKind.Exit);
    }

    private void SpawnGateFrames(Transform root, CellKind[,] cells)
    {
        Vector2Int center = new Vector2Int(width / 2, length / 2);
        float y = platformLevel * levelHeight;
        float h = 8.5f;
        float span = tileSize * Mathf.Clamp(centralPlatformRadius * 2 + 1, 7, 13);

        CreateGate(root, "NorthGate", CellCenter(center.x, center.y + centralPlatformRadius + 2, y), span, h, true);
        CreateGate(root, "SouthGate", CellCenter(center.x, center.y - centralPlatformRadius - 2, y), span, h, true);
        CreateGate(root, "EastGate", CellCenter(center.x + centralPlatformRadius + 2, center.y, y), span, h, false);
        CreateGate(root, "WestGate", CellCenter(center.x - centralPlatformRadius - 2, center.y, y), span, h, false);
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
        for (int i = -1; i <= 1; i++)
        {
            Vector2Int cell = new Vector2Int(center.x + (i * 3), center.y);
            float y = GetCellHeight(CellKind.Platform);
            CreateCube(root, $"ShopDisplay_{i + 2}", CellCenter(cell.x, cell.y, y + 0.65f), new Vector3(2.4f, 1.3f, 1.0f), itemMaterial);
        }
    }

    private void SpawnBossArenaMarkers(Transform root, CellKind[,] cells)
    {
        Vector2Int center = new Vector2Int(width / 2, length / 2);
        float y = GetCellHeight(CellKind.Platform);
        CreateCube(root, "BossCorePlaceholder", CellCenter(center.x, center.y, y + 1.6f), new Vector3(2.2f, 3.2f, 2.2f), hazardMaterial, false);
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
        playerToPlace.position = CellCenter(spawn.x, spawn.y, y);
        playerToPlace.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
    }

    private Vector2Int FindFirst(CellKind[,] cells, CellKind kind)
    {
        for (int x = 0; x < width; x++)
            for (int z = 0; z < length; z++)
                if (cells[x, z] == kind)
                    return new Vector2Int(x, z);
        return new Vector2Int(width / 2, 2);
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

    private float GetCellHeight(CellKind kind)
    {
        switch (kind)
        {
            case CellKind.Bridge:
                return bridgeLevel * levelHeight;
            case CellKind.Platform:
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
        floorMaterial = EnsureMaterial(floorMaterial, "Cybergrind Floor", new Color(0.055f, 0.06f, 0.066f), Color.black, false);
        darkMaterial = EnsureMaterial(darkMaterial, "Cybergrind Dark", new Color(0.012f, 0.014f, 0.017f), Color.black, false);
        accentMaterial = EnsureMaterial(accentMaterial, "Cybergrind Cyan Accent", new Color(0.025f, 0.23f, 0.28f), new Color(0.0f, 0.22f, 0.30f), true);
        hazardMaterial = EnsureMaterial(hazardMaterial, "Cybergrind Hazard", new Color(0.30f, 0.035f, 0.018f), new Color(0.38f, 0.04f, 0.0f), true);
        spawnMaterial = EnsureMaterial(spawnMaterial, "Cybergrind Spawn", new Color(0.02f, 0.23f, 0.10f), new Color(0.0f, 0.25f, 0.08f), true);
        exitMaterial = EnsureMaterial(exitMaterial, "Cybergrind Exit", new Color(0.28f, 0.13f, 0.025f), new Color(0.32f, 0.12f, 0.0f), true);
        itemMaterial = EnsureMaterial(itemMaterial, "Cybergrind Item", new Color(0.08f, 0.17f, 0.16f), new Color(0.0f, 0.20f, 0.16f), true);
        puzzleMaterial = EnsureMaterial(puzzleMaterial, "Cybergrind Puzzle", new Color(0.09f, 0.07f, 0.13f), new Color(0.13f, 0.05f, 0.20f), true);
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
