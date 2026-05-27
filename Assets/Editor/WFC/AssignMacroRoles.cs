#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Editor utility: scans WFCTile3D assets and assigns MacroTileRole heuristically.
public static class AssignMacroRoles
{
    [MenuItem("Tools/WFC/Assign Macro Roles (Heuristic)")]
    public static void AssignRoles()
    {
        string[] guids = AssetDatabase.FindAssets("t:WFCTile3D", new[] { "Assets/Prefabs/WFC_Tiles" });
        int changed = 0;
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var tile = AssetDatabase.LoadAssetAtPath<WFCTile3D>(path);
            if (tile == null) continue;

            var old = tile.macroRole;
            var heuristic = GuessRole(tile, path);
            if (tile.macroRole != heuristic)
            {
                Undo.RecordObject(tile, "Assign MacroRole");
                tile.macroRole = heuristic;
                EditorUtility.SetDirty(tile);
                changed++;
            }
        }

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[AssignMacroRoles] Assigned macro roles for {changed} WFCTile3D assets.");
        }
        else
        {
            Debug.Log("[AssignMacroRoles] No changes required (roles already set).");
        }
    }

    private static WFCTile3D.MacroTileRole GuessRole(WFCTile3D tile, string path)
    {
        string name = Path.GetFileNameWithoutExtension(path).ToLower();
        string top = (tile.topSocket ?? "").ToLower();
        string bottom = (tile.bottomSocket ?? "").ToLower();
        string north = (tile.northSocket ?? "").ToLower();

        if (name.Contains("spawn") || name.Contains("goal") || name.Contains("pit") || name.Contains("exit"))
            return WFCTile3D.MacroTileRole.Marker;

        if (name.Contains("air") || top.Contains("j_air"))
            return WFCTile3D.MacroTileRole.Decoration;

        if (name.Contains("floor") || name.Contains("flat") || bottom.Contains("j_base") || top.Contains("j_gnd") || top.Contains("j_floor"))
            return WFCTile3D.MacroTileRole.Floor;

        if (name.Contains("wall") || name.Contains("block") || north.Contains("j_wall_face") || top.Contains("j_block"))
            return WFCTile3D.MacroTileRole.Wall;

        if (name.Contains("pillar") || name.Contains("pillar") || name.Contains("cap") || name.Contains("column"))
            return WFCTile3D.MacroTileRole.Structural;

        // Default to Decoration so macro allows them in open spaces only if harmless
        return WFCTile3D.MacroTileRole.Decoration;
    }
}
#endif
