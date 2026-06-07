using System;
using TMPro;
using UnityEngine;
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
        panelImage.color = new Color(0.01f, 0.02f, 0.03f, 0.92f);

        overlayText = CreateText(panelRoot.transform, "OverlayTitle", 54, TextAlignmentOptions.Center, new Vector2(0.5f, 0.68f));
        rankText = CreateText(panelRoot.transform, "OverlayRank", 28, TextAlignmentOptions.Center, new Vector2(0.5f, 0.54f));
        subtitleText = CreateText(panelRoot.transform, "OverlaySubtitle", 24, TextAlignmentOptions.Center, new Vector2(0.5f, 0.38f));
        detailText = CreateText(panelRoot.transform, "OverlayDetail", 18, TextAlignmentOptions.Center, new Vector2(0.5f, 0.23f));
        footerText = CreateText(panelRoot.transform, "OverlayFooter", 16, TextAlignmentOptions.Center, new Vector2(0.5f, 0.08f));
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
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1000f, 180f);
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
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
            rankText.text = $"DESCENT PROTOCOL // {runState.CountUnlockedWeapons()}/{runState.maxTrackedWeaponPresets} VARIANTS ONLINE";
        subtitleText.text =
            BuildTitleIntro() + "\n\n" +
            "WASD move  SHIFT dash  CTRL/C slide  SPACE jump  E interact\n" +
            "1/2 switch family  Q/E cycle variants  Right click special  ESC settings";
        if (detailText != null)
            detailText.text = BuildTitleDetail(runState);
        if (footerText != null)
            footerText.text = "ENTER / SPACE // OPEN DESCENT ROUTE";

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
            rankText.text = $"{summary.rankLabel}  //  {summary.signature}";
        subtitleText.text =
            "The structure opened, but it did not end.\n" +
            $"{summary.epitaph}";
        if (detailText != null)
        {
            detailText.text =
                $"{summary.highlightLine}\n\n" +
                $"Descent score: {summary.score}\n" +
                $"Floors broken: {runState.floorsClearedThisRun}   Bosses broken: {runState.bossesClearedThisRun}\n" +
                $"Enemies dismantled: {runState.enemiesDefeatedThisRun}   Terminals solved: {runState.terminalsSolvedThisRun}\n" +
                $"Interchange syncs: {runState.shopInteractionsThisRun}   Hull loss: {Mathf.RoundToInt(runState.damageTakenThisRun)}\n" +
                $"Descent time: {FormatTime(duration)}";
        }
        if (footerText != null)
            footerText.text = "ENTER / SPACE // RUN ANOTHER DESCENT";
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
        if (panelRoot != null)
            panelRoot.SetActive(false);
        ShowTitleScreen();
    }

    private void ShowFailure()
    {
        failureShown = true;
        if (panelRoot == null) return;

        panelRoot.SetActive(true);
        ProjectStructureUIRoot.BringToFront(panelRoot.transform);
        if (panelImage != null)
            panelImage.color = new Color(0.03f, 0.01f, 0.015f, 0.94f);

        overlayText.text = "RUN BROKEN";
        overlayText.color = new Color(1f, 0.78f, 0.72f);
        CybergrindRunState runState = CybergrindRunState.GetOrCreate();
        if (rankText != null)
            rankText.text = "DESCENT LOSS // SYSTEM DISENGAGED";
        subtitleText.text =
            "The route collapsed before the core answered.\n" +
            "Another descent can still cut deeper.";
        if (detailText != null && runState != null)
        {
            detailText.text =
                $"Floor reached: {Mathf.Max(1, arenaDirector != null ? arenaDirector.floor : 1):00}\n" +
                $"Enemies dismantled: {runState.enemiesDefeatedThisRun}   Terminals solved: {runState.terminalsSolvedThisRun}\n" +
                $"Interchange syncs: {runState.shopInteractionsThisRun}   Hull loss: {Mathf.RoundToInt(runState.damageTakenThisRun)}";
        }
        if (footerText != null)
            footerText.text = "ENTER / SPACE // RESTART DESCENT    ESC // RETURN TO TITLE";

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
        if (runState == null) return "You reached a living core and forced it to answer.";
        if (runState.damageTakenThisRun < 80f && runState.bossesClearedThisRun >= 2)
            return "You reached a living core and carved a clean line through its sentries.";
        if (runState.terminalsSolvedThisRun >= 8)
            return "You reached a living core and bent its machine logic into an open wound.";
        if (runState.enemiesDefeatedThisRun >= 40)
            return "You reached a living core after tearing a path through its entire defense lattice.";
        return "You reached a living core and forced it to answer.";
    }

    private string BuildTitleIntro()
    {
        return
            "Descend through a self-building megastructure.\n" +
            "Break the machine lock. Dismantle the robots. Claim new variants and push deeper toward the core.";
    }

    private string BuildTitleDetail(CybergrindRunState runState)
    {
        int unlocked = runState != null ? runState.CountUnlockedWeapons() : 1;
        int maxTracked = runState != null ? runState.maxTrackedWeaponPresets : 6;

        if (unlocked >= maxTracked)
            return "Full variant lattice retained. The route is open; only the descent remains.";
        if (unlocked >= 4)
            return "Variant lattice partially restored. Champion chambers are feeding the armory back online.";

        return "Early descent state. Break champion chambers to bring more weapon variants online.";
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
            "CORE SIGNAL",
            "The descent did not stop.\nThe entire structure narrowed into a single response.",
            "Stand by for core imprint.",
            new Color(0.01f, 0.02f, 0.03f, 0.82f),
            0.85f);

        yield return PlayEndingBeat(
            "INNER SHELL OPEN",
            "A chamber answered from beneath the arena stack.\nThe machine recognized the run.",
            "Run signature accepted.",
            new Color(0.02f, 0.03f, 0.05f, 0.88f),
            0.8f);

        yield return PlayEndingBeat(
            "IMPRINT RECORDED",
            "Your descent was catalogued inside the living core.\nIt will remember the route you forced open.",
            "Preparing final run imprint.",
            new Color(0.04f, 0.03f, 0.02f, 0.9f),
            0.82f);

        yield return PlayEndingBeat(
            "CORE RESPONSE",
            "The megastructure marked the breach and kept the route alive.\nAnother descent can follow the scar you made.",
            "Finalizing descent summary.",
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
            "HULL BREACH",
            "The descent lost containment.\nThe structure kept moving without you.",
            "Run telemetry collapsing.",
            new Color(0.03f, 0.01f, 0.015f, 0.88f),
            0.82f);

        yield return PlayEndingBeat(
            "SIGNAL LOST",
            "The route sealed over the failure.\nAnother attempt can still reopen the scar.",
            "Preparing restart channel.",
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
            rankText.text = "CORE CHANNEL // ACTIVE";
        subtitleText.text = subtitle;
        if (detailText != null)
            detailText.text = detail;
        if (footerText != null)
            footerText.text = "CHANNEL HELD // STAND BY";

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
            summary.rankLabel = "UNCLASSIFIED DESCENT";
            summary.signature = "NO SIGNAL";
            summary.epitaph = "You reached a living core and forced it to answer.";
            summary.highlightLine = "No run telemetry was preserved.";
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
        if (score >= 1000) return "BLACK-CHANNEL DESCENT";
        if (score >= 820) return "CORELINE BREACH";
        if (score >= 650) return "THREAT-LATTICE CUT";
        if (score >= 480) return "DEEP STACK ADVANCE";
        return "SURFACE SCAR";
    }

    private string BuildRunSignature(CybergrindRunState runState, float duration)
    {
        if (runState == null) return "NO SIGNAL";

        bool clean = runState.damageTakenThisRun <= 90f;
        bool fast = duration > 0f && duration <= 420f;
        bool puzzleHeavy = runState.terminalsSolvedThisRun >= 8;
        bool violent = runState.enemiesDefeatedThisRun >= 45;

        if (clean && fast && violent) return "CLEAN CUT";
        if (puzzleHeavy && clean) return "LOGIC KNIFE";
        if (violent && runState.bossesClearedThisRun >= 2) return "SIEGE ENGINE";
        if (fast) return "HOT DESCENT";
        if (puzzleHeavy) return "MACHINE WHISPER";
        return "OPEN WOUND";
    }

    private string BuildHighlightLine(CybergrindRunState runState, float duration)
    {
        if (runState == null) return "The structure marked the breach and stayed awake.";

        if (runState.damageTakenThisRun <= 60f)
            return "Minimal hull loss. The structure barely touched you.";
        if (duration > 0f && duration <= 300f)
            return "Fast breach. You moved faster than the stack could settle.";
        if (runState.terminalsSolvedThisRun >= 8)
            return "Machine lock pressure stayed under control the whole descent.";
        if (runState.enemiesDefeatedThisRun >= 50)
            return "Defense lattice shattered under sustained pressure.";
        if (runState.shopInteractionsThisRun <= 1)
            return "You barely stopped moving. The route stayed hot all the way down.";

        return "The route is scarred open. Another descent can follow it.";
    }

    private void EnsureRuntimePresentation()
    {
        Canvas hudCanvas = ProjectStructureUIRoot.GetOrCreateCanvas();

        if (FindAnyObjectByType<ProjectStructureAtmosphereHUD>() == null)
        {
            GameObject go = new GameObject("ProjectStructureAtmosphereHUD");
            go.AddComponent<ProjectStructureAtmosphereHUD>();
        }

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

        if (FindAnyObjectByType<CombatFeedbackHUD>() == null)
        {
            GameObject go = new GameObject("CombatFeedbackHUD");
            go.AddComponent<CombatFeedbackHUD>();
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
