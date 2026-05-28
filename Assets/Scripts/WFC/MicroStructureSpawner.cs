using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine;

/// <summary>
/// Post-processor that spawns structure prefabs on top of generated floor cells.
/// It is deterministic (seeded), edge-biased, center-clear, and spacing-aware.
/// </summary>
public class MicroStructureSpawner : PostProcessor
{
    [Flags]
    public enum RegionMask
    {
        None = 0,
        Open = 1 << 0,
        CombatRoom = 1 << 1,
        BossRoom = 1 << 2,
        Shop = 1 << 3,
        Platform = 1 << 4,
        Terrain = 1 << 6,
        Hill = 1 << 7,
        Bridge = 1 << 8,
        MicroDetail = 1 << 9,
        MicroCrate = 1 << 10,
        Any = ~0
    }

    [Serializable]
    public class StructureEntry
    {
        public string id = "Structure";
        public GameObject prefab;
        [Min(1)] public int weight = 1;
        [Min(1)] public int footprintX = 1;
        [Min(1)] public int footprintZ = 1;
        [Min(0)] public int minSpacingCells = 2;
        [Min(0)] public int minDistanceFromSpawn = 3;
        [Min(0)] public int minDistanceFromExit = 4;
        public float yOffset = 0f;
        public bool randomYaw = true;
        public RegionMask allowedRegions = RegionMask.Open | RegionMask.CombatRoom | RegionMask.Shop;
    }

    [Header("Structure Pool")]
    public List<StructureEntry> structures = new List<StructureEntry>();

    [Header("Placement Rules")]
    [Range(0f, 1f)] public float baseSpawnChancePerCell = 0.03f;
    [Range(0f, 0.5f)] public float centerClearRadius = 0.30f;
    [Range(1f, 4f)] public float edgeBiasMultiplier = 2.25f;
    [Min(0)] public int maxStructures = 32;
    public bool requireAirAbove = true;
    [Min(1)] public int requiredAirHeight = 1;
    public bool strictFootprintValidation = true;
    public bool snapToFloorSurface = true;
    public bool preferReservedMicroRegions = true;
    public bool autoNormalizeLegacyEntries = true;
    [Min(0)] public int minStructuresToSpawn = 4;
    public bool physicsResolveAfterSpawn = true;
    [Min(1)] public int resolveFrames = 20;
    public float resolveRayStartHeight = 64f;
    public float resolveRayDistance = 256f;

    [Header("Runtime")]
    public string containerName = "_MicroStructures";

    private readonly List<PlacedInfo> placed = new List<PlacedInfo>();

    private struct PlacedInfo
    {
        public Vector2Int center;
        public int minSpacing;
    }

    // Track the Y level used for each macro region to ensure height coherence
    private Dictionary<MacroRegion, int> regionYLevelCache = new Dictionary<MacroRegion, int>();

    private class SpawnMeta : MonoBehaviour
    {
        public float yOffset;
    }

    public override void Process(WFCGenerator3D generator)
    {
        if (generator == null) return;

        CleanupOldContainer(generator.transform);
        regionYLevelCache.Clear(); // Reset height coherence cache for new generation

        if (structures == null || structures.Count == 0)
        {
            Debug.Log("[MicroStructureSpawner] No structures configured.");
            return;
        }

        var validStructures = structures.Where(s => s != null && s.prefab != null && s.weight > 0).ToList();
        if (validStructures.Count == 0)
        {
            Debug.Log("[MicroStructureSpawner] No valid prefabs in structure list.");
            return;
        }

        if (autoNormalizeLegacyEntries)
            NormalizeLegacyEntries(validStructures, generator);

        Transform container = new GameObject(containerName).transform;
        container.SetParent(generator.transform, false);

        int width = generator.GridWidth;
        int height = generator.GridHeight;
        int length = generator.GridLength;

        bool[,] occupied = new bool[width, length];

        Vector3Int? spawn = generator.FindSpawnCell();
        Vector3Int? exit = generator.FindExitPitCell();

        int deterministicSeed = generator.seed ^ (width * 73856093) ^ (length * 19349663) ^ (height * 83492791);
        System.Random rng = new System.Random(deterministicSeed);

        int placedCount = 0;
        int reservedAttempts = 0;
        int fallbackAttempts = 0;

        var cells = BuildShuffledCells(width, length, rng);
        int reservedCellCount = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (IsReservedMicroRegion(GetRegion(generator, c.x, c.y))) reservedCellCount++;
        }

        // Pre-pass: Build explicit connecting structures between macro features for coherence
        // DISABLED: This was creating too many structures. Reserved micro regions should handle connectivity via staircases instead.
        //placedCount += BuildConnectivityStructures(generator, container, validStructures, width, length, height, occupied, spawn, exit, rng);

        // Pass 1: reserved micro regions (preferred)
        for (int i = 0; i < cells.Count; i++)
        {
            int x = cells[i].x;
            int z = cells[i].y;
            if (placedCount >= maxStructures) break;
            if (occupied[x, z]) continue;

            // Skip critical macro cells (Spawn, ExitPit, marked room centers)
            if (IsCriticalMacroCell(generator, x, z)) continue;

            MacroRegion region = GetRegion(generator, x, z);
            bool reservedMicro = IsReservedMicroRegion(region);
            if (!reservedMicro) continue;
            reservedAttempts++;

            if (!IsRegionSpawnable(region)) continue;

            int surfaceY = FindPlacementSurfaceY(generator, x, z, region);
            if (surfaceY < 0) continue;

            // Skip wall tiles
            if (IsWallTile(generator, x, z, surfaceY)) continue;

            WFCTile3D floor = generator.GetTileAt(x, surfaceY, z);
            if (!IsFloorLike(floor)) continue;

            if (requireAirAbove && !HasClearance(generator, x, z, surfaceY, height)) continue;

            float edgeChance = ComputeEdgeBiasedChance(x, z, width, length);
            if (preferReservedMicroRegions)
            {
                // Reserved micro regions should have a much higher spawn chance than generic cells.
                edgeChance = reservedMicro ? Mathf.Max(edgeChance, 0.85f) : edgeChance * 0.45f;
            }
            if (placedCount < minStructuresToSpawn)
                edgeChance = 1f;
            if (rng.NextDouble() > edgeChance) continue;

            var candidates = validStructures.Where(s => IsRegionAllowed(s.allowedRegions, region)).ToList();
            if (candidates.Count == 0)
                candidates = validStructures.Where(s => s.footprintX == 1 && s.footprintZ == 1).ToList();

            candidates = candidates.Where(s => s.footprintX == 1 && s.footprintZ == 1).ToList();
            if (candidates.Count == 0) continue;

            StructureEntry picked = PickWeighted(candidates, rng);
            if (picked == null) continue;

            int yawQuarter = picked.randomYaw ? rng.Next(0, 4) : 0;
            int footprintX = ((yawQuarter & 1) == 1) ? picked.footprintZ : picked.footprintX;
            int footprintZ = ((yawQuarter & 1) == 1) ? picked.footprintX : picked.footprintZ;

            if (reservedMicro && (footprintX != 1 || footprintZ != 1)) continue;

            if (!CanPlace(generator, picked, x, z, footprintX, footprintZ, surfaceY, width, height, length, occupied, spawn, exit)) continue;

            PlaceStructure(generator, container, picked, x, z, surfaceY, yawQuarter);
            MarkOccupied(occupied, x, z, footprintX, footprintZ);
            placed.Add(new PlacedInfo
            {
                center = new Vector2Int(x, z),
                minSpacing = picked.minSpacingCells
            });
            placedCount++;
        }

        // Pass 2 fallback: if nothing spawned, relax to general walkable zones with 1x1 structures.
        // Prioritize cells near macro features (Bridge, Platform, Terrain, Hill) to form coherent clusters.
        if (placedCount < minStructuresToSpawn)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                int x = cells[i].x;
                int z = cells[i].y;
                if (placedCount >= maxStructures) break;
                if (occupied[x, z]) continue;

                // Skip critical macro cells during fallback too
                if (IsCriticalMacroCell(generator, x, z)) continue;

                MacroRegion region = GetRegion(generator, x, z);
                if (IsReservedMicroRegion(region)) continue;
                if (!IsRegionSpawnable(region)) continue;
                if (!IsCenterAllowed(x, z, width, length)) continue;

                fallbackAttempts++;

                int surfaceY = FindPlacementSurfaceY(generator, x, z, region);
                if (surfaceY < 0) continue;

                // Skip wall tiles
                if (IsWallTile(generator, x, z, surfaceY)) continue;

                WFCTile3D floor = generator.GetTileAt(x, surfaceY, z);
                if (!IsFloorLike(floor)) continue;
                
                if (requireAirAbove && !HasClearance(generator, x, z, surfaceY, height)) continue;
                
                // Feature proximity boost: near Bridge/Platform/Terrain/Hill, spawn with higher chance and targeted structures
                float featureProximity = MeasureFeatureProximity(generator, x, z, width, length);
                float chance = Mathf.Clamp01(baseSpawnChancePerCell * (1.0f + featureProximity * 0.15f));
                if (placedCount < minStructuresToSpawn)
                    chance = 1f;
                if (rng.NextDouble() > chance) continue;
                
                // Use feature-aware placement to prefer structures appropriate for this region
                var candidates = GetPreferredStructuresForRegion(validStructures, region, generator, x, z, rng, featureProximity > 0.3f);
                if (candidates.Count == 0) continue;

                var weightedCandidates = ExpandCandidatesForFallback(candidates);
                StructureEntry picked = PickWeighted(weightedCandidates, rng);
                if (picked == null) continue;

                int yawQuarter = picked.randomYaw ? rng.Next(0, 4) : 0;
                int footprintX = ((yawQuarter & 1) == 1) ? picked.footprintZ : picked.footprintX;
                int footprintZ = ((yawQuarter & 1) == 1) ? picked.footprintX : picked.footprintZ;

                if (!CanPlace(generator, picked, x, z, footprintX, footprintZ, surfaceY, width, height, length, occupied, spawn, exit)) continue;

                PlaceStructure(generator, container, picked, x, z, surfaceY, yawQuarter);
                MarkOccupied(occupied, x, z, footprintX, footprintZ);
                placed.Add(new PlacedInfo
                {
                    center = new Vector2Int(x, z),
                    minSpacing = picked.minSpacingCells
                });
                placedCount++;
            }
        }

        if (physicsResolveAfterSpawn)
            StartCoroutine(ResolveAfterSpawn(container));

        Debug.Log($"[MicroStructureSpawner] Spawned {placedCount} structures (reserved attempts: {reservedAttempts}, fallback attempts: {fallbackAttempts}).");
        if (reservedAttempts == 0)
            Debug.LogWarning("[MicroStructureSpawner] No reserved micro cells detected in current blueprint. Fallback placement used.");
    }

    private int BuildConnectivityStructures(WFCGenerator3D generator, Transform container, List<StructureEntry> pool, int width, int length, int height, bool[,] occupied, Vector3Int? spawn, Vector3Int? exit, System.Random rng)
    {
        // Identify clusters of Bridge/Platform/Terrain/Hill regions and draw connecting structures
        int placed = 0;
        var featureCells = new List<Vector2Int>();
        
        // Find all significant feature cells
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                MacroRegion r = GetRegion(generator, x, z);
                if (r == MacroRegion.Bridge || r == MacroRegion.Platform || 
                    r == MacroRegion.Terrain || r == MacroRegion.Hill)
                {
                    featureCells.Add(new Vector2Int(x, z));
                }
            }
        }

        // Draw connecting paths between DISTANT feature clusters only
        for (int i = 0; i < featureCells.Count - 1; i++)
        {
            Vector2Int p1 = featureCells[i];
            Vector2Int p2 = featureCells[i + 1];
            
            float distance = Vector2.Distance(p1, p2);
            if (distance < 6f) continue;  // Only connect reasonably distant features
            if (distance > 25f) continue;  // Don't connect across entire map
            
            // Draw a connecting path using Bresenham line
            var pathCells = GetBresenhamLinePoints(p1.x, p1.y, p2.x, p2.y);
            
            // Place structures every 2nd cell along the path (less sparse)
            for (int j = 2; j < pathCells.Count - 1; j += 2)
            {
                Vector2Int cell = pathCells[j];
                if (occupied[cell.x, cell.y]) continue;
                if (IsCriticalMacroCell(generator, cell.x, cell.y)) continue;
                
                MacroRegion region = GetRegion(generator, cell.x, cell.y);
                if (!IsRegionSpawnable(region)) continue;
                
                int surfaceY = FindPlacementSurfaceY(generator, cell.x, cell.y, region);
                if (surfaceY < 0) continue;
                if (IsWallTile(generator, cell.x, cell.y, surfaceY)) continue;
                
                WFCTile3D floor = generator.GetTileAt(cell.x, surfaceY, cell.y);
                if (!IsFloorLike(floor)) continue;
                if (requireAirAbove && !HasClearance(generator, cell.x, cell.y, surfaceY, height)) continue;
                
                // Higher spawn chance for connectivity structures (70%)
                if (rng.NextDouble() > 0.7f) continue;
                
                // Strongly prefer connecting structures (supports, bridges, walkways)
                var connectors = pool.Where(s => 
                    (s.id.Contains("Support") || s.id.Contains("Bridge") || s.id.Contains("Walkway")) &&
                    s.footprintX == 1 && s.footprintZ == 1 && 
                    IsRegionAllowed(s.allowedRegions, region)).ToList();
                
                if (connectors.Count == 0) continue;
                
                StructureEntry picked = PickWeighted(connectors, rng);
                if (picked == null) continue;
                
                int yaw = rng.Next(0, 4);
                if (!CanPlace(generator, picked, cell.x, cell.y, 1, 1, surfaceY, width, height, length, occupied, spawn, exit)) continue;
                
                PlaceStructure(generator, container, picked, cell.x, cell.y, surfaceY, yaw);
                MarkOccupied(occupied, cell.x, cell.y, 1, 1);
                placed++;
            }
        }
        
        return placed;
    }

    private List<Vector2Int> GetBresenhamLinePoints(int x0, int y0, int x1, int y1)
    {
        var points = new List<Vector2Int>();
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        
        int x = x0, y = y0;
        while (true)
        {
            points.Add(new Vector2Int(x, y));
            if (x == x1 && y == y1) break;
            
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }
        
        return points;
    }

    private List<Vector2Int> BuildShuffledCells(int width, int length, System.Random rng)
    {
        var cells = new List<Vector2Int>(width * length);
        for (int x = 0; x < width; x++)
            for (int z = 0; z < length; z++)
                cells.Add(new Vector2Int(x, z));

        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var tmp = cells[i];
            cells[i] = cells[j];
            cells[j] = tmp;
        }

        return cells;
    }

    private IEnumerator ResolveAfterSpawn(Transform container)
    {
        if (container == null) yield break;

        int frames = Mathf.Max(1, resolveFrames);
        for (int i = 0; i < frames; i++)
            yield return null;

        var children = new List<Transform>();
        for (int i = 0; i < container.childCount; i++)
            children.Add(container.GetChild(i));

        for (int i = 0; i < children.Count; i++)
        {
            var t = children[i];
            if (t == null) continue;

            var meta = t.GetComponent<SpawnMeta>();
            float yOffset = meta != null ? meta.yOffset : 0f;

            Vector3 origin = t.position + Vector3.up * resolveRayStartHeight;
            var hits = Physics.RaycastAll(origin, Vector3.down, resolveRayDistance);
            if (hits == null || hits.Length == 0) continue;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int h = 0; h < hits.Length; h++)
            {
                var hit = hits[h];
                if (hit.collider == null) continue;
                if (hit.collider.transform.IsChildOf(container)) continue;

                Bounds b = CalculateBounds(t.gameObject);
                if (b.size == Vector3.zero) break;

                float delta = (hit.point.y + yOffset) - b.min.y;
                t.position += new Vector3(0f, delta, 0f);
                break;
            }
        }
    }

    private void NormalizeLegacyEntries(List<StructureEntry> entries, WFCGenerator3D generator)
    {
        foreach (var entry in entries)
        {
            if (entry == null || entry.prefab == null) continue;

            // Legacy configs may have oversized footprints from old macro-scale assumptions.
            int estimatedX = EstimateFootprintCells(entry.prefab, generator.tileSizeXZ, true);
            int estimatedZ = EstimateFootprintCells(entry.prefab, generator.tileSizeXZ, false);

            if (estimatedX > 0 && estimatedX < entry.footprintX)
                entry.footprintX = estimatedX;
            if (estimatedZ > 0 && estimatedZ < entry.footprintZ)
                entry.footprintZ = estimatedZ;

            // If no explicit micro reserve flags were set, add them so reserved slots can be used.
            if ((entry.allowedRegions & (RegionMask.MicroDetail | RegionMask.MicroCrate)) == 0)
                entry.allowedRegions |= (RegionMask.MicroDetail | RegionMask.MicroCrate);
        }
    }

    private int EstimateFootprintCells(GameObject prefab, float tileSize, bool xAxis)
    {
        if (prefab == null || tileSize <= 0f) return 1;

        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return 1;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        float size = xAxis ? b.size.x : b.size.z;
        int cells = Mathf.Max(1, Mathf.RoundToInt(size / tileSize));
        return cells;
    }

    private void CleanupOldContainer(Transform parent)
    {
        Transform old = parent.Find(containerName);
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old.gameObject);

        placed.Clear();
    }

    private bool IsCenterAllowed(int x, int z, int width, int length)
    {
        float nx = width > 1 ? x / (float)(width - 1) : 0.5f;
        float nz = length > 1 ? z / (float)(length - 1) : 0.5f;
        float centerDistance = Mathf.Max(Mathf.Abs(nx - 0.5f), Mathf.Abs(nz - 0.5f));
        return centerDistance >= centerClearRadius;
    }

    private int FindPlacementSurfaceY(WFCGenerator3D generator, int x, int z, MacroRegion region)
    {
        int height = generator.GridHeight;

        // If this region already has an established Y level, prefer that for height coherence
        if (regionYLevelCache.TryGetValue(region, out int cachedY))
        {
            if (IsValidSurface(generator, x, cachedY, z, height))
                return cachedY;
            // If cached level doesn't work at this cell, fall through to find another
        }

        int preferred = region switch
        {
            MacroRegion.Platform => 1,
            MacroRegion.Terrain => 1,
            MacroRegion.Hill => 2,
            MacroRegion.Bridge => 1,
            _ => 0
        };

        preferred = Mathf.Clamp(preferred, 0, height - 1);

        // First try the preferred level, then scan downward/upward for the highest valid surface.
        for (int offset = 0; offset < height; offset++)
        {
            int yDown = preferred - offset;
            if (yDown >= 0 && IsValidSurface(generator, x, yDown, z, height))
            {
                // Cache this Y level for the region for future placements
                if (!regionYLevelCache.ContainsKey(region))
                    regionYLevelCache[region] = yDown;
                return yDown;
            }

            int yUp = preferred + offset;
            if (offset != 0 && yUp < height && IsValidSurface(generator, x, yUp, z, height))
            {
                if (!regionYLevelCache.ContainsKey(region))
                    regionYLevelCache[region] = yUp;
                return yUp;
            }
        }

        return -1;
    }

    private float ComputeEdgeBiasedChance(int x, int z, int width, int length)
    {
        float nx = width > 1 ? x / (float)(width - 1) : 0.5f;
        float nz = length > 1 ? z / (float)(length - 1) : 0.5f;
        float distToEdge = Mathf.Min(Mathf.Min(nx, 1f - nx), Mathf.Min(nz, 1f - nz));
        float edge01 = 1f - Mathf.Clamp01(distToEdge / 0.5f);
        float multiplier = Mathf.Lerp(1f, edgeBiasMultiplier, edge01);
        return Mathf.Clamp01(baseSpawnChancePerCell * multiplier);
    }

    private MacroRegion GetRegion(WFCGenerator3D generator, int x, int z)
    {
        var bp = generator.CurrentBlueprint;
        if (bp == null) return MacroRegion.Open;
        if (x < 0 || z < 0 || x >= bp.GetLength(0) || z >= bp.GetLength(1)) return MacroRegion.Open;
        return bp[x, z];
    }

    private bool IsRegionSpawnable(MacroRegion region)
    {
        return  region != MacroRegion.Wall &&
                region != MacroRegion.Spawn &&
                region != MacroRegion.ExitPit &&
               (region == MacroRegion.Open ||
                region == MacroRegion.CombatRoom ||
                region == MacroRegion.BossRoom ||
                region == MacroRegion.Shop ||
                region == MacroRegion.Platform ||
                region == MacroRegion.Terrain ||
                region == MacroRegion.Hill ||
                region == MacroRegion.Bridge ||
                region == MacroRegion.MicroDetail ||
                region == MacroRegion.MicroCrate);
    }

    private bool IsReservedMicroRegion(MacroRegion region)
    {
        return region == MacroRegion.MicroDetail || region == MacroRegion.MicroCrate;
    }

    private bool IsCriticalMacroCell(WFCGenerator3D generator, int x, int z)
    {
        MacroRegion region = GetRegion(generator, x, z);
        // Critical cells that should never have structures placed on them
        return region == MacroRegion.Spawn || region == MacroRegion.ExitPit ||       
               region == MacroRegion.CombatRoom || region == MacroRegion.BossRoom ||
               region == MacroRegion.Shop;
    }

    private bool IsRegionAllowed(RegionMask mask, MacroRegion region)
    {
        if (mask == RegionMask.Any) return true;

        RegionMask bit = region switch
        {
            MacroRegion.Open => RegionMask.Open,
            MacroRegion.CombatRoom => RegionMask.CombatRoom,
            MacroRegion.BossRoom => RegionMask.BossRoom,
            MacroRegion.Shop => RegionMask.Shop,
            MacroRegion.Platform => RegionMask.Platform,
            MacroRegion.Terrain => RegionMask.Terrain,
            MacroRegion.Hill => RegionMask.Hill,
            MacroRegion.Bridge => RegionMask.Bridge,
            MacroRegion.MicroDetail => RegionMask.MicroDetail,
            MacroRegion.MicroCrate => RegionMask.MicroCrate,
            _ => RegionMask.None
        };

        return (mask & bit) != 0;
    }

    private bool IsFloorLike(WFCTile3D tile)
    {
        if (tile == null) return false;
        if (tile.macroRole == WFCTile3D.MacroTileRole.Floor || tile.macroRole == WFCTile3D.MacroTileRole.Marker)
            return true;
        string top = (tile.topSocket ?? string.Empty).ToLower();
        return top.Contains("floor") || top.Contains("j_gnd");
    }

    private bool HasClearance(WFCGenerator3D generator, int x, int z, int surfaceY, int height)
    {
        int maxY = Mathf.Min(height - 1, surfaceY + requiredAirHeight);
        for (int y = surfaceY + 1; y <= maxY; y++)
        {
            WFCTile3D t = generator.GetTileAt(x, y, z);
            if (t == null) continue;

            bool looksAir = t == generator.airTile ||
                            t.macroRole == WFCTile3D.MacroTileRole.Decoration ||
                            (t.topSocket ?? string.Empty).ToLower().Contains("air");
            if (!looksAir) return false;
        }
        return true;
    }

    private bool IsValidSurface(WFCGenerator3D generator, int x, int y, int z, int height)
    {
        if (y < 0 || y >= height) return false;

        WFCTile3D t = generator.GetTileAt(x, y, z);
        if (!IsFloorLike(t)) return false;

        return !requireAirAbove || HasClearance(generator, x, z, y, height);
    }

    private StructureEntry PickWeighted(List<StructureEntry> pool, System.Random rng)
    {
        int total = 0;
        for (int i = 0; i < pool.Count; i++) total += Mathf.Max(1, pool[i].weight);
        if (total <= 0) return null;

        int roll = rng.Next(0, total);
        int running = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            running += Mathf.Max(1, pool[i].weight);
            if (roll < running) return pool[i];
        }

        return pool[pool.Count - 1];
    }

    private List<StructureEntry> ExpandCandidatesForFallback(List<StructureEntry> candidates)
    {
        var expanded = new List<StructureEntry>();
        for (int i = 0; i < candidates.Count; i++)
        {
            var entry = candidates[i];
            // Penalize large structures, boost small ones (inverse of area)
            // 1x1 = bias 1x (keep as-is), larger structures = lower bias
            int area = Mathf.Max(1, entry.footprintX * entry.footprintZ);
            float areaBias = 1f / area;  // 1x1->1.0, 2x2->0.25, 4x4->0.0625
            int copies = Mathf.Max(1, Mathf.RoundToInt(entry.weight * areaBias));
            for (int c = 0; c < copies; c++)
                expanded.Add(entry);
        }

        return expanded.Count > 0 ? expanded : candidates;
    }

    private bool CanPlace(
        WFCGenerator3D generator,
        StructureEntry entry,
        int x,
        int z,
        int footprintX,
        int footprintZ,
        int surfaceY,
        int width,
        int height,
        int length,
        bool[,] occupied,
        Vector3Int? spawn,
        Vector3Int? exit)
    {
        int halfX = footprintX / 2;
        int halfZ = footprintZ / 2;

        int xMin = x - halfX;
        int xMax = xMin + footprintX - 1;
        int zMin = z - halfZ;
        int zMax = zMin + footprintZ - 1;

        if (xMin < 0 || zMin < 0 || xMax >= width || zMax >= length) return false;

        for (int ix = xMin; ix <= xMax; ix++)
        {
            for (int iz = zMin; iz <= zMax; iz++)
            {
                // Never place on critical macro cells
                if (IsCriticalMacroCell(generator, ix, iz)) return false;

                if (occupied[ix, iz]) return false;

                if (strictFootprintValidation)
                {
                    MacroRegion region = GetRegion(generator, ix, iz);
                    if (!IsRegionSpawnable(region)) return false;
                    if (!IsRegionAllowed(entry.allowedRegions, region)) return false;

                    int expectedSurface = FindPlacementSurfaceY(generator, ix, iz, region);
                    if (expectedSurface < 0 || expectedSurface != surfaceY) return false;

                    WFCTile3D floor = generator.GetTileAt(ix, surfaceY, iz);
                    if (!IsFloorLike(floor)) return false;
                    
                    // Never place on actual wall tiles
                    if (IsWallTile(generator, ix, iz, surfaceY)) return false;

                    if (requireAirAbove && !HasClearance(generator, ix, iz, surfaceY, height)) return false;
                }
            }
        }

        if (spawn.HasValue)
        {
            int d = Mathf.Abs(spawn.Value.x - x) + Mathf.Abs(spawn.Value.z - z);
            if (d < entry.minDistanceFromSpawn) return false;
        }

        if (exit.HasValue)
        {
            int d = Mathf.Abs(exit.Value.x - x) + Mathf.Abs(exit.Value.z - z);
            if (d < entry.minDistanceFromExit) return false;
        }

        for (int i = 0; i < placed.Count; i++)
        {
            int dx = Mathf.Abs(placed[i].center.x - x);
            int dz = Mathf.Abs(placed[i].center.y - z);
            int minDist = Mathf.Max(entry.minSpacingCells, placed[i].minSpacing);
            if (Mathf.Max(dx, dz) < minDist) return false;
        }

        return true;
    }


    private void MarkOccupied(bool[,] occupied, int x, int z, int footprintX, int footprintZ)
    {
        int halfX = footprintX / 2;
        int halfZ = footprintZ / 2;

        int xMin = x - halfX;
        int xMax = xMin + footprintX - 1;
        int zMin = z - halfZ;
        int zMax = zMin + footprintZ - 1;

        for (int ix = xMin; ix <= xMax; ix++)
            for (int iz = zMin; iz <= zMax; iz++)
                occupied[ix, iz] = true;
    }

    private bool IsWallTile(WFCGenerator3D generator, int x, int z, int surfaceY)
    {
        if (surfaceY < 0 || surfaceY >= generator.GridHeight) return true;
        WFCTile3D tile = generator.GetTileAt(x, surfaceY, z);
        if (tile == null) return false;
        
        // Check if tile looks like a wall (solid, non-floor, non-air)
        bool isWall = tile.macroRole == WFCTile3D.MacroTileRole.Wall;
        bool hasWallSocket = (tile.topSocket ?? string.Empty).ToLower().Contains("wall") ||
                             (tile.topSocket ?? string.Empty).ToLower().Contains("solid");
        
        return isWall || hasWallSocket;
    }

    private List<StructureEntry> GetPreferredStructuresForRegion(List<StructureEntry> pool, MacroRegion region, WFCGenerator3D generator, int x, int z, System.Random rng, bool nearSignificantFeature = false)
    {
        // Return structures that make sense for this region
        var result = pool.Where(s => IsRegionAllowed(s.allowedRegions, region)).ToList();
        
        // Feature-aware: adjust preference based on nearby macro features
        int wallCount = 0;
        int bridgeCount = 0;
        int platformCount = 0;
        int terrainCount = 0;
        
        for (int dx = -3; dx <= 3; dx++)
        {
            for (int dz = -3; dz <= 3; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                int nx = x + dx;
                int nz = z + dz;
                MacroRegion nearRegion = GetRegion(generator, nx, nz);
                if (nearRegion == MacroRegion.Wall) wallCount++;
                if (nearRegion == MacroRegion.Bridge) bridgeCount++;
                if (nearRegion == MacroRegion.Platform) platformCount++;
                if (nearRegion == MacroRegion.Terrain || nearRegion == MacroRegion.Hill) terrainCount++;
            }
        }
        
        // Strategy: context-dependent structure selection
        // Near bridges, prefer supports and walkways
        if (bridgeCount > 1 && (region == MacroRegion.Open || region == MacroRegion.Bridge))
        {
            var bridgeStructures = result.Where(s => s.id.Contains("Support") || s.id.Contains("Walkway") || s.id.Contains("Bridge") || s.id.Contains("Arch")).ToList();
            result = bridgeStructures.Count > 0 ? bridgeStructures : result;
        }
        
        // Near walls/pillars, prefer pillars, supports, and covers
        if (wallCount > 2 && (region == MacroRegion.Open || region == MacroRegion.CombatRoom))
        {
            var wallStructures = result.Where(s => s.id.Contains("Pillar") || s.id.Contains("Support") || s.id.Contains("Cover")).ToList();
            result = wallStructures.Count > 0 ? wallStructures : result;
        }
        
        // Near platforms/terrain/hills, prefer supports and decorative structures
        if ((platformCount > 0 || terrainCount > 0) && nearSignificantFeature)
        {
            var structureStructures = result.Where(s => s.id.Contains("Support") || s.id.Contains("Pillar") || s.id.Contains("Decorative") || s.id.Contains("Console") || s.id.Contains("Pedestal")).ToList();
            result = structureStructures.Count > 0 ? structureStructures : result;
        }
        
        return result;
    }

    private float MeasureFeatureProximity(WFCGenerator3D generator, int x, int z, int width, int length)
    {
        // Check how close this cell is to Bridge/Platform/Terrain/Hill regions
        int featureCount = 0;
        int searchRadius = 3;
        
        for (int dx = -searchRadius; dx <= searchRadius; dx++)
        {
            for (int dz = -searchRadius; dz <= searchRadius; dz++)
            {
                int nx = x + dx;
                int nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= width || nz >= length) continue;
                
                MacroRegion r = GetRegion(generator, nx, nz);
                if (r == MacroRegion.Bridge || r == MacroRegion.Platform || r == MacroRegion.Terrain || r == MacroRegion.Hill)
                    featureCount++;
            }
        }
        
        float maxFeatures = (searchRadius * 2 + 1) * (searchRadius * 2 + 1);
        return Mathf.Clamp01(featureCount / maxFeatures);
    }

    private void PlaceStructure(WFCGenerator3D generator, Transform parent, StructureEntry entry, int x, int z, int surfaceY, int yawQuarter)
    {
        float floorSurfaceY = generator.transform.position.y + (surfaceY * generator.tileSizeY) + (generator.tileSizeY * 0.5f);
        Vector3 position = new Vector3(
            generator.transform.position.x + (x * generator.tileSizeXZ),
            floorSurfaceY,
            generator.transform.position.z + (z * generator.tileSizeXZ)
        );

        float yaw = yawQuarter * 90f;

        GameObject go = UnityEngine.Object.Instantiate(entry.prefab, position, Quaternion.Euler(0f, yaw, 0f), parent);
        go.name = $"{entry.id}_{x}_{z}";
        var meta = go.GetComponent<SpawnMeta>();
        if (meta == null) meta = go.AddComponent<SpawnMeta>();
        meta.yOffset = entry.yOffset;

        if (snapToFloorSurface)
        {
            Bounds bounds = CalculateBounds(go);
            if (bounds.size != Vector3.zero)
            {
                // The floor anchor is the cell's world Y position plus any explicit offset.
                float targetMinY = floorSurfaceY + entry.yOffset;
                float deltaY = targetMinY - bounds.min.y;
                go.transform.position += new Vector3(0f, deltaY, 0f);
            }
        }
        else if (Mathf.Abs(entry.yOffset) > 0.0001f)
        {
            go.transform.position += new Vector3(0f, entry.yOffset, 0f);
        }
    }

    private Bounds CalculateBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        var colliders = go.GetComponentsInChildren<Collider>();
        if (colliders.Length > 0)
        {
            Bounds b = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                b.Encapsulate(colliders[i].bounds);
            return b;
        }

        return new Bounds(go.transform.position, Vector3.zero);
    }
}
