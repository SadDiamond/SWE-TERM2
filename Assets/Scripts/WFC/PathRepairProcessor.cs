using System.Collections.Generic;
using UnityEngine;

// Flood-fills the walkable layer from the spawn cell. Any cell that can't be reached
// gets its walk-layer tile rewritten to `airReplacement`, which carves a hole through
// stray solid masses WFC produced. Coarse but effective for greybox playability.
public class PathRepairProcessor : PostProcessor
{
    [Header("Repair Settings")]
    public int walkLayer = 1;
    public WFCTile3D airReplacement;

    public override void Process(WFCGenerator3D gen)
    {
        Vector3Int? spawn = gen.FindSpawnCell();
        if (!spawn.HasValue)
        {
            Debug.LogWarning("[PathRepair] No spawn cell found — skipping reachability pass.");
            return;
        }

        int W = gen.GridWidth;
        int L = gen.GridLength;

        bool[,] reachable = new bool[W, L];
        var queue = new Queue<Vector2Int>();

        Vector2Int start = new Vector2Int(spawn.Value.x, spawn.Value.z);
        queue.Enqueue(start);
        reachable[start.x, start.y] = true;

        int[] dx = { 1, -1, 0, 0 };
        int[] dz = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int nx = c.x + dx[d];
                int nz = c.y + dz[d];
                if (nx < 0 || nx >= W || nz < 0 || nz >= L) continue;
                if (reachable[nx, nz]) continue;
                if (!gen.IsCellPassable(nx, walkLayer, nz)) continue;
                reachable[nx, nz] = true;
                queue.Enqueue(new Vector2Int(nx, nz));
            }
        }

        if (airReplacement == null)
        {
            int unreachableCount = 0;
            for (int x = 0; x < W; x++)
                for (int z = 0; z < L; z++)
                    if (!reachable[x, z]) unreachableCount++;
            Debug.LogWarning($"[PathRepair] {unreachableCount} cells unreachable but no airReplacement set; nothing repaired.");
            return;
        }

        int repaired = 0;
        for (int x = 0; x < W; x++)
        {
            for (int z = 0; z < L; z++)
            {
                if (reachable[x, z]) continue;
                if (gen.IsCellPassable(x, walkLayer, z)) continue;
                gen.SetTileAt(x, walkLayer, z, airReplacement);
                repaired++;
            }
        }

        Debug.Log($"[PathRepair] Replaced {repaired} unreachable solid cells with air.");
    }
}
