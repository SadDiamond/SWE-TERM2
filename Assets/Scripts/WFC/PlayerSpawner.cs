using UnityEngine;

/// <summary>
/// Helper for spawning the player at the generated spawn point.
/// Attach to your player GameObject or game controller.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    public WFCGenerator3D wfcGenerator;
    public Transform playerTransform;  // Drag your player here, or we'll find it
    
    [Header("Spawn Settings")]
    public float heightAboveSpawn = 2f;  // Y offset above the spawn tile
    
    void Start()
    {
        if (playerTransform == null)
            playerTransform = GetComponent<Transform>();
    }
    
    /// <summary>
    /// Call this after a room is generated to place player at spawn tile.
    /// </summary>
    public void SpawnPlayerAtSpawnTile()
    {
        if (wfcGenerator == null)
        {
            Debug.LogError("[PlayerSpawner] WFCGenerator3D not assigned!");
            return;
        }
        
        Vector3Int? spawnCell = wfcGenerator.FindSpawnCell();
        if (!spawnCell.HasValue)
        {
            Debug.LogError("[PlayerSpawner] No spawn cell found in generated room!");
            return;
        }
        
        // Calculate world position
        float tileSizeXZ = wfcGenerator.tileSizeXZ;
        float tileSizeY = wfcGenerator.tileSizeY;
        
        Vector3 spawnWorldPos = new Vector3(
            spawnCell.Value.x * tileSizeXZ,
            spawnCell.Value.y * tileSizeY + heightAboveSpawn,
            spawnCell.Value.z * tileSizeXZ
        ) + wfcGenerator.transform.position;
        
        // Teleport player
        playerTransform.position = spawnWorldPos;
        
        Debug.Log($"[PlayerSpawner] Spawned player at {spawnWorldPos}");
    }
    
    /// <summary>
    /// Check if player has reached the exit pit.
    /// </summary>
    public bool HasPlayerReachedExitPit(float proximityRadius = 2f)
    {
        if (wfcGenerator == null) return false;
        
        Vector3Int? exitCell = wfcGenerator.FindExitPitCell();
        if (!exitCell.HasValue) return false;
        
        float tileSizeXZ = wfcGenerator.tileSizeXZ;
        float tileSizeY = wfcGenerator.tileSizeY;
        
        Vector3 exitWorldPos = new Vector3(
            exitCell.Value.x * tileSizeXZ,
            exitCell.Value.y * tileSizeY,
            exitCell.Value.z * tileSizeXZ
        ) + wfcGenerator.transform.position;
        
        float distToExit = Vector3.Distance(playerTransform.position, exitWorldPos);
        return distToExit < proximityRadius;
    }
    
    /// <summary>
    /// Get the exit pit world position (for visualization, waypoints, etc)
    /// </summary>
    public Vector3? GetExitPitPosition()
    {
        if (wfcGenerator == null) return null;
        
        Vector3Int? exitCell = wfcGenerator.FindExitPitCell();
        if (!exitCell.HasValue) return null;
        
        float tileSizeXZ = wfcGenerator.tileSizeXZ;
        float tileSizeY = wfcGenerator.tileSizeY;
        
        return new Vector3(
            exitCell.Value.x * tileSizeXZ,
            exitCell.Value.y * tileSizeY,
            exitCell.Value.z * tileSizeXZ
        ) + wfcGenerator.transform.position;
    }
}
