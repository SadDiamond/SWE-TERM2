using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class CybergrindTransitionController : MonoBehaviour
{
    [Header("Structure Shift Timing")]
    [Min(0.05f)] public float oldArenaDropDuration = 0.9f;
    [Min(0.05f)] public float newArenaRiseDuration = 1.05f;
    [Min(0f)] public float swapHoldDuration = 0.12f;
    public AnimationCurve shiftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Structure Shift Motion")]
    public float oldArenaDropDistance = 42f;
    public float newArenaRiseDistance = 38f;
    public float exitLiftDistance = 8f;
    public float exitLiftDuration = 0.65f;

    [Header("Camera")]
    public Camera targetCamera;
    public float fovKick = 8f;
    public float fovOvershoot = 3.2f;

    [Header("Exit / Cage FX")]
    public Color exitHighlightColor = new Color(0.9f, 0.6f, 0.1f, 0.5f);
    public float exitHighlightHeight = 0.06f;
    public float exitHighlightScale = 1.12f;
    public Color coverColor = new Color(0.4f, 0.92f, 1f, 0.18f);
    public float coverScaleDuration = 0.28f;
    public float cageWallThickness = 0.12f;
    public float cageWallHeight = 9f;
    [Range(0.3f, 0.8f)] public float cageCellScale = 0.52f;

    [Header("Structure Detail Motion")]
    public float detailPieceDropDistance = 10f;
    public float detailPieceRiseDistance = 8f;
    public float detailStaggerRange = 0.22f;

    [Header("Transient Structure FX")]
    [Range(4, 12)] public int latticeColumnCount = 6;
    public float latticeRadius = 4.6f;
    public float latticeColumnHeight = 10f;
    public float latticeFxDuration = 0.75f;
    public float shockwaveScale = 9f;

    [Header("Overlay FX")]
    public Color flashColor = new Color(0.72f, 0.95f, 1f, 0.28f);
    public float flashDuration = 0.22f;
    public float bannerDuration = 2.3f;

    [Header("Events")]
    public UnityEvent onTransitionStarted;
    public UnityEvent onSwapMoment;
    public UnityEvent onTransitionFinished;

    public bool IsTransitioning { get; private set; }
    public string DebugStage { get; private set; } = "Idle";

    private float baseFov;
    private Image flashOverlay;
    private CanvasGroup bannerGroup;
    private TMP_Text bannerTitleText;
    private TMP_Text bannerSubtitleText;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null)
            baseFov = targetCamera.fieldOfView;

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
        IsTransitioning = true;
        DebugStage = "Start";
        onTransitionStarted?.Invoke();
        string themeLabel = generator != null ? generator.GetThemeLabel() : "Signal Void";
        string directiveTitle = generator != null ? generator.GetThemeDirectiveTitle() : "Directive";
        ShowTransitionBanner("STRUCTURE SHIFT", $"{themeLabel} route reconfiguring // {directiveTitle}");
        StartCoroutine(PulseFlash(flashColor, flashDuration));

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
            StartCoroutine(PulseFlash(new Color(coverColor.r, coverColor.g, coverColor.b, 0.16f), flashDuration * 0.8f));
        }

        if (rig.anchor != null)
            StartCoroutine(EmitAnchorLattice(rig.anchor.position, coverColor, latticeFxDuration));

        if (rig.anchor != null)
        {
            DebugStage = "Lift";
            yield return LiftAnchor(rig, exitLiftDistance, exitLiftDuration);
            StartCoroutine(EmitShockwave(rig.anchor.position, exitHighlightColor, shockwaveScale, 0.45f));
        }

        DebugStage = "DropOld";
        yield return AnimateArenaRoot(oldRoot, Vector3.zero, Vector3.down * oldArenaDropDistance, oldArenaDropDuration, true);

        if (generator != null)
            generator.skipPlayerPlacementOnce = true;

        DebugStage = "Swap";
        DestroyTransientContent(oldRoot);
        if (rig.anchor != null)
            StartCoroutine(EmitAnchorLattice(rig.anchor.position + Vector3.up * 1.2f, exitHighlightColor, latticeFxDuration * 0.9f));
        onSwapMoment?.Invoke();
        swapAction?.Invoke();

        DebugStage = "Hold";
        yield return new WaitForSecondsRealtime(swapHoldDuration);

        Transform newRoot = generator != null ? generator.CurrentArenaRoot : null;
        if (newRoot != null)
            newRoot.position += Vector3.down * newArenaRiseDistance;

        DebugStage = "RiseNew";
        ShowTransitionBanner("DESCENT LOCKED", $"{themeLabel} floor assembled // {directiveTitle}");
        StartCoroutine(PulseFlash(new Color(exitHighlightColor.r, exitHighlightColor.g, exitHighlightColor.b, 0.22f), flashDuration * 1.1f));
        if (newRoot != null)
            StartCoroutine(EmitArenaRiseAccent(newRoot));
        yield return AnimateArenaRoot(newRoot, Vector3.down * newArenaRiseDistance, Vector3.zero, newArenaRiseDuration, false);

        DebugStage = "PlacePlayer";
        if (generator != null && player != null)
            generator.PlacePlayerAtSpawn();

        if (rig.root != null)
            Destroy(rig.root.gameObject);
        if (oldRoot != null)
            Destroy(oldRoot.gameObject);

        if (targetCamera != null)
            targetCamera.fieldOfView = baseFov;

        if (player != null)
            player.SetTransitionLock(false);

        IsTransitioning = false;
        DebugStage = "Complete";
        onTransitionFinished?.Invoke();
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

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ring.name = "ExitHighlight";
        ring.transform.SetParent(exit, false);
        ring.transform.localPosition = new Vector3(0f, exitHighlightHeight, 0f);
        ring.transform.localRotation = Quaternion.identity;
        ring.transform.localScale = new Vector3(exitHighlightScale, 0.04f, exitHighlightScale);

        Material mat = BuildTransparentMaterial(exitHighlightColor, false);
        if (ring.TryGetComponent(out Renderer renderer))
            renderer.material = mat;

        Collider col = ring.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        return ring;
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
        mat.renderQueue = 3000;

        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", new Color(color.r * 0.9f, color.g * 1.4f, color.b * 1.6f, 1f));
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

        float startTime = Time.realtimeSinceStartup;
        Vector3 from = open ? Vector3.zero : Vector3.one;
        Vector3 to = open ? Vector3.one : Vector3.zero;
        while (Time.realtimeSinceStartup - startTime < duration)
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / Mathf.Max(0.01f, duration));
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
        float startTime = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup - startTime < duration)
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / Mathf.Max(0.01f, duration));
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
        float bounceStart = Time.realtimeSinceStartup;
        Vector3 bounceEnd = end + Vector3.up * 0.34f;
        while (Time.realtimeSinceStartup - bounceStart < bounceDuration)
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - bounceStart) / bounceDuration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            Vector3 current = Vector3.LerpUnclamped(end, bounceEnd, eased);
            rig.anchor.position = current;
            yield return null;
        }

        rig.anchor.position = bounceEnd;
    }

    private IEnumerator AnimateArenaRoot(Transform root, Vector3 fromOffset, Vector3 toOffset, float duration, bool cameraOut)
    {
        if (root == null) yield break;

        Vector3 basePosition = root.position - fromOffset;
        float startTime = Time.realtimeSinceStartup;
        Coroutine detailRoutine = StartCoroutine(AnimateStructureDetails(root, cameraOut, duration));

        while (Time.realtimeSinceStartup - startTime < duration)
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / Mathf.Max(0.01f, duration));
            float eased = shiftCurve != null ? shiftCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);
            root.position = basePosition + Vector3.LerpUnclamped(fromOffset, toOffset, eased);

            if (targetCamera != null)
            {
                float pulse = Mathf.Sin(t * Mathf.PI);
                float kick = cameraOut ? fovKick : fovKick * 0.7f;
                float overshoot = cameraOut ? fovOvershoot : fovOvershoot * 0.55f;
                targetCamera.fieldOfView = baseFov + pulse * kick + Mathf.SmoothStep(0f, 1f, t) * overshoot;
            }

            yield return null;
        }

        root.position = basePosition + toOffset;
        if (detailRoutine != null)
            yield return detailRoutine;
    }

    private void DestroyTransientContent(Transform root)
    {
        if (root == null) return;

        Terminal[] terminals = root.GetComponentsInChildren<Terminal>(true);
        for (int i = 0; i < terminals.Length; i++)
        {
            if (terminals[i] != null)
                Destroy(terminals[i].gameObject);
        }

        CybergrindPickup[] pickups = root.GetComponentsInChildren<CybergrindPickup>(true);
        for (int i = 0; i < pickups.Length; i++)
        {
            if (pickups[i] != null)
                Destroy(pickups[i].gameObject);
        }

        BasicEnemyAI[] enemies = root.GetComponentsInChildren<BasicEnemyAI>(true);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null)
                Destroy(enemies[i].gameObject);
        }
    }

    private struct TransitionRig
    {
        public Transform root;
        public Transform anchor;
        public GameObject cage;
        public PlayerController player;
    }

    private IEnumerator AnimateStructureDetails(Transform root, bool dropping, float duration)
    {
        if (root == null) yield break;

        List<Transform> pieces = CollectAnimatedPieces(root);
        if (pieces.Count == 0) yield break;

        List<Vector3> originalPositions = new List<Vector3>(pieces.Count);
        float startTime = Time.realtimeSinceStartup;
        for (int i = 0; i < pieces.Count; i++)
        {
            Transform piece = pieces[i];
            originalPositions.Add(piece.localPosition);
            if (!dropping)
            {
                float preOffset = detailPieceRiseDistance * (1f + Mathf.Repeat(i * 0.17f, 0.45f));
                piece.localPosition -= Vector3.up * preOffset;
            }
        }

        while (Time.realtimeSinceStartup - startTime < duration)
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / Mathf.Max(0.01f, duration));
            for (int i = 0; i < pieces.Count; i++)
            {
                Transform piece = pieces[i];
                if (piece == null) continue;

                float stagger = Mathf.Clamp01(t - GetPieceStagger(piece, i));
                float eased = shiftCurve != null ? shiftCurve.Evaluate(stagger) : Mathf.SmoothStep(0f, 1f, stagger);
                Vector3 origin = originalPositions[i];
                float travel = dropping ? detailPieceDropDistance : detailPieceRiseDistance;
                Vector3 from = dropping ? origin : origin - Vector3.up * travel;
                Vector3 to = dropping ? origin - Vector3.up * travel : origin;
                piece.localPosition = Vector3.LerpUnclamped(from, to, eased);
            }

            yield return null;
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] == null) continue;
            if (dropping)
                pieces[i].localPosition = originalPositions[i] - Vector3.up * detailPieceDropDistance;
            else
                pieces[i].localPosition = originalPositions[i];
        }
    }

    private List<Transform> CollectAnimatedPieces(Transform root)
    {
        List<Transform> pieces = new List<Transform>();
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate == null || candidate == root) continue;
            if (!ShouldAnimatePiece(candidate)) continue;
            pieces.Add(candidate);
        }

        return pieces;
    }

    private bool ShouldAnimatePiece(Transform candidate)
    {
        if (candidate == null) return false;
        if (candidate.GetComponent<Renderer>() == null) return false;

        string name = candidate.name;
        return name.Contains("Pillar") ||
               name.Contains("Gate") ||
               name.Contains("Shop") ||
               name.Contains("BossArena") ||
               name.Contains("Exit") ||
               name.Contains("Canopy") ||
               name.Contains("Glow") ||
               name.Contains("Sign") ||
               name.Contains("Reactor");
    }

    private float GetPieceStagger(Transform piece, int index)
    {
        float normalized = Mathf.Repeat(index * 0.071f, 1f);
        float heightBias = Mathf.Clamp01(piece.localPosition.y / Mathf.Max(0.1f, cageWallHeight + 6f)) * 0.08f;
        return normalized * detailStaggerRange + heightBias;
    }

    private void EnsureOverlay()
    {
        if (flashOverlay != null && bannerGroup != null) return;

        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;

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
            elapsed += Time.unscaledDeltaTime;
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
            elapsed += Time.unscaledDeltaTime;
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

    private IEnumerator EmitAnchorLattice(Vector3 center, Color color, float duration)
    {
        Material material = BuildTransparentMaterial(new Color(color.r, color.g, color.b, 0.22f), true);
        List<Transform> columns = new List<Transform>();
        for (int i = 0; i < latticeColumnCount; i++)
        {
            float angle = (Mathf.PI * 2f * i) / Mathf.Max(1, latticeColumnCount);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * latticeRadius;
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cube);
            column.name = "TransitionLatticeColumn";
            column.transform.position = center + offset + Vector3.up * (latticeColumnHeight * 0.5f);
            column.transform.localScale = new Vector3(0.16f, latticeColumnHeight, 0.16f);
            Renderer renderer = column.GetComponent<Renderer>();
            if (renderer != null) renderer.material = material;
            Collider collider = column.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            columns.Add(column.transform);
        }

        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < duration)
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / Mathf.Max(0.01f, duration));
            for (int i = 0; i < columns.Count; i++)
            {
                Transform column = columns[i];
                if (column == null) continue;
                float scalePulse = Mathf.Sin((t + i * 0.1f) * Mathf.PI);
                column.localScale = new Vector3(0.12f + scalePulse * 0.08f, latticeColumnHeight * Mathf.Lerp(0.25f, 1f, t), 0.12f + scalePulse * 0.08f);
            }

            yield return null;
        }

        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i] != null)
                Destroy(columns[i].gameObject);
        }
    }

    private IEnumerator EmitShockwave(Vector3 center, Color color, float finalScale, float duration)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "TransitionShockwave";
        ring.transform.position = center + Vector3.up * 0.08f;
        ring.transform.localScale = new Vector3(0.1f, 0.03f, 0.1f);
        Renderer renderer = ring.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = BuildTransparentMaterial(new Color(color.r, color.g, color.b, 0.35f), true);
        Collider collider = ring.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < duration)
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / Mathf.Max(0.01f, duration));
            float scale = Mathf.Lerp(0.1f, finalScale, t);
            ring.transform.localScale = new Vector3(scale, 0.03f, scale);
            if (renderer != null)
            {
                Color c = renderer.material.HasProperty("_BaseColor") ? renderer.material.GetColor("_BaseColor") : color;
                c.a = Mathf.Lerp(0.32f, 0f, t);
                if (renderer.material.HasProperty("_BaseColor")) renderer.material.SetColor("_BaseColor", c);
                if (renderer.material.HasProperty("_Color")) renderer.material.SetColor("_Color", c);
            }
            yield return null;
        }

        if (ring != null)
            Destroy(ring);
    }

    private IEnumerator EmitArenaRiseAccent(Transform root)
    {
        if (root == null) yield break;

        Transform exit = FindExitTransform(root);
        Vector3 center = exit != null ? exit.position : root.position;
        yield return EmitAnchorLattice(center, new Color(0.66f, 0.94f, 1f, 1f), latticeFxDuration * 0.85f);
    }
}
