using UnityEngine;

[CreateAssetMenu(fileName = "New WFC Tile", menuName = "WFC/Tile")]
public class WFCTile : ScriptableObject
{
    [Header("Tile Prefab")]
    public GameObject prefab;

    [Header("Spawn Rotation")]
    [Tooltip("Apply this rotation to the prefab when spawned (e.g. 0, 90, 0 for horizontal hallway)")]
    public Vector3 spawnRotation = Vector3.zero;

    [Header("Generation Probability")]
    [Tooltip("Higher values mean this tile will be picked more often")]
    public int weight = 1;

    [Header("Connection Sockets")]
    [Tooltip("Index 0: North (Z+)\nIndex 1: East (X+)\nIndex 2: South (Z-)\nIndex 3: West (X-)")]
    public string[] sockets = new string[4];

    // Helper method to get the correct edge for matching
    // 0 = North, 1 = East, 2 = South, 3 = West
    public string GetSocket(int edgeIndex)
    {
        return sockets[edgeIndex];
    }
}
