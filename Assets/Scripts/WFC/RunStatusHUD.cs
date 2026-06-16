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
    public TMP_Text hpText;
    public TMP_Text coinText;
    public Image coreProgressFill;
    public Image hpFill;
    public Image headerPanel;
    public Image objectivePanel;
    public Image vitalsPanel;

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
        RefreshVitals();

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
            objectiveText.text = "Core open";
            return;
        }

        if (isShop)
        {
            objectiveText.text = arenaDirector != null && arenaDirector.HasShopInteractionThisFloor()
                ? "Exit open"
                : "Choose one station";
            return;
        }

        if (unsolvedTerminals > 0)
        {
            objectiveText.text = $"Terminals left: {unsolvedTerminals}";
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
                objectiveText.text = $"Boss: {phase}";
            }
            else
            {
                objectiveText.text = livingEnemies <= 2
                    ? $"Enemies left: {livingEnemies} - marked"
                    : $"Enemies left: {livingEnemies}";
            }
            return;
        }

        if (isBoss && bossRewardRevealActive)
        {
            objectiveText.text = "Boss down - take the drop";
            return;
        }

        if (coreAccessActive)
        {
            objectiveText.text = "Enter core";
            return;
        }

        if (pendingReward)
        {
            objectiveText.text = isBoss
                ? (arenaDirector != null && arenaDirector.IsFinalBossFloor()
                    ? "Take the drop"
                    : "Take the drop")
                : "Take the weapon";
            return;
        }

        objectiveText.text = "Exit open";
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
            cycleText.text = $"{themeLabel} - Fight {position + 1}/2";
        }
        else if (position == arenaDirector.combatFloorsBeforeShop)
        {
            cycleText.text = $"{themeLabel} - Shop";
        }
        else if (position < arenaDirector.combatFloorsBeforeShop + 1 + arenaDirector.combatFloorsAfterShop)
        {
            int backHalfIndex = position - arenaDirector.combatFloorsBeforeShop;
            cycleText.text = $"{themeLabel} - Fight {backHalfIndex + 2}/4";
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
        seedText.text = $"Run {runState.currentRunSeed}";
    }

    private void RefreshCoreProgress()
    {
        if (arenaDirector == null || runState == null) return;

        if (coreProgressText != null)
            coreProgressText.text = $"Core {runState.bossesClearedThisRun}/{arenaDirector.bossFloorsToReachCore}";

        if (coreProgressFill != null)
            coreProgressFill.fillAmount = Mathf.Clamp01((float)runState.bossesClearedThisRun / Mathf.Max(1, arenaDirector.bossFloorsToReachCore));

        if (headerPanel != null && arenaDirector.generator != null)
            headerPanel.color = ResolvePanelColor(arenaDirector.generator.arenaMode, 0.84f);
        if (objectivePanel != null && arenaDirector.generator != null)
            objectivePanel.color = ResolvePanelColor(arenaDirector.generator.arenaMode, 0.62f);
    }

    private void RefreshVitals()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        if (hpText != null)
            hpText.text = $"HP {Mathf.CeilToInt(player.currentHealth)}/{Mathf.CeilToInt(player.EffectiveMaxHealth)}";
        if (coinText != null)
            coinText.text = $"Coins {player.currency}";
        if (hpFill != null)
            hpFill.fillAmount = Mathf.Clamp01(player.currentHealth / Mathf.Max(1f, player.EffectiveMaxHealth));
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

        floorText = floorText != null ? floorText : CreateHudText("FloorText", new Vector2(18f, -70f), 16f);
        ApplyTextLayout(floorText, new Vector2(18f, -70f), new Vector2(320f, 20f), 16f);
        cycleText = cycleText != null ? cycleText : CreateHudText("CycleText", new Vector2(18f, -92f), 12.5f);
        ApplyTextLayout(cycleText, new Vector2(18f, -92f), new Vector2(320f, 20f), 12.5f);
        directiveText = directiveText != null ? directiveText : CreateHudText("DirectiveText", new Vector2(18f, -110f), 9.5f);
        if (directiveText != null)
        {
            directiveText.textWrappingMode = TextWrappingModes.Normal;
            ApplyTextLayout(directiveText, new Vector2(18f, -110f), new Vector2(310f, 28f), 9.5f);
        }
        objectiveText = objectiveText != null ? objectiveText : CreateHudText("ObjectiveText", new Vector2(18f, -146f), 13f);
        ApplyTextLayout(objectiveText, new Vector2(18f, -146f), new Vector2(320f, 20f), 13f);
        seedText = seedText != null ? seedText : CreateHudText("SeedText", new Vector2(18f, -168f), 9f);
        ApplyTextLayout(seedText, new Vector2(18f, -168f), new Vector2(320f, 18f), 9f);
        coreProgressText = coreProgressText != null ? coreProgressText : CreateHudText("CoreProgressText", new Vector2(18f, -186f), 9f);
        ApplyTextLayout(coreProgressText, new Vector2(18f, -186f), new Vector2(320f, 18f), 9f);
        if (coreProgressFill == null)
            coreProgressFill = CreateProgressBar("CoreProgressBar", new Vector2(18f, -204f), new Vector2(156f, 6f), new Color(0.74f, 0.95f, 1f, 0.95f));
        ApplyProgressLayout(coreProgressFill, new Vector2(18f, -204f), new Vector2(156f, 6f));
        hpText = hpText != null ? hpText : CreateHudText("HPText", new Vector2(18f, -244f), 12f);
        ApplyTextLayout(hpText, new Vector2(18f, -244f), new Vector2(150f, 20f), 12f);
        coinText = coinText != null ? coinText : CreateHudText("CoinText", new Vector2(188f, -244f), 12f);
        ApplyTextLayout(coinText, new Vector2(188f, -244f), new Vector2(140f, 20f), 12f);
        if (hpFill == null)
            hpFill = CreateProgressBar("HPProgressBar", new Vector2(18f, -264f), new Vector2(260f, 7f), new Color(0.78f, 1f, 0.72f, 0.95f));
        ApplyProgressLayout(hpFill, new Vector2(18f, -264f), new Vector2(260f, 7f));
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
        rect.sizeDelta = new Vector2(320f, 20f);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        ProjectStructureUIRoot.ApplyDefaultFont(text);
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Left;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private void ApplyTextLayout(TMP_Text text, Vector2 anchoredPos, Vector2 size, float fontSize)
    {
        if (text == null) return;
        RectTransform rect = text.rectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
        }

        text.fontSize = fontSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void EnsurePanels()
    {
        RectTransform parentRect = transform as RectTransform;
        if (parentRect == null) return;

        headerPanel = headerPanel != null ? headerPanel : CreatePanel("HeaderPanel", new Vector2(10f, -64f), new Vector2(342f, 62f));
        ApplyPanelLayout(headerPanel, new Vector2(10f, -64f), new Vector2(342f, 62f));
        objectivePanel = objectivePanel != null ? objectivePanel : CreatePanel("ObjectivePanel", new Vector2(10f, -136f), new Vector2(342f, 86f));
        ApplyPanelLayout(objectivePanel, new Vector2(10f, -136f), new Vector2(342f, 86f));
        vitalsPanel = vitalsPanel != null ? vitalsPanel : CreatePanel("VitalsPanel", new Vector2(10f, -234f), new Vector2(342f, 48f));
        ApplyPanelLayout(vitalsPanel, new Vector2(10f, -234f), new Vector2(342f, 48f));
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

    private void ApplyPanelLayout(Image image, Vector2 anchoredPos, Vector2 size)
    {
        if (image == null || image.rectTransform == null) return;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
    }

    private Image CreateProgressBar(string name, Vector2 anchoredPos, Vector2 size, Color fillColor)
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
        backRect.sizeDelta = size;

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
        fillImage.color = fillColor;
        return fillImage;
    }

    private void ApplyProgressLayout(Image fill, Vector2 anchoredPos, Vector2 size)
    {
        if (fill == null || fill.transform.parent == null) return;
        RectTransform backRect = fill.transform.parent as RectTransform;
        if (backRect == null) return;

        backRect.anchorMin = new Vector2(0f, 1f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 1f);
        backRect.anchoredPosition = anchoredPos;
        backRect.sizeDelta = size;
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
