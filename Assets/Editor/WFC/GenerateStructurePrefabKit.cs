#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GenerateStructurePrefabKit
{
    private const float Tile = 4f;
    private const string RootFolder = "Assets/Prefabs/WFC_Structures";
    private const string FloorMatPath = RootFolder + "/Mat_CyberFloor.mat";
    private const string DarkMatPath = RootFolder + "/Mat_CyberDark.mat";
    private const string AccentMatPath = RootFolder + "/Mat_CyberAccent.mat";
    private const string HazardMatPath = RootFolder + "/Mat_CyberHazard.mat";

    private static Material floorMat;
    private static Material darkMat;
    private static Material accentMat;
    private static Material hazardMat;

    [MenuItem("Tools/WFC/Generate Structure Prefab Kit")]
    public static void GenerateKit()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(RootFolder);
        EnsureMaterials();

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
        return AddCube(parent, name, localPos, localScale, Quaternion.identity, floorMat);
    }

    private static GameObject AddCube(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material material)
    {
        return AddCube(parent, name, localPos, localScale, Quaternion.identity, material);
    }

    private static GameObject AddCube(Transform parent, string name, Vector3 localPos, Vector3 localScale, Quaternion localRotation, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRotation;
        go.transform.localScale = localScale;
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
        return go;
    }

    private static void EnsureMaterials()
    {
        floorMat = LoadOrCreateMaterial(FloorMatPath, new Color(0.12f, 0.14f, 0.16f), Color.black);
        darkMat = LoadOrCreateMaterial(DarkMatPath, new Color(0.025f, 0.028f, 0.032f), Color.black);
        accentMat = LoadOrCreateMaterial(AccentMatPath, new Color(0.02f, 0.34f, 0.42f), new Color(0f, 0.38f, 0.52f));
        hazardMat = LoadOrCreateMaterial(HazardMatPath, new Color(0.42f, 0.04f, 0.015f), new Color(0.62f, 0.06f, 0f));
    }

    private static Material LoadOrCreateMaterial(string path, Color color, Color emission)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(FindProjectShader(false));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.color = color;
        if (emission.maxColorComponent > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Shader FindProjectShader(bool unlit)
    {
        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader;
    }

    private static void CreateFloorVarA()
    {
        var root = NewRoot("S_FloorVar_1x1_A");
        AddCube(root.transform, "Floor", new Vector3(0f, 0.05f, 0f), new Vector3(Tile, 0.1f, Tile), floorMat);
        AddCube(root.transform, "NorthGlow", new Vector3(0f, 0.13f, 1.65f), new Vector3(3.2f, 0.03f, 0.08f), accentMat);
        AddCube(root.transform, "SouthGlow", new Vector3(0f, 0.13f, -1.65f), new Vector3(3.2f, 0.03f, 0.08f), accentMat);
        SavePrefab(root, root.name);
    }

    private static void CreateFloorVarB()
    {
        var root = NewRoot("S_FloorVar_1x1_B");
        AddCube(root.transform, "Floor", new Vector3(0f, 0.05f, 0f), new Vector3(Tile, 0.1f, Tile), floorMat);
        AddCube(root.transform, "InsetPanel", new Vector3(0f, 0.12f, 0f), new Vector3(2.7f, 0.04f, 2.7f), darkMat);
        AddCube(root.transform, "GlowX", new Vector3(0f, 0.18f, 0f), new Vector3(3.3f, 0.03f, 0.08f), accentMat);
        AddCube(root.transform, "GlowZ", new Vector3(0f, 0.18f, 0f), new Vector3(0.08f, 0.03f, 3.3f), accentMat);
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
        AddCube(root.transform, "LegNW", new Vector3(-1.7f, 2.4f, -1.7f), new Vector3(0.45f, 4.8f, 0.45f), darkMat);
        AddCube(root.transform, "LegNE", new Vector3(1.7f, 2.4f, -1.7f), new Vector3(0.45f, 4.8f, 0.45f), darkMat);
        AddCube(root.transform, "LegSW", new Vector3(-1.7f, 2.4f, 1.7f), new Vector3(0.45f, 4.8f, 0.45f), darkMat);
        AddCube(root.transform, "LegSE", new Vector3(1.7f, 2.4f, 1.7f), new Vector3(0.45f, 4.8f, 0.45f), darkMat);
        AddCube(root.transform, "Deck", new Vector3(0f, 4.9f, 0f), new Vector3(4.8f, 0.32f, 4.8f), floorMat);
        AddCube(root.transform, "UnderGlowX", new Vector3(0f, 4.65f, 0f), new Vector3(4.4f, 0.06f, 0.1f), accentMat);
        AddCube(root.transform, "UnderGlowZ", new Vector3(0f, 4.65f, 0f), new Vector3(0.1f, 0.06f, 4.4f), accentMat);
        AddCube(root.transform, "BraceA", new Vector3(0f, 2.5f, 0f), new Vector3(0.18f, 4.8f, 0.18f), Quaternion.Euler(0f, 45f, 22f), darkMat);
        AddCube(root.transform, "BraceB", new Vector3(0f, 2.5f, 0f), new Vector3(0.18f, 4.8f, 0.18f), Quaternion.Euler(0f, -45f, -22f), darkMat);
        SavePrefab(root, root.name);
    }

    private static void CreateBridgeStraight()
    {
        var root = NewRoot("S_Bridge_2x1_A");
        AddCube(root.transform, "LeftStanchion", new Vector3(-1.8f, 1.7f, 0f), new Vector3(0.45f, 3.4f, 0.45f), darkMat);
        AddCube(root.transform, "RightStanchion", new Vector3(1.8f, 1.7f, 0f), new Vector3(0.45f, 3.4f, 0.45f), darkMat);
        AddCube(root.transform, "Deck", new Vector3(0f, 3.5f, 0f), new Vector3(4.0f, 0.28f, 1.75f), floorMat);
        AddCube(root.transform, "DeckInset", new Vector3(0f, 3.68f, 0f), new Vector3(3.2f, 0.05f, 1.05f), darkMat);
        AddCube(root.transform, "LeftRail", new Vector3(-1.8f, 4.05f, 0f), new Vector3(0.25f, 0.75f, 1.75f), darkMat);
        AddCube(root.transform, "RightRail", new Vector3(1.8f, 4.05f, 0f), new Vector3(0.25f, 0.75f, 1.75f), darkMat);
        AddCube(root.transform, "CenterGlow", new Vector3(0f, 3.78f, 0f), new Vector3(3.5f, 0.04f, 0.08f), accentMat);
        AddCube(root.transform, "UnderBraceA", new Vector3(0f, 2.4f, 0f), new Vector3(0.16f, 3.7f, 0.16f), Quaternion.Euler(0f, 0f, 28f), darkMat);
        AddCube(root.transform, "UnderBraceB", new Vector3(0f, 2.4f, 0f), new Vector3(0.16f, 3.7f, 0.16f), Quaternion.Euler(0f, 0f, -28f), darkMat);
        SavePrefab(root, root.name);
    }

    private static void CreateBridgeSegmented()
    {
        var root = NewRoot("S_Bridge_2x2_A");
        AddCube(root.transform, "Stanchion1", new Vector3(-1.8f, 1.6f, -1.8f), new Vector3(0.45f, 3.2f, 0.45f), darkMat);
        AddCube(root.transform, "Stanchion2", new Vector3(1.8f, 1.6f, -1.8f), new Vector3(0.45f, 3.2f, 0.45f), darkMat);
        AddCube(root.transform, "Stanchion3", new Vector3(-1.8f, 1.6f, 1.8f), new Vector3(0.45f, 3.2f, 0.45f), darkMat);
        AddCube(root.transform, "Stanchion4", new Vector3(1.8f, 1.6f, 1.8f), new Vector3(0.45f, 3.2f, 0.45f), darkMat);
        AddCube(root.transform, "CenterDeck", new Vector3(0f, 3.45f, 0f), new Vector3(4.2f, 0.28f, 4.2f), floorMat);
        AddCube(root.transform, "NorthRail", new Vector3(0f, 3.95f, 2f), new Vector3(4.2f, 0.5f, 0.18f), darkMat);
        AddCube(root.transform, "SouthRail", new Vector3(0f, 3.95f, -2f), new Vector3(4.2f, 0.5f, 0.18f), darkMat);
        AddCube(root.transform, "EastRail", new Vector3(2f, 3.95f, 0f), new Vector3(0.18f, 0.5f, 4.2f), darkMat);
        AddCube(root.transform, "WestRail", new Vector3(-2f, 3.95f, 0f), new Vector3(0.18f, 0.5f, 4.2f), darkMat);
        AddCube(root.transform, "CrossGlowX", new Vector3(0f, 3.63f, 0f), new Vector3(3.6f, 0.04f, 0.08f), accentMat);
        AddCube(root.transform, "CrossGlowZ", new Vector3(0f, 3.63f, 0f), new Vector3(0.08f, 0.04f, 3.6f), accentMat);
        SavePrefab(root, root.name);
    }

    private static void CreateElevatedWalkway()
    {
        var root = NewRoot("S_Walkway_1x1_A");
        AddCube(root.transform, "Leg", new Vector3(0f, 1.2f, 0f), new Vector3(0.4f, 2.4f, 0.4f), darkMat);
        AddCube(root.transform, "Platform", new Vector3(0f, 3.1f, 0f), new Vector3(3.2f, 0.25f, 3.2f), floorMat);
        AddCube(root.transform, "Inset", new Vector3(0f, 3.26f, 0f), new Vector3(2.2f, 0.04f, 2.2f), darkMat);
        AddCube(root.transform, "GlowX", new Vector3(0f, 3.35f, 0f), new Vector3(2.8f, 0.04f, 0.08f), accentMat);
        AddCube(root.transform, "GlowZ", new Vector3(0f, 3.35f, 0f), new Vector3(0.08f, 0.04f, 2.8f), accentMat);
        AddCube(root.transform, "BraceA", new Vector3(0f, 1.8f, 0f), new Vector3(0.12f, 2.7f, 0.12f), Quaternion.Euler(0f, 45f, 25f), darkMat);
        AddCube(root.transform, "BraceB", new Vector3(0f, 1.8f, 0f), new Vector3(0.12f, 2.7f, 0.12f), Quaternion.Euler(0f, -45f, -25f), darkMat);
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
