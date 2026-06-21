using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CybergrindDebugMode : MonoBehaviour
{
    private static CybergrindDebugMode instance;
    [Header("Mode")]
    public bool debugEnabled;
    public bool showOverlay = true;
    public bool invulnerable;
    public bool freezeTime;

    [Header("Actions")]
    public int coinsPerGrant = 25;
    [Min(0.05f)] public float refreshInterval = 0.12f;

    private PlayerController player;
    private CybergrindArenaDirector director;
    private CybergrindArenaGenerator generator;
    private CybergrindTransitionController transition;
    private CybergrindRunState runState;
    private Gun gun;
    private CharacterController controller;

    private GameObject panelRoot;
    private Image panelImage;
    private TMP_Text titleText;
    private TMP_Text bodyText;
    private float refreshTimer;
    private float previousTimeScale = 1f;
    private bool hadTimeScaleOverride;
    private bool transitionPreviewRunning;

    private readonly StringBuilder builder = new StringBuilder(1200);

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        debugEnabled = false;
        invulnerable = false;
        freezeTime = false;
        RefreshReferences(true);
        BuildOverlay();
        SetOverlayVisible(false);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            debugEnabled = !debugEnabled;
            SetOverlayVisible(debugEnabled && showOverlay);
        }

        if (!debugEnabled)
        {
            ApplyFreezeTime(false);
            return;
        }

        RefreshReferences(false);
        HandleHotkeys();

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = refreshInterval;
            RefreshOverlay();
        }
    }

    private void LateUpdate()
    {
        if (!debugEnabled || !invulnerable || player == null) return;
        player.Heal(player.EffectiveMaxHealth);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
        ApplyFreezeTime(false);
    }

    private void HandleHotkeys()
    {
        if (Keyboard.current.f1Key.wasPressedThisFrame)
            PreviewTransitionAssembly();

        if (Keyboard.current.f2Key.wasPressedThisFrame)
            CompleteCurrentTerminals();

        if (Keyboard.current.backquoteKey.wasPressedThisFrame)
            LogActiveRuntimeAudits();

        if (Keyboard.current.f4Key.wasPressedThisFrame)
            invulnerable = !invulnerable;

        if (Keyboard.current.f5Key.wasPressedThisFrame && player != null)
            player.AddCurrency(coinsPerGrant);

        if (Keyboard.current.f6Key.wasPressedThisFrame && director != null)
            director.ForceAdvanceFloor();

        if (Keyboard.current.f7Key.wasPressedThisFrame && generator != null)
            generator.GenerateArena();

        if (Keyboard.current.f8Key.wasPressedThisFrame)
            ClearCurrentEnemies();

        if (Keyboard.current.f9Key.wasPressedThisFrame && director != null)
            director.ResetRun();

        if (Keyboard.current.f10Key.wasPressedThisFrame && generator != null)
            generator.PlacePlayerAtSpawn();

        if (Keyboard.current.f11Key.wasPressedThisFrame)
            ApplyFreezeTime(!freezeTime);

        if (Keyboard.current.f12Key.wasPressedThisFrame)
        {
            showOverlay = !showOverlay;
            SetOverlayVisible(debugEnabled && showOverlay);
        }
    }

    private void RefreshReferences(bool force)
    {
        if (force || player == null)
            player = FindAnyObjectByType<PlayerController>();
        if (force || director == null)
            director = FindAnyObjectByType<CybergrindArenaDirector>();
        if (force || generator == null)
            generator = FindAnyObjectByType<CybergrindArenaGenerator>();
        if (force || transition == null)
            transition = FindAnyObjectByType<CybergrindTransitionController>();
        if (force || runState == null)
            runState = CybergrindRunState.GetOrCreate();
        if (force || gun == null)
            gun = FindAnyObjectByType<Gun>();

        controller = player != null ? player.GetComponent<CharacterController>() : null;
    }

    private void ApplyFreezeTime(bool enabled)
    {
        if (enabled == freezeTime && (!enabled || hadTimeScaleOverride)) return;

        if (enabled)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            hadTimeScaleOverride = true;
            freezeTime = true;
            return;
        }

        if (hadTimeScaleOverride)
            Time.timeScale = previousTimeScale;
        hadTimeScaleOverride = false;
        freezeTime = false;
    }

    private void ClearCurrentEnemies()
    {
        Transform root = generator != null ? generator.CurrentArenaRoot : null;
        BasicEnemyAI[] enemies = root != null
            ? root.GetComponentsInChildren<BasicEnemyAI>(true)
            : Object.FindObjectsByType<BasicEnemyAI>();

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null) continue;
            if (Application.isPlaying) Destroy(enemies[i].gameObject);
            else DestroyImmediate(enemies[i].gameObject);
        }
    }

    private void PreviewTransitionAssembly()
    {
        if (transitionPreviewRunning) return;
        if (transition == null || generator == null || generator.CurrentArenaRoot == null) return;

        StartCoroutine(PreviewTransitionAssemblyRoutine());
    }

    private IEnumerator PreviewTransitionAssemblyRoutine()
    {
        transitionPreviewRunning = true;
        yield return transition.DebugPreviewReconfigureTransition(generator, 1.65f);
        transitionPreviewRunning = false;
        RefreshOverlay();
    }

    public int DebugCompleteCurrentTerminalsForEditorVerification()
    {
        RefreshReferences(true);
        return CompleteCurrentTerminals();
    }

    private int CompleteCurrentTerminals()
    {
        Transform root = generator != null ? generator.CurrentArenaRoot : null;
        Terminal[] terminals = root != null
            ? root.GetComponentsInChildren<Terminal>(true)
            : Object.FindObjectsByType<Terminal>();

        if (runState == null)
            runState = CybergrindRunState.GetOrCreate();

        int solved = 0;
        for (int i = 0; i < terminals.Length; i++)
        {
            Terminal terminal = terminals[i];
            if (terminal == null) continue;
            if (!terminal.name.StartsWith("PuzzleTerminal")) continue;
            if (terminal.isSolved) continue;

            runState?.RegisterTerminalSolved();

            if (terminal is CybergrindPuzzleTerminal puzzleTerminal)
                puzzleTerminal.SolvePuzzle(player);
            else
                terminal.SolvePuzzle(player);

            solved++;
        }

        if (solved > 0)
            Debug.Log($"[Debug] Solved {solved} terminal(s) on the current floor.");
        else
            Debug.Log("[Debug] No unsolved terminals on the current floor.");

        RefreshOverlay();
        return solved;
    }

    private void BuildOverlay()
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;

        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = canvas.transform.GetChild(i);
            if (child == null || child.name != "ArenaDebugPanel") continue;
            if (panelRoot == null)
            {
                panelRoot = child.gameObject;
                continue;
            }

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        Transform existing = canvas.transform.Find("ArenaDebugPanel");
        if (existing != null)
        {
            panelRoot = existing.gameObject;
            panelImage = panelRoot.GetComponent<Image>();
            titleText = panelRoot.transform.Find("DebugTitle")?.GetComponent<TMP_Text>();
            bodyText = panelRoot.transform.Find("DebugBody")?.GetComponent<TMP_Text>();
            return;
        }

        panelRoot = new GameObject("ArenaDebugPanel");
        panelRoot.transform.SetParent(canvas.transform, false);

        RectTransform rect = panelRoot.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-18f, -18f);
        rect.sizeDelta = new Vector2(520f, 540f);

        panelImage = panelRoot.AddComponent<Image>();
        panelImage.color = new Color(0.015f, 0.024f, 0.032f, 0.88f);

        titleText = CreateText(panelRoot.transform, "DebugTitle", 18f, new Vector2(0f, 1f), new Vector2(16f, -14f), new Vector2(488f, 28f), TextAlignmentOptions.Left);
        titleText.color = new Color(0.78f, 0.94f, 1f);

        bodyText = CreateText(panelRoot.transform, "DebugBody", 13f, new Vector2(0f, 1f), new Vector2(16f, -48f), new Vector2(488f, 470f), TextAlignmentOptions.TopLeft);
        bodyText.color = new Color(0.9f, 0.95f, 0.98f);
    }

    public void DebugRefreshForEditorVerification()
    {
        RefreshReferences(true);
        BuildOverlay();
        SetOverlayVisible(debugEnabled && showOverlay);
        RefreshOverlay();
    }

    private TMP_Text CreateText(Transform parent, string name, float size, Vector2 anchor, Vector2 position, Vector2 sizeDelta, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        ProjectStructureUIRoot.ApplyDefaultFont(text);
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private void SetOverlayVisible(bool visible)
    {
        if (panelRoot == null)
            BuildOverlay();

        if (panelRoot == null) return;
        panelRoot.SetActive(visible);
        if (visible)
            ProjectStructureUIRoot.BringToFront(panelRoot.transform);
    }

    private void RefreshOverlay()
    {
        if (panelRoot == null)
            BuildOverlay();
        if (titleText == null || bodyText == null) return;

        int livingEnemies = CountLivingEnemies();
        int terminalsLeft = CountUnsolvedTerminals();
        int animatedPieces = transition != null && generator != null && generator.CurrentArenaRoot != null
            ? transition.CountAnimatedTransitionPieces(generator.CurrentArenaRoot)
            : 0;

        titleText.text = debugEnabled
            ? "DEBUG MODE"
            : "DEBUG MODE OFF";

        builder.Clear();
        builder.AppendLine($"F3 toggle  F12 panel  F11 freeze: {OnOff(freezeTime)}");
        builder.AppendLine($"F1 preview transition: {OnOff(transitionPreviewRunning)}  F2 solve terminals  ` log audits");
        builder.AppendLine($"F4 god: {OnOff(invulnerable)}  F5 +{coinsPerGrant} coins  F8 clear enemies");
        builder.AppendLine("F6 next floor  F7 rebuild arena  F9 reset run  F10 spawn");
        builder.AppendLine();

        if (player != null)
        {
            Vector3 planarVelocity = controller != null ? new Vector3(controller.velocity.x, 0f, controller.velocity.z) : Vector3.zero;
            builder.AppendLine($"Player  HP {Mathf.CeilToInt(player.currentHealth)}/{Mathf.CeilToInt(player.EffectiveMaxHealth)}  coins {player.currency}");
            builder.AppendLine($"Move    speed {planarVelocity.magnitude:0.0}  grounded {YesNo(player.isGrounded)}  slide {YesNo(player.DebugIsSliding)}  slam {YesNo(player.DebugIsSlamming)}");
            builder.AppendLine($"Dash    timer {player.DebugDashTimer:0.00}  momentum {FormatVector(player.DebugMomentum)}  dashVel {FormatVector(player.DebugDashVelocity)}");
        }
        else
        {
            builder.AppendLine("Player  missing");
        }

        if (gun != null)
            builder.AppendLine($"Gun     {gun.GetActiveDisplayName()}  {gun.GetRunModifierStatus()}");
        else
            builder.AppendLine("Gun     missing");

        builder.AppendLine();
        if (director != null)
        {
            builder.AppendLine($"Run     floor {director.floor:00}  cycle {director.CyclePosition + 1}/{director.CycleLength}  theme {director.CurrentThemeLabel}");
            builder.AppendLine($"State   reward {YesNo(director.HasPendingReward())}  shop {YesNo(director.HasShopInteractionThisFloor())}  core {YesNo(director.IsCoreAccessActive)}");
            AppendEncounterDiagnostics();
        }
        else
        {
            builder.AppendLine("Run     director missing");
        }

        if (runState != null)
            builder.AppendLine($"Stats   kills {runState.enemiesDefeatedThisRun}  terms {runState.terminalsSolvedThisRun}  bosses {runState.bossesClearedThisRun}  weapons {runState.CountUnlockedWeapons()}");

        if (generator != null)
        {
            builder.AppendLine($"Arena   {generator.arenaMode}  seed {generator.lastGeneratedSeed}  size {generator.width}x{generator.length}");
            builder.AppendLine($"Counts  enemies {livingEnemies}  terminals {terminalsLeft}  transition pieces {animatedPieces}");
            builder.AppendLine($"Layout  districts {generator.debugLastReconfigureDistricts}  repairs {generator.debugLastRuntimeConnectivityRepairs}  culls {generator.debugLastRuntimeConnectivityCulls}");
        }
        else
        {
            builder.AppendLine("Arena   generator missing");
        }

        if (transition != null)
        {
            builder.AppendLine($"Shift   {transition.DebugStage}  active {YesNo(transition.IsTransitioning)}  budget {transition.maxAnimatedTransitionPieces}");
            builder.AppendLine($"Diff    groups {transition.DebugLastOldPieceGroups}/{transition.DebugLastNewPieceGroups}  states {transition.DebugLastReconfigureStates}  match/up/down {transition.DebugLastMatchedPieces}/{transition.DebugLastRaisedPieces}/{transition.DebugLastRetractedPieces}");
            builder.AppendLine($"Motion  vertical {transition.DebugLastVerticalMatchedPieces}  maxY {transition.DebugLastMaxVerticalDelta:0.0}");
        }

        bodyText.text = builder.ToString();
        if (panelImage != null)
            panelImage.color = freezeTime
                ? new Color(0.06f, 0.035f, 0.015f, 0.9f)
                : new Color(0.015f, 0.024f, 0.032f, 0.88f);
    }

    private int CountLivingEnemies()
    {
        Transform root = generator != null ? generator.CurrentArenaRoot : null;
        BasicEnemyAI[] enemies = root != null
            ? root.GetComponentsInChildren<BasicEnemyAI>(true)
            : Object.FindObjectsByType<BasicEnemyAI>();

        int count = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null && !enemies[i].IsCombatResolved)
                count++;
        }
        return count;
    }

    private int CountUnsolvedTerminals()
    {
        Transform root = generator != null ? generator.CurrentArenaRoot : null;
        Terminal[] terminals = root != null
            ? root.GetComponentsInChildren<Terminal>(true)
            : Object.FindObjectsByType<Terminal>();

        int count = 0;
        for (int i = 0; i < terminals.Length; i++)
        {
            if (terminals[i] != null && terminals[i].name.StartsWith("PuzzleTerminal") && !terminals[i].isSolved)
                count++;
        }
        return count;
    }

    private void AppendEncounterDiagnostics()
    {
        if (director == null)
            return;

        string pressureSummary = director.DebugSummarizeEncounterPressure();
        string encounterAudit = director.DebugAuditEncounterPressure();
        string auditState = encounterAudit.Contains("WARN") ? "WARN" : encounterAudit.Contains("PASS") ? "PASS" : "INFO";
        builder.AppendLine($"Threat   audit {auditState}");
        builder.AppendLine("Threat   " + CompactDebugLine(pressureSummary, 180));
        builder.AppendLine("Audit    " + CompactDebugLine(encounterAudit, 180));
    }

    private void LogActiveRuntimeAudits()
    {
        if (director != null)
        {
            string encounterAudit = director.DebugAuditEncounterPressure();
            if (encounterAudit.Contains("WARN"))
                Debug.LogWarning(encounterAudit);
            else
                Debug.Log(encounterAudit);
        }

        if (generator != null)
        {
            string arenaAudit = generator.DebugAuditFastMovementLayout();
            if (arenaAudit.Contains("WARN"))
                Debug.LogWarning(arenaAudit);
            else
                Debug.Log(arenaAudit);
        }

        RefreshOverlay();
    }

    private string CompactDebugLine(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "n/a";

        string compact = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        while (compact.Contains("  "))
            compact = compact.Replace("  ", " ");

        if (compact.Length <= maxLength)
            return compact;

        return compact.Substring(0, Mathf.Max(0, maxLength - 3)) + "...";
    }

    private string FormatVector(Vector3 value)
    {
        return $"{value.x:0.0},{value.y:0.0},{value.z:0.0}";
    }

    private string OnOff(bool value)
    {
        return value ? "ON" : "OFF";
    }

    private string YesNo(bool value)
    {
        return value ? "yes" : "no";
    }
}
