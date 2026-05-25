using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WFCGenerator : MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 10;
    public int length = 10;
    public float tileSize = 4f; // How far apart to spawn the prefabs

    [Header("Tile Set")]
    public WFCTile[] availableTiles;

    // Internal class representing a single spot in our grid
    private class Cell
    {
        public bool isCollapsed = false;
        public List<WFCTile> possibleTiles;
        public WFCTile finalTile;

        public Cell(WFCTile[] initialTiles)
        {
            possibleTiles = new List<WFCTile>(initialTiles);
        }
    }

    private Cell[,] grid;
    private List<GameObject> spawnedObjects = new List<GameObject>();

    void Start()
    {
        GenerateLevel();
    }

    public void GenerateLevel()
    {
        // Cleanup old level
        foreach (var obj in spawnedObjects) Destroy(obj);
        spawnedObjects.Clear();

        // 1. Initialize Grid with maximum entropy (all cells can be ANY tile)
        grid = new Cell[width, length];
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                grid[x, z] = new Cell(availableTiles);
            }
        }

        // 2. Run the Wave Function Collapse loop
        int maxIterations = width * length * 2;
        int safetyNet = 0;
        
        while (!IsGridFullyCollapsed() && safetyNet < maxIterations)
        {
            safetyNet++;
            
            Vector2Int lowestEntropyCell = GetLowestEntropyCell();
            
            // If valid cell isn't found, we hit a contradiction. We must restart.
            if (lowestEntropyCell.x == -1)
            {
                Debug.LogWarning("[WFC] Contradiction reached! Restarting level generation...");
                GenerateLevel();
                return;
            }

            CollapseCell(lowestEntropyCell.x, lowestEntropyCell.y);
            PropagateConstraints();
        }

        // 3. Spawn the actual 3D prefabs!
        SpawnPrefabs();
    }

    private bool IsGridFullyCollapsed()
    {
        for (int x = 0; x < width; x++)
            for (int z = 0; z < length; z++)
                if (!grid[x, z].isCollapsed) return false;
        
        return true;
    }

    // Finds the uncollapsed cell with the fewest possible tiles remaining
    private Vector2Int GetLowestEntropyCell()
    {
        int lowestEntropy = int.MaxValue;
        Vector2Int target = new Vector2Int(-1, -1);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                if (!grid[x, z].isCollapsed)
                {
                    int entropy = grid[x, z].possibleTiles.Count;
                    
                    if (entropy == 0) return new Vector2Int(-1, -1); // Contradiction!
                    
                    if (entropy < lowestEntropy)
                    {
                        lowestEntropy = entropy;
                        target = new Vector2Int(x, z);
                    }
                }
            }
        }
        return target;
    }

    private void CollapseCell(int x, int z)
    {
        Cell cell = grid[x, z];
        
        // Pick exactly 1 random tile from the list of possible valid tiles
        WFCTile chosenTile = cell.possibleTiles[Random.Range(0, cell.possibleTiles.Count)];
        
        cell.possibleTiles.Clear();
        cell.possibleTiles.Add(chosenTile);
        cell.finalTile = chosenTile;
        cell.isCollapsed = true;
    }

    // Tells neighboring cells which tiles they are no longer allowed to be
    private void PropagateConstraints()
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < length; z++)
                {
                    if (grid[x, z].isCollapsed) continue;

                    int originalCount = grid[x, z].possibleTiles.Count;

                    // North (Z+1)
                    if (z < length - 1) ConstrainCell(x, z, x, z + 1, 0, 2);
                    // East (X+1)
                    if (x < width - 1) ConstrainCell(x, z, x + 1, z, 1, 3);
                    // South (Z-1)
                    if (z > 0) ConstrainCell(x, z, x, z - 1, 2, 0);
                    // West (X-1)
                    if (x > 0) ConstrainCell(x, z, x - 1, z, 3, 1);

                    if (grid[x, z].possibleTiles.Count < originalCount)
                    {
                        changed = true;
                    }
                }
            }
        }
    }

    private void ConstrainCell(int currentX, int currentZ, int neighborX, int neighborZ, int edgeToCheck, int oppositeEdgeToCheck)
    {
        Cell current = grid[currentX, currentZ];
        Cell neighbor = grid[neighborX, neighborZ];

        // Gather all valid sockets that the neighbor is exposing to us
        HashSet<string> validNeighborSockets = new HashSet<string>();
        foreach (WFCTile possibleNeighborTile in neighbor.possibleTiles)
        {
            validNeighborSockets.Add(possibleNeighborTile.GetSocket(oppositeEdgeToCheck));
        }

        // Remove any of our possible tiles that don't match the neighbor's exposed sockets
        current.possibleTiles.RemoveAll(tile => !validNeighborSockets.Contains(tile.GetSocket(edgeToCheck)));
    }

    private void SpawnPrefabs()
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                WFCTile tileInfo = grid[x, z].finalTile;
                if (tileInfo != null && tileInfo.prefab != null)
                {
                    Vector3 position = new Vector3(x * tileSize, 0, z * tileSize) + transform.position;
                    GameObject newTile = Instantiate(tileInfo.prefab, position, Quaternion.identity);
                    newTile.transform.SetParent(this.transform);
                    spawnedObjects.Add(newTile);
                }
            }
        }
        Debug.Log("[WFC] Level Generation Complete!");
    }
}
