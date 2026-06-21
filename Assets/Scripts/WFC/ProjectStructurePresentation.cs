using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ProjectStructurePresentation : MonoBehaviour
{
    public string placeholderTitle = "Term 2 SWE project";
    public CybergrindArenaDirector arenaDirector;
    public CybergrindArenaGenerator arenaGenerator;
    public PlayerController player;

    private bool runStarted;
    private bool endingShown;
    private bool failureShown;
    private bool endingFlashActive;
    private TMP_Text overlayText;
    private TMP_Text subtitleText;
    private GameObject panelRoot;
    private TMP_Text rankText;
    private TMP_Text detailText;
    private TMP_Text footerText;
    private Image panelImage;
    private Image frameImage;
    private Image accentBarImage;
    private Image topRuleImage;
    private Image lowerRuleImage;
    private Image sideMassLeftImage;
    private Image sideMassRightImage;
    private Image conduitTopImage;
    private Image conduitBottomImage;
    private bool endingSequenceStarted;
    private bool failureSequenceStarted;
    private static CybergrindArenaDirector cachedArenaDirector;
    private static CybergrindArenaGenerator cachedArenaGenerator;
    private static PlayerController cachedPlayer;

    public bool IsTitleVisible => !runStarted;
    public bool IsEndingVisible => endingShown || endingSequenceStarted;
    public bool IsFailureVisible => failureShown || failureSequenceStarted;

    private void Start()
    {
        if (arenaDirector == null) arenaDirector = GetArenaDirector();
        if (arenaGenerator == null) arenaGenerator = GetArenaGenerator();
        if (player == null) player = GetPlayerController();
        EnsureRuntimePresentation();
        BuildOverlay();
        if (StartMenuController.ConsumeArenaLaunch())
            StartRun();
        else
            ShowTitleScreen();
    }

    private void Update()
    {
        UpdateOverlayAmbient();

        if (!runStarted)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame ||
                 UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame))
            {
                StartRun();
            }
            return;
        }

        if (failureShown)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame ||
                    UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    RestartRun();
                }
                else if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    ReturnToTitle();
                }
            }
            return;
        }

        if (!endingSequenceStarted && !endingShown && arenaDirector != null && arenaDirector.RunComplete)
        {
            StartCoroutine(EndingSequenceRoutine());
        }

        if (!failureSequenceStarted && !endingShown && !failureShown && player != null && player.isDead)
        {
            StartCoroutine(FailureSequenceRoutine());
            return;
        }

        if (endingShown && UnityEngine.InputSystem.Keyboard.current != null &&
            (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame ||
             UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame))
        {
            RestartRun();
        }
    }

    private void BuildOverlay()
    {
        Canvas hudCanvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (hudCanvas == null) return;

        panelRoot = new GameObject("ProjectStructureOverlay");
        panelRoot.transform.SetParent(hudCanvas.transform, false);
        panelRoot.transform.SetAsLastSibling();

        RectTransform rootRect = panelRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        panelImage = panelRoot.AddComponent<Image>();
        panelImage.color = new Color(0.006f, 0.012f, 0.018f, 0.8f);

        GameObject frame = new GameObject("OverlayFrame");
        frame.transform.SetParent(panelRoot.transform, false);
        RectTransform frameRect = frame.AddComponent<RectTransform>();
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.12f, 0.5f);
        frameRect.pivot = new Vector2(0f, 0.5f);
        frameRect.sizeDelta = new Vector2(760f, 340f);
        frameImage = frame.AddComponent<Image>();
        frameImage.color = new Color(0.015f, 0.03f, 0.04f, 0.9f);

        accentBarImage = CreateOverlayPanel(frame.transform, "FrameAccentBar", new Vector2(0f, 0.5f), new Vector2(4f, 292f), new Color(0.2f, 0.9f, 1f, 0.9f));
        topRuleImage = CreateOverlayPanel(frame.transform, "FrameTopRule", new Vector2(0.5f, 1f), new Vector2(700f, 2f), new Color(0.42f, 0.9f, 1f, 0.36f));
        lowerRuleImage = CreateOverlayPanel(frame.transform, "FrameLowerRule", new Vector2(0.5f, 0f), new Vector2(620f, 2f), new Color(0.42f, 0.9f, 1f, 0.22f));
        sideMassLeftImage = CreateOverlayPanel(frame.transform, "FrameSideMassLeft", new Vector2(0.94f, 0.52f), new Vector2(34f, 288f), new Color(0.018f, 0.03f, 0.042f, 0.9f));
        sideMassRightImage = CreateOverlayPanel(frame.transform, "FrameSideMassRight", new Vector2(0.985f, 0.42f), new Vector2(14f, 214f), new Color(0.03f, 0.022f, 0.018f, 0.92f));
        conduitTopImage = CreateOverlayPanel(frame.transform, "FrameConduitTop", new Vector2(0.76f, 0.86f), new Vector2(132f, 4f), new Color(0.42f, 0.9f, 1f, 0.18f));
        conduitBottomImage = CreateOverlayPanel(frame.transform, "FrameConduitBottom", new Vector2(0.66f, 0.16f), new Vector2(96f, 4f), new Color(0.42f, 0.9f, 1f, 0.14f));

        overlayText = CreateText(frame.transform, "OverlayTitle", 56, TextAlignmentOptions.Left, new Vector2(0.08f, 0.72f), new Vector2(640f, 120f));
        rankText = CreateText(frame.transform, "OverlayRank", 12, TextAlignmentOptions.Left, new Vector2(0.08f, 0.86f), new Vector2(320f, 26f));
        subtitleText = CreateText(frame.transform, "OverlaySubtitle", 17, TextAlignmentOptions.Left, new Vector2(0.08f, 0.5f), new Vector2(620f, 78f));
        detailText = CreateText(frame.transform, "OverlayDetail", 12, TextAlignmentOptions.Left, new Vector2(0.08f, 0.29f), new Vector2(620f, 84f));
        footerText = CreateText(frame.transform, "OverlayFooter", 11, TextAlignmentOptions.Left, new Vector2(0.08f, 0.1f), new Vector2(620f, 26f));
        overlayText.rectTransform.pivot = new Vector2(0f, 0.5f);
        rankText.rectTransform.pivot = new Vector2(0f, 0.5f);
        subtitleText.rectTransform.pivot = new Vector2(0f, 0.5f);
        detailText.rectTransform.pivot = new Vector2(0f, 0.5f);
        footerText.rectTransform.pivot = new Vector2(0f, 0.5f);
        if (rankText != null)
            rankText.color = new Color(0.82f, 0.94f, 1f);
        if (detailText != null)
            detailText.color = new Color(0.74f, 0.82f, 0.88f);
        if (footerText != null)
            footerText.color = new Color(0.78f, 0.88f, 0.94f);
        panelRoot.SetActive(false);
    }

    private TMP_Text CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment, Vector2 anchor)
    {
        return CreateText(parent, name, size, alignment, anchor, new Vector2(1000f, 180f));
    }

    private TMP_Text CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment, Vector2 anchor, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = sizeDelta;
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        ProjectStructureUIRoot.ApplyDefaultFont(text);
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private Image CreateOverlayPanel(Transform parent, string name, Vector2 anchor, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private void ShowTitleScreen()
    {
        if (panelRoot == null) return;
        ApplyOverlayTypography(false, false);
        runStarted = false;
        endingShown = false;
        failureShown = false;
        endingSequenceStarted = false;
        failureSequenceStarted = false;
        panelRoot.SetActive(true);
        ProjectStructureUIRoot.BringToFront(panelRoot.transform);
        if (panelImage != null)
            panelImage.color = ResolveOverlayPanelColor();
        ApplyOverlayTheme(OverlayMood.Title);
        overlayText.text = placeholderTitle;
        overlayText.color = ResolveOverlayAccent();
        CybergrindRunState runState = CybergrindRunState.GetOrCreate();
        if (rankText != null)
            rankText.text = "NEW RUN";
        subtitleText.text =
            BuildTitleIntro();
        if (detailText != null)
            detailText.text = BuildTitleDetail(runState);
        if (footerText != null)
            footerText.text = "ENTER START   ESC SETTINGS";

        Time.timeScale = 0f;
        if (player != null) player.ToggleUIMode(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void StartRun()
    {
        runStarted = true;
        failureShown = false;
        failureSequenceStarted = false;
        endingSequenceStarted = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = 1f;
        if (player != null) player.ToggleUIMode(false);
        if (arenaDirector != null) arenaDirector.ResetRun();
    }

    private void EnterFreeMode()
    {
        runStarted = true;
        failureShown = false;
        failureSequenceStarted = false;
        endingSequenceStarted = false;
        endingShown = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = 1f;
        if (player != null) player.ToggleUIMode(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    [ContextMenu("Begin Run")]
    public void BeginRun()
    {
        StartRun();
    }

    public void RestartRunFromMenu()
    {
        if (!runStarted)
        {
            StartRun();
            return;
        }

        RestartRun();
    }

    public void ReturnToTitleFromMenu()
    {
        ReturnToTitle();
    }

    private void ShowEnding()
    {
        endingShown = true;
        failureShown = false;
        if (panelRoot == null) return;
        ApplyOverlayTypography(false, true);
        panelRoot.SetActive(true);
        ProjectStructureUIRoot.BringToFront(panelRoot.transform);
        if (panelImage != null)
            panelImage.color = new Color(0.01f, 0.02f, 0.03f, 0.94f);
        ApplyOverlayTheme(OverlayMood.Ending);
        overlayText.text = "CORE REACHED";
        overlayText.color = new Color(0.88f, 0.96f, 1f);
        CybergrindRunState runState = CybergrindRunState.GetOrCreate();
        float duration = runState.GetRunDurationSeconds();
        RunSummary summary = BuildRunSummary(runState, duration);
        if (rankText != null)
            rankText.text = $"{summary.rankLabel} / {summary.signature}";
        subtitleText.text =
            summary.epitaph;
        if (detailText != null)
        {
            detailText.text =
                $"{summary.highlightLine}\n\n" +
                $"Score  {summary.score}    Time  {FormatTime(duration)}\n" +
                $"Floors  {runState.floorsClearedThisRun}    Bosses  {runState.bossesClearedThisRun}\n" +
                $"Kills  {runState.enemiesDefeatedThisRun}    Terminals  {runState.terminalsSolvedThisRun}\n" +
                $"Shop  {runState.shopInteractionsThisRun}    Damage  {Mathf.RoundToInt(runState.damageTakenThisRun)}";
        }
        if (footerText != null)
            footerText.text = "ENTER restart";
        Time.timeScale = 0f;
        if (player != null) player.ToggleUIMode(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestartRun()
    {
        endingShown = false;
        failureShown = false;
        failureSequenceStarted = false;
        endingSequenceStarted = false;
        runStarted = true;
        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = 1f;
        if (player != null) player.ToggleUIMode(false);
        if (arenaDirector != null) arenaDirector.ResetRun();
    }

    private void ReturnToTitle()
    {
        Time.timeScale = 1f;
        if (player != null)
            player.ToggleUIMode(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("StartMenu");
    }

    private void ShowFailure()
    {
        failureShown = true;
        if (panelRoot == null) return;
        ApplyOverlayTypography(true, false);

        panelRoot.SetActive(true);
        ProjectStructureUIRoot.BringToFront(panelRoot.transform);
        if (panelImage != null)
            panelImage.color = new Color(0.02f, 0.005f, 0.008f, 0.965f);
        ApplyOverlayTheme(OverlayMood.Failure);

        overlayText.text = "YOU DIED";
        overlayText.color = new Color(1f, 0.5f, 0.44f);
        CybergrindRunState runState = CybergrindRunState.GetOrCreate();
        if (rankText != null)
        {
            rankText.text = "RUN FAILED";
            rankText.color = new Color(1f, 0.76f, 0.72f, 0.96f);
        }
        subtitleText.text = "Try again.";
        if (detailText != null && runState != null)
        {
            float duration = runState.GetRunDurationSeconds();
            detailText.text =
                $"FLOOR  {Mathf.Max(1, arenaDirector != null ? arenaDirector.floor : 1):00}    TIME  {FormatTime(duration)}\n" +
                $"KILLS  {runState.enemiesDefeatedThisRun:00}    TERMINALS  {runState.terminalsSolvedThisRun:00}\n" +
                $"SHOPS  {runState.shopInteractionsThisRun:00}    DAMAGE  {Mathf.RoundToInt(runState.damageTakenThisRun):000}";
            detailText.color = new Color(1f, 0.86f, 0.84f, 0.94f);
        }
        if (footerText != null)
        {
            footerText.text = "ENTER RESTART   ESC TITLE";
            footerText.color = new Color(1f, 0.72f, 0.68f, 0.94f);
        }

        Time.timeScale = 0f;
        if (player != null) player.ToggleUIMode(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ApplyOverlayTypography(bool failureStyle, bool endingStyle)
    {
        if (overlayText != null)
            overlayText.fontSize = failureStyle ? 82f : 56f;
        if (rankText != null)
            rankText.fontSize = 12f;
        if (subtitleText != null)
            subtitleText.fontSize = failureStyle ? 22f : 17f;
        if (detailText != null)
            detailText.fontSize = failureStyle ? 17f : 12f;
        if (footerText != null)
            footerText.fontSize = endingStyle ? 11f : 11f;
    }

    private string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
        int minutes = totalSeconds / 60;
        int remaining = totalSeconds % 60;
        return $"{minutes:00}:{remaining:00}";
    }

    private string BuildCoreEpitaph(CybergrindRunState runState)
    {
        if (runState == null) return "You reached the core.";
        if (runState.damageTakenThisRun < 80f && runState.bossesClearedThisRun >= 2)
            return "You reached the core with a clean run.";
        if (runState.terminalsSolvedThisRun >= 8)
            return "You reached the core after clearing every objective.";
        if (runState.enemiesDefeatedThisRun >= 40)
            return "You reached the core after clearing out the whole arena.";
        return "You reached the core.";
    }

    private string BuildTitleIntro()
    {
        return "Clear each floor and keep moving.";
    }

    private string BuildTitleDetail(CybergrindRunState runState)
    {
        return "SHIFT DASH   CTRL SLIDE   SPACE JUMP";
    }

    private Color ResolveOverlayAccent()
    {
        int themeIndex = arenaDirector != null ? arenaDirector.CurrentThemeIndex : 0;
        return ProjectStructureThemePalette.ResolveOverlayAccent(themeIndex);
    }

    private Color ResolveOverlayPanelColor()
    {
        int themeIndex = arenaDirector != null ? arenaDirector.CurrentThemeIndex : 0;
        return ProjectStructureThemePalette.ResolveOverlayPanel(themeIndex);
    }

    private void ApplyOverlayTheme(OverlayMood mood)
    {
        Color accent = ResolveOverlayAccent();
        Color panel = ResolveOverlayPanelColor();
        Color frameColor;
        Color barColor;
        Color ruleColor;

        switch (mood)
        {
            case OverlayMood.Failure:
                frameColor = new Color(0.08f, 0.02f, 0.025f, 0.92f);
                barColor = new Color(1f, 0.42f, 0.34f, 0.95f);
                ruleColor = new Color(1f, 0.52f, 0.44f, 0.32f);
                break;
            case OverlayMood.Ending:
                frameColor = new Color(0.012f, 0.026f, 0.04f, 0.92f);
                barColor = new Color(accent.r, accent.g, accent.b, 0.95f);
                ruleColor = new Color(accent.r, accent.g, accent.b, 0.34f);
                break;
            default:
                frameColor = new Color(panel.r * 0.8f, panel.g * 1.1f, panel.b * 1.15f, 0.92f);
                barColor = new Color(accent.r, accent.g, accent.b, 0.92f);
                ruleColor = new Color(accent.r, accent.g, accent.b, 0.28f);
                break;
        }

        if (frameImage != null)
            frameImage.color = frameColor;
        if (accentBarImage != null)
            accentBarImage.color = barColor;
        if (topRuleImage != null)
            topRuleImage.color = ruleColor;
        if (lowerRuleImage != null)
            lowerRuleImage.color = new Color(ruleColor.r, ruleColor.g, ruleColor.b, ruleColor.a * 0.72f);
        if (sideMassLeftImage != null)
            sideMassLeftImage.color = new Color(panel.r * 0.95f, panel.g * 1.04f, panel.b * 1.08f, 0.92f);
        if (sideMassRightImage != null)
            sideMassRightImage.color = new Color(panel.r * 1.1f, panel.g * 0.88f, panel.b * 0.84f, 0.92f);
        if (conduitTopImage != null)
            conduitTopImage.color = new Color(accent.r, accent.g, accent.b, 0.22f);
        if (conduitBottomImage != null)
            conduitBottomImage.color = new Color(accent.r, accent.g, accent.b, 0.18f);
    }

    private void UpdateOverlayAmbient()
    {
        if (panelRoot == null || !panelRoot.activeSelf)
            return;

        float pulseA = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.35f);
        float pulseB = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 0.9f + 1.1f);

        if (accentBarImage != null)
        {
            Color c = accentBarImage.color;
            c.a = Mathf.Clamp01(0.78f + pulseA * 0.18f);
            accentBarImage.color = c;
        }

        if (topRuleImage != null)
        {
            Color c = topRuleImage.color;
            c.a = Mathf.Clamp01(0.16f + pulseB * 0.22f);
            topRuleImage.color = c;
        }

        if (lowerRuleImage != null)
        {
            Color c = lowerRuleImage.color;
            c.a = Mathf.Clamp01(0.12f + pulseA * 0.14f);
            lowerRuleImage.color = c;
        }

        if (frameImage != null)
        {
            Color c = frameImage.color;
            c.a = Mathf.Clamp01(0.88f + pulseB * 0.05f);
            frameImage.color = c;
        }

        if (sideMassLeftImage != null)
        {
            Color c = sideMassLeftImage.color;
            c.a = Mathf.Clamp01(0.84f + pulseA * 0.08f);
            sideMassLeftImage.color = c;
        }

        if (sideMassRightImage != null)
        {
            Color c = sideMassRightImage.color;
            c.a = Mathf.Clamp01(0.82f + pulseB * 0.1f);
            sideMassRightImage.color = c;
        }

        if (conduitTopImage != null)
        {
            Color c = conduitTopImage.color;
            c.a = Mathf.Clamp01(0.08f + pulseA * 0.2f);
            conduitTopImage.color = c;
            conduitTopImage.rectTransform.anchoredPosition = new Vector2(0f, pulseB * 4f);
        }

        if (conduitBottomImage != null)
        {
            Color c = conduitBottomImage.color;
            c.a = Mathf.Clamp01(0.06f + pulseB * 0.18f);
            conduitBottomImage.color = c;
            conduitBottomImage.rectTransform.anchoredPosition = new Vector2(0f, -pulseA * 4f);
        }
    }

    private System.Collections.IEnumerator EndingSequenceRoutine()
    {
        if (panelRoot == null)
        {
            ShowEnding();
            yield break;
        }

        endingSequenceStarted = true;
        panelRoot.SetActive(true);
        ProjectStructureUIRoot.BringToFront(panelRoot.transform);
        if (player != null) player.ToggleUIMode(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return PlayEndingBeat(
            "CORE REACHED",
            "The floor opens under you.",
            "Hold position.",
            new Color(0.01f, 0.02f, 0.03f, 0.82f),
            0.85f);

        yield return PlayEndingBeat(
            "INNER FLOOR",
            "A chamber opens below the arena.",
            "Route expanding.",
            new Color(0.02f, 0.03f, 0.05f, 0.88f),
            0.8f);

        yield return PlayEndingBeat(
            "RUN SAVED",
            "Your route is marked.",
            "Run archived.",
            new Color(0.04f, 0.03f, 0.02f, 0.9f),
            0.82f);

        yield return PlayEndingBeat(
            "END OF SLICE",
            "The core is still below.",
            "Run complete.",
            new Color(0.02f, 0.03f, 0.03f, 0.92f),
            0.78f);

        ShowEnding();
    }

    private System.Collections.IEnumerator FailureSequenceRoutine()
    {
        if (panelRoot == null)
        {
            ShowFailure();
            yield break;
        }

        failureSequenceStarted = true;
        panelRoot.SetActive(true);
        ProjectStructureUIRoot.BringToFront(panelRoot.transform);
        if (player != null) player.ToggleUIMode(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return PlayEndingBeat(
            "YOU DIED",
            "Momentum cut.",
            "Reset the run.",
            new Color(0.045f, 0.008f, 0.012f, 0.94f),
            0.44f);

        ShowFailure();
    }

    private System.Collections.IEnumerator PlayEndingBeat(string title, string subtitle, string detail, Color panelColor, float duration)
    {
        if (panelImage != null)
            panelImage.color = panelColor;
        ApplyOverlayTheme(title == "YOU DIED" ? OverlayMood.Failure : OverlayMood.Ending);

        overlayText.text = title;
        overlayText.color = ResolveOverlayAccent();
        if (rankText != null)
            rankText.text = "RUN RESULT";
        subtitleText.text = subtitle;
        if (detailText != null)
            detailText.text = detail;
        if (footerText != null)
            footerText.text = "Hold position";

        yield return PulseEndingFlash();
        yield return new WaitForSecondsRealtime(duration);
    }

    private System.Collections.IEnumerator PulseEndingFlash()
    {
        if (endingFlashActive || panelImage == null)
            yield break;

        endingFlashActive = true;
        Color baseColor = panelImage.color;
        Color flashColor = new Color(
            Mathf.Clamp01(baseColor.r + 0.08f),
            Mathf.Clamp01(baseColor.g + 0.08f),
            Mathf.Clamp01(baseColor.b + 0.1f),
            baseColor.a);

        float duration = 0.18f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(t * Mathf.PI);
            panelImage.color = Color.Lerp(baseColor, flashColor, pulse);
            yield return null;
        }

        panelImage.color = baseColor;
        endingFlashActive = false;
    }

    private RunSummary BuildRunSummary(CybergrindRunState runState, float duration)
    {
        RunSummary summary = new RunSummary();
        if (runState == null)
        {
            summary.rankLabel = "UNRANKED";
            summary.signature = "NO DATA";
            summary.epitaph = "You reached the end.";
            summary.highlightLine = "No run data was saved.";
            summary.score = 0;
            return summary;
        }

        int timeSeconds = Mathf.Max(1, Mathf.RoundToInt(duration));
        int speedScore = Mathf.Clamp(360 - timeSeconds * 3, 0, 360);
        int combatScore = runState.enemiesDefeatedThisRun * 9;
        int puzzleScore = runState.terminalsSolvedThisRun * 30;
        int bossScore = runState.bossesClearedThisRun * 180;
        int floorScore = runState.floorsClearedThisRun * 26;
        int disciplineScore = Mathf.Clamp(220 - Mathf.RoundToInt(runState.damageTakenThisRun * 2.2f), 0, 220);
        int momentumScore = Mathf.Clamp(50 - runState.shopInteractionsThisRun * 6, 0, 50);

        summary.score = speedScore + combatScore + puzzleScore + bossScore + floorScore + disciplineScore + momentumScore;
        summary.rankLabel = ResolveRankLabel(summary.score);
        summary.signature = BuildRunSignature(runState, duration);
        summary.epitaph = BuildCoreEpitaph(runState);
        summary.highlightLine = BuildHighlightLine(runState, duration);
        return summary;
    }

    private string ResolveRankLabel(int score)
    {
        if (score >= 1000) return "S RANK";
        if (score >= 820) return "A RANK";
        if (score >= 650) return "B RANK";
        if (score >= 480) return "C RANK";
        return "D RANK";
    }

    private string BuildRunSignature(CybergrindRunState runState, float duration)
    {
        if (runState == null) return "NO DATA";

        bool clean = runState.damageTakenThisRun <= 90f;
        bool fast = duration > 0f && duration <= 420f;
        bool puzzleHeavy = runState.terminalsSolvedThisRun >= 8;
        bool violent = runState.enemiesDefeatedThisRun >= 45;

        if (clean && fast && violent) return "FAST CLEAR";
        if (puzzleHeavy && clean) return "CLEAN CLEAR";
        if (violent && runState.bossesClearedThisRun >= 2) return "FULL CLEAR";
        if (fast) return "SPEED RUN";
        if (puzzleHeavy) return "OBJECTIVE RUN";
        return "STANDARD RUN";
    }

    private string BuildHighlightLine(CybergrindRunState runState, float duration)
    {
        if (runState == null) return "No run data was saved.";

        if (runState.damageTakenThisRun <= 60f)
            return "You took very little damage.";
        if (duration > 0f && duration <= 300f)
            return "You cleared the run quickly.";
        if (runState.terminalsSolvedThisRun >= 8)
            return "You cleared every terminal on the way down.";
        if (runState.enemiesDefeatedThisRun >= 50)
            return "You cleared out most of the arena.";
        if (runState.shopInteractionsThisRun <= 1)
            return "You barely stopped moving.";

        return "You made it through the run.";
    }

    private void EnsureRuntimePresentation()
    {
        Canvas hudCanvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        RemoveLegacyScreenOverlays(hudCanvas);

        ProjectStructureAtmosphereHUD atmosphere = UnityEngine.Object.FindAnyObjectByType<ProjectStructureAtmosphereHUD>();
        if (atmosphere != null)
        {
            if (Application.isPlaying)
                Destroy(atmosphere.gameObject);
            else
                DestroyImmediate(atmosphere.gameObject);
        }

        EnsureSingletonComponent<ProjectStructureAudioDirector>("ProjectStructureAudioDirector");
        EnsureFullscreenHud<RunStatusHUD>(hudCanvas, "RunStatusHUD");
        EnsureSingletonComponent<BossEncounterHUD>("BossEncounterHUD");
        EnsureSingletonComponent<EnemyPriorityHUD>("EnemyPriorityHUD");
        EnsureSingletonComponent<ProjectStructureHintOverlay>("ProjectStructureHintOverlay");
        EnsureSingletonComponent<ProjectStructureSettingsMenu>("ProjectStructureSettingsMenu");
        EnsureSingletonComponent<ShopPreviewHUD>("ShopPreviewHUD");
        EnsureSingletonComponent<WeaponStatusHUD>("WeaponStatusHUD");
    }

    private static T EnsureSingletonComponent<T>(string objectName) where T : Component
    {
        T existing = UnityEngine.Object.FindAnyObjectByType<T>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject(objectName);
        return go.AddComponent<T>();
    }

    private static T EnsureFullscreenHud<T>(Canvas hudCanvas, string objectName) where T : Component
    {
        T existing = UnityEngine.Object.FindAnyObjectByType<T>();
        if (existing != null || hudCanvas == null)
            return existing;

        GameObject go = new GameObject(objectName);
        go.transform.SetParent(hudCanvas.transform, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return go.AddComponent<T>();
    }

    private static CybergrindArenaDirector GetArenaDirector()
    {
        if (cachedArenaDirector == null)
            cachedArenaDirector = UnityEngine.Object.FindAnyObjectByType<CybergrindArenaDirector>();
        return cachedArenaDirector;
    }

    private static CybergrindArenaGenerator GetArenaGenerator()
    {
        if (cachedArenaGenerator == null)
            cachedArenaGenerator = UnityEngine.Object.FindAnyObjectByType<CybergrindArenaGenerator>();
        return cachedArenaGenerator;
    }

    private static PlayerController GetPlayerController()
    {
        if (cachedPlayer == null)
            cachedPlayer = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        return cachedPlayer;
    }

    private void RemoveLegacyScreenOverlays(Canvas hudCanvas)
    {
        if (hudCanvas == null) return;

        RemoveCanvasChild(hudCanvas.transform, "ProjectStructureScanlines");
        RemoveCanvasChild(hudCanvas.transform, "ProjectStructureModeTint");
        RemoveCanvasChild(hudCanvas.transform, "ProjectStructureVignette");
    }

    private void RemoveCanvasChild(Transform parent, string childName)
    {
        Transform child = parent != null ? parent.Find(childName) : null;
        if (child == null) return;

        if (Application.isPlaying)
            Destroy(child.gameObject);
        else
            DestroyImmediate(child.gameObject);
    }

    private struct RunSummary
    {
        public string rankLabel;
        public string signature;
        public string epitaph;
        public string highlightLine;
        public int score;
    }

    private enum OverlayMood
    {
        Title,
        Ending,
        Failure
    }
}
