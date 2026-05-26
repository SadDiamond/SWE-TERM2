using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WFCGenerator3D : MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 5;
    public int height = 3;
    public int length = 5;

    public float tileSizeXZ = 4f;
    public float tileSizeY = 4f;

    [Header("Tile Set")]
    public WFCTile3D[] availableTiles;
    public WFCTile3D baseFloorTile;
    public bool forceBaseFloor = true;

    [Header("Decorations")]
    public GameObject[] floorDecorations;
    [Range(0f, 1f)] public float floorDecorationDensity = 0.2f;

    [Header("Golden Path (Guaranteed Route)")]
    public bool generateGoldenPath = true;
    public WFCTile3D goldenPathTile;

    [Header("Macro/Micro Pipeline")]
    // If assigned, the macro pass paints a region blueprint and pre-constrains WFC cells
    // before collapse. When set, golden-path generation is skipped (the macro provides
    // its own start/goal). Leave null to use the legacy golden-path-only behavior.
    public MacroGenerator macroGenerator;
    public List<PostProcessor> postProcessors = new List<PostProcessor>();
    public int seed = 0;

    [Header("Macro Tile References")]
    // Specific tile assets the macro pass forces into Wall / Spawn / Goal regions.
    public WFCTile3D wallMassTile;   // BlockGround — fills wall columns at y>=1
    public WFCTile3D wallCapTile;    // BlockCap — tops a wall column at the topmost y
    public WFCTile3D spawnMarkerTile;
    public WFCTile3D goalMarkerTile;
    public WFCTile3D airTile;        // Air — used to clear open/corridor columns

    [Header("Animation Settings")]
    public bool animateSpawning = true;
    public float timeBetweenSpawns = 0.02f;

    private class Cell
    {
        public bool isCollapsed = false;
        public List<WFCTile3D> possibleTiles;
        public WFCTile3D finalTile;

        public Cell(WFCTile3D[] initialTiles)
        {
            possibleTiles = new List<WFCTile3D>(initialTiles);
        }
    }

    private Cell[,,] grid;
    private List<GameObject> spawnedObjects = new List<GameObject>();

    void Start()
    {
        StartCoroutine(GenerateLevelRoutine());
    }

    public void GenerateLevel()
    {
        StartCoroutine(GenerateLevelRoutine());
    }

    // Expanded set including auto-generated rotation variants. Built once per generation.
    private WFCTile3D[] workingTiles;

    public IEnumerator GenerateLevelRoutine()
    {
        // Cleanup old level
        foreach (var obj in spawnedObjects) if (obj != null) Destroy(obj);
        spawnedObjects.Clear();

        // Expand any rotatable tiles into 4 runtime variants (R0..R270).
        workingTiles = BuildTileSet(availableTiles);

        // 1. Initialize 3D Grid
        Queue<Vector3Int> propagationQueue = new Queue<Vector3Int>();

        grid = new Cell[width, height, length];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < length; z++)
                {
                    grid[x, y, z] = new Cell(workingTiles);

                    if (y == 0 && forceBaseFloor && baseFloorTile != null)
                    {
                        grid[x, y, z].finalTile = baseFloorTile;
                        grid[x, y, z].possibleTiles.Clear();
                        grid[x, y, z].possibleTiles.Add(baseFloorTile);
                        grid[x, y, z].isCollapsed = true;
                        propagationQueue.Enqueue(new Vector3Int(x, y, z));
                    }
                }
            }
        }

        // --- MACRO PASS ---
        // The macro generator paints a coarse blueprint (rooms, corridors, walls, spawn,
        // goal). We translate those region tags into concrete cell forcings / restrictions
        // before WFC collapse runs, so the layout has top-level intent the local socket
        // rules alone can't produce.
        bool macroDroveLayout = false;
        if (macroGenerator != null)
        {
            if (seed == 0) seed = (int)System.DateTime.Now.Ticks;
            Random.InitState(seed);
            MacroRegion[,] blueprint = macroGenerator.Generate(width, length, seed);
            ApplyMacroBlueprint(blueprint, propagationQueue);
            macroDroveLayout = true;
        }

        // --- GOLDEN PATH GENERATION ---
        // Skipped when a macro generator is driving layout — the macro provides its own
        // start→goal intent and the golden path would overwrite it.
        if (!macroDroveLayout && generateGoldenPath && goldenPathTile != null)
        {
            int currX = 0, currY = 0, currZ = 0;
            int endX = width - 1, endY = 0, endZ = length - 1;

            // FIX: Only force cells that aren't already collapsed (e.g. base floor row).
            // Overwriting a locked baseFloorTile cell silently broke socket propagation.
            ForceCell(currX, currY, currZ, goldenPathTile, propagationQueue);

            while (currX != endX || currY != endY || currZ != endZ)
            {
                List<int> validMoves = new List<int>();
                if (currX < endX) validMoves.Add(0);
                if (currY < endY) validMoves.Add(1);
                if (currZ < endZ) validMoves.Add(2);

                if (validMoves.Count > 0)
                {
                    int move = validMoves[Random.Range(0, validMoves.Count)];
                    if (move == 0) currX++;
                    else if (move == 1) currY++;
                    else if (move == 2) currZ++;

                    ForceCell(currX, currY, currZ, goldenPathTile, propagationQueue);
                }
            }
        }

        // Propagate all initial forced constraints before free-collapsing the rest
        Propagate(propagationQueue);

        // 2. Count only truly uncollapsed cells
        int cellsToCollapse = 0;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                for (int z = 0; z < length; z++)
                    if (!grid[x, y, z].isCollapsed) cellsToCollapse++;

        int failsafe = 100000;

        while (cellsToCollapse > 0 && failsafe > 0)
        {
            failsafe--;

            // Find uncollapsed cell with lowest entropy
            int minEntropy = int.MaxValue;
            int targetX = -1, targetY = -1, targetZ = -1;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int z = 0; z < length; z++)
                    {
                        if (!grid[x, y, z].isCollapsed && grid[x, y, z].possibleTiles.Count < minEntropy)
                        {
                            minEntropy = grid[x, y, z].possibleTiles.Count;
                            targetX = x; targetY = y; targetZ = z;
                        }
                    }
                }
            }

            if (targetX != -1)
            {
                // FIX: CollapseCell can fail (0 tiles) — always decrement the counter so we
                // don't spin forever on a permanently broken cell.
                bool collapsed = CollapseCell(targetX, targetY, targetZ);

                propagationQueue.Clear();
                propagationQueue.Enqueue(new Vector3Int(targetX, targetY, targetZ));
                Propagate(propagationQueue);

                cellsToCollapse--;

                if (cellsToCollapse % 50 == 0) yield return null;
            }
            else
            {
                break;
            }
        }

        // --- POST-PROCESSORS ---
        // Pipeline tail: each processor reads/writes the grid via the public accessors.
        // Use this for invariants WFC's local rules can't enforce (reachability, theming,
        // forced decoration placement).
        foreach (var pp in postProcessors)
        {
            if (pp != null) pp.Process(this);
        }

        // 3. Spawn Objects (animated or instant)
        yield return StartCoroutine(SpawnPrefabsRoutine());

        // 4. Decorations
        SpawnDecorations();
    }

    // Translates macro region tags into concrete cell forcings:
    //  - Spawn / Goal: y=0 locked to the marker tile, y>=1 cleared to air.
    //  - Wall: y=1..height-2 locked to wallMassTile, top layer to wallCapTile.
    //  - Corridor / Open: no hard forcing; possibility sets stay as authored.
    private void ApplyMacroBlueprint(MacroRegion[,] blueprint, Queue<Vector3Int> queue)
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                MacroRegion r = blueprint[x, z];
                switch (r)
                {
                    case MacroRegion.Spawn:
                        if (spawnMarkerTile != null) ForceCell(x, 0, z, spawnMarkerTile, queue);
                        if (airTile != null)
                            for (int y = 1; y < height; y++) ForceCell(x, y, z, airTile, queue);
                        break;
                    case MacroRegion.Goal:
                        if (goalMarkerTile != null) ForceCell(x, 0, z, goalMarkerTile, queue);
                        if (airTile != null)
                            for (int y = 1; y < height; y++) ForceCell(x, y, z, airTile, queue);
                        break;
                    case MacroRegion.Wall:
                        if (wallMassTile != null)
                        {
                            int topWallY = height - 1;
                            for (int y = 1; y < topWallY; y++)
                                ForceCell(x, y, z, wallMassTile, queue);
                            ForceCell(x, topWallY, z,
                                wallCapTile != null ? wallCapTile : wallMassTile, queue);
                        }
                        break;
                    case MacroRegion.Corridor:
                    case MacroRegion.Open:
                        // Leave possibility set intact; WFC weights decide the fill.
                        break;
                }
            }
        }
    }

    // ----- Public accessors used by PostProcessor subclasses -----

    public int GridWidth  => width;
    public int GridHeight => height;
    public int GridLength => length;

    public WFCTile3D GetTileAt(int x, int y, int z)
    {
        if (grid == null) return null;
        if (x < 0 || x >= width || y < 0 || y >= height || z < 0 || z >= length) return null;
        return grid[x, y, z].finalTile;
    }

    public void SetTileAt(int x, int y, int z, WFCTile3D tile)
    {
        if (grid == null) return;
        if (x < 0 || x >= width || y < 0 || y >= height || z < 0 || z >= length) return;
        grid[x, y, z].finalTile = tile;
    }

    // Finds the first cell whose final tile matches spawnMarkerTile. PostProcessors use
    // this as the seed for reachability flood-fills.
    public Vector3Int? FindSpawnCell()
    {
        if (grid == null || spawnMarkerTile == null) return null;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                for (int z = 0; z < length; z++)
                    if (grid[x, y, z].finalTile == spawnMarkerTile)
                        return new Vector3Int(x, y, z);
        return null;
    }

    // A cell is passable if at least one horizontal face declares an opening
    // (j_air_side / j_floor_side / j_door_open). Tiles with only j_wall_face on all sides
    // are treated as solid blockers.
    public bool IsCellPassable(int x, int y, int z)
    {
        var t = GetTileAt(x, y, z);
        if (t == null) return false;
        string[] sides = { t.northSocket, t.eastSocket, t.southSocket, t.westSocket };
        foreach (var s in sides)
        {
            if (string.IsNullOrEmpty(s)) continue;
            string lower = s.ToLower();
            if (lower.Contains("j_air_side") ||
                lower.Contains("j_floor_side") ||
                lower.Contains("j_door_open"))
                return true;
        }
        return false;
    }

    // Expands `rotatable` source tiles into 4 runtime variants (R0/R90/R180/R270),
    // cycling the N/E/S/W sockets each quarter turn so authoring stays one-canonical-tile.
    // Top/bottom sockets are invariant under Y-axis rotation, so they're copied as-is.
    // Weight is split across the 4 variants to keep the canonical tile's total spawn rate.
    private WFCTile3D[] BuildTileSet(WFCTile3D[] sourceTiles)
    {
        List<WFCTile3D> result = new List<WFCTile3D>();

        foreach (var t in sourceTiles)
        {
            if (t == null) continue;

            if (!t.rotatable)
            {
                result.Add(t);
                continue;
            }

            int variantWeight = Mathf.Max(1, t.weight / 4);
            string[] sides = { t.northSocket, t.eastSocket, t.southSocket, t.westSocket };

            for (int r = 0; r < 4; r++)
            {
                WFCTile3D v = ScriptableObject.CreateInstance<WFCTile3D>();
                v.prefab = t.prefab;
                v.weight = variantWeight;
                v.spawnRotation = t.spawnRotation + new Vector3(0f, 90f * r, 0f);
                v.maxJitter = t.maxJitter;
                v.topSocket = t.topSocket;
                v.bottomSocket = t.bottomSocket;
                // Clockwise rotation viewed from above: new N = old W, new E = old N, etc.
                v.northSocket = sides[(4 - r) % 4];
                v.eastSocket  = sides[(5 - r) % 4];
                v.southSocket = sides[(6 - r) % 4];
                v.westSocket  = sides[(7 - r) % 4];
                v.rotatable = false;
                v.name = r == 0 ? t.name : $"{t.name}_R{r * 90}";
                v.hideFlags = HideFlags.HideAndDontSave;
                result.Add(v);
            }
        }

        return result.ToArray();
    }

    // FIX: ForceCell was called throughout the code but was never defined.
    // It safely skips already-collapsed cells so the base floor layer isn't
    // silently overwritten by a golden-path tile with different sockets.
    private void ForceCell(int x, int y, int z, WFCTile3D tile, Queue<Vector3Int> queue)
    {
        Cell cell = grid[x, y, z];

        if (cell.isCollapsed)
        {
            // Cell already locked (e.g. baseFloorTile at y==0); don't overwrite it.
            // Still re-enqueue so its constraints propagate if needed.
            if (!queue.Contains(new Vector3Int(x, y, z)))
                queue.Enqueue(new Vector3Int(x, y, z));
            return;
        }

        cell.finalTile = tile;
        cell.possibleTiles.Clear();
        cell.possibleTiles.Add(tile);
        cell.isCollapsed = true;
        queue.Enqueue(new Vector3Int(x, y, z));
    }

    private void SpawnDecorations()
    {
        if (floorDecorations == null || floorDecorations.Length == 0) return;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 1; y++)
            {
                for (int z = 0; z < length; z++)
                {
                    Cell currentCell = grid[x, y, z];
                    Cell slotAbove = grid[x, y + 1, z];

                    if (currentCell.finalTile != null &&
                        (currentCell.finalTile.topSocket.Contains("floor") || currentCell.finalTile.topSocket.Contains("solid")))
                    {
                        if (slotAbove.finalTile != null && slotAbove.finalTile.bottomSocket.Contains("air"))
                        {
                            if (Random.value <= floorDecorationDensity)
                            {
                                GameObject decalOrProp = floorDecorations[Random.Range(0, floorDecorations.Length)];
                                Vector3 spawnPos = new Vector3(
                                    x * tileSizeXZ,
                                    y * tileSizeY + (tileSizeY * 0.5f),
                                    z * tileSizeXZ
                                );
                                GameObject spawnedDeco = Instantiate(decalOrProp, spawnPos, Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0));
                                spawnedObjects.Add(spawnedDeco);
                            }
                        }
                    }
                }
            }
        }
    }

    // FIX: Returns bool so the caller knows whether collapse succeeded, and logs a clear
    // error on contradiction (0 options) instead of silently producing a null tile.
    private bool CollapseCell(int x, int y, int z)
    {
        Cell cell = grid[x, y, z];

        if (cell.possibleTiles.Count == 0)
        {
            Debug.LogWarning($"[WFC 3D] Contradiction at ({x},{y},{z}) — no valid tiles remain. " +
                             "Consider adding more tile variety or relaxing socket constraints.");
            cell.isCollapsed = true;
            return false;
        }

        // Weighted random selection
        int totalWeight = cell.possibleTiles.Sum(t => t.weight);
        int randomValue = Random.Range(0, totalWeight);
        int runningTally = 0;

        foreach (var tile in cell.possibleTiles)
        {
            runningTally += tile.weight;
            if (randomValue < runningTally)
            {
                cell.finalTile = tile;
                break;
            }
        }

        if (cell.finalTile == null) cell.finalTile = cell.possibleTiles[0];

        cell.possibleTiles.Clear();
        cell.possibleTiles.Add(cell.finalTile);
        cell.isCollapsed = true;
        return true;
    }

    private void Propagate(Queue<Vector3Int> queue)
    {
        while (queue.Count > 0)
        {
            Vector3Int currentPos = queue.Dequeue();
            int cx = currentPos.x;
            int cy = currentPos.y;
            int cz = currentPos.z;

            // Directions: 0=Top, 1=Bottom, 2=North, 3=East, 4=South, 5=West
            if (cy < height - 1) if (ConstrainNeighbor(cx, cy + 1, cz, cx, cy, cz, 1, 0)) queue.Enqueue(new Vector3Int(cx, cy + 1, cz));
            if (cy > 0)          if (ConstrainNeighbor(cx, cy - 1, cz, cx, cy, cz, 0, 1)) queue.Enqueue(new Vector3Int(cx, cy - 1, cz));
            if (cz < length - 1) if (ConstrainNeighbor(cx, cy, cz + 1, cx, cy, cz, 4, 2)) queue.Enqueue(new Vector3Int(cx, cy, cz + 1));
            if (cx < width - 1)  if (ConstrainNeighbor(cx + 1, cy, cz, cx, cy, cz, 5, 3)) queue.Enqueue(new Vector3Int(cx + 1, cy, cz));
            if (cz > 0)          if (ConstrainNeighbor(cx, cy, cz - 1, cx, cy, cz, 2, 4)) queue.Enqueue(new Vector3Int(cx, cy, cz - 1));
            if (cx > 0)          if (ConstrainNeighbor(cx - 1, cy, cz, cx, cy, cz, 3, 5)) queue.Enqueue(new Vector3Int(cx - 1, cy, cz));
        }
    }

    private bool ConstrainNeighbor(int nX, int nY, int nZ, int cX, int cY, int cZ, int neighborFace, int currentFace)
    {
        Cell current = grid[cX, cY, cZ];
        Cell neighbor = grid[nX, nY, nZ];

        if (neighbor.isCollapsed) return false;

        HashSet<string> validCurrentSockets = new HashSet<string>();
        foreach (var pct in current.possibleTiles)
        {
            string[] cSockets = pct.GetSocket(currentFace).ToLower().Split('|');
            foreach (string s in cSockets) validCurrentSockets.Add(s.Trim());
        }

        int originalCount = neighbor.possibleTiles.Count;

        neighbor.possibleTiles.RemoveAll(tile =>
        {
            string[] mySockets = tile.GetSocket(neighborFace).ToLower().Split('|');
            foreach (string mySocket in mySockets)
                if (validCurrentSockets.Contains(mySocket.Trim())) return false;
            return true;
        });

        // FIX: Warn clearly when propagation wipes out all options for a neighbour cell
        // so contradictions are debuggable rather than manifesting as silent null tiles.
        if (neighbor.possibleTiles.Count == 0 && originalCount > 0)
        {
            Debug.LogWarning($"[WFC 3D] Propagation wiped all tiles from neighbor ({nX},{nY},{nZ}). " +
                            "Sockets from ({cX},{cY},{cZ}) face {currentFace} had no match on neighbor face {neighborFace}.");
        }

        return neighbor.possibleTiles.Count < originalCount;
    }

    // FIX: SpawnPrefabs was a plain void but animateSpawning / timeBetweenSpawns were
    // declared and never used. Converted to a coroutine so animated mode actually works.
    private IEnumerator SpawnPrefabsRoutine()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < length; z++)
                {
                    WFCTile3D tileInfo = grid[x, y, z].finalTile;
                    if (tileInfo != null && tileInfo.prefab != null)
                    {
                        Vector3 position = new Vector3(x * tileSizeXZ, y * tileSizeY, z * tileSizeXZ) + transform.position;
                        // Per-tile jitter lets pillars / decorations vary within their cell.
                        Vector3 j = tileInfo.maxJitter;
                        if (j != Vector3.zero)
                        {
                            position += new Vector3(
                                Random.Range(-j.x, j.x),
                                Random.Range(-j.y, j.y),
                                Random.Range(-j.z, j.z));
                        }
                        Quaternion rotation = Quaternion.Euler(tileInfo.spawnRotation);
                        GameObject newTile = Instantiate(tileInfo.prefab, position, rotation);
                        newTile.transform.SetParent(this.transform);
                        spawnedObjects.Add(newTile);

                        if (animateSpawning && timeBetweenSpawns > 0f)
                            yield return new WaitForSeconds(timeBetweenSpawns);
                    }
                }
            }
        }

        Debug.Log("[WFC 3D] Area Generation Complete!");
    }
}