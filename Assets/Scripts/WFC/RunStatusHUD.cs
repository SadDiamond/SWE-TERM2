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
    public TMP_Text speedText;
    public TMP_Text dashText;
    public TMP_Text hpText;
    public TMP_Text coinText;
    public Image coreProgressFill;
    public Image hpFill;
    public Image dashRechargeFill;
    public Image[] dashPips = new Image[5];
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
        bool showObjective = !(isBoss && livingEnemies > 0);
        if (objectivePanel != null) objectivePanel.gameObject.SetActive(showObjective);
        if (objectiveText != null) objectiveText.gameObject.SetActive(showObjective);

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
            headerPanel.color = ResolvePanelColor(arenaDirector.generator.arenaMode, 0.86f);
        if (objectivePanel != null && arenaDirector.generator != null)
            objectivePanel.color = ResolvePanelColor(arenaDirector.generator.arenaMode, 0.78f);
    }

    private void RefreshVitals()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        if (hpText != null)
            hpText.text = $"{Mathf.CeilToInt(player.currentHealth):000}";
        if (coinText != null)
            coinText.text = $"{player.currency:000} C";
        if (speedText != null)
            speedText.text = $"{player.PlanarSpeed:0.0} SPEED" + (player.SlideJumpChain > 0 ? $"   x{player.SlideJumpChain}" : string.Empty);
        if (dashText != null)
            dashText.text = "Dash";
        RefreshDashPips(player);
        if (hpFill != null)
            hpFill.fillAmount = Mathf.Clamp01(player.currentHealth / Mathf.Max(1f, player.EffectiveMaxHealth));
        if (hpFill != null)
            hpFill.color = player.Health01 < 0.3f ? new Color(1f, 0.16f, 0.12f, 1f) : new Color(0.12f, 0.88f, 0.95f, 1f);
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

        floorText = floorText != null ? floorText : CreateHudText("FloorText", new Vector2(22f, -68f), 18f);
        ApplyTextLayout(floorText, new Vector2(22f, -68f), new Vector2(330f, 22f), 18f);
        ApplyTextStyle(floorText, new Color(0.90f, 1f, 0.96f, 1f), FontStyles.Bold);
        cycleText = cycleText != null ? cycleText : CreateHudText("CycleText", new Vector2(22f, -93f), 12f);
        ApplyTextLayout(cycleText, new Vector2(22f, -93f), new Vector2(330f, 18f), 12f);
        ApplyTextStyle(cycleText, new Color(0.55f, 0.92f, 1f, 0.95f), FontStyles.Normal);
        directiveText = directiveText != null ? directiveText : CreateHudText("DirectiveText", new Vector2(22f, -110f), 9.5f);
        if (directiveText != null)
        {
            directiveText.textWrappingMode = TextWrappingModes.Normal;
            ApplyTextLayout(directiveText, new Vector2(22f, -110f), new Vector2(320f, 28f), 9.5f);
            ApplyTextStyle(directiveText, new Color(0.72f, 0.78f, 0.82f, 0.92f), FontStyles.Normal);
        }
        objectiveText = objectiveText != null ? objectiveText : CreateHudText("ObjectiveText", new Vector2(22f, -150f), 14f);
        ApplyTextLayout(objectiveText, new Vector2(22f, -150f), new Vector2(330f, 21f), 14f);
        ApplyTextStyle(objectiveText, new Color(1f, 0.92f, 0.62f, 1f), FontStyles.Bold);
        seedText = seedText != null ? seedText : CreateHudText("SeedText", new Vector2(22f, -174f), 9f);
        ApplyTextLayout(seedText, new Vector2(22f, -174f), new Vector2(330f, 18f), 9f);
        ApplyTextStyle(seedText, new Color(0.62f, 0.70f, 0.74f, 0.9f), FontStyles.Normal);
        coreProgressText = coreProgressText != null ? coreProgressText : CreateHudText("CoreProgressText", new Vector2(22f, -193f), 10f);
        ApplyTextLayout(coreProgressText, new Vector2(22f, -193f), new Vector2(330f, 18f), 10f);
        ApplyTextStyle(coreProgressText, new Color(0.80f, 0.96f, 1f, 0.95f), FontStyles.Normal);
        if (coreProgressFill == null)
            coreProgressFill = CreateProgressBar("CoreProgressBar", new Vector2(22f, -213f), new Vector2(178f, 8f), new Color(0.74f, 0.95f, 1f, 0.95f));
        ApplyProgressLayout(coreProgressFill, new Vector2(22f, -213f), new Vector2(178f, 8f));
        speedText = speedText != null ? speedText : CreateHudText("SpeedText", new Vector2(22f, -246f), 14f);
        ApplyTextLayout(speedText, new Vector2(22f, -246f), new Vector2(220f, 22f), 14f);
        ApplyTextStyle(speedText, new Color(0.88f, 1f, 0.62f, 1f), FontStyles.Bold);
        dashText = dashText != null ? dashText : CreateHudText("DashText", new Vector2(22f, -270f), 11f);
        ApplyTextLayout(dashText, new Vector2(22f, -270f), new Vector2(60f, 18f), 11f);
        ApplyTextStyle(dashText, new Color(0.70f, 0.92f, 1f, 0.96f), FontStyles.Bold);
        EnsureDashPips();
        hpText = hpText != null ? hpText : CreateHudText("HPText", new Vector2(22f, -296f), 13f);
        ApplyTextLayout(hpText, new Vector2(22f, -296f), new Vector2(160f, 20f), 13f);
        ApplyTextStyle(hpText, new Color(0.70f, 1f, 0.62f, 1f), FontStyles.Bold);
        coinText = coinText != null ? coinText : CreateHudText("CoinText", new Vector2(204f, -296f), 12f);
        ApplyTextLayout(coinText, new Vector2(204f, -296f), new Vector2(145f, 20f), 12f);
        ApplyTextStyle(coinText, new Color(1f, 0.85f, 0.42f, 0.96f), FontStyles.Bold);
        if (hpFill == null)
            hpFill = CreateProgressBar("HPProgressBar", new Vector2(22f, -320f), new Vector2(285f, 10f), new Color(0.45f, 1f, 0.34f, 0.95f));
        ApplyProgressLayout(hpFill, new Vector2(22f, -320f), new Vector2(285f, 10f));

        directiveText.gameObject.SetActive(false);
        seedText.gameObject.SetActive(false);
        dashText.gameObject.SetActive(false);
        ApplyViewportLayout(floorText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -28f), new Vector2(120f, 28f));
        ApplyViewportLayout(cycleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -58f), new Vector2(280f, 20f));
        ApplyViewportLayout(objectiveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(420f, 28f));
        objectiveText.alignment = TextAlignmentOptions.Center;
        ApplyViewportLayout(coreProgressText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f, -31f), new Vector2(125f, 20f));
        ApplyViewportLayout(hpText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(30f, 96f), new Vector2(130f, 52f));
        hpText.fontSize = 36f;
        ApplyViewportLayout(coinText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(190f, 96f), new Vector2(130f, 28f));
        coinText.alignment = TextAlignmentOptions.Right;
        ApplyViewportLayout(speedText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(30f, 136f), new Vector2(280f, 24f));
        ApplyViewportLayout(hpFill.transform.parent as RectTransform, Vector2.zero, Vector2.zero, new Vector2(30f, 58f), new Vector2(290f, 14f));
        ApplyViewportLayout(coreProgressFill.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f, -54f), new Vector2(125f, 6f));
    }

    private void EnsureDashPips()
    {
        if (dashPips == null || dashPips.Length != 5)
            dashPips = new Image[5];

        for (int i = 0; i < dashPips.Length; i++)
        {
            string name = $"DashPip{i + 1}";
            dashPips[i] = dashPips[i] != null ? dashPips[i] : CreateDashPip(name, i);
        }
    }

    private void RefreshDashPips(PlayerController player)
    {
        if (player == null) return;
        EnsureDashPips();

        int max = Mathf.Clamp(player.MaxDashCharges, 1, dashPips.Length);
        int charges = Mathf.Clamp(player.DashCharges, 0, max);
        float recharge = player.DashRecharge01;

        for (int i = 0; i < dashPips.Length; i++)
        {
            Image pip = dashPips[i];
            if (pip == null) continue;

            GameObject pipRoot = pip.transform.parent != null ? pip.transform.parent.gameObject : pip.gameObject;
            pipRoot.SetActive(i < max);
            if (i >= max) continue;

            pip.fillAmount = i < charges ? 1f : (i == charges ? recharge : 0f);
            pip.color = i < charges
                ? new Color(0.34f, 0.94f, 1f, 1f)
                : new Color(1f, 0.72f, 0.22f, 0.95f);
            Image back = pip.transform.parent != null ? pip.transform.parent.GetComponent<Image>() : null;
            if (back != null)
                back.color = i < charges
                    ? new Color(0.04f, 0.12f, 0.14f, 0.92f)
                    : new Color(0.22f, 0.035f, 0.035f, 0.96f);
        }
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
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
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

    private void ApplyTextStyle(TMP_Text text, Color color, FontStyles style)
    {
        if (text == null) return;
        text.color = color;
        text.fontStyle = style;
    }

    private void EnsurePanels()
    {
        RectTransform parentRect = transform as RectTransform;
        if (parentRect == null) return;

        headerPanel = headerPanel != null ? headerPanel : CreatePanel("HeaderPanel", new Vector2(12f, -60f), new Vector2(360f, 72f));
        ApplyPanelLayout(headerPanel, new Vector2(12f, -60f), new Vector2(360f, 72f));
        objectivePanel = objectivePanel != null ? objectivePanel : CreatePanel("ObjectivePanel", new Vector2(12f, -140f), new Vector2(360f, 94f));
        ApplyPanelLayout(objectivePanel, new Vector2(12f, -140f), new Vector2(360f, 94f));
        vitalsPanel = vitalsPanel != null ? vitalsPanel : CreatePanel("VitalsPanel", new Vector2(12f, -238f), new Vector2(360f, 104f));
        ApplyPanelLayout(vitalsPanel, new Vector2(12f, -238f), new Vector2(360f, 104f));

        ApplyViewportLayout(headerPanel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(310f, 66f));
        ApplyViewportLayout(objectivePanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(460f, 48f));
        ApplyViewportLayout(vitalsPanel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(20f, 20f), new Vector2(330f, 136f));
        headerPanel.color = new Color(0.015f, 0.025f, 0.032f, 0.84f);
        objectivePanel.color = new Color(0.015f, 0.025f, 0.032f, 0.78f);
        vitalsPanel.color = new Color(0.015f, 0.025f, 0.032f, 0.9f);
        DisablePanelOutline(headerPanel);
        DisablePanelOutline(objectivePanel);
        DisablePanelOutline(vitalsPanel);
    }

    private void DisablePanelOutline(Image panel)
    {
        if (panel == null) return;
        Outline outline = panel.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    private void ApplyViewportLayout(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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
        image.color = new Color(0.01f, 0.025f, 0.035f, 0.82f);
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.85f, 1f, 0.22f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
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
        backImage.color = new Color(0.015f, 0.02f, 0.025f, 0.95f);
        Outline outline = back.AddComponent<Outline>();
        outline.effectColor = new Color(0.75f, 1f, 0.95f, 0.24f);
        outline.effectDistance = new Vector2(1f, -1f);

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

    private Image CreateDashPip(string name, int index)
    {
        Transform existing = transform.Find(name);
        if (existing != null)
            return existing.Find("Fill")?.GetComponent<Image>();

        GameObject back = new GameObject(name);
        back.transform.SetParent(transform, false);
        RectTransform backRect = back.AddComponent<RectTransform>();
        float angle = 200f + index * 35f;
        Vector2 radialPosition = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * 37f;
        backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0.5f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.anchoredPosition = radialPosition;
        backRect.sizeDelta = new Vector2(21f, 7f);
        backRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);

        Image backImage = back.AddComponent<Image>();
        backImage.color = new Color(0.08f, 0.12f, 0.14f, 0.72f);

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
        fillImage.fillAmount = 1f;
        fillImage.color = new Color(0.50f, 0.95f, 1f, 0.96f);
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

    private void ApplyDashPipLayout(Image fill, Vector2 anchoredPos, Vector2 size)
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
            CybergrindArenaGenerator.ArenaMode.Boss => new Color(0.055f, 0.025f, 0.028f, alpha),
            CybergrindArenaGenerator.ArenaMode.Shop => new Color(0.02f, 0.045f, 0.04f, alpha),
            _ => new Color(0.015f, 0.025f, 0.032f, alpha)
        };
    }
}
