using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight run HUD for floor and objective progress.
/// Attach to any UI object and assign TMP text fields.
/// </summary>
public class RunStatusHUD : MonoBehaviour
{
    [Header("References")]
    public CybergrindArenaDirector arenaDirector;
    public CybergrindRunState runState;
    public TMP_Text floorText;
    public TMP_Text cycleText;
    public TMP_Text directiveText;
    public TMP_Text objectiveText;
    public TMP_Text seedText;
    public TMP_Text coreProgressText;
    public Image coreProgressFill;
    public Image headerPanel;
    public Image objectivePanel;

    [Header("Refresh")]
    [Min(0.05f)] public float refreshInterval = 0.2f;

    private float refreshTimer;

    private void Start()
    {
        if (arenaDirector == null)
            arenaDirector = FindAnyObjectByType<CybergrindArenaDirector>();
        if (runState == null)
            runState = FindAnyObjectByType<CybergrindRunState>();

        EnsureHudTexts();

        RefreshUI();
    }

    private void Update()
    {
        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f) return;

        refreshTimer = refreshInterval;
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (arenaDirector != null && floorText != null)
            floorText.text = $"Floor {arenaDirector.floor:00}";

        RefreshCycleText();
        RefreshDirectiveText();
        RefreshSeedText();
        RefreshCoreProgress();

        if (objectiveText == null) return;

        int unsolvedTerminals = CountUnsolvedPuzzleTerminals();
        int livingEnemies = CountLivingEnemies();
        bool pendingReward = arenaDirector != null && arenaDirector.HasPendingReward();
        bool runComplete = arenaDirector != null && arenaDirector.RunComplete;
        bool isShop = arenaDirector != null && arenaDirector.generator != null && arenaDirector.generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Shop;
        bool isBoss = arenaDirector != null && arenaDirector.generator != null && arenaDirector.generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Boss;
        bool bossRewardRevealActive = arenaDirector != null && arenaDirector.IsBossRewardRevealActive;
        bool coreAccessActive = arenaDirector != null && arenaDirector.IsCoreAccessActive;

        if (runComplete)
        {
            objectiveText.text = "Core is open";
            return;
        }

        if (isShop)
        {
            objectiveText.text = arenaDirector != null && arenaDirector.HasShopInteractionThisFloor()
                ? "Shop done - take the exit"
                : "Use one station, then leave";
            return;
        }

        if (unsolvedTerminals > 0)
        {
            objectiveText.text = $"Finish terminals: {unsolvedTerminals} left";
            return;
        }

        if (livingEnemies > 0)
        {
            if (isBoss)
            {
                BasicEnemyAI boss = FindCurrentBoss();
                string phase = boss != null ? boss.BossPhase switch
                {
                    2 => "PHASE III",
                    1 => "PHASE II",
                    _ => "PHASE I"
                } : "BOSS";
                objectiveText.text = $"Beat the boss - {phase}";
            }
            else
            {
                objectiveText.text = livingEnemies <= 2
                    ? $"Clear enemies: {livingEnemies} left - marked"
                    : $"Clear enemies: {livingEnemies} left";
            }
            return;
        }

        if (isBoss && bossRewardRevealActive)
        {
            objectiveText.text = "Boss down - grab the reward";
            return;
        }

        if (coreAccessActive)
        {
            objectiveText.text = "Enter the core";
            return;
        }

        if (pendingReward)
        {
            objectiveText.text = isBoss
                ? (arenaDirector != null && arenaDirector.IsFinalBossFloor()
                    ? "Grab the boss weapon to wake the core"
                    : "Grab the boss weapon to open the next floor")
                : "Grab the weapon to open the exit";
            return;
        }

        objectiveText.text = "Floor clear - take the exit";
    }

    private void RefreshCycleText()
    {
        if (arenaDirector == null || cycleText == null) return;

        int position = arenaDirector.CyclePosition;
        string themeLabel = arenaDirector.generator != null
            ? arenaDirector.generator.GetThemeLabel()
            : CybergrindArenaGenerator.GetThemeLabel(arenaDirector.CurrentThemeIndex);

        if (position < arenaDirector.combatFloorsBeforeShop)
        {
            cycleText.text = $"{themeLabel} - Combat {position + 1}/2";
        }
        else if (position == arenaDirector.combatFloorsBeforeShop)
        {
            cycleText.text = $"{themeLabel} - Shop";
        }
        else if (position < arenaDirector.combatFloorsBeforeShop + 1 + arenaDirector.combatFloorsAfterShop)
        {
            int backHalfIndex = position - arenaDirector.combatFloorsBeforeShop;
            cycleText.text = $"{themeLabel} - Combat {backHalfIndex + 2}/4";
        }
        else
        {
            cycleText.text = $"{themeLabel} - Boss";
        }
    }

    private void RefreshDirectiveText()
    {
        if (arenaDirector == null || directiveText == null) return;
        directiveText.text = $"{arenaDirector.CurrentDirectiveTitle}: {arenaDirector.CurrentDirectiveDetail}";
    }

    private void RefreshSeedText()
    {
        if (seedText == null || runState == null) return;
        seedText.text = $"Seed {runState.currentRunSeed} / {runState.currentFloorSeed}";
    }

    private void RefreshCoreProgress()
    {
        if (arenaDirector == null || runState == null) return;

        if (coreProgressText != null)
            coreProgressText.text = $"Bosses: {runState.bossesClearedThisRun}/{arenaDirector.bossFloorsToReachCore}";

        if (coreProgressFill != null)
            coreProgressFill.fillAmount = Mathf.Clamp01((float)runState.bossesClearedThisRun / Mathf.Max(1, arenaDirector.bossFloorsToReachCore));

        if (headerPanel != null && arenaDirector.generator != null)
            headerPanel.color = ResolvePanelColor(arenaDirector.generator.arenaMode, 0.84f);
        if (objectivePanel != null && arenaDirector.generator != null)
            objectivePanel.color = ResolvePanelColor(arenaDirector.generator.arenaMode, 0.62f);
    }

    private int CountUnsolvedPuzzleTerminals()
    {
        Terminal[] terminals = GetCurrentArenaRoot() != null
            ? GetCurrentArenaRoot().GetComponentsInChildren<Terminal>(true)
            : FindObjectsByType<Terminal>();

        if (terminals == null || terminals.Length == 0) return 0;

        int unsolved = 0;
        for (int i = 0; i < terminals.Length; i++)
        {
            Terminal terminal = terminals[i];
            if (terminal == null) continue;
            if (!terminal.name.StartsWith("PuzzleTerminal")) continue;
            if (!terminal.isSolved) unsolved++;
        }

        return unsolved;
    }

    private int CountLivingEnemies()
    {
        BasicEnemyAI[] enemies = GetCurrentArenaRoot() != null
            ? GetCurrentArenaRoot().GetComponentsInChildren<BasicEnemyAI>(true)
            : FindObjectsByType<BasicEnemyAI>();

        if (enemies == null || enemies.Length == 0) return 0;

        int alive = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            BasicEnemyAI enemy = enemies[i];
            if (enemy == null) continue;
            if (enemy.IsCombatResolved) continue;
            alive++;
        }

        return alive;
    }

    private BasicEnemyAI FindCurrentBoss()
    {
        BasicEnemyAI[] enemies = GetCurrentArenaRoot() != null
            ? GetCurrentArenaRoot().GetComponentsInChildren<BasicEnemyAI>(true)
            : FindObjectsByType<BasicEnemyAI>();

        if (enemies == null || enemies.Length == 0) return null;

        for (int i = 0; i < enemies.Length; i++)
        {
            BasicEnemyAI enemy = enemies[i];
            if (enemy == null || enemy.IsCombatResolved) continue;
            if (enemy.isBoss) return enemy;
        }

        return null;
    }

    private Transform GetCurrentArenaRoot()
    {
        if (arenaDirector != null && arenaDirector.generator != null)
            return arenaDirector.generator.CurrentArenaRoot;

        return null;
    }

    private void EnsureHudTexts()
    {
        EnsurePanels();
        if (floorText != null && cycleText != null && directiveText != null && objectiveText != null && seedText != null && coreProgressText != null && coreProgressFill != null) return;

        floorText = floorText != null ? floorText : CreateHudText("FloorText", new Vector2(14f, -84f), 18f);
        cycleText = cycleText != null ? cycleText : CreateHudText("CycleText", new Vector2(14f, -108f), 14f);
        directiveText = directiveText != null ? directiveText : CreateHudText("DirectiveText", new Vector2(14f, -128f), 10.5f);
        if (directiveText != null)
        {
            directiveText.textWrappingMode = TextWrappingModes.Normal;
            if (directiveText.rectTransform != null)
                directiveText.rectTransform.sizeDelta = new Vector2(340f, 30f);
        }
        objectiveText = objectiveText != null ? objectiveText : CreateHudText("ObjectiveText", new Vector2(14f, -160f), 14f);
        seedText = seedText != null ? seedText : CreateHudText("SeedText", new Vector2(14f, -184f), 10f);
        coreProgressText = coreProgressText != null ? coreProgressText : CreateHudText("CoreProgressText", new Vector2(14f, -204f), 10f);
        if (coreProgressFill == null)
            coreProgressFill = CreateProgressBar("CoreProgressBar", new Vector2(14f, -224f));
    }

    private TMP_Text CreateHudText(string name, Vector2 anchoredPos, float fontSize)
    {
        RectTransform parentRect = transform as RectTransform;
        if (parentRect == null) return null;

        Transform existing = transform.Find(name);
        if (existing != null)
            return existing.GetComponent<TMP_Text>();

        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(360f, 22f);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Left;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private void EnsurePanels()
    {
        RectTransform parentRect = transform as RectTransform;
        if (parentRect == null) return;

        headerPanel = headerPanel != null ? headerPanel : CreatePanel("HeaderPanel", new Vector2(8f, -78f), new Vector2(370f, 70f));
        objectivePanel = objectivePanel != null ? objectivePanel : CreatePanel("ObjectivePanel", new Vector2(8f, -146f), new Vector2(370f, 94f));
    }

    private Image CreatePanel(string name, Vector2 anchoredPos, Vector2 size)
    {
        Transform existing = transform.Find(name);
        if (existing != null)
            return existing.GetComponent<Image>();

        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling();
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.02f, 0.05f, 0.08f, 0.8f);
        return image;
    }

    private Image CreateProgressBar(string name, Vector2 anchoredPos)
    {
        Transform existing = transform.Find(name);
        if (existing != null)
            return existing.Find("Fill")?.GetComponent<Image>();

        GameObject back = new GameObject(name);
        back.transform.SetParent(transform, false);
        RectTransform backRect = back.AddComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 1f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 1f);
        backRect.anchoredPosition = anchoredPos;
        backRect.sizeDelta = new Vector2(170f, 7f);

        Image backImage = back.AddComponent<Image>();
        backImage.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(back.transform, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fillImage = fill.AddComponent<Image>();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 0f;
        fillImage.color = new Color(0.74f, 0.95f, 1f, 0.95f);
        return fillImage;
    }

    private Color ResolvePanelColor(CybergrindArenaGenerator.ArenaMode mode, float alpha)
    {
        return mode switch
        {
            CybergrindArenaGenerator.ArenaMode.Boss => new Color(0.12f, 0.03f, 0.03f, alpha),
            CybergrindArenaGenerator.ArenaMode.Shop => new Color(0.02f, 0.10f, 0.09f, alpha),
            _ => new Color(0.02f, 0.05f, 0.08f, alpha)
        };
    }
}
