#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;

public static class GenerationFixTools
{
    [MenuItem("Tools/WFC/Fix: Disable Debug Macro Regions")]
    public static void DisableDebugMacroRegions()
    {
        var objects = UnityEngine.Object.FindObjectsByType<WFCGenerator3D>();
        if (objects.Length == 0)
        {
            Debug.LogWarning("[WFC] No WFCGenerator3D found in scene.");
            return;
        }

        foreach (var obj in objects)
        {
            Undo.RecordObject(obj, "Disable Debug Macro Regions");
            obj.debugMacroRegions = false;
            EditorUtility.SetDirty(obj);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[WFC] Disabled debugMacroRegions on all WFCGenerator3D components.");
    }

    [MenuItem("Tools/WFC/Fix: Standardize Prefab Heights")]
    public static void StandardizePrefabHeights()
    {
        string prefabPath = "Assets/Prefabs/WFC_Structures";
        if (!Directory.Exists(prefabPath))
        {
            Debug.LogError($"[WFC] Prefab directory not found: {prefabPath}");
            return;
        }

        string[] prefabFiles = Directory.GetFiles(prefabPath, "*.prefab");
        float targetHeight = 4f;  // One tile height
        int fixed_count = 0;

        foreach (string file in prefabFiles)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file);
            if (prefab == null) continue;

            // Create a temporary instance to measure
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null) continue;

            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Object.DestroyImmediate(instance);
                continue;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float currentHeight = bounds.size.y;
            if (Mathf.Abs(currentHeight - targetHeight) > 0.1f)
            {
                float scale = targetHeight / Mathf.Max(0.1f, currentHeight);
                instance.transform.localScale = Vector3.one * scale;
                
                // Update the prefab
                PrefabUtility.SaveAsPrefabAsset(instance, file);
                fixed_count++;
                Debug.Log($"[WFC] Rescaled {Path.GetFileName(file)}: {currentHeight:F2} → {targetHeight:F2}");
            }

            Object.DestroyImmediate(instance);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[WFC] Standardized {fixed_count} prefabs to height {targetHeight}.");
    }

    [MenuItem("Tools/WFC/Fix: Enable Path-Based Coherent Placement")]
    public static void EnableCoherentPlacement()
    {
        var spawners = UnityEngine.Object.FindObjectsByType<MicroStructureSpawner>();
        if (spawners.Length == 0)
        {
            Debug.LogWarning("[WFC] No MicroStructureSpawner found in scene.");
            return;
        }

        foreach (var spawner in spawners)
        {
            Undo.RecordObject(spawner, "Enable Coherent Placement");
            // Store a flag or config that the spawner should use path-based placement
            spawner.preferReservedMicroRegions = true;
            EditorUtility.SetDirty(spawner);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[WFC] Coherent path-based placement is already integrated. Regenerate to apply.");
    }
}
#endif
