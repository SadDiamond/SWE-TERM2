#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GenerateStructurePrefabKit
{
    private const float Tile = 4f;
    private const string RootFolder = "Assets/Prefabs/WFC_Structures";

    [MenuItem("Tools/WFC/Generate Structure Prefab Kit")]
    public static void GenerateKit()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(RootFolder);

        CreateFloorVarA();
        CreateFloorVarB();
        CreatePillarA();
        CreatePillarB();
        CreateLowCover();
        CreateHighCover();
        CreateConsole();
        CreateMachineCrate();
        CreateSupport();
        CreateArch();
        CreateGantry();
        CreateBridgeStraight();
        CreateBridgeSegmented();
        CreateElevatedWalkway();
        CreatePuzzlePedestal();
        CreateDecorativeWall();
        CreateSupportCross();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[WFC] Generated structure prefab kit at Assets/Prefabs/WFC_Structures");
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string child = Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, child);
    }

    private static void SavePrefab(GameObject root, string name)
    {
        string path = $"{RootFolder}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static GameObject NewRoot(string name)
    {
        var root = new GameObject(name);
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        return root;
    }

    private static GameObject AddCube(Transform parent, string name, Vector3 localPos, Vector3 localScale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        return go;
    }

    private static void CreateFloorVarA()
    {
        var root = NewRoot("S_FloorVar_1x1_A");
        AddCube(root.transform, "Floor", new Vector3(0f, 0.05f, 0f), new Vector3(Tile, 0.1f, Tile));
        SavePrefab(root, root.name);
    }

    private static void CreateFloorVarB()
    {
        var root = NewRoot("S_FloorVar_1x1_B");
        AddCube(root.transform, "Floor", new Vector3(0f, 0.05f, 0f), new Vector3(Tile, 0.1f, Tile));
        AddCube(root.transform, "Trim", new Vector3(0f, 0.12f, 0f), new Vector3(0.9f, 0.03f, 0.9f));
        SavePrefab(root, root.name);
    }

    private static void CreatePillarA()
    {
        var root = NewRoot("S_Pillar_1x1_A");
        AddCube(root.transform, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.1f, 0.9f));
        AddCube(root.transform, "Column", new Vector3(0f, 0.55f, 0f), new Vector3(0.35f, 1.0f, 0.35f));
        SavePrefab(root, root.name);
    }

    private static void CreatePillarB()
    {
        var root = NewRoot("S_Pillar_1x1_B");
        AddCube(root.transform, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.1f, 0.9f));
        AddCube(root.transform, "ColumnLower", new Vector3(0f, 0.48f, 0f), new Vector3(0.45f, 0.9f, 0.45f));
        AddCube(root.transform, "Cap", new Vector3(0f, 1.15f, 0f), new Vector3(0.55f, 0.12f, 0.55f));
        SavePrefab(root, root.name);
    }

    private static void CreateLowCover()
    {
        var root = NewRoot("S_LowCover_1x1_A");
        AddCube(root.transform, "Cover", new Vector3(0f, 0.35f, 0f), new Vector3(0.9f, 0.7f, 0.35f));
        SavePrefab(root, root.name);
    }

    private static void CreateHighCover()
    {
        var root = NewRoot("S_HighCover_1x1_A");
        AddCube(root.transform, "Cover", new Vector3(0f, 0.65f, 0f), new Vector3(0.9f, 1.3f, 0.35f));
        SavePrefab(root, root.name);
    }

    private static void CreateConsole()
    {
        var root = NewRoot("S_Console_1x1_A");
        AddCube(root.transform, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(2.8f, 1.8f, 1.4f));
        AddCube(root.transform, "Screen", new Vector3(0f, 1.9f, 0.6f), new Vector3(1.4f, 0.9f, 0.1f));
        SavePrefab(root, root.name);
    }

    private static void CreateMachineCrate()
    {
        var root = NewRoot("S_MachineOrCrate_1x1_A");
        AddCube(root.transform, "Body", new Vector3(0f, 1.2f, 0f), new Vector3(3.2f, 2.4f, 3.2f));
        AddCube(root.transform, "Top", new Vector3(0f, 2.55f, 0f), new Vector3(3.4f, 0.2f, 3.4f));
        AddCube(root.transform, "Panel", new Vector3(0f, 1.65f, 1.7f), new Vector3(1.8f, 1.0f, 0.12f));
        SavePrefab(root, root.name);
    }

    private static void CreateSupport()
    {
        var root = NewRoot("S_Support_1x1_A");
        AddCube(root.transform, "Foot", new Vector3(0f, 0.05f, 0f), new Vector3(2.0f, 0.1f, 2.0f));
        AddCube(root.transform, "Pole", new Vector3(0f, 1.6f, 0f), new Vector3(0.5f, 3.2f, 0.5f));
        AddCube(root.transform, "Cap", new Vector3(0f, 3.3f, 0f), new Vector3(1.5f, 0.12f, 1.5f));
        SavePrefab(root, root.name);
    }

    private static void CreateArch()
    {
        var root = NewRoot("S_Arch_2x1_A");
        AddCube(root.transform, "LeftLeg", new Vector3(-1.4f, 1.6f, 0f), new Vector3(1.2f, 3.2f, 1.2f));
        AddCube(root.transform, "RightLeg", new Vector3(1.4f, 1.6f, 0f), new Vector3(1.2f, 3.2f, 1.2f));
        AddCube(root.transform, "TopBeam", new Vector3(0f, 3.4f, 0f), new Vector3(4f, 0.6f, 1.2f));
        SavePrefab(root, root.name);
    }

    private static void CreateGantry()
    {
        var root = NewRoot("S_Gantry_2x2_A");
        AddCube(root.transform, "LeftLeg", new Vector3(-1.7f, 2.4f, -1.7f), new Vector3(1.0f, 4.8f, 1.0f));
        AddCube(root.transform, "RightLeg", new Vector3(1.7f, 2.4f, 1.7f), new Vector3(1.0f, 4.8f, 1.0f));
        AddCube(root.transform, "CrossBeam", new Vector3(0f, 4.9f, 0f), new Vector3(4.8f, 0.7f, 4.8f));
        SavePrefab(root, root.name);
    }

    private static void CreateBridgeStraight()
    {
        var root = NewRoot("S_Bridge_2x1_A");
        // Elevated bridge walkway
        AddCube(root.transform, "LeftStanchion", new Vector3(-1.8f, 1.6f, 0f), new Vector3(0.8f, 3.2f, 0.8f));
        AddCube(root.transform, "RightStanchion", new Vector3(1.8f, 1.6f, 0f), new Vector3(0.8f, 3.2f, 0.8f));
        AddCube(root.transform, "Deck", new Vector3(0f, 3.6f, 0f), new Vector3(4.0f, 0.4f, 1.6f));
        AddCube(root.transform, "LeftRail", new Vector3(-1.8f, 4.0f, -0.8f), new Vector3(0.3f, 0.6f, 1.6f));
        AddCube(root.transform, "RightRail", new Vector3(1.8f, 4.0f, -0.8f), new Vector3(0.3f, 0.6f, 1.6f));
        SavePrefab(root, root.name);
    }

    private static void CreateBridgeSegmented()
    {
        var root = NewRoot("S_Bridge_2x2_A");
        // Segmented elevated platform bridge
        AddCube(root.transform, "Stanchion1", new Vector3(-1.8f, 1.6f, -1.8f), new Vector3(0.8f, 3.2f, 0.8f));
        AddCube(root.transform, "Stanchion2", new Vector3(1.8f, 1.6f, -1.8f), new Vector3(0.8f, 3.2f, 0.8f));
        AddCube(root.transform, "Stanchion3", new Vector3(-1.8f, 1.6f, 1.8f), new Vector3(0.8f, 3.2f, 0.8f));
        AddCube(root.transform, "Stanchion4", new Vector3(1.8f, 1.6f, 1.8f), new Vector3(0.8f, 3.2f, 0.8f));
        AddCube(root.transform, "CenterDeck", new Vector3(0f, 3.6f, 0f), new Vector3(4.2f, 0.4f, 4.2f));
        SavePrefab(root, root.name);
    }

    private static void CreateElevatedWalkway()
    {
        var root = NewRoot("S_Walkway_1x1_A");
        // Smaller elevated platform
        AddCube(root.transform, "Leg", new Vector3(0f, 1.2f, 0f), new Vector3(0.6f, 2.4f, 0.6f));
        AddCube(root.transform, "Platform", new Vector3(0f, 3.2f, 0f), new Vector3(3.2f, 0.3f, 3.2f));
        SavePrefab(root, root.name);
    }

    private static void CreatePuzzlePedestal()
    {
        var root = NewRoot("S_PuzzlePedestal_1x1_A");
        // Interactive puzzle piece
        AddCube(root.transform, "Base", new Vector3(0f, 0.1f, 0f), new Vector3(2.0f, 0.2f, 2.0f));
        AddCube(root.transform, "Column", new Vector3(0f, 0.9f, 0f), new Vector3(1.0f, 1.8f, 1.0f));
        AddCube(root.transform, "TopPlate", new Vector3(0f, 2.0f, 0f), new Vector3(1.4f, 0.2f, 1.4f));
        AddCube(root.transform, "Activation", new Vector3(0f, 2.4f, 0f), new Vector3(0.8f, 0.6f, 0.8f));
        SavePrefab(root, root.name);
    }

    private static void CreateDecorativeWall()
    {
        var root = NewRoot("S_DecorativeWall_1x1_A");
        // Partial wall for visual/tactical cover
        AddCube(root.transform, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(3.6f, 0.1f, 3.6f));
        AddCube(root.transform, "Panel", new Vector3(0f, 1.2f, 0f), new Vector3(3.2f, 2.4f, 0.4f));
        SavePrefab(root, root.name);
    }

    private static void CreateSupportCross()
    {
        var root = NewRoot("S_SupportCross_1x1_A");
        // X-shaped support structure
        AddCube(root.transform, "Base", new Vector3(0f, 0.08f, 0f), new Vector3(2.0f, 0.16f, 2.0f));
        AddCube(root.transform, "LegDiag1", new Vector3(-0.8f, 1.0f, -0.8f), new Vector3(0.6f, 2.0f, 0.6f));
        AddCube(root.transform, "LegDiag2", new Vector3(0.8f, 1.0f, 0.8f), new Vector3(0.6f, 2.0f, 0.6f));
        AddCube(root.transform, "Cap", new Vector3(0f, 2.2f, 0f), new Vector3(1.2f, 0.2f, 1.2f));
        SavePrefab(root, root.name);
    }

    private static void RemoveCreateBulkWall()
    {
        // Removed - bulk walls are macro-level and should not be placed as micro structures
    }
}
#endif
