using UnityEngine;

[CreateAssetMenu(fileName = "New 3D WFC Tile", menuName = "WFC/3D Tile")]
public class WFCTile3D : ScriptableObject
{
    public enum MacroTileRole
    {
        Unknown,
        Floor,
        Wall,
        Structural,
        Decoration,
        Marker
    }

    [Header("Macro Role (used by macro pass)")]
    public MacroTileRole macroRole = MacroTileRole.Unknown;

    public GameObject prefab;
    public int weight = 10;
    public Vector3 spawnRotation = Vector3.zero;

    // When true, WFCGenerator3D auto-generates 3 extra runtime variants (90°/180°/270°)
    // with rotated N/E/S/W sockets, so you only need to author the north-facing tile.
    public bool rotatable = false;

    // Random ±offset (in world units) applied to the spawn position. Per-axis: x/z let
    // the tile drift inside its cell footprint, y can sink/raise it. Default zero = tile
    // always lands at the cell center. Use ~1.5 on x/z for a 4-unit cell to allow free
    // placement anywhere inside the cell while keeping the tile inside its own footprint.
    public Vector3 maxJitter = Vector3.zero;

    [Header("Sockets (Must match neighbors)")]
    public string topSocket = "j_air";    // Y+ (Up)
    public string bottomSocket = "j_air"; // Y- (Down)
    public string northSocket = "j_air_side";  // Z+ (Forward)
    public string eastSocket = "j_air_side";   // X+ (Right)
    public string southSocket = "j_air_side";  // Z- (Backward)
    public string westSocket = "j_air_side";   // X- (Left)

    // Helper to get socket by direction index
    public string GetSocket(int direction)
    {
        switch (direction)
        {
            case 0: return topSocket;
            case 1: return bottomSocket;
            case 2: return northSocket;
            case 3: return eastSocket;
            case 4: return southSocket;
            case 5: return westSocket;
            default: return "";
        }
    }
}
