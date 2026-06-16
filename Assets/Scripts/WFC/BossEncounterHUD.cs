using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossEncounterHUD : MonoBehaviour
{
    public CybergrindArenaDirector arenaDirector;
    public TMP_Text bannerText;
    public TMP_Text bannerDetailText;
    public TMP_Text bossNameText;
    public TMP_Text bossPhaseText;
    public TMP_Text bossHealthValueText;
    public Image bossHealthFill;
    public Image bossHealthBack;

    [Min(0.05f)] public float refreshInterval = 0.1f;
    public float bannerDuration = 2.8f;

    private float refreshTimer;
    private float bannerTimer;
    private string lastBannerKey = string.Empty;
    private string lastBossName = string.Empty;
    private int lastBossPhase = -1;
    private bool lastShopReady;
    private bool lastBossVisible;
    private bool lastRewardPending;
    private bool lastBossRewardRevealActive;
    private bool lastCoreAccessActive;
    private GameObject bannerRoot;
    private GameObject bossRoot;
    private Image bannerPanelImage;
    private Image bossPanelImage;

    private void Start()
    {
        if (arenaDirector == null)
            arenaDirector = FindAnyObjectByType<CybergrindArenaDirector>();

        BuildUI();
        SetBossVisible(false);
        SetBannerVisible(false);
    }

    private void Update()
    {
        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = refreshInterval;
            RefreshState();
        }

        if (bannerTimer > 0f)
        {
            bannerTimer -= Time.deltaTime;
            if (bannerTimer <= 0f)
                SetBannerVisible(false);
        }
    }

    private void RefreshState()
    {
        if (arenaDirector == null || arenaDirector.generator == null)
        {
            SetBossVisible(false);
            return;
        }

        CybergrindArenaGenerator.ArenaMode mode = arenaDirector.generator.arenaMode;
        string bannerKey = $"F{arenaDirector.floor}_{mode}";
        if (bannerKey != lastBannerKey)
        {
            lastBannerKey = bannerKey;
            ShowBannerForMode(mode);
            lastShopReady = arenaDirector.HasShopInteractionThisFloor();
            lastRewardPending = arenaDirector.HasPendingReward();
            lastBossRewardRevealActive = arenaDirector.IsBossRewardRevealActive;
            lastCoreAccessActive = arenaDirector.IsCoreAccessActive;
            lastBossVisible = false;
            lastBossPhase = -1;
        }

        if (mode == CybergrindArenaGenerator.ArenaMode.Shop)
        {
            bool shopReady = arenaDirector.HasShopInteractionThisFloor();
            if (shopReady && !lastShopReady)
            {
                ShowEncounterBanner(
                    "SHOP DONE",
                    "You used a station. The exit is open.",
                    new Color(0.04f, 0.15f, 0.12f, 0.92f),
                    bannerDuration + 0.4f);
            }

            lastShopReady = shopReady;
            SetBossVisible(false);
            return;
        }

        if (mode != CybergrindArenaGenerator.ArenaMode.Boss)
        {
            SetBossVisible(false);
            lastBossVisible = false;
            lastBossPhase = -1;
            return;
        }

        BasicEnemyAI boss = FindCurrentBoss();
        bool rewardPending = arenaDirector.HasPendingReward();
        bool rewardRevealActive = arenaDirector.IsBossRewardRevealActive;
        bool coreAccessActive = arenaDirector.IsCoreAccessActive;

        if (boss == null)
        {
            if (rewardRevealActive && !lastBossRewardRevealActive)
            {
                ShowEncounterBanner(
                    "BOSS DOWN",
                    "Reward incoming.",
                    new Color(0.12f, 0.05f, 0.03f, 0.92f),
                    bannerDuration + 0.3f);
            }
            if (rewardPending && !lastRewardPending)
            {
                ShowEncounterBanner(
                    arenaDirector.IsFinalBossFloor() ? "CORE DROP READY" : "REWARD READY",
                    arenaDirector.IsFinalBossFloor()
                        ? "Take the weapon, then enter the core."
                        : "Grab the weapon before you leave.",
                    new Color(0.14f, 0.08f, 0.03f, 0.94f),
                    bannerDuration + 0.55f);
            }
            if (coreAccessActive && !lastCoreAccessActive)
            {
                ShowEncounterBanner(
                    "CORE OPEN",
                    "Step in to finish the run.",
                    new Color(0.03f, 0.11f, 0.13f, 0.94f),
                    bannerDuration + 0.75f);
            }
            if (lastBossVisible)
            {
                ShowEncounterBanner(
                    arenaDirector.IsFinalBossFloor() ? "CORE OPEN" : "BOSS DEFEATED",
                    arenaDirector.IsFinalBossFloor()
                        ? "Take the weapon, then enter the core."
                        : "The floor is clear. Take your reward and move on.",
                    new Color(0.16f, 0.06f, 0.04f, 0.92f),
                    bannerDuration + 0.55f);
            }

            lastBossName = string.Empty;
            SetBossVisible(false);
            lastBossVisible = false;
            lastBossPhase = -1;
            lastRewardPending = rewardPending;
            lastBossRewardRevealActive = rewardRevealActive;
            lastCoreAccessActive = coreAccessActive;
            return;
        }

        SetBossVisible(true);
        lastBossVisible = true;
        if (!string.Equals(lastBossName, boss.displayName))
        {
            lastBossName = boss.displayName;
            ShowBossBanner(boss);
        }
        if (boss.BossPhase != lastBossPhase)
        {
            lastBossPhase = boss.BossPhase;
            ShowBossPhaseBanner(boss, lastBossPhase);
        }
        if (bossNameText != null)
            bossNameText.text = boss.displayName.ToUpperInvariant();
        if (bossPhaseText != null)
            bossPhaseText.text = BuildBossPhaseText(boss);
        if (bossHealthValueText != null)
            bossHealthValueText.text = $"{Mathf.CeilToInt(boss.CurrentHealth)} / {Mathf.CeilToInt(boss.maxHealth)} HULL";
        if (bossHealthFill != null)
        {
            bossHealthFill.fillAmount = boss.Health01;
            bossHealthFill.color = GetBossColor(boss, boss.Health01);
        }
        if (bossPanelImage != null)
            bossPanelImage.color = Color.Lerp(new Color(0.05f, 0.02f, 0.03f, 0.88f), GetBossColor(boss, boss.Health01) * new Color(1f, 1f, 1f, 0.38f), 0.35f);
        lastRewardPending = rewardPending;
        lastBossRewardRevealActive = rewardRevealActive;
        lastCoreAccessActive = coreAccessActive;
    }

    private BasicEnemyAI FindCurrentBoss()
    {
        Transform root = arenaDirector != null && arenaDirector.generator != null ? arenaDirector.generator.CurrentArenaRoot : null;
        if (root == null) return null;

        BasicEnemyAI[] enemies = root.GetComponentsInChildren<BasicEnemyAI>(true);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null && enemies[i].isBoss && !enemies[i].IsCombatResolved)
                return enemies[i];
        }

        return null;
    }

    private void ShowBannerForMode(CybergrindArenaGenerator.ArenaMode mode)
    {
        string themeLabel = arenaDirector != null ? arenaDirector.CurrentThemeLabel : "Arena";
        switch (mode)
        {
            case CybergrindArenaGenerator.ArenaMode.Shop:
                ShowEncounterBanner(
                    $"{themeLabel.ToUpperInvariant()} SHOP",
                    "Buy once. Leave when ready.",
                    new Color(0.02f, 0.11f, 0.10f, 0.9f),
                    bannerDuration);
                break;
            case CybergrindArenaGenerator.ArenaMode.Boss:
                ShowEncounterBanner(
                    $"{themeLabel.ToUpperInvariant()} BOSS",
                    "Read the pattern. Hit the opening.",
                    new Color(0.11f, 0.03f, 0.04f, 0.92f),
                    bannerDuration + 0.2f);
                break;
            default:
                ShowEncounterBanner(
                    $"{themeLabel.ToUpperInvariant()} FLOOR {arenaDirector.floor:00}",
                    "Clear the room. Finish the terminal. Take the drop.",
                    new Color(0.01f, 0.05f, 0.08f, 0.88f),
                    bannerDuration);
                break;
        }
    }

    private void ShowBossBanner(BasicEnemyAI boss)
    {
        if (boss == null) return;

        string suffix = boss.bossArchetype switch
        {
            BasicEnemyAI.BossArchetype.Warden => "RINGS / CROSSFIRE",
            BasicEnemyAI.BossArchetype.Striker => "RUSH / SLAM",
            BasicEnemyAI.BossArchetype.Sentinel => "DIVE / VOLLEY",
            _ => "BOSS"
        };

        ShowEncounterBanner(
            $"{boss.displayName.ToUpperInvariant()} - {suffix}",
            BuildBossArchetypeDetail(boss),
            GetBannerColorForBoss(boss),
            Mathf.Max(bannerDuration, 3.4f));
    }

    private void ShowBossPhaseBanner(BasicEnemyAI boss, int phase)
    {
        if (boss == null || phase <= 0) return;

        string phaseLabel = phase switch
        {
            2 => "PHASE III",
            1 => "PHASE II",
            _ => "PHASE I"
        };

        ShowEncounterBanner(
            $"{phaseLabel}",
            $"{boss.displayName.ToUpperInvariant()} changed pattern. {BuildBossArchetypeDetail(boss)}",
            GetBannerColorForBoss(boss),
            2.2f);
    }

    private void BuildUI()
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;

        bannerRoot = new GameObject("EncounterBanner");
        bannerRoot.transform.SetParent(canvas.transform, false);
        RectTransform bannerRect = bannerRoot.AddComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.5f, 1f);
        bannerRect.anchorMax = new Vector2(0.5f, 1f);
        bannerRect.pivot = new Vector2(0.5f, 1f);
        bannerRect.anchoredPosition = new Vector2(0f, -14f);
        bannerRect.sizeDelta = new Vector2(560f, 56f);
        bannerPanelImage = bannerRoot.AddComponent<Image>();
        bannerPanelImage.color = new Color(0.01f, 0.05f, 0.08f, 0.88f);
        bannerText = CreateText(bannerRoot.transform, "BannerText", 17f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.67f), new Vector2(520f, 24f));
        bannerText.color = new Color(0.72f, 0.95f, 1f);
        bannerDetailText = CreateText(bannerRoot.transform, "BannerDetailText", 11f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.28f), new Vector2(520f, 22f));
        bannerDetailText.color = new Color(0.82f, 0.9f, 0.95f);
        bannerDetailText.textWrappingMode = TextWrappingModes.Normal;

        bossRoot = new GameObject("BossEncounterPanel");
        bossRoot.transform.SetParent(canvas.transform, false);
        RectTransform bossRect = bossRoot.AddComponent<RectTransform>();
        bossRect.anchorMin = new Vector2(0.5f, 1f);
        bossRect.anchorMax = new Vector2(0.5f, 1f);
        bossRect.pivot = new Vector2(0.5f, 1f);
        bossRect.anchoredPosition = new Vector2(0f, -62f);
        bossRect.sizeDelta = new Vector2(560f, 64f);
        bossPanelImage = bossRoot.AddComponent<Image>();
        bossPanelImage.color = new Color(0.05f, 0.02f, 0.03f, 0.88f);

        bossNameText = CreateText(bossRoot.transform, "BossNameText", 19f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.73f), new Vector2(510f, 26f));
        bossNameText.color = new Color(1f, 0.82f, 0.7f);
        bossPhaseText = CreateText(bossRoot.transform, "BossPhaseText", 12f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(510f, 20f));
        bossPhaseText.color = new Color(0.86f, 0.92f, 0.96f);
        bossHealthValueText = CreateText(bossRoot.transform, "BossHealthValueText", 11f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.28f), new Vector2(510f, 18f));
        bossHealthValueText.color = new Color(0.98f, 0.88f, 0.76f);

        GameObject back = new GameObject("BossHealthBack");
        back.transform.SetParent(bossRoot.transform, false);
        RectTransform backRect = back.AddComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.07f, 0.06f);
        backRect.anchorMax = new Vector2(0.93f, 0.23f);
        backRect.offsetMin = Vector2.zero;
        backRect.offsetMax = Vector2.zero;
        bossHealthBack = back.AddComponent<Image>();
        bossHealthBack.color = new Color(0.08f, 0.03f, 0.04f, 1f);

        GameObject fill = new GameObject("BossHealthFill");
        fill.transform.SetParent(back.transform, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        bossHealthFill = fill.AddComponent<Image>();
        bossHealthFill.type = Image.Type.Filled;
        bossHealthFill.fillMethod = Image.FillMethod.Horizontal;
        bossHealthFill.fillAmount = 1f;
        bossHealthFill.color = new Color(1f, 0.35f, 0.18f, 1f);
    }

    private TMP_Text CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment, Vector2 anchor, Vector2 boxSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = boxSize;

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        ProjectStructureUIRoot.ApplyDefaultFont(text);
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private void SetBannerVisible(bool visible)
    {
        if (bannerRoot != null)
        {
            bannerRoot.SetActive(visible);
            if (visible)
                ProjectStructureUIRoot.BringToFront(bannerRoot.transform);
        }
    }

    private void ShowEncounterBanner(string title, string detail, Color panelColor, float duration)
    {
        if (bannerText == null) return;

        bannerText.text = title;
        if (bannerDetailText != null)
            bannerDetailText.text = detail;
        if (bannerPanelImage != null)
            bannerPanelImage.color = panelColor;

        bannerTimer = duration;
        SetBannerVisible(true);
    }

    public void ShowShopServiceBanner(CybergrindShopStation.ShopService service, string title, string detail)
    {
        Color color = service switch
        {
            CybergrindShopStation.ShopService.Repair => new Color(0.05f, 0.14f, 0.10f, 0.93f),
            CybergrindShopStation.ShopService.Refit => new Color(0.05f, 0.08f, 0.16f, 0.93f),
            CybergrindShopStation.ShopService.Overclock => new Color(0.16f, 0.09f, 0.03f, 0.95f),
            _ => new Color(0.08f, 0.05f, 0.15f, 0.93f)
        };

        ShowEncounterBanner(title, detail, color, bannerDuration + 0.2f);
    }

    public void ShowSystemBanner(string title, string detail, Color panelColor, float duration = -1f)
    {
        ShowEncounterBanner(title, detail, panelColor, duration > 0f ? duration : bannerDuration);
    }

    private void SetBossVisible(bool visible)
    {
        if (bossRoot != null)
        {
            bossRoot.SetActive(visible);
            if (visible)
                ProjectStructureUIRoot.BringToFront(bossRoot.transform);
        }
    }

    private string BuildBossPhaseText(BasicEnemyAI boss)
    {
        if (boss == null) return string.Empty;

        string pattern = boss.bossArchetype switch
        {
            BasicEnemyAI.BossArchetype.Warden => "Ring lock / crossfire",
            BasicEnemyAI.BossArchetype.Striker => "Dash chain / impact",
            BasicEnemyAI.BossArchetype.Sentinel => "Dive run / strafe volley",
            _ => "Threat pattern"
        };

        string phase = boss.BossPhase switch
        {
            2 => "PHASE III",
            1 => "PHASE II",
            _ => "PHASE I"
        };

        return $"{phase}  {Mathf.RoundToInt(boss.Health01 * 100f)}%  {pattern.ToUpperInvariant()}";
    }

    private Color GetBossColor(BasicEnemyAI boss, float health01)
    {
        Color baseColor = boss != null ? boss.bossArchetype switch
        {
            BasicEnemyAI.BossArchetype.Warden => new Color(1f, 0.48f, 0.14f, 1f),
            BasicEnemyAI.BossArchetype.Striker => new Color(1f, 0.22f, 0.18f, 1f),
            BasicEnemyAI.BossArchetype.Sentinel => new Color(0.36f, 0.88f, 1f, 1f),
            _ => new Color(1f, 0.35f, 0.18f, 1f)
        } : new Color(1f, 0.35f, 0.18f, 1f);

        if (health01 < 0.34f)
        {
            float pulse = 0.75f + Mathf.Sin(Time.time * 8f) * 0.25f;
            return Color.Lerp(baseColor, Color.white, pulse * 0.45f);
        }

        return baseColor;
    }

    private Color GetBannerColorForBoss(BasicEnemyAI boss)
    {
        if (boss == null)
            return new Color(0.11f, 0.03f, 0.04f, 0.92f);

        return boss.bossArchetype switch
        {
            BasicEnemyAI.BossArchetype.Warden => new Color(0.16f, 0.07f, 0.02f, 0.92f),
            BasicEnemyAI.BossArchetype.Striker => new Color(0.16f, 0.03f, 0.03f, 0.92f),
            BasicEnemyAI.BossArchetype.Sentinel => new Color(0.03f, 0.09f, 0.13f, 0.92f),
            _ => new Color(0.11f, 0.03f, 0.04f, 0.92f)
        };
    }

    private string BuildBossArchetypeDetail(BasicEnemyAI boss)
    {
        if (boss == null) return "Pattern shift incoming.";

        return boss.bossArchetype switch
        {
            BasicEnemyAI.BossArchetype.Warden => "Expanding rings and crossfire.",
            BasicEnemyAI.BossArchetype.Striker => "Dash chains and slam attacks.",
            BasicEnemyAI.BossArchetype.Sentinel => "Strafe volleys and dive runs.",
            _ => "Pattern shift incoming."
        };
    }
}
