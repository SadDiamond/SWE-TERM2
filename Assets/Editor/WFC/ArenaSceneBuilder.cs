#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CybergrindArenaSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/CybergrindArena.unity";
    private const string MaterialFolder = "Assets/Materials/Cybergrind";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [MenuItem("Tools/Cybergrind/Build Cybergrind Arena Scene")]
    public static void BuildScene()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Materials");
        EnsureFolder(MaterialFolder);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "CybergrindArena";

        Material floor = CreateMaterial("M_Cybergrind_Floor", new Color(0.055f, 0.06f, 0.066f), Color.black);
        Material dark = CreateMaterial("M_Cybergrind_Dark", new Color(0.012f, 0.014f, 0.017f), Color.black);
        Material accent = CreateMaterial("M_Cybergrind_Cyan", new Color(0.025f, 0.23f, 0.28f), new Color(0.0f, 0.22f, 0.30f));
        Material hazard = CreateMaterial("M_Cybergrind_Hazard", new Color(0.30f, 0.035f, 0.018f), new Color(0.38f, 0.04f, 0.0f));
        Material spawn = CreateMaterial("M_Cybergrind_Spawn", new Color(0.02f, 0.23f, 0.10f), new Color(0.0f, 0.25f, 0.08f));
        Material exit = CreateMaterial("M_Cybergrind_Exit", new Color(0.28f, 0.13f, 0.025f), new Color(0.32f, 0.12f, 0.0f));

        GameObject generatorObject = new GameObject("Cybergrind Arena Generator");
        CybergrindArenaGenerator generator = generatorObject.AddComponent<CybergrindArenaGenerator>();
        generator.width = 25;
        generator.length = 25;
        generator.tileSize = 4f;
        generator.floorThickness = 0.16f;
        generator.bridgeLevel = 1;
        generator.platformLevel = 1;
        generator.levelHeight = 5.4f;
        generator.centralPlatformRadius = 4;
        generator.cornerPlatformSize = 4;
        generator.mainBridgeHalfWidth = 1;
        generator.outerGapChance = 0.055f;
        generator.hazardChance = 0.035f;
        generator.coverChance = 0.07f;
        generator.seed = 0;
        generator.randomizeSeedEachGeneration = true;
        generator.floorMaterial = floor;
        generator.darkMaterial = dark;
        generator.accentMaterial = accent;
        generator.hazardMaterial = hazard;
        generator.spawnMaterial = spawn;
        generator.exitMaterial = exit;

        GameObject player = InstantiatePlayer();
        if (player != null)
            generator.playerToPlace = player.transform;

        CybergrindArenaDirector director = generatorObject.AddComponent<CybergrindArenaDirector>();
        director.generator = generator;
        if (player != null) director.player = player.transform;

        AddWorldCameraIfNeeded(player);
        generator.GenerateArena();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CybergrindArenaSceneBuilder] Built scene at {ScenePath}.");
    }

    private static GameObject InstantiatePlayer()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[CybergrindArenaSceneBuilder] Player prefab not found at {PlayerPrefabPath}.");
            return null;
        }

        GameObject player = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (player == null) return null;

        player.name = "Player";
        try
        {
            if (!player.CompareTag("Player"))
                player.tag = "Player";
        }
        catch (UnityException)
        {
            Debug.LogWarning("[CybergrindArenaSceneBuilder] Player tag is not defined; leaving prefab tag unchanged.");
        }

        Gun gun = player.GetComponentInChildren<Gun>(true);
        if (gun != null)
            gun.RebuildModel();

        return player;
    }

    private static void AddWorldCameraIfNeeded(GameObject player)
    {
        if (player != null && player.GetComponentInChildren<Camera>(true) != null) return;

        GameObject cameraObject = new GameObject("Preview Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(48f, 34f, -34f);
        camera.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
        camera.fieldOfView = 62f;
    }

    private static Material CreateMaterial(string name, Color baseColor, Color emission)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(FindUrpShader(false));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.name = name;
        if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, baseColor);
        if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, baseColor);

        if (emission.maxColorComponent > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty(EmissionColorId)) mat.SetColor(EmissionColorId, emission);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            if (mat.HasProperty(EmissionColorId)) mat.SetColor(EmissionColorId, Color.black);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Shader FindUrpShader(bool unlit)
    {
        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader;
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
}
#endif
