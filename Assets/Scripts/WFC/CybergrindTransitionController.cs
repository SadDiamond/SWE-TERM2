using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class CybergrindTransitionController : MonoBehaviour
{
    [Header("Transition Mode")]
    public bool useScreenSpaceTransition = true;

    [Header("Screen Space Transition")]
    [Min(0.05f)] public float curtainCloseDuration = 0.55f;
    [Min(0f)] public float curtainHoldDuration = 1.35f;
    [Min(0.05f)] public float curtainOpenDuration = 0.55f;
    public Color curtainColor = new Color(0.008f, 0.016f, 0.022f, 1f);

    [Header("Structure Shift Timing")]
    [Min(0.05f)] public float oldArenaDropDuration = 2.15f;
    [Min(0.05f)] public float newArenaRiseDuration = 2.45f;
    [Min(0f)] public float swapHoldDuration = 0.58f;
    public AnimationCurve shiftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Structure Shift Motion")]
    public float oldArenaDropDistance = 18f;
    public float newArenaRiseDistance = 16f;
    [Min(1f)] public float unmatchedTileTravelDistance = 3.8f;
    public float exitLiftDistance = 9.5f;
    public float exitLiftDuration = 0.92f;

    [Header("Exit / Cage FX")]
    public Color exitHighlightColor = new Color(0.9f, 0.6f, 0.1f, 0.5f);
    public float exitHighlightHeight = 0.06f;
    public float exitHighlightScale = 1.12f;
    public Color coverColor = new Color(0.74f, 0.9f, 1f, 0.12f);
    public float coverScaleDuration = 0.42f;
    public float cageWallThickness = 0.12f;
    public float cageWallHeight = 9f;
    [Range(0.3f, 0.8f)] public float cageCellScale = 0.52f;

    [Header("Structure Reconfigure Motion")]
    public float detailStaggerRange = 0.34f;

    [Header("Transition Performance")]
    [Min(128)] public int maxAnimatedTransitionPieces = 420;
    [Range(1, 6)] public int transitionDetailStride = 4;
    public bool animateSurfaceMicroDetails = false;

    [Header("Transient Structure FX")]
    public Color assemblyColor = new Color(0.56f, 0.92f, 1f, 0.18f);
    [Range(8, 24)] public int assemblySlabCount = 16;
    public float assemblyRadius = 8.2f;
    public float assemblyTravel = 8.5f;
    public Vector3 assemblySlabSize = new Vector3(3.8f, 0.18f, 2.4f);
    public float assemblyFxDuration = 1.2f;

    [Header("Overlay FX")]
    public Color flashColor = new Color(0.72f, 0.95f, 1f, 0.28f);
    public float flashDuration = 0.22f;
    public float bannerDuration = 2.8f;

    [Header("Events")]
    public UnityEvent onTransitionStarted;
    public UnityEvent onSwapMoment;
    public UnityEvent onTransitionFinished;

    public bool IsTransitioning { get; private set; }
    public string DebugStage { get; private set; } = "Idle";
    public int DebugLastReconfigureStates { get; private set; }
    public int DebugLastMatchedPieces { get; private set; }
    public int DebugLastRaisedPieces { get; private set; }
    public int DebugLastRetractedPieces { get; private set; }
    public int DebugLastOldPieceGroups { get; private set; }
    public int DebugLastNewPieceGroups { get; private set; }
    public int DebugLastVerticalMatchedPieces { get; private set; }
    public float DebugLastMaxVerticalDelta { get; private set; }

    private Image flashOverlay;
    private Image transitionCurtain;
    private CanvasGroup klotskiLoaderGroup;
    private Transform loadingCubeRoot;
    private Camera loadingCubeCamera;
    private RenderTexture loadingCubeTexture;
    private readonly List<Renderer> loadingCubeRenderers = new List<Renderer>();
    private readonly List<Color> loadingCubeStartColors = new List<Color>();
    private readonly List<Transform> loadingCubePieces = new List<Transform>();
    private readonly List<Vector3Int> loadingCubeCoordinates = new List<Vector3Int>();
    private CanvasGroup bannerGroup;
    private TMP_Text bannerTitleText;
    private TMP_Text bannerSubtitleText;
    private const string TransitionPreviewSnapshotName = "TransitionPreviewSnapshot";

    private void Awake()
    {
        EnsureOverlay();
    }

    public void Play(CybergrindArenaGenerator generator, Action swapAction)
    {
        if (IsTransitioning) return;
        StartCoroutine(ExitTransitionRoutine(generator, FindAnyObjectByType<PlayerController>(), swapAction));
    }

    public IEnumerator StartExitSequence(PlayerController player, CybergrindArenaGenerator generator, Action swapAction)
    {
        if (IsTransitioning) yield break;
        yield return ExitTransitionRoutine(generator, player != null ? player : FindAnyObjectByType<PlayerController>(), swapAction);
    }

    private IEnumerator ExitTransitionRoutine(CybergrindArenaGenerator generator, PlayerController player, Action swapAction)
    {
        if (useScreenSpaceTransition)
        {
            yield return ScreenSpaceTransitionRoutine(generator, player, swapAction);
            yield break;
        }

        IsTransitioning = true;
        DebugStage = "Start";
        onTransitionStarted?.Invoke();
        string themeLabel = generator != null ? generator.GetThemeLabel() : "Arena";
        ShowTransitionBanner("FLOOR SHIFT", $"{themeLabel}. Tiles moving.");
        StartTransitionRoutine(PulseFlash(flashColor, flashDuration));

        Transform oldRoot = generator != null ? generator.CurrentArenaRoot : null;
        Transform exit = FindExitCellTransform(generator);

        if (player != null)
        {
            player.ToggleUIMode(false);
            player.SetTransitionLock(true);
        }

        TransitionRig rig = CreateTransitionRig(exit, generator, player);
        if (rig.anchor != null)
            HighlightExit(rig.anchor);

        if (rig.cage != null)
        {
            DebugStage = "Cage";
            yield return AnimateCover(rig.cage, true, coverScaleDuration);
            StartTransitionRoutine(PulseFlash(new Color(coverColor.r, coverColor.g, coverColor.b, 0.16f), flashDuration * 0.8f));
        }

        if (rig.anchor != null)
            StartTransitionRoutine(EmitAssemblyField(rig.anchor.position, assemblyColor, assemblyFxDuration, false));

        if (rig.anchor != null)
        {
            DebugStage = "Lift";
            yield return LiftAnchor(rig, exitLiftDistance, exitLiftDuration);
        }

        if (generator != null)
            generator.skipPlayerPlacementOnce = true;

        DebugStage = "Swap";
        DestroyTransientContent(oldRoot);
        if (rig.anchor != null)
            StartTransitionRoutine(EmitAssemblyField(rig.anchor.position + Vector3.up * 0.5f, assemblyColor, assemblyFxDuration * 1.15f, true));
        onSwapMoment?.Invoke();
        swapAction?.Invoke();

        Transform newRoot = generator != null ? generator.CurrentArenaRoot : null;
        if (newRoot == oldRoot)
            newRoot = null;

        DebugStage = "Reconfigure";
        string nextThemeLabel = generator != null ? generator.GetThemeLabel() : themeLabel;
        string nextDirectiveTitle = generator != null ? generator.GetThemeDirectiveTitle() : string.Empty;
        ShowTransitionBanner("NEXT FLOOR", $"{nextThemeLabel}. {nextDirectiveTitle}");
        StartTransitionRoutine(PulseFlash(new Color(exitHighlightColor.r, exitHighlightColor.g, exitHighlightColor.b, 0.22f), flashDuration * 1.1f));
        if (newRoot != null)
            StartTransitionRoutine(EmitArenaRiseAccent(newRoot));

        float reconfigureDuration = Mathf.Max(oldArenaDropDuration, newArenaRiseDuration);
        yield return AnimateArenaReconfiguration(oldRoot, newRoot, reconfigureDuration);

        if (swapHoldDuration > 0f)
        {
            DebugStage = "Hold";
            yield return new WaitForSecondsRealtime(swapHoldDuration);
        }

        DebugStage = "PlacePlayer";
        if (generator != null && player != null)
            generator.PlacePlayerAtSpawn();

        if (rig.root != null)
            DestroyTransitionObject(rig.root.gameObject);
        if (oldRoot != null)
            DestroyTransitionObject(oldRoot.gameObject);

        if (player != null)
            player.SetTransitionLock(false);

        IsTransitioning = false;
        DebugStage = "Complete";
        onTransitionFinished?.Invoke();
    }

    private IEnumerator ScreenSpaceTransitionRoutine(CybergrindArenaGenerator generator, PlayerController player, Action swapAction)
    {
        if (IsTransitioning) yield break;

        IsTransitioning = true;
        DebugStage = "CurtainClose";
        onTransitionStarted?.Invoke();

        string themeLabel = generator != null ? generator.GetThemeLabel() : "Arena";
        ShowTransitionBanner("FLOOR SHIFT", $"{themeLabel}. Rebuilding arena.");

        if (player != null)
        {
            player.ToggleUIMode(false);
            player.SetTransitionLock(true);
        }

        Transform oldRoot = generator != null ? generator.CurrentArenaRoot : null;
        ShowKlotskiLoader(true);
        StartTransitionRoutine(AnimateScreenSpaceKlotski());
        yield return FadeTransitionCurtain(0f, 1f, curtainCloseDuration);

        DebugStage = "Swap";
        if (oldRoot != null)
            oldRoot.gameObject.SetActive(false);

        if (generator != null)
            generator.skipPlayerPlacementOnce = true;

        DestroyTransientContent(oldRoot);
        onSwapMoment?.Invoke();
        swapAction?.Invoke();
        yield return null;

        Transform newRoot = generator != null ? generator.CurrentArenaRoot : null;
        if (newRoot != null)
            newRoot.gameObject.SetActive(true);

        DebugStage = "PlacePlayer";
        if (generator != null && player != null)
            generator.PlacePlayerAtSpawn();

        if (oldRoot != null && oldRoot != newRoot)
            DestroyTransitionObject(oldRoot.gameObject);

        string nextThemeLabel = generator != null ? generator.GetThemeLabel() : themeLabel;
        string nextDirectiveTitle = generator != null ? generator.GetThemeDirectiveTitle() : string.Empty;
        ShowTransitionBanner(
            "NEXT FLOOR",
            string.IsNullOrWhiteSpace(nextDirectiveTitle)
                ? nextThemeLabel
                : $"{nextThemeLabel}. {nextDirectiveTitle}");

        if (curtainHoldDuration > 0f)
        {
            DebugStage = "CurtainHold";
            yield return new WaitForSecondsRealtime(curtainHoldDuration);
        }

        DebugStage = "CurtainOpen";
        yield return FadeTransitionCurtain(1f, 0f, curtainOpenDuration);
        ShowKlotskiLoader(false);

        if (player != null)
            player.SetTransitionLock(false);

        IsTransitioning = false;
        DebugStage = "Complete";
        onTransitionFinished?.Invoke();
    }

    private IEnumerator FadeTransitionCurtain(float fromAlpha, float toAlpha, float duration)
    {
        EnsureOverlay();
        if (transitionCurtain == null) yield break;

        ProjectStructureUIRoot.BringToFront(transitionCurtain.transform);
        transitionCurtain.enabled = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetTransitionDelta(duration);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float eased = shiftCurve != null
                ? Mathf.Clamp01(shiftCurve.Evaluate(t))
                : Mathf.SmoothStep(0f, 1f, t);
            Color color = curtainColor;
            color.a *= Mathf.Lerp(fromAlpha, toAlpha, eased);
            transitionCurtain.color = color;
            yield return null;
        }

        Color finalColor = curtainColor;
        finalColor.a *= toAlpha;
        transitionCurtain.color = finalColor;
        transitionCurtain.enabled = toAlpha > 0.001f;
    }

    private void ShowKlotskiLoader(bool visible)
    {
        EnsureOverlay();
        if (visible && klotskiLoaderGroup == null && transitionCurtain != null)
            EnsureScreenSpaceKlotski(transitionCurtain.transform);
        if (klotskiLoaderGroup == null) return;
        klotskiLoaderGroup.gameObject.SetActive(visible);
        klotskiLoaderGroup.alpha = visible ? 1f : 0f;
        if (loadingCubeCamera != null)
            loadingCubeCamera.enabled = visible;
        if (visible)
        {
            for (int i = 0; i < loadingCubeRenderers.Count && i < loadingCubeStartColors.Count; i++)
                if (loadingCubeRenderers[i] != null)
                    loadingCubeRenderers[i].material.color = loadingCubeStartColors[i];
            PrepareLoadingCubeScramble();
        }
    }

    private IEnumerator AnimateScreenSpaceKlotski()
    {
        if (klotskiLoaderGroup == null || loadingCubeRoot == null) yield break;
        loadingCubeRoot.localRotation = Quaternion.Euler(18f, -28f, 8f);
        yield return new WaitForSecondsRealtime(0.2f);

        // A short, readable inverse scramble: every move turns one complete 3x3 slice.
        yield return RotateLoadingCubeSlice(0, 1, -1, 0.28f);
        yield return RotateLoadingCubeSlice(1, -1, 1, 0.28f);
        yield return RotateLoadingCubeSlice(2, 1, -1, 0.28f);
        yield return RotateLoadingCubeSlice(0, -1, 1, 0.28f);
        yield return RotateLoadingCubeSlice(1, 1, -1, 0.28f);

        while (klotskiLoaderGroup != null && klotskiLoaderGroup.gameObject.activeSelf)
        {
            loadingCubeRoot.Rotate(Vector3.up, 9f * Time.unscaledDeltaTime, Space.World);
            yield return null;
        }
    }

    private IEnumerator RotateLoadingCubeSlice(int axis, int layer, int direction, float duration)
    {
        GameObject pivotObject = new GameObject("CubeSlicePivot");
        Transform pivot = pivotObject.transform;
        pivot.SetParent(loadingCubeRoot, false);
        List<int> selected = new List<int>(9);
        for (int i = 0; i < loadingCubePieces.Count; i++)
        {
            Vector3Int coordinate = loadingCubeCoordinates[i];
            int value = axis == 0 ? coordinate.x : axis == 1 ? coordinate.y : coordinate.z;
            if (value != layer || loadingCubePieces[i] == null) continue;
            selected.Add(i);
            loadingCubePieces[i].SetParent(pivot, true);
        }

        Vector3 rotationAxis = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            pivot.localRotation = Quaternion.AngleAxis(90f * direction * eased, rotationAxis);
            yield return null;
        }

        pivot.localRotation = Quaternion.AngleAxis(90f * direction, rotationAxis);
        for (int i = 0; i < selected.Count; i++)
        {
            int index = selected[i];
            Transform piece = loadingCubePieces[index];
            piece.SetParent(loadingCubeRoot, true);
            piece.localPosition = SnapCubeVector(piece.localPosition, 0.72f);
            piece.localRotation = SnapCubeRotation(piece.localRotation);
            loadingCubeCoordinates[index] = Vector3Int.RoundToInt(piece.localPosition / 0.72f);
        }
        Destroy(pivotObject);
        yield return new WaitForSecondsRealtime(0.06f);
    }

    private void PrepareLoadingCubeScramble()
    {
        loadingCubeRoot.localRotation = Quaternion.Euler(18f, -28f, 8f);
        for (int i = 0; i < loadingCubePieces.Count; i++)
        {
            Transform piece = loadingCubePieces[i];
            Vector3Int coordinate = loadingCubeCoordinates[i];
            if (piece == null) continue;
            piece.SetParent(loadingCubeRoot, false);
            piece.localPosition = new Vector3(coordinate.x, coordinate.y, coordinate.z) * 0.72f;
            piece.localRotation = Quaternion.identity;
        }

        ApplyLoadingCubeSliceInstant(1, 1, 1);
        ApplyLoadingCubeSliceInstant(0, -1, -1);
        ApplyLoadingCubeSliceInstant(2, 1, 1);
        ApplyLoadingCubeSliceInstant(1, -1, -1);
        ApplyLoadingCubeSliceInstant(0, 1, 1);
    }

    private void ApplyLoadingCubeSliceInstant(int axis, int layer, int direction)
    {
        Vector3 rotationAxis = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        Quaternion rotation = Quaternion.AngleAxis(90f * direction, rotationAxis);
        for (int i = 0; i < loadingCubePieces.Count; i++)
        {
            Vector3Int coordinate = loadingCubeCoordinates[i];
            int value = axis == 0 ? coordinate.x : axis == 1 ? coordinate.y : coordinate.z;
            if (value != layer || loadingCubePieces[i] == null) continue;
            Transform piece = loadingCubePieces[i];
            piece.localPosition = SnapCubeVector(rotation * piece.localPosition, 0.72f);
            piece.localRotation = SnapCubeRotation(rotation * piece.localRotation);
            loadingCubeCoordinates[i] = Vector3Int.RoundToInt(piece.localPosition / 0.72f);
        }
    }

    private static Vector3 SnapCubeVector(Vector3 value, float step)
    {
        return new Vector3(Mathf.Round(value.x / step), Mathf.Round(value.y / step), Mathf.Round(value.z / step)) * step;
    }

    private static Quaternion SnapCubeRotation(Quaternion value)
    {
        Vector3 euler = value.eulerAngles;
        return Quaternion.Euler(Mathf.Round(euler.x / 90f) * 90f, Mathf.Round(euler.y / 90f) * 90f, Mathf.Round(euler.z / 90f) * 90f);
    }

    public IEnumerator DebugPreviewTransitionLook(CybergrindArenaGenerator generator, float duration)
    {
        yield return DebugPreviewReconfigureTransition(generator, duration);
    }



























    public IEnumerator DebugPreviewReconfigureTransition(CybergrindArenaGenerator generator, float duration)
    {
        if (generator == null || generator.CurrentArenaRoot == null) yield break;

        Transform oldRoot = generator.CurrentArenaRoot;
        GameObject previewRoot = new GameObject("TransitionReconfigurePreviewRoot");
        previewRoot.transform.position = generator.transform.position;
        previewRoot.transform.rotation = generator.transform.rotation;

        CybergrindArenaGenerator previewGenerator = previewRoot.AddComponent<CybergrindArenaGenerator>();
        CopyGeneratorSettingsForTransitionPreview(generator, previewGenerator);
        previewGenerator.randomizeSeedEachGeneration = false;
        previewGenerator.generateOnStart = false;
        previewGenerator.clearBeforeGenerate = true;
        previewGenerator.skipPlayerPlacementOnce = true;
        previewGenerator.playerToPlace = null;
        previewGenerator.enemyPrefab = null;
        previewGenerator.seed = generator.lastGeneratedSeed != 0
            ? unchecked(generator.lastGeneratedSeed + 7919)
            : unchecked(Environment.TickCount ^ 7919);
        previewGenerator.GenerateArena();

        Transform newRoot = previewGenerator.CurrentArenaRoot;
        Collider[] previewColliders = newRoot != null ? newRoot.GetComponentsInChildren<Collider>(true) : null;
        SetCollidersEnabled(previewColliders, false);

        if (!Application.isPlaying)
        {
            DebugStage = "PreviewDiff";
            string plan = DebugDescribeReconfigurePlan(oldRoot, newRoot);
            Debug.Log($"[ArenaTransition] Preview diff plan: {plan}");
            if (newRoot != null)
                DestroyTransitionObject(newRoot.gameObject);
            DestroyTransitionObject(previewRoot);
            DebugStage = IsTransitioning ? DebugStage : "Idle";
            yield break;
        }

        Transform exit = FindExitCellTransform(generator);
        Vector3 center = exit != null ? exit.position : oldRoot.position;
        float previewDuration = Mathf.Max(0.35f, duration);

        DebugStage = "PreviewDiff";
        if (exit != null)
            HighlightExit(exit);

        ShowTransitionBanner("FLOOR SHIFT", "Previewing tile reconfigure.");
        StartTransitionRoutine(PulseFlash(new Color(assemblyColor.r, assemblyColor.g, assemblyColor.b, 0.16f), flashDuration));
        StartTransitionRoutine(EmitAssemblyField(center + Vector3.up * 0.35f, assemblyColor, Mathf.Min(assemblyFxDuration, previewDuration), true));

        yield return AnimateArenaReconfiguration(oldRoot, newRoot, previewDuration, true);

        if (newRoot != null)
            DestroyTransitionObject(newRoot.gameObject);
        DestroyTransitionObject(previewRoot);
        DebugStage = IsTransitioning ? DebugStage : "Idle";
    }

    private void CopyGeneratorSettingsForTransitionPreview(CybergrindArenaGenerator source, CybergrindArenaGenerator target)
    {
        if (source == null || target == null) return;

        target.width = source.width;
        target.length = source.length;
        target.tileSize = source.tileSize;
        target.floorThickness = source.floorThickness;
        target.pillarDepth = source.pillarDepth;
        target.killPlaneY = source.killPlaneY;
        target.generatedRootName = "_ArenaPreviewNext";
        target.arenaMode = source.arenaMode;
        target.themeIndex = source.themeIndex;
        target.useThemePaletteVariants = source.useThemePaletteVariants;
        target.bridgeLevel = source.bridgeLevel;
        target.platformLevel = source.platformLevel;
        target.crownLevel = source.crownLevel;
        target.levelHeight = source.levelHeight;
        target.mainBridgeHalfWidth = source.mainBridgeHalfWidth;
        target.centralPlatformRadius = source.centralPlatformRadius;
        target.cornerPlatformSize = source.cornerPlatformSize;
        target.outerGapChance = source.outerGapChance;
        target.hazardChance = source.hazardChance;
        target.coverChance = source.coverChance;
        target.itemChance = source.itemChance;
        target.safeRadiusAroundSpawn = source.safeRadiusAroundSpawn;
        target.safeRadiusAroundExit = source.safeRadiusAroundExit;
        target.playerSpawnHeight = source.playerSpawnHeight;
        target.decorativeDensity = Mathf.Min(source.decorativeDensity, 0.32f);
        target.microDetailDensity = Mathf.Min(source.microDetailDensity, 0.08f);
        target.combatEnemyMin = 0;
        target.combatEnemyMax = 0;
        target.bossEnemyMin = 0;
        target.bossEnemyMax = 0;
        target.spawnBossChampion = false;
        target.floorMaterial = source.floorMaterial;
        target.darkMaterial = source.darkMaterial;
        target.accentMaterial = source.accentMaterial;
        target.hazardMaterial = source.hazardMaterial;
        target.spawnMaterial = source.spawnMaterial;
        target.exitMaterial = source.exitMaterial;
        target.itemMaterial = source.itemMaterial;
        target.puzzleMaterial = source.puzzleMaterial;
    }

    private void SetCollidersEnabled(Collider[] colliders, bool enabled)
    {
        if (colliders == null) return;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
        }
    }

    public void DebugCancelTransitionForEditor()
    {
        IsTransitioning = false;
        DebugStage = "Idle";
        DestroyTransitionPreviewSnapshots();

        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            if (t.name == "TransitionAssemblySlab" || t.name == "ExitTransitionRig")
                DestroyTransitionObject(t.gameObject);
        }

        if (flashOverlay != null)
        {
            flashOverlay.color = new Color(flashOverlay.color.r, flashOverlay.color.g, flashOverlay.color.b, 0f);
            flashOverlay.enabled = false;
        }

        if (bannerGroup != null)
            bannerGroup.alpha = 0f;
    }

    public GameObject DebugBuildTransitionSnapshot(CybergrindArenaGenerator generator, float progress, bool rising)
    {
        DestroyTransitionPreviewSnapshots();
        if (generator == null || generator.CurrentArenaRoot == null)
            return null;

        Transform root = generator.CurrentArenaRoot;
        Transform exit = FindExitCellTransform(generator);
        Vector3 center = exit != null ? exit.position : root.position;
        Color color = assemblyColor;
        float t = Mathf.Clamp01(progress);
        float eased = shiftCurve != null ? shiftCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);
        float pulse = Mathf.Sin(t * Mathf.PI);

        GameObject snapshot = new GameObject(TransitionPreviewSnapshotName);
        snapshot.transform.position = center;
        Material material = BuildTransparentMaterial(new Color(color.r, color.g, color.b, 0.26f), true);
        BuildSnapshotCage(snapshot.transform, generator);
        int slabCount = Mathf.Max(4, assemblySlabCount);
        Vector3 slabScale = new Vector3(
            Mathf.Max(1.2f, assemblySlabSize.x),
            Mathf.Max(0.08f, assemblySlabSize.y),
            Mathf.Max(1.2f, assemblySlabSize.z));

        for (int i = 0; i < slabCount; i++)
        {
            float angle = (Mathf.PI * 2f * i) / slabCount;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float ring = ((i & 1) == 0) ? assemblyRadius : assemblyRadius * 0.58f;
            float spread = Mathf.Lerp(ring * (rising ? 0.64f : 0.92f), ring * (rising ? 1.02f : 1.22f), eased);
            float height = rising
                ? Mathf.Lerp(-assemblyTravel, 1.7f, eased)
                : Mathf.Lerp(1.4f, -assemblyTravel, eased);

            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "TransitionSnapshotSlab";
            slab.transform.SetParent(snapshot.transform, true);
            slab.transform.position = center + radial * spread + Vector3.up * height;
            slab.transform.rotation = Quaternion.LookRotation(radial, Vector3.up) * Quaternion.Euler(0f, 0f, Mathf.Lerp(rising ? -10f : 0f, rising ? 0f : 10f, eased));
            slab.transform.localScale = new Vector3(
                Mathf.Lerp(slabScale.x * 0.76f, slabScale.x * (rising ? 1.08f : 0.92f), eased) + pulse * 0.12f,
                slabScale.y,
                Mathf.Lerp(slabScale.z * 0.82f, slabScale.z * (rising ? 1.12f : 1.28f), eased));

            Renderer renderer = slab.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            Collider collider = slab.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
        }

        if (exit != null)
            HighlightExit(exit);

        return snapshot;
    }

    private void BuildSnapshotCage(Transform parent, CybergrindArenaGenerator generator)
    {
        if (parent == null) return;

        float tileSize = generator != null ? Mathf.Max(1f, generator.tileSize) : 4f;
        float cellSize = tileSize * cageCellScale;
        float half = cellSize * 0.5f;
        float wallHeight = Mathf.Max(3.5f, cageWallHeight);
        float wallThickness = Mathf.Max(0.06f, cageWallThickness);
        float pillarWidth = wallThickness * 1.8f;
        Material glassMaterial = BuildTransparentMaterial(coverColor, true);

        GameObject cageRoot = new GameObject("TransitionSnapshotCage");
        cageRoot.transform.SetParent(parent, false);
        cageRoot.transform.localPosition = Vector3.zero;
        cageRoot.transform.localRotation = Quaternion.identity;
        cageRoot.transform.localScale = Vector3.one;

        CreatePreviewWall(cageRoot.transform, "SnapshotNorthWall", new Vector3(0f, wallHeight * 0.5f, half), new Vector3(cellSize, wallHeight, wallThickness), glassMaterial);
        CreatePreviewWall(cageRoot.transform, "SnapshotSouthWall", new Vector3(0f, wallHeight * 0.5f, -half), new Vector3(cellSize, wallHeight, wallThickness), glassMaterial);
        CreatePreviewWall(cageRoot.transform, "SnapshotEastWall", new Vector3(half, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, cellSize), glassMaterial);
        CreatePreviewWall(cageRoot.transform, "SnapshotWestWall", new Vector3(-half, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, cellSize), glassMaterial);
        CreatePreviewWall(cageRoot.transform, "SnapshotRoof", new Vector3(0f, wallHeight + wallThickness * 0.5f, 0f), new Vector3(cellSize, wallThickness, cellSize), glassMaterial);
        CreatePreviewWall(cageRoot.transform, "SnapshotCornerNE", new Vector3(half, wallHeight * 0.5f, half), new Vector3(pillarWidth, wallHeight, pillarWidth), glassMaterial);
        CreatePreviewWall(cageRoot.transform, "SnapshotCornerNW", new Vector3(-half, wallHeight * 0.5f, half), new Vector3(pillarWidth, wallHeight, pillarWidth), glassMaterial);
        CreatePreviewWall(cageRoot.transform, "SnapshotCornerSE", new Vector3(half, wallHeight * 0.5f, -half), new Vector3(pillarWidth, wallHeight, pillarWidth), glassMaterial);
        CreatePreviewWall(cageRoot.transform, "SnapshotCornerSW", new Vector3(-half, wallHeight * 0.5f, -half), new Vector3(pillarWidth, wallHeight, pillarWidth), glassMaterial);
        CreatePreviewWall(cageRoot.transform, "SnapshotMidRingX", new Vector3(0f, wallHeight * 0.58f, 0f), new Vector3(cellSize * 0.94f, wallThickness * 0.8f, wallThickness), glassMaterial);
        CreatePreviewWall(cageRoot.transform, "SnapshotMidRingZ", new Vector3(0f, wallHeight * 0.58f, 0f), new Vector3(wallThickness, wallThickness * 0.8f, cellSize * 0.94f), glassMaterial);
    }

    private GameObject CreatePreviewWall(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = localPosition;
        wall.transform.localRotation = Quaternion.identity;
        wall.transform.localScale = localScale;
        if (wall.TryGetComponent(out Renderer renderer))
            renderer.sharedMaterial = material;

        Collider collider = wall.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }

        return wall;
    }

    public int DestroyTransitionPreviewSnapshots()
    {
        int destroyed = 0;
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.name != TransitionPreviewSnapshotName) continue;
            DestroyTransitionObject(t.gameObject);
            destroyed++;
        }

        return destroyed;
    }

    public Transform FindExitTransform(Transform root)
    {
        if (root == null) return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null) continue;
            string lowerName = candidate.name.ToLowerInvariant();
            if (lowerName.StartsWith("exit") || lowerName.Contains("_exit") || lowerName.Contains("exit_"))
                return candidate;
        }

        return null;
    }

    public Transform FindExitCellTransform(CybergrindArenaGenerator generator)
    {
        if (generator == null || generator.CurrentArenaRoot == null) return null;

        string exactName = $"Exit_{generator.width / 2}_{generator.length - 3}";
        Transform exact = generator.CurrentArenaRoot.Find(exactName);
        return exact != null ? exact : FindExitTransform(generator.CurrentArenaRoot);
    }

    public GameObject HighlightExit(Transform exit)
    {
        if (exit == null) return null;

        Transform existing = exit.Find("ExitHighlight");
        if (existing != null) return existing.gameObject;

        GameObject ring = new GameObject("ExitHighlight");
        ring.transform.SetParent(exit, false);
        ring.transform.localPosition = Vector3.zero;
        ring.transform.localRotation = Quaternion.identity;

        Material mat = BuildTransparentMaterial(exitHighlightColor, false);
        CreateHighlightPiece(ring.transform, "ExitHighlightPad", new Vector3(0f, exitHighlightHeight, 0f), new Vector3(exitHighlightScale, 0.04f, exitHighlightScale), mat, new Vector3(0f, 18f, 0f), 0.08f);
        CreateHighlightPiece(ring.transform, "ExitHighlightLineA", new Vector3(0f, exitHighlightHeight + 0.04f, 0f), new Vector3(exitHighlightScale * 1.2f, 0.035f, 0.08f), mat, new Vector3(0f, -36f, 0f), 0.18f);
        CreateHighlightPiece(ring.transform, "ExitHighlightLineB", new Vector3(0f, exitHighlightHeight + 0.045f, 0f), new Vector3(0.08f, 0.035f, exitHighlightScale * 1.2f), mat, new Vector3(0f, 36f, 0f), 0.18f);
        CreateHighlightPiece(ring.transform, "ExitHighlightNeedle", new Vector3(0f, 1.65f, 0f), new Vector3(0.055f, 3.25f, 0.055f), mat, new Vector3(0f, 70f, 0f), 0.24f);

        return ring;
    }

    private void CreateHighlightPiece(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material, Vector3 rotationSpeed, float pulse)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = name;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = scale;
        Renderer renderer = piece.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (Application.isPlaying) renderer.material = material;
            else renderer.sharedMaterial = material;
        }
        Collider col = piece.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        ArenaPulseFx fx = piece.AddComponent<ArenaPulseFx>();
        fx.SetBaseScale(scale);
        fx.scalePulse = pulse;
        fx.pulseSpeed = 2.8f;
        fx.rotationDegreesPerSecond = rotationSpeed;
        fx.emissionColor = exitHighlightColor;
        fx.emissionStrength = 0.75f;
    }

    public GameObject HighlightExitInGenerator(CybergrindArenaGenerator generator)
    {
        if (generator == null) return null;
        Transform exit = FindExitCellTransform(generator);
        return exit != null ? HighlightExit(exit) : null;
    }

    private TransitionRig CreateTransitionRig(Transform exitTransform, CybergrindArenaGenerator generator, PlayerController player)
    {
        if (exitTransform == null) return default;

        float tileSize = generator != null ? Mathf.Max(1f, generator.tileSize) : 4f;
        float cellSize = tileSize * cageCellScale;
        float half = cellSize * 0.5f;
        float wallHeight = Mathf.Max(3.5f, cageWallHeight);
        float wallThickness = Mathf.Max(0.06f, cageWallThickness);

        GameObject rootObject = new GameObject("ExitTransitionRig");
        rootObject.transform.position = exitTransform.position;

        if (player != null)
            CenterPlayerInRig(player, rootObject.transform, wallHeight);

        GameObject cageRoot = new GameObject("ExitGlassCage");
        cageRoot.transform.SetParent(rootObject.transform, false);

        Material glassMaterial = BuildTransparentMaterial(coverColor, true);
        CreateWall(cageRoot.transform, "NorthWall", new Vector3(0f, wallHeight * 0.5f, half), new Vector3(cellSize, wallHeight, wallThickness), glassMaterial);
        CreateWall(cageRoot.transform, "SouthWall", new Vector3(0f, wallHeight * 0.5f, -half), new Vector3(cellSize, wallHeight, wallThickness), glassMaterial);
        CreateWall(cageRoot.transform, "EastWall", new Vector3(half, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, cellSize), glassMaterial);
        CreateWall(cageRoot.transform, "WestWall", new Vector3(-half, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, cellSize), glassMaterial);
        CreateWall(cageRoot.transform, "Roof", new Vector3(0f, wallHeight + wallThickness * 0.5f, 0f), new Vector3(cellSize, wallThickness, cellSize), glassMaterial);

        float pillarWidth = wallThickness * 1.8f;
        CreateWall(cageRoot.transform, "CornerNE", new Vector3(half, wallHeight * 0.5f, half), new Vector3(pillarWidth, wallHeight, pillarWidth), glassMaterial);
        CreateWall(cageRoot.transform, "CornerNW", new Vector3(-half, wallHeight * 0.5f, half), new Vector3(pillarWidth, wallHeight, pillarWidth), glassMaterial);
        CreateWall(cageRoot.transform, "CornerSE", new Vector3(half, wallHeight * 0.5f, -half), new Vector3(pillarWidth, wallHeight, pillarWidth), glassMaterial);
        CreateWall(cageRoot.transform, "CornerSW", new Vector3(-half, wallHeight * 0.5f, -half), new Vector3(pillarWidth, wallHeight, pillarWidth), glassMaterial);
        CreateWall(cageRoot.transform, "FloorPlate", new Vector3(0f, -wallThickness * 0.5f, 0f), new Vector3(cellSize * 0.94f, wallThickness, cellSize * 0.94f), glassMaterial);
        CreateWall(cageRoot.transform, "MidRingX", new Vector3(0f, wallHeight * 0.58f, 0f), new Vector3(cellSize * 0.94f, wallThickness * 0.8f, wallThickness), glassMaterial);
        CreateWall(cageRoot.transform, "MidRingZ", new Vector3(0f, wallHeight * 0.58f, 0f), new Vector3(wallThickness, wallThickness * 0.8f, cellSize * 0.94f), glassMaterial);
        CreateWall(cageRoot.transform, "InnerRibA", new Vector3(half * 0.56f, wallHeight * 0.5f, 0f), new Vector3(wallThickness * 0.75f, wallHeight * 0.94f, wallThickness * 0.75f), glassMaterial);
        CreateWall(cageRoot.transform, "InnerRibB", new Vector3(-half * 0.56f, wallHeight * 0.5f, 0f), new Vector3(wallThickness * 0.75f, wallHeight * 0.94f, wallThickness * 0.75f), glassMaterial);

        cageRoot.transform.localScale = Vector3.zero;

        return new TransitionRig
        {
            root = rootObject.transform,
            anchor = rootObject.transform,
            cage = cageRoot,
            player = player
        };
    }

    private void CenterPlayerInRig(PlayerController player, Transform anchor, float wallHeight)
    {
        if (player == null || anchor == null) return;

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        Vector3 target = anchor.position + Vector3.up * Mathf.Clamp(wallHeight * 0.18f, 1.15f, 1.45f);
        player.transform.position = target;

        if (controller != null)
            controller.enabled = true;
    }

    private Material BuildTransparentMaterial(Color color, bool emissive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.92f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.04f);
        mat.renderQueue = 3000;

        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", new Color(color.r * 0.35f, color.g * 0.52f, color.b * 0.58f, 1f));
        }

        return mat;
    }

    private void CreateWall(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = localPosition;
        wall.transform.localRotation = Quaternion.identity;
        wall.transform.localScale = localScale;
        if (wall.TryGetComponent(out Renderer renderer))
            renderer.material = material;
    }

    private IEnumerator AnimateCover(GameObject cover, bool open, float duration)
    {
        if (cover == null) yield break;

        float elapsed = 0f;
        Vector3 from = open ? Vector3.zero : Vector3.one;
        Vector3 to = open ? Vector3.one : Vector3.zero;
        while (elapsed < duration)
        {
            elapsed += GetTransitionDelta(duration);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float eased = Mathf.SmoothStep(0f, 1f, t);
            cover.transform.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }

        cover.transform.localScale = to;
    }

    private IEnumerator LiftAnchor(TransitionRig rig, float distance, float duration)
    {
        if (rig.anchor == null) yield break;

        Vector3 start = rig.anchor.position;
        Vector3 end = start + Vector3.up * distance;
        Vector3 previous = start;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += GetTransitionDelta(duration);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float eased = Mathf.SmoothStep(0f, 1f, t);
            eased = Mathf.Sin(eased * Mathf.PI * 0.5f);
            Vector3 current = Vector3.LerpUnclamped(start, end, eased);
            rig.anchor.position = current;

            if (rig.player != null)
            {
                Vector3 delta = current - previous;
                CharacterController controller = rig.player.GetComponent<CharacterController>();
                if (controller != null)
                    controller.Move(delta);
                else
                    rig.player.transform.position += delta;
            }

            previous = current;
            yield return null;
        }

        rig.anchor.position = end;

        float bounceDuration = 0.14f;
        float bounceElapsed = 0f;
        Vector3 bounceEnd = end + Vector3.up * 0.34f;
        while (bounceElapsed < bounceDuration)
        {
            bounceElapsed += GetTransitionDelta(bounceDuration);
            float t = Mathf.Clamp01(bounceElapsed / bounceDuration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            Vector3 current = Vector3.LerpUnclamped(end, bounceEnd, eased);
            rig.anchor.position = current;
            yield return null;
        }

        rig.anchor.position = bounceEnd;
    }

    private void DestroyTransientContent(Transform root)
    {
        if (root == null) return;

        Terminal[] terminals = root.GetComponentsInChildren<Terminal>(true);
        for (int i = 0; i < terminals.Length; i++)
        {
            if (terminals[i] != null)
                DestroyTransitionObject(terminals[i].gameObject);
        }

        CybergrindPickup[] pickups = root.GetComponentsInChildren<CybergrindPickup>(true);
        for (int i = 0; i < pickups.Length; i++)
        {
            if (pickups[i] != null)
                DestroyTransitionObject(pickups[i].gameObject);
        }

        BasicEnemyAI[] enemies = root.GetComponentsInChildren<BasicEnemyAI>(true);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null)
                DestroyTransitionObject(enemies[i].gameObject);
        }
    }

    private struct TransitionRig
    {
        public Transform root;
        public Transform anchor;
        public GameObject cage;
        public PlayerController player;
    }

    private struct ReconfigurePieceState
    {
        public Transform transform;
        public Transform parent;
        public int siblingIndex;
        public Vector3 finalLocalPosition;
        public Quaternion finalLocalRotation;
        public Vector3 finalLocalScale;
        public Vector3 startPosition;
        public Vector3 endPosition;
        public Quaternion startRotation;
        public Quaternion endRotation;
        public Vector3 startLocalScale;
        public Vector3 endLocalScale;
        public Renderer[] renderers;
        public bool revealAtEnd;
        public bool destroyAtEnd;
        public bool restoreParentAtEnd;
        public bool preserveFootprintDuringMotion;
        public float stagger;
        public float settleAmplitude;
    }

    private IEnumerator AnimateArenaReconfiguration(Transform oldRoot, Transform newRoot, float duration, bool preserveOldRoot = false)
    {
        if (oldRoot == null && newRoot == null) yield break;

        Dictionary<string, List<Transform>> oldPieces = CollectAnimatedPiecesByKey(oldRoot);
        Dictionary<string, List<Transform>> newPieces = CollectAnimatedPiecesByKey(newRoot);
        List<ReconfigurePieceState> states = new List<ReconfigurePieceState>(Mathf.Max(oldPieces.Count, newPieces.Count));
        HashSet<string> keys = new HashSet<string>();
        int matchedPieces = 0;
        int raisedPieces = 0;
        int retractedPieces = 0;
        int verticalMatchedPieces = 0;
        float maxVerticalDelta = 0f;

        foreach (string key in oldPieces.Keys)
            keys.Add(key);
        foreach (string key in newPieces.Keys)
            keys.Add(key);

        int motionIndex = 0;
        foreach (string key in keys)
        {
            oldPieces.TryGetValue(key, out List<Transform> oldList);
            newPieces.TryGetValue(key, out List<Transform> newList);

            int oldCount = oldList != null ? oldList.Count : 0;
            int newCount = newList != null ? newList.Count : 0;
            int paired = Mathf.Min(oldCount, newCount);

            for (int i = 0; i < paired; i++)
            {
                Transform oldPiece = oldList[i];
                Transform newPiece = newList[i];
                if (oldPiece == null || newPiece == null) continue;
                matchedPieces++;
                float verticalDelta = Mathf.Abs(newPiece.position.y - oldPiece.position.y);
                if (verticalDelta > 0.08f)
                    verticalMatchedPieces++;
                maxVerticalDelta = Mathf.Max(maxVerticalDelta, verticalDelta);

                Renderer[] newRenderers = newPiece.GetComponentsInChildren<Renderer>(true);
                SetRenderersEnabled(newRenderers, false);

                states.Add(CreateReconfigureState(
                    oldPiece,
                    oldPiece.position,
                    newPiece.position,
                    oldPiece.rotation,
                    newPiece.rotation,
                    newPiece.localScale,
                    null,
                    false,
                    !preserveOldRoot,
                    preserveOldRoot,
                    IsGridLockedPiece(oldPiece),
                    motionIndex++));
            }

            for (int i = paired; i < oldCount; i++)
            {
                Transform oldPiece = oldList[i];
                if (oldPiece == null) continue;
                retractedPieces++;
                float drop = GetUnmatchedReconfigureTravel(oldPiece, true);
                states.Add(CreateReconfigureState(
                    oldPiece,
                    oldPiece.position,
                    oldPiece.position + Vector3.down * drop,
                    oldPiece.rotation,
                    oldPiece.rotation,
                    oldPiece.localScale,
                    null,
                    false,
                    !preserveOldRoot,
                    preserveOldRoot,
                    false,
                    motionIndex++));
            }

            for (int i = paired; i < newCount; i++)
            {
                Transform newPiece = newList[i];
                if (newPiece == null) continue;
                raisedPieces++;
                float rise = GetUnmatchedReconfigureTravel(newPiece, false);
                states.Add(CreateReconfigureState(
                    newPiece,
                    newPiece.position + Vector3.down * rise,
                    newPiece.position,
                    newPiece.rotation,
                    newPiece.rotation,
                    newPiece.localScale,
                    newPiece.GetComponentsInChildren<Renderer>(true),
                    false,
                    false,
                    true,
                    false,
                    motionIndex++));
            }
        }

        DebugLastOldPieceGroups = oldPieces.Count;
        DebugLastNewPieceGroups = newPieces.Count;
        DebugLastReconfigureStates = states.Count;
        DebugLastMatchedPieces = matchedPieces;
        DebugLastRaisedPieces = raisedPieces;
        DebugLastRetractedPieces = retractedPieces;
        DebugLastVerticalMatchedPieces = verticalMatchedPieces;
        DebugLastMaxVerticalDelta = maxVerticalDelta;

        if (states.Count == 0)
        {
            if (oldRoot != null && !preserveOldRoot)
                DestroyTransitionObject(oldRoot.gameObject);
            if (newRoot != null)
                SetRenderersEnabled(newRoot.GetComponentsInChildren<Renderer>(true), true);
            yield break;
        }

        Renderer[] allNewRenderers = newRoot != null ? newRoot.GetComponentsInChildren<Renderer>(true) : null;
        SetRenderersEnabled(allNewRenderers, false);

        for (int i = 0; i < states.Count; i++)
        {
            ReconfigurePieceState state = states[i];
            if (state.transform == null) continue;
            state.transform.SetParent((oldRoot != null ? oldRoot.parent : (newRoot != null ? newRoot.parent : null)), true);
            state.transform.position = state.startPosition;
            state.transform.rotation = state.startRotation;
            state.transform.localScale = state.startLocalScale;
            if (state.renderers != null && !state.revealAtEnd)
                SetRenderersEnabled(state.renderers, true);
            states[i] = state;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetTransitionDelta(duration);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            for (int i = 0; i < states.Count; i++)
            {
                ReconfigurePieceState state = states[i];
                if (state.transform == null) continue;

                float localT = Mathf.Clamp01(t - state.stagger);
                float eased = shiftCurve != null ? Mathf.Clamp01(shiftCurve.Evaluate(localT)) : Mathf.SmoothStep(0f, 1f, localT);
                Vector3 position = Vector3.Lerp(state.startPosition, state.endPosition, eased);
                if (!state.destroyAtEnd && eased > 0.78f && state.settleAmplitude > 0f)
                {
                    float settleT = Mathf.InverseLerp(0.78f, 1f, eased);
                    position += Vector3.up * Mathf.Sin(settleT * Mathf.PI * 2f) * (1f - settleT) * state.settleAmplitude;
                }

                state.transform.position = position;
                state.transform.rotation = Quaternion.Slerp(state.startRotation, state.endRotation, eased);
                state.transform.localScale = state.preserveFootprintDuringMotion
                    ? state.startLocalScale
                    : Vector3.Lerp(state.startLocalScale, state.endLocalScale, eased);
            }

            yield return null;
        }

        for (int i = 0; i < states.Count; i++)
        {
            ReconfigurePieceState state = states[i];
            if (state.transform == null) continue;

            if (state.destroyAtEnd)
            {
                DestroyTransitionObject(state.transform.gameObject);
                continue;
            }

            if (state.renderers != null)
                SetRenderersEnabled(state.renderers, true);

            state.transform.position = state.endPosition;
            state.transform.rotation = state.endRotation;
            state.transform.localScale = state.endLocalScale;
            if (state.restoreParentAtEnd && state.parent != null)
            {
                state.transform.SetParent(state.parent, true);
                state.transform.SetSiblingIndex(Mathf.Clamp(state.siblingIndex, 0, state.parent.childCount - 1));
                state.transform.localPosition = state.finalLocalPosition;
                state.transform.localRotation = state.finalLocalRotation;
                state.transform.localScale = state.finalLocalScale;
            }
        }

        SetRenderersEnabled(allNewRenderers, true);
    }

    private ReconfigurePieceState CreateReconfigureState(
        Transform piece,
        Vector3 start,
        Vector3 end,
        Quaternion startRotation,
        Quaternion endRotation,
        Vector3 endLocalScale,
        Renderer[] renderers,
        bool revealAtEnd,
        bool destroyAtEnd,
        bool restoreParentAtEnd,
        bool preserveFootprintDuringMotion,
        int index)
    {
        return new ReconfigurePieceState
        {
            transform = piece,
            parent = piece != null ? piece.parent : null,
            siblingIndex = piece != null ? piece.GetSiblingIndex() : 0,
            finalLocalPosition = piece != null ? piece.localPosition : Vector3.zero,
            finalLocalRotation = piece != null ? piece.localRotation : Quaternion.identity,
            finalLocalScale = piece != null ? piece.localScale : Vector3.one,
            startPosition = start,
            endPosition = end,
            startRotation = startRotation,
            endRotation = endRotation,
            startLocalScale = piece != null ? piece.localScale : Vector3.one,
            endLocalScale = endLocalScale,
            renderers = renderers,
            revealAtEnd = revealAtEnd,
            destroyAtEnd = destroyAtEnd,
            restoreParentAtEnd = restoreParentAtEnd,
            preserveFootprintDuringMotion = preserveFootprintDuringMotion,
            stagger = GetGridReconfigureStagger(piece, index),
            settleAmplitude = GetPieceSettleAmplitude(piece, index)
        };
    }

    private Dictionary<string, List<Transform>> CollectAnimatedPiecesByKey(Transform root)
    {
        Dictionary<string, List<Transform>> result = new Dictionary<string, List<Transform>>();
        if (root == null) return result;

        List<Transform> pieces = CollectAnimatedPieces(root);
        for (int i = 0; i < pieces.Count; i++)
        {
            Transform piece = pieces[i];
            if (piece == null) continue;
            if (!TryGetCellKey(piece.name, out string cellKey)) continue;
            string key = cellKey + ":" + GetTransitionPieceRole(piece.name);
            if (!result.TryGetValue(key, out List<Transform> list))
            {
                list = new List<Transform>();
                result.Add(key, list);
            }
            list.Add(piece);
        }

        return result;
    }

    private string GetTransitionPieceRole(string name)
    {
        if (string.IsNullOrEmpty(name)) return "other";
        if (name.StartsWith("ArenaDistrict_")) return "district-root";
        if (name.StartsWith("Floor_") ||
            name.StartsWith("Bridge_") ||
            name.StartsWith("Platform_") ||
            name.StartsWith("UpperPlatform_") ||
            name.StartsWith("Spawn_") ||
            name.StartsWith("Exit_") ||
            name.StartsWith("Hazard_"))
            return "base";

        if (name.StartsWith("HazardInset")) return "hazard-inset";
        if (name.StartsWith("ExitBeacon")) return "exit-beacon";
        if (name.StartsWith("SurfacePanel_")) return "surface-panel";
        if (name.StartsWith("RailN") || name.StartsWith("RailS") || name.StartsWith("RailE") || name.StartsWith("RailW")) return name.Substring(0, 5);
        if (name.StartsWith("BrokenRail")) return "broken-rail";
        if (name.StartsWith("RouteLip")) return "route-lip";
        if (name.StartsWith("RouteLanding")) return "route-landing";
        if (name.StartsWith("RoutePost")) return "route-post";
        if (name.StartsWith("RouteOverhead")) return "route-overhead";
        if (name.StartsWith("RouteStripeX")) return "route-stripe-x";
        if (name.StartsWith("RouteStripeZ")) return "route-stripe-z";
        if (name.StartsWith("TraversalGapGlow")) return "route-gap";
        if (name.StartsWith("HeightFascia")) return "height-fascia";
        if (name.StartsWith("Step_")) return "step";
        if (name.StartsWith("JumpPad_")) return "jump-pad";
        if (name.StartsWith("ParkourBlock_")) return "parkour";
        if (name.StartsWith("RecoveryDeck")) return "recovery-deck";
        if (name.StartsWith("RecoveryPad")) return "recovery-pad";
        if (name.StartsWith("ArenaPylon_")) return "pylon";
        if (name.StartsWith("PylonCore_")) return "pylon-core";
        if (name.StartsWith("PylonGlow_")) return "pylon-glow";
        if (name.StartsWith("ServiceRib_")) return "service-rib";
        if (name.StartsWith("ServiceGlowChip_")) return "service-chip";
        if (name.StartsWith("Cover_")) return "cover";
        if (name.StartsWith("DistrictPlate_")) return "district-plate";
        if (name.StartsWith("DistrictUndercarriage_")) return "district-under";
        if (name.StartsWith("DistrictSeamGlow_")) return "district-seam";
        if (name.StartsWith("DistrictActuatorGlow_")) return "district-actuator-glow";
        if (name.StartsWith("DistrictActuator_")) return "district-actuator";
        if (name.StartsWith("DistrictCornerLineA_")) return "district-corner-a";
        if (name.StartsWith("DistrictCornerLineB_")) return "district-corner-b";
        if (name.StartsWith("MegaPillarCore_")) return "mega-pillar-core";
        if (name.StartsWith("MegaPillarCrown_")) return "mega-pillar-crown";
        if (name.StartsWith("MegaPillarGlowA_")) return "mega-pillar-glow-a";
        if (name.StartsWith("MegaPillarGlowB_")) return "mega-pillar-glow-b";
        if (name.StartsWith("BossArenaDaisInset_")) return "boss-dais-inset";
        if (name.StartsWith("BossArenaDais_")) return "boss-dais";
        if (name.StartsWith("BossArenaInnerPad_")) return "boss-inner-pad";
        if (name.StartsWith("BossArenaNorthArch_")) return "boss-arch-n";
        if (name.StartsWith("BossArenaSouthArch_")) return "boss-arch-s";
        if (name.StartsWith("BossArenaEastArch_")) return "boss-arch-e";
        if (name.StartsWith("BossArenaWestArch_")) return "boss-arch-w";
        if (name.StartsWith("BossArenaGlowRingA_")) return "boss-glow-a";
        if (name.StartsWith("BossArenaGlowRingB_")) return "boss-glow-b";
        if (name.StartsWith("BossArenaPylonGlow_")) return "boss-pylon-glow";
        if (name.StartsWith("BossArenaPylon_")) return "boss-pylon";
        return "other";
    }

    private bool TryGetCellKey(string name, out string key)
    {
        key = null;
        if (string.IsNullOrEmpty(name)) return false;

        string[] parts = name.Split('_');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (int.TryParse(parts[i], out int x) && int.TryParse(parts[i + 1], out int z))
            {
                key = x + "_" + z;
                return true;
            }
        }

        return false;
    }

    private float GetGridReconfigureStagger(Transform piece, int index)
    {
        if (piece == null) return Mathf.Repeat(index * 0.041f, 1f) * detailStaggerRange;
        Vector3 p = piece.position;
        float wave = Mathf.Repeat(Mathf.Abs(p.x) * 0.014f + Mathf.Abs(p.z) * 0.021f + index * 0.011f, 1f);
        return wave * detailStaggerRange;
    }

    private float GetUnmatchedReconfigureTravel(Transform piece, bool dropping)
    {
        if (piece == null) return Mathf.Max(1f, unmatchedTileTravelDistance);

        float baseTravel = Mathf.Clamp(unmatchedTileTravelDistance, 1f, 4.8f);
        if (IsGridLockedPiece(piece))
            return baseTravel;
        if (IsRoutePiece(piece))
            return baseTravel * 1.18f;
        if (IsTilePiece(piece))
            return baseTravel * 1.08f;

        float fallback = dropping ? oldArenaDropDistance : newArenaRiseDistance;
        return Mathf.Clamp(fallback * 0.35f * GetPieceVerticalMultiplier(piece), baseTravel, baseTravel * 1.75f);
    }

    private void SetRenderersEnabled(Renderer[] renderers, bool enabled)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = enabled;
        }
    }

    public int CountAnimatedTransitionPieces(Transform root)
    {
        return root == null ? 0 : CollectAnimatedPieces(root).Count;
    }

    public string DebugDescribeReconfigurePlan(Transform oldRoot, Transform newRoot)
    {
        Dictionary<string, List<Transform>> oldPieces = CollectAnimatedPiecesByKey(oldRoot);
        Dictionary<string, List<Transform>> newPieces = CollectAnimatedPiecesByKey(newRoot);
        CalculateReconfigureStats(oldPieces, newPieces, out int matched, out int raised, out int retracted, out int states, out int verticalMatched, out float maxVerticalDelta);
        DebugLastOldPieceGroups = oldPieces.Count;
        DebugLastNewPieceGroups = newPieces.Count;
        DebugLastReconfigureStates = states;
        DebugLastMatchedPieces = matched;
        DebugLastRaisedPieces = raised;
        DebugLastRetractedPieces = retracted;
        DebugLastVerticalMatchedPieces = verticalMatched;
        DebugLastMaxVerticalDelta = maxVerticalDelta;

        return $"groups old/new {oldPieces.Count}/{newPieces.Count}, states {states}, matched {matched}, vertical {verticalMatched}, maxY {maxVerticalDelta:0.0}, raised {raised}, retracted {retracted}, budget {maxAnimatedTransitionPieces}";
    }

    private void CalculateReconfigureStats(
        Dictionary<string, List<Transform>> oldPieces,
        Dictionary<string, List<Transform>> newPieces,
        out int matched,
        out int raised,
        out int retracted,
        out int states,
        out int verticalMatched,
        out float maxVerticalDelta)
    {
        HashSet<string> keys = new HashSet<string>();
        foreach (string key in oldPieces.Keys)
            keys.Add(key);
        foreach (string key in newPieces.Keys)
            keys.Add(key);

        matched = 0;
        raised = 0;
        retracted = 0;
        verticalMatched = 0;
        maxVerticalDelta = 0f;
        foreach (string key in keys)
        {
            int oldCount = oldPieces.TryGetValue(key, out List<Transform> oldList) ? oldList.Count : 0;
            int newCount = newPieces.TryGetValue(key, out List<Transform> newList) ? newList.Count : 0;
            int paired = Mathf.Min(oldCount, newCount);
            matched += paired;
            for (int i = 0; i < paired; i++)
            {
                Transform oldPiece = oldList[i];
                Transform newPiece = newList[i];
                if (oldPiece == null || newPiece == null) continue;
                float verticalDelta = Mathf.Abs(newPiece.position.y - oldPiece.position.y);
                if (verticalDelta > 0.08f)
                    verticalMatched++;
                maxVerticalDelta = Mathf.Max(maxVerticalDelta, verticalDelta);
            }
            if (newCount > oldCount)
                raised += newCount - oldCount;
            if (oldCount > newCount)
                retracted += oldCount - newCount;
        }

        states = matched + raised + retracted;
    }

    private List<Transform> CollectAnimatedPieces(Transform root)
    {
        List<Transform> corePieces = new List<Transform>();
        List<Transform> detailPieces = new List<Transform>();
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate == null || candidate == root) continue;
            if (!ShouldAnimatePiece(candidate)) continue;
            if (HasAnimatedAncestor(candidate, root)) continue;
            if (IsCoreTransitionPiece(candidate))
                corePieces.Add(candidate);
            else if (ShouldKeepDetailPiece(candidate, detailPieces.Count))
                detailPieces.Add(candidate);
        }

        int maxPieces = Mathf.Clamp(maxAnimatedTransitionPieces, 128, 420);
        List<Transform> pieces = new List<Transform>(Mathf.Min(maxPieces, corePieces.Count + detailPieces.Count));
        for (int i = 0; i < corePieces.Count && pieces.Count < maxPieces; i++)
            pieces.Add(corePieces[i]);
        for (int i = 0; i < detailPieces.Count && pieces.Count < maxPieces; i++)
            pieces.Add(detailPieces[i]);

        return pieces;
    }

    private bool ShouldAnimatePiece(Transform candidate)
    {
        if (candidate == null) return false;
        if (IsTransitionGroupingRoot(candidate.name)) return false;
        if (candidate.GetComponent<Renderer>() == null && !candidate.name.StartsWith("ArenaDistrict_")) return false;

        return ShouldAnimatePieceName(candidate.name);
    }

    private bool HasAnimatedAncestor(Transform candidate, Transform root)
    {
        Transform current = candidate.parent;
        while (current != null && current != root)
        {
            if (!IsTransitionGroupingRoot(current.name) && ShouldAnimatePieceName(current.name))
                return true;
            current = current.parent;
        }

        return false;
    }

    private bool IsTransitionGroupingRoot(string name)
    {
        return !string.IsNullOrEmpty(name) && name.StartsWith("ArenaDistrict_");
    }

    private bool ShouldAnimatePieceName(string name)
    {
        return name.StartsWith("Floor_") ||
               name.StartsWith("Bridge_") ||
               name.StartsWith("Platform_") ||
               name.StartsWith("UpperPlatform_") ||
               name.StartsWith("Spawn_") ||
               name.StartsWith("Exit_") ||
               name.StartsWith("Hazard_") ||
               name.StartsWith("HazardInset") ||
               name.StartsWith("Cover_") ||
               name.StartsWith("ExitBeacon") ||
               name.StartsWith("Rail") ||
               name.StartsWith("BrokenRail") ||
               name.StartsWith("RouteLip") ||
               name.StartsWith("RouteLanding") ||
               name.StartsWith("RoutePost") ||
               name.StartsWith("RouteOverhead") ||
               name.StartsWith("Step_") ||
               name.StartsWith("JumpPad_") ||
               name.StartsWith("ParkourBlock_") ||
               name.StartsWith("RecoveryDeck") ||
               name.StartsWith("RecoveryPad") ||
               name.StartsWith("RecoveryGlow") ||
               name.StartsWith("ArenaPylon_") ||
               name.StartsWith("PylonCore_") ||
               name.StartsWith("PylonGlow_") ||
               name.StartsWith("District") ||
               name.StartsWith("HeightFascia") ||
               name.StartsWith("BossArena") ||
               name.StartsWith("BossLane") ||
               name.StartsWith("BossOuterRing") ||
               name.StartsWith("BossCornerRoutePad") ||
               name.StartsWith("SurfacePanel_") ||
               name.StartsWith("Edge") ||
               name.StartsWith("TraversalGapGlow") ||
               name.StartsWith("ServiceRib") ||
               name.StartsWith("ServiceGlowChip") ||
               name.StartsWith("RouteStripe") ||
               name.StartsWith("PylonGlow_") ||
               name.StartsWith("RecoveryGlow") ||
               name.Contains("Pillar") ||
               name.Contains("Gate") ||
               name.Contains("Shop") ||
               name.Contains("Canopy") ||
               name.Contains("Shell") ||
               name.Contains("Sign") ||
               name.Contains("Reactor");
    }

    private bool IsCoreTransitionPiece(Transform piece)
    {
        if (piece == null) return false;
        string name = piece.name;
        return IsGridLockedPiece(piece) ||
               IsRoutePiece(piece) ||
               name.StartsWith("JumpPad_") ||
               name.StartsWith("ParkourBlock_") ||
               name.StartsWith("RecoveryDeck") ||
               name.StartsWith("RecoveryPad") ||
               name.StartsWith("RecoveryGlow") ||
               name.StartsWith("ArenaPylon_") ||
               name.StartsWith("PylonCore_") ||
               name.StartsWith("PylonGlow_") ||
               name.StartsWith("BossArena") ||
               name.StartsWith("BossLane") ||
               name.StartsWith("BossOuterRing") ||
               name.StartsWith("BossCornerRoutePad") ||
               name.Contains("Gate") ||
               name.Contains("Shop") ||
               name.Contains("Reactor");
    }

    private bool ShouldKeepDetailPiece(Transform piece, int detailIndex)
    {
        if (piece == null) return false;
        string name = piece.name;
        if (!animateSurfaceMicroDetails && IsMicroTransitionDetail(name))
            return false;

        int stride = Mathf.Max(1, transitionDetailStride);
        return detailIndex % stride == 0;
    }

    private bool IsMicroTransitionDetail(string name)
    {
        return name.StartsWith("SurfacePanel_") ||
               name.StartsWith("Edge") ||
               name.StartsWith("TraversalGapGlow") ||
               name.StartsWith("ServiceRib") ||
               name.StartsWith("ServiceGlowChip") ||
               name.StartsWith("RouteStripe") ||
               name.StartsWith("PylonGlow_") ||
               name.StartsWith("RecoveryGlow") ||
               name.StartsWith("HeightFasciaGlow");
    }

    private bool IsTilePiece(Transform piece)
    {
        if (piece == null) return false;
        string name = piece.name;
        return name.StartsWith("Floor_") ||
               name.StartsWith("Bridge_") ||
               name.StartsWith("Platform_") ||
               name.StartsWith("UpperPlatform_") ||
               name.StartsWith("Spawn_") ||
               name.StartsWith("Exit_") ||
               name.StartsWith("Hazard_") ||
               name.StartsWith("HazardInset") ||
               name.StartsWith("Cover_") ||
               name.StartsWith("ExitBeacon") ||
               name.StartsWith("RouteLip") ||
               name.StartsWith("RouteLanding") ||
               name.StartsWith("RouteOverhead") ||
               name.StartsWith("TraversalGapGlow") ||
               name.StartsWith("Step_") ||
               name.StartsWith("JumpPad_") ||
               name.StartsWith("ParkourBlock_") ||
               name.StartsWith("RecoveryDeck") ||
               name.StartsWith("RecoveryPad") ||
               name.StartsWith("RecoveryGlow") ||
               name.StartsWith("DistrictPlate") ||
               name.StartsWith("DistrictUndercarriage") ||
               name.StartsWith("DistrictActuator") ||
               name.StartsWith("DistrictSeamGlow") ||
               name.StartsWith("DistrictCornerLine") ||
               name.StartsWith("HeightFascia");
    }

    private bool IsGridLockedPiece(Transform piece)
    {
        if (piece == null) return false;
        string name = piece.name;
        return name.StartsWith("Floor_") ||
               name.StartsWith("Bridge_") ||
               name.StartsWith("Platform_") ||
               name.StartsWith("UpperPlatform_") ||
               name.StartsWith("Spawn_") ||
               name.StartsWith("Exit_") ||
               name.StartsWith("Hazard_") ||
               name.StartsWith("DistrictPlate") ||
               name.StartsWith("DistrictUndercarriage") ||
               name.StartsWith("DistrictActuator") ||
               name.StartsWith("DistrictSeamGlow") ||
               name.StartsWith("DistrictCornerLine") ||
               name.StartsWith("HeightFascia");
    }

    private bool IsRoutePiece(Transform piece)
    {
        if (piece == null) return false;
        string name = piece.name;
        return name.StartsWith("RouteLip") ||
               name.StartsWith("RouteLanding") ||
               name.StartsWith("RoutePost") ||
               name.StartsWith("RouteOverhead") ||
               name.StartsWith("BrokenRail") ||
               name.StartsWith("TraversalGapGlow") ||
               name.StartsWith("HeightFascia") ||
               name.StartsWith("RouteStripe") ||
               name.StartsWith("Step_") ||
               name.StartsWith("JumpPad_") ||
               name.StartsWith("ParkourBlock_") ||
               name.StartsWith("RecoveryDeck") ||
               name.StartsWith("RecoveryPad");
    }

    private float GetPieceVerticalMultiplier(Transform piece)
    {
        if (piece == null) return 1f;
        string name = piece.name;
        if (name.StartsWith("Floor_")) return 1.12f;
        if (name.StartsWith("Bridge_")) return 1.34f;
        if (name.StartsWith("Platform_")) return 1.48f;
        if (name.StartsWith("UpperPlatform_")) return 1.62f;
        if (name.StartsWith("Spawn_") || name.StartsWith("Exit_")) return 1.2f;
        if (name.StartsWith("RouteLip") || name.StartsWith("TraversalGapGlow")) return 1.5f;
        if (name.StartsWith("RouteLanding")) return 1.46f;
        if (name.StartsWith("RouteOverhead")) return 1.56f;
        if (name.StartsWith("RouteStripe")) return 1.52f;
        if (name.StartsWith("Step_")) return 1.44f;
        if (name.StartsWith("JumpPad_") || name.StartsWith("ParkourBlock_")) return 1.36f;
        if (name.StartsWith("RecoveryDeck") || name.StartsWith("RecoveryPad")) return 1.24f;
        if (name.StartsWith("DistrictPlate")) return 1.46f;
        if (name.StartsWith("DistrictUndercarriage")) return 1.58f;
        if (name.StartsWith("DistrictActuator")) return 1.68f;
        if (name.StartsWith("DistrictSeamGlow") || name.StartsWith("DistrictCornerLine")) return 1.42f;
        if (name.StartsWith("HeightFascia")) return 1.42f;
        if (name.StartsWith("RoutePost") || name.StartsWith("BrokenRail") || name.StartsWith("Rail")) return 1.38f;
        if (name.StartsWith("ServiceRib") || name.StartsWith("ServiceGlowChip")) return 1.26f;
        return 1f;
    }

    private float GetPieceSettleAmplitude(Transform piece, int index)
    {
        if (piece == null) return 0f;
        string name = piece.name;
        if (name.StartsWith("Floor_")) return 0.10f + Mathf.Repeat(index * 0.017f, 0.08f);
        if (name.StartsWith("ArenaDistrict_")) return 0.24f + Mathf.Repeat(index * 0.021f, 0.12f);
        if (name.StartsWith("Bridge_")) return 0.16f + Mathf.Repeat(index * 0.023f, 0.10f);
        if (name.StartsWith("Platform_") || name.StartsWith("UpperPlatform_")) return 0.22f + Mathf.Repeat(index * 0.031f, 0.12f);
        if (name.StartsWith("DistrictPlate") || name.StartsWith("DistrictUndercarriage")) return 0.18f + Mathf.Repeat(index * 0.027f, 0.10f);
        if (name.StartsWith("DistrictActuator")) return 0.12f;
        if (name.StartsWith("Step_") || name.StartsWith("RouteLanding")) return 0.10f;
        if (name.StartsWith("Route") || name.StartsWith("Rail") || name.StartsWith("BrokenRail")) return 0.08f;
        return 0f;
    }

    private void EnsureOverlay()
    {
        if (flashOverlay != null && transitionCurtain != null && bannerGroup != null) return;

        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;

        Transform curtain = canvas.transform.Find("TransitionCurtain");
        if (curtain == null)
        {
            GameObject curtainGo = new GameObject("TransitionCurtain");
            curtainGo.transform.SetParent(canvas.transform, false);
            RectTransform rect = curtainGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            transitionCurtain = curtainGo.AddComponent<Image>();
            transitionCurtain.raycastTarget = false;
            transitionCurtain.color = new Color(curtainColor.r, curtainColor.g, curtainColor.b, 0f);
            transitionCurtain.enabled = false;
        }
        else
        {
            transitionCurtain = curtain.GetComponent<Image>();
        }


        Transform flash = canvas.transform.Find("TransitionFlashOverlay");
        if (flash == null)
        {
            GameObject flashGo = new GameObject("TransitionFlashOverlay");
            flashGo.transform.SetParent(canvas.transform, false);
            RectTransform rect = flashGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            flashOverlay = flashGo.AddComponent<Image>();
            flashOverlay.raycastTarget = false;
            flashOverlay.enabled = false;
        }
        else
        {
            flashOverlay = flash.GetComponent<Image>();
        }

        Transform banner = canvas.transform.Find("TransitionBanner");
        if (banner == null)
        {
            GameObject bannerGo = new GameObject("TransitionBanner");
            bannerGo.transform.SetParent(canvas.transform, false);
            RectTransform rect = bannerGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.12f);
            rect.anchorMax = new Vector2(0.5f, 0.12f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(760f, 120f);

            bannerGroup = bannerGo.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;

            bannerTitleText = CreateBannerText(bannerGo.transform, "TransitionBannerTitle", 34f, new Vector2(0.5f, 0.68f), Color.white);
            bannerSubtitleText = CreateBannerText(bannerGo.transform, "TransitionBannerSubtitle", 20f, new Vector2(0.5f, 0.34f), new Color(0.72f, 0.92f, 1f));
        }
        else
        {
            bannerGroup = banner.GetComponent<CanvasGroup>();
            bannerTitleText = banner.Find("TransitionBannerTitle")?.GetComponent<TMP_Text>();
            bannerSubtitleText = banner.Find("TransitionBannerSubtitle")?.GetComponent<TMP_Text>();
        }

    }

    private void EnsureScreenSpaceKlotski(Transform curtain)
    {
        if (klotskiLoaderGroup != null || curtain == null) return;

        GameObject root = new GameObject("RubikCubeLoader");
        root.transform.SetParent(curtain, false);
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(1f, 0f);
        rootRect.anchoredPosition = new Vector2(-38f, 38f);
        rootRect.sizeDelta = new Vector2(176f, 176f);
        klotskiLoaderGroup = root.AddComponent<CanvasGroup>();

        loadingCubeTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
        loadingCubeTexture.name = "TransitionCubeTexture";
        RawImage preview = root.AddComponent<RawImage>();
        preview.texture = loadingCubeTexture;
        preview.raycastTarget = false;
        preview.color = Color.white;

        GameObject cameraObject = new GameObject("TransitionCubeCamera");
        cameraObject.transform.SetParent(transform, false);
        loadingCubeCamera = cameraObject.AddComponent<Camera>();
        loadingCubeCamera.targetTexture = loadingCubeTexture;
        loadingCubeCamera.clearFlags = CameraClearFlags.SolidColor;
        loadingCubeCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        loadingCubeCamera.cullingMask = 1 << 30;
        loadingCubeCamera.fieldOfView = 32f;
        loadingCubeCamera.nearClipPlane = 0.1f;
        loadingCubeCamera.farClipPlane = 50f;

        loadingCubeRoot = new GameObject("TransitionRubikCube").transform;
        loadingCubeRoot.SetParent(transform, false);
        loadingCubeRoot.localPosition = new Vector3(10000f, 10000f, 10012f);
        cameraObject.transform.localPosition = new Vector3(10004.8f, 10004.2f, 10005.2f);
        cameraObject.transform.LookAt(loadingCubeRoot.position);
        BuildLoadingCube(loadingCubeRoot);
        loadingCubeCamera.enabled = false;
        root.SetActive(false);
    }

    private void BuildLoadingCube(Transform parent)
    {
        loadingCubeRenderers.Clear();
        loadingCubeStartColors.Clear();
        loadingCubePieces.Clear();
        loadingCubeCoordinates.Clear();
        Color solvedGrey = new Color(0.58f, 0.62f, 0.66f, 1f);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        for (int z = -1; z <= 1; z++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = $"Cube_{x}_{y}_{z}";
            piece.layer = 30;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = new Vector3(x, y, z) * 0.72f;
            piece.transform.localScale = Vector3.one * 0.64f;
            Collider collider = piece.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Material material = new Material(shader);
            material.color = solvedGrey;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", material.color * 0.35f);
            }
            Renderer renderer = piece.GetComponent<Renderer>();
            renderer.material = material;
            loadingCubeRenderers.Add(renderer);
            loadingCubeStartColors.Add(material.color);
            loadingCubePieces.Add(piece.transform);
            loadingCubeCoordinates.Add(new Vector3Int(x, y, z));
        }
    }

    private TMP_Text CreateBannerText(Transform parent, string name, float fontSize, Vector2 anchor, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(720f, 56f);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        ProjectStructureUIRoot.ApplyDefaultFont(text);
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private void ShowTransitionBanner(string title, string subtitle)
    {
        EnsureOverlay();
        if (bannerGroup == null) return;

        ProjectStructureUIRoot.BringToFront(bannerGroup.transform);
        if (bannerTitleText != null) bannerTitleText.text = title;
        if (bannerSubtitleText != null) bannerSubtitleText.text = subtitle;
        StartCoroutine(BannerRoutine());
    }

    private IEnumerator BannerRoutine()
    {
        if (bannerGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < bannerDuration)
        {
            elapsed += GetTransitionDelta(bannerDuration);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, bannerDuration));
            float alpha = t < 0.2f ? t / 0.2f : (t > 0.78f ? 1f - ((t - 0.78f) / 0.22f) : 1f);
            bannerGroup.alpha = alpha;
            yield return null;
        }

        bannerGroup.alpha = 0f;
    }

    private IEnumerator PulseFlash(Color color, float duration)
    {
        EnsureOverlay();
        if (flashOverlay == null) yield break;

        ProjectStructureUIRoot.BringToFront(flashOverlay.transform);
        flashOverlay.enabled = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetTransitionDelta(duration);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float alpha = Mathf.Sin(t * Mathf.PI);
            Color c = color;
            c.a *= alpha;
            flashOverlay.color = c;
            yield return null;
        }

        flashOverlay.color = new Color(color.r, color.g, color.b, 0f);
        flashOverlay.enabled = false;
    }

    private IEnumerator EmitAssemblyField(Vector3 center, Color color, float duration, bool rising)
    {
        Material material = BuildTransparentMaterial(new Color(color.r, color.g, color.b, 0.22f), true);
        List<Transform> slabs = new List<Transform>();
        int slabCount = Mathf.Max(4, assemblySlabCount);
        Vector3 slabScale = new Vector3(
            Mathf.Max(1.2f, assemblySlabSize.x),
            Mathf.Max(0.08f, assemblySlabSize.y),
            Mathf.Max(1.2f, assemblySlabSize.z));

        for (int i = 0; i < slabCount; i++)
        {
            float angle = (Mathf.PI * 2f * i) / slabCount;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float ring = ((i & 1) == 0) ? assemblyRadius : assemblyRadius * 0.58f;

            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "TransitionAssemblySlab";
            slab.transform.position = center + radial * ring + Vector3.up * (rising ? -assemblyTravel : 1.4f);
            slab.transform.rotation = Quaternion.LookRotation(radial, Vector3.up);
            slab.transform.localScale = slabScale;
            Renderer renderer = slab.GetComponent<Renderer>();
            if (renderer != null) renderer.material = material;
            Collider collider = slab.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
            slabs.Add(slab.transform);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetTransitionDelta(duration);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float eased = shiftCurve != null ? shiftCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);
            float pulse = Mathf.Sin(t * Mathf.PI);

            for (int i = 0; i < slabs.Count; i++)
            {
                Transform slab = slabs[i];
                if (slab == null) continue;

                Vector3 radial = slab.position - center;
                radial.y = 0f;
                radial = radial.sqrMagnitude > 0.001f ? radial.normalized : Vector3.forward;
                float ring = ((i & 1) == 0) ? assemblyRadius : assemblyRadius * 0.58f;
                float spread = Mathf.Lerp(ring * (rising ? 0.64f : 0.92f), ring * (rising ? 1.02f : 1.22f), eased);
                float height = rising
                    ? Mathf.Lerp(-assemblyTravel, 1.7f, eased)
                    : Mathf.Lerp(1.4f, -assemblyTravel, eased);
                slab.position = center + radial * spread + Vector3.up * height;
                slab.rotation = Quaternion.LookRotation(radial, Vector3.up) * Quaternion.Euler(0f, 0f, Mathf.Lerp(rising ? -10f : 0f, rising ? 0f : 10f, eased));
                slab.localScale = new Vector3(
                    Mathf.Lerp(slabScale.x * 0.76f, slabScale.x * (rising ? 1.08f : 0.92f), eased) + pulse * 0.12f,
                    slabScale.y,
                    Mathf.Lerp(slabScale.z * 0.82f, slabScale.z * (rising ? 1.12f : 1.28f), eased));
            }

            yield return null;
        }

        for (int i = 0; i < slabs.Count; i++)
        {
            if (slabs[i] != null)
                DestroyTransitionObject(slabs[i].gameObject);
        }
    }

    private void DestroyTransitionObject(GameObject target)
    {
        if (target == null) return;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private void StartTransitionRoutine(IEnumerator routine)
    {
        if (routine == null) return;

        if (Application.isPlaying)
        {
            StartCoroutine(routine);
            return;
        }

        int guard = 0;
        while (routine.MoveNext() && guard < 10000)
            guard++;
    }

    private float GetTransitionDelta(float duration)
    {
        if (Application.isPlaying)
            return Time.unscaledDeltaTime;

        return Mathf.Max(0.001f, Mathf.Max(0.01f, duration) / 90f);
    }

    private IEnumerator EmitArenaRiseAccent(Transform root)
    {
        if (root == null) yield break;

        Transform exit = FindExitTransform(root);
        Vector3 center = exit != null ? exit.position : root.position;
        yield return EmitAssemblyField(center, assemblyColor, assemblyFxDuration * 0.85f, true);
    }
}
