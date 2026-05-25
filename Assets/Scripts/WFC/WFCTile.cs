using UnityEngine;

[CreateAssetMenu(fileName = "New WFC Tile", menuName = "WFC/Tile")]
public class WFCTile : ScriptableObject
{
    [Header("Tile Prefab")]
    public GameObject prefab;

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
