#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SetupMicroStructureSpawner
{
    [MenuItem("Tools/WFC/Setup Micro Structure Spawner (Selected Generator)")]
    public static void SetupOnSelectedGenerator()
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("[WFC] Select a GameObject with WFCGenerator3D first.");
            return;
        }

        var generator = selected.GetComponent<WFCGenerator3D>();
        if (generator == null)
        {
            Debug.LogWarning("[WFC] Selected object has no WFCGenerator3D component.");
            return;
        }

        var spawner = selected.GetComponent<MicroStructureSpawner>();
        if (spawner == null)
        {
            Undo.RecordObject(selected, "Add MicroStructureSpawner");
            spawner = Undo.AddComponent<MicroStructureSpawner>(selected);
        }

        ConfigureSpawner(spawner);

        if (generator.postProcessors == null)
            generator.postProcessors = new List<PostProcessor>();

        if (!generator.postProcessors.Contains(spawner))
            generator.postProcessors.Add(spawner);

        EditorUtility.SetDirty(spawner);
        EditorUtility.SetDirty(generator);
        AssetDatabase.SaveAssets();

        Debug.Log("[WFC] MicroStructureSpawner configured and linked in postProcessors.");
    }

    private static void ConfigureSpawner(MicroStructureSpawner spawner)
    {
        spawner.baseSpawnChancePerCell = 0.08f;
        spawner.centerClearRadius = 0.30f;
        spawner.edgeBiasMultiplier = 2.2f;
        spawner.maxStructures = 64;
        spawner.requireAirAbove = true;
        spawner.requiredAirHeight = 1;
        spawner.strictFootprintValidation = true;
        spawner.snapToFloorSurface = true;
        spawner.preferReservedMicroRegions = true;
        spawner.autoNormalizeLegacyEntries = true;
        spawner.minStructuresToSpawn = 10;
        spawner.physicsResolveAfterSpawn = true;
        spawner.resolveFrames = 20;
        spawner.resolveRayStartHeight = 64f;
        spawner.resolveRayDistance = 256f;
        spawner.containerName = "_MicroStructures";

        spawner.structures = new List<MicroStructureSpawner.StructureEntry>();

        // Medium-scale architectural pieces (bridges, arches, walkways, etc.)
        // These are the first micro layer - structural details that define gameplay spaces
        
        Add(spawner,
            "S_Arch_2x1_A",
            "Assets/Prefabs/WFC_Structures/S_Arch_2x1_A.prefab",
            5, 2, 1, 2, 3, 4,
            MicroStructureSpawner.RegionMask.Open | MicroStructureSpawner.RegionMask.CombatRoom | MicroStructureSpawner.RegionMask.BossRoom);

        Add(spawner,
            "S_Bridge_2x1_A",
            "Assets/Prefabs/WFC_Structures/S_Bridge_2x1_A.prefab",
            4, 2, 1, 2, 3, 4,
            MicroStructureSpawner.RegionMask.Open | MicroStructureSpawner.RegionMask.CombatRoom | MicroStructureSpawner.RegionMask.BossRoom | MicroStructureSpawner.RegionMask.Platform);

        Add(spawner,
            "S_Bridge_2x2_A",
            "Assets/Prefabs/WFC_Structures/S_Bridge_2x2_A.prefab",
            4, 2, 2, 3, 4, 5,
            MicroStructureSpawner.RegionMask.CombatRoom | MicroStructureSpawner.RegionMask.BossRoom | MicroStructureSpawner.RegionMask.Platform | MicroStructureSpawner.RegionMask.Terrain);

        Add(spawner,
            "S_Gantry_2x2_A",
            "Assets/Prefabs/WFC_Structures/S_Gantry_2x2_A.prefab",
            4, 2, 2, 3, 4, 5,
            MicroStructureSpawner.RegionMask.CombatRoom | MicroStructureSpawner.RegionMask.BossRoom | MicroStructureSpawner.RegionMask.Platform);

        Add(spawner,
            "S_Walkway_1x1_A",
            "Assets/Prefabs/WFC_Structures/S_Walkway_1x1_A.prefab",
            3, 1, 1, 1, 2, 3,
            MicroStructureSpawner.RegionMask.Open | MicroStructureSpawner.RegionMask.CombatRoom | MicroStructureSpawner.RegionMask.Platform | MicroStructureSpawner.RegionMask.Terrain);

        Add(spawner,
            "S_DecorativeWall_1x1_A",
            "Assets/Prefabs/WFC_Structures/S_DecorativeWall_1x1_A.prefab",
            3, 1, 1, 1, 2, 3,
            MicroStructureSpawner.RegionMask.CombatRoom | MicroStructureSpawner.RegionMask.Open | MicroStructureSpawner.RegionMask.MicroDetail);

        Add(spawner,
            "S_PuzzlePedestal_1x1_A",
            "Assets/Prefabs/WFC_Structures/S_PuzzlePedestal_1x1_A.prefab",
            2, 1, 1, 1, 2, 3,
            MicroStructureSpawner.RegionMask.MicroDetail | MicroStructureSpawner.RegionMask.CombatRoom | MicroStructureSpawner.RegionMask.BossRoom);

        // Smaller detail and micro elements (for reserved micro cells)
        Add(spawner,
            "S_Pillar_1x1_A",
            "Assets/Prefabs/WFC_Structures/S_Pillar_1x1_A.prefab",
            2, 1, 1, 1, 2, 3,
            MicroStructureSpawner.RegionMask.MicroDetail | MicroStructureSpawner.RegionMask.MicroCrate | MicroStructureSpawner.RegionMask.CombatRoom);

        Add(spawner,
            "S_Pillar_1x1_B",
            "Assets/Prefabs/WFC_Structures/S_Pillar_1x1_B.prefab",
            1, 1, 1, 1, 2, 3,
            MicroStructureSpawner.RegionMask.MicroDetail | MicroStructureSpawner.RegionMask.MicroCrate | MicroStructureSpawner.RegionMask.BossRoom);

        Add(spawner,
            "S_LowCover_1x1_A",
            "Assets/Prefabs/WFC_Structures/S_LowCover_1x1_A.prefab",
            2, 1, 1, 1, 2, 3,
            MicroStructureSpawner.RegionMask.Open | MicroStructureSpawner.RegionMask.CombatRoom | MicroStructureSpawner.RegionMask.Shop | MicroStructureSpawner.RegionMask.MicroDetail);

        Add(spawner,
            "S_HighCover_1x1_A",
            "Assets/Prefabs/WFC_Structures/S_HighCover_1x1_A.prefab",
            1, 1, 1, 1, 2, 3,
            MicroStructureSpawner.RegionMask.CombatRoom | MicroStructureSpawner.RegionMask.BossRoom | MicroStructureSpawner.RegionMask.MicroDetail);

        Add(spawner,
            "S_Console_1x1_A",
            "Assets/Prefabs/WFC_Structures/S_Console_1x1_A.prefab",
            2, 1, 1, 1, 2, 3,
            MicroStructureSpawner.RegionMask.MicroDetail | MicroStructureSpawner.RegionMask.MicroCrate | MicroStructureSpawner.RegionMask.Open | MicroStructureSpawner.RegionMask.Shop);

        Add(spawner,
            "S_MachineOrCrate_1x1_A",
            "Assets/Prefabs/WFC_Structures/S_MachineOrCrate_1x1_A.prefab",
            3, 1, 1, 1, 2, 3,
            MicroStructureSpawner.RegionMask.MicroDetail | MicroStructureSpawner.RegionMask.MicroCrate | MicroStructureSpawner.RegionMask.Shop);

        Add(spawner,
            "S_Support_1x1_A",
            "Assets/Prefabs/WFC_Structures/S_Support_1x1_A.prefab",
            1, 1, 1, 1, 2, 3,
            MicroStructureSpawner.RegionMask.Platform | MicroStructureSpawner.RegionMask.Terrain | MicroStructureSpawner.RegionMask.Hill | MicroStructureSpawner.RegionMask.MicroDetail);

        Add(spawner,
            "S_SupportCross_1x1_A",
            "Assets/Prefabs/WFC_Structures/S_SupportCross_1x1_A.prefab",
            1, 1, 1, 1, 2, 3,
            MicroStructureSpawner.RegionMask.Platform | MicroStructureSpawner.RegionMask.Terrain | MicroStructureSpawner.RegionMask.Hill);
    }

    private static void Add(
        MicroStructureSpawner spawner,
        string id,
        string prefabPath,
        int weight,
        int footprintX,
        int footprintZ,
        int minSpacing,
        int minDistSpawn,
        int minDistExit,
        MicroStructureSpawner.RegionMask allowed)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[WFC] Missing prefab for spawner setup: {prefabPath}");
            return;
        }

        var entry = new MicroStructureSpawner.StructureEntry
        {
            id = id,
            prefab = prefab,
            weight = weight,
            footprintX = footprintX,
            footprintZ = footprintZ,
            minSpacingCells = minSpacing,
            minDistanceFromSpawn = minDistSpawn,
            minDistanceFromExit = minDistExit,
            yOffset = 0f,
            randomYaw = true,
            allowedRegions = allowed
        };

        spawner.structures.Add(entry);
    }
}
#endif
