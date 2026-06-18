using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ProjectStructurePresentation : MonoBehaviour
{
    public string placeholderTitle = "PROJECT STRUCTURE";
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
    private bool endingSequenceStarted;
    private bool failureSequenceStarted;

    public bool IsTitleVisible => !runStarted;
    public bool IsEndingVisible => endingShown || endingSequenceStarted;
    public bool IsFailureVisible => failureShown || failureSequenceStarted;

    private void Start()
    {
        if (arenaDirector == null) arenaDirector = FindAnyObjectByType<CybergrindArenaDirector>();
        if (arenaGenerator == null) arenaGenerator = FindAnyObjectByType<CybergrindArenaGenerator>();
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        EnsureRuntimePresentation();
        BuildOverlay();
        bool launchSandbox = StartMenuController.ConsumeSandboxLaunch();
        if (StartMenuController.ConsumeArenaLaunch())
        {
            StartRun();
            if (launchSandbox)
                gameObject.AddComponent<WeaponSandboxController>();
        }
        else
            ShowTitleScreen();
    }

    private void Update()
    {
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
        panelImage.color = new Color(0.006f, 0.012f, 0.018f, 0.76f);

        CreateOverlayPanel(panelRoot.transform, "TitleRail", new Vector2(0.075f, 0.57f), new Vector2(5f, 410f), new Color(0.2f, 0.9f, 1f, 0.86f));
        CreateOverlayPanel(panelRoot.transform, "TitleBandTop", new Vector2(0.3f, 0.78f), new Vector2(560f, 2f), new Color(0.42f, 0.9f, 1f, 0.5f));
        CreateOverlayPanel(panelRoot.transform, "TitleStartPlate", new Vector2(0.22f, 0.29f), new Vector2(300f, 42f), new Color(0.015f, 0.035f, 0.045f, 0.9f));

        overlayText = CreateText(panelRoot.transform, "OverlayTitle", 64, TextAlignmentOptions.Left, new Vector2(0.09f, 0.65f), new Vector2(920f, 150f));
        rankText = CreateText(panelRoot.transform, "OverlayRank", 14, TextAlignmentOptions.Left, new Vector2(0.09f, 0.53f), new Vector2(620f, 34f));
        subtitleText = CreateText(panelRoot.transform, "OverlaySubtitle", 18, TextAlignmentOptions.Left, new Vector2(0.09f, 0.38f), new Vector2(620f, 92f));
        detailText = CreateText(panelRoot.transform, "OverlayDetail", 12, TextAlignmentOptions.Left, new Vector2(0.09f, 0.2f), new Vector2(620f, 54f));
        footerText = CreateText(panelRoot.transform, "OverlayFooter", 11, TextAlignmentOptions.Left, new Vector2(0.09f, 0.08f), new Vector2(720f, 42f));
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
        runStarted = false;
        endingShown = false;
        failureShown = false;
        endingSequenceStarted = false;
        failureSequenceStarted = false;
        panelRoot.SetActive(true);
        ProjectStructureUIRoot.BringToFront(panelRoot.transform);
        if (panelImage != null)
            panelImage.color = ResolveOverlayPanelColor();
        overlayText.text = placeholderTitle;
        overlayText.color = ResolveOverlayAccent();
        CybergrindRunState runState = CybergrindRunState.GetOrCreate();
        if (rankText != null)
            rankText.text = "MOMENTUM / COMBAT / DESCENT";
        subtitleText.text =
            BuildTitleIntro() + "\n\n" +
            "ENTER  Start run     ESC  Settings";
        if (detailText != null)
            detailText.text = BuildTitleDetail(runState);
        if (footerText != null)
            footerText.text = "WASD  MOVE     SHIFT  DASH     CTRL  SLIDE     SPACE  JUMP     F  MELEE";

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
        panelRoot.SetActive(true);
        ProjectStructureUIRoot.BringToFront(panelRoot.transform);
        if (panelImage != null)
            panelImage.color = new Color(0.01f, 0.02f, 0.03f, 0.94f);
        overlayText.text = "CORE REACHED";
        overlayText.color = new Color(0.88f, 0.96f, 1f);
        CybergrindRunState runState = CybergrindRunState.GetOrCreate();
        float duration = runState.GetRunDurationSeconds();
        RunSummary summary = BuildRunSummary(runState, duration);
        if (rankText != null)
            rankText.text = $"{summary.rankLabel} - {summary.signature}";
        subtitleText.text =
            "You made it to the end of the run.\n" +
            $"{summary.epitaph}";
        if (detailText != null)
        {
            detailText.text =
                $"{summary.highlightLine}\n\n" +
                $"Score: {summary.score}\n" +
                $"Floors: {runState.floorsClearedThisRun}   Bosses: {runState.bossesClearedThisRun}\n" +
                $"Kills: {runState.enemiesDefeatedThisRun}   Terminals: {runState.terminalsSolvedThisRun}\n" +
                $"Shop uses: {runState.shopInteractionsThisRun}   Damage taken: {Mathf.RoundToInt(runState.damageTakenThisRun)}\n" +
                $"Time: {FormatTime(duration)}";
        }
        if (footerText != null)
            footerText.text = "Enter or Space: play again";
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

        panelRoot.SetActive(true);
        ProjectStructureUIRoot.BringToFront(panelRoot.transform);
        if (panelImage != null)
            panelImage.color = new Color(0.03f, 0.01f, 0.015f, 0.94f);

        overlayText.text = "RUN OVER";
        overlayText.color = new Color(1f, 0.78f, 0.72f);
        CybergrindRunState runState = CybergrindRunState.GetOrCreate();
        if (rankText != null)
            rankText.text = "RUN OVER";
        subtitleText.text =
            "You died before reaching the end.\n" +
            "Start another run and try again.";
        if (detailText != null && runState != null)
        {
            detailText.text =
                $"Floor reached: {Mathf.Max(1, arenaDirector != null ? arenaDirector.floor : 1):00}\n" +
                $"Kills: {runState.enemiesDefeatedThisRun}   Terminals: {runState.terminalsSolvedThisRun}\n" +
                $"Shop uses: {runState.shopInteractionsThisRun}   Damage taken: {Mathf.RoundToInt(runState.damageTakenThisRun)}";
        }
        if (footerText != null)
            footerText.text = "Enter or Space: restart    Esc: title";

        Time.timeScale = 0f;
        if (player != null) player.ToggleUIMode(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
        return
            "Clear rooms. Buy gun mods. Reach the core.";
    }

    private string BuildTitleDetail(CybergrindRunState runState)
    {
        int bossTargets = arenaDirector != null ? Mathf.Max(1, arenaDirector.bossFloorsToReachCore) : 3;
        return $"Each run starts from scratch. Beat {bossTargets} boss floor{(bossTargets == 1 ? string.Empty : "s")} to reach the core.";
    }

    private Color ResolveOverlayAccent()
    {
        int themeIndex = arenaDirector != null ? arenaDirector.CurrentThemeIndex : 0;
        switch (Math.Abs(themeIndex) % 4)
        {
            case 1:
                return new Color(0.76f, 0.86f, 1f);
            case 2:
                return new Color(1f, 0.82f, 0.56f);
            case 3:
                return new Color(0.78f, 1f, 0.82f);
            default:
                return new Color(0.84f, 0.96f, 1f);
        };
    }

    private Color ResolveOverlayPanelColor()
    {
        int themeIndex = arenaDirector != null ? arenaDirector.CurrentThemeIndex : 0;
        switch (Math.Abs(themeIndex) % 4)
        {
            case 1:
                return new Color(0.01f, 0.025f, 0.05f, 0.94f);
            case 2:
                return new Color(0.04f, 0.02f, 0.015f, 0.94f);
            case 3:
                return new Color(0.015f, 0.035f, 0.02f, 0.94f);
            default:
                return new Color(0.01f, 0.02f, 0.03f, 0.92f); 
        };
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
            "The floor opens under you.\nThe whole place goes quiet.",
            "Stay ready.",
            new Color(0.01f, 0.02f, 0.03f, 0.82f),
            0.85f);

        yield return PlayEndingBeat(
            "INNER FLOOR",
            "A chamber opens below the arena.\nYou made it farther than the last run.",
            "Going down.",
            new Color(0.02f, 0.03f, 0.05f, 0.88f),
            0.8f);

        yield return PlayEndingBeat(
            "RUN SAVED",
            "Your route is marked.\nThe next attempt starts with a cleaner path.",
            "Saving.",
            new Color(0.04f, 0.03f, 0.02f, 0.9f),
            0.82f);

        yield return PlayEndingBeat(
            "END OF SLICE",
            "The core is still below.\nThis is as far as this build goes.",
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
            "You lost the run.\nThe structure kept moving without you.",
            "Run ended.",
            new Color(0.03f, 0.01f, 0.015f, 0.88f),
            0.82f);

        yield return PlayEndingBeat(
            "SIGNAL LOST",
            "The path sealed over behind you.\nYou can still break it open again.",
            "Restart when ready.",
            new Color(0.04f, 0.015f, 0.02f, 0.92f),
            0.78f);

        ShowFailure();
    }

    private System.Collections.IEnumerator PlayEndingBeat(string title, string subtitle, string detail, Color panelColor, float duration)
    {
        if (panelImage != null)
            panelImage.color = panelColor;

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

        ProjectStructureAtmosphereHUD atmosphere = FindAnyObjectByType<ProjectStructureAtmosphereHUD>();
        if (atmosphere != null)
            atmosphere.gameObject.SetActive(false);

        if (FindAnyObjectByType<ProjectStructureAudioDirector>() == null)
        {
            GameObject go = new GameObject("ProjectStructureAudioDirector");
            go.AddComponent<ProjectStructureAudioDirector>();
        }

        if (FindAnyObjectByType<RunStatusHUD>() == null && hudCanvas != null)
        {
            GameObject go = new GameObject("RunStatusHUD");
            go.transform.SetParent(hudCanvas.transform, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<RunStatusHUD>();
        }

        if (FindAnyObjectByType<BossEncounterHUD>() == null)
        {
            GameObject go = new GameObject("BossEncounterHUD");
            go.AddComponent<BossEncounterHUD>();
        }

        if (FindAnyObjectByType<EnemyPriorityHUD>() == null)
        {
            GameObject go = new GameObject("EnemyPriorityHUD");
            go.AddComponent<EnemyPriorityHUD>();
        }

        if (FindAnyObjectByType<ProjectStructureHintOverlay>() == null)
        {
            GameObject go = new GameObject("ProjectStructureHintOverlay");
            go.AddComponent<ProjectStructureHintOverlay>();
        }

        if (FindAnyObjectByType<ProjectStructureSettingsMenu>() == null)
        {
            GameObject go = new GameObject("ProjectStructureSettingsMenu");
            go.AddComponent<ProjectStructureSettingsMenu>();
        }

        if (FindAnyObjectByType<ShopPreviewHUD>() == null)
        {
            GameObject go = new GameObject("ShopPreviewHUD");
            go.AddComponent<ShopPreviewHUD>();
        }
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
}
