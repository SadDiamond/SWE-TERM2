using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProjectStructureHintOverlay : MonoBehaviour
{
    private static ProjectStructureHintOverlay instance;
    public PlayerController player;
    public CybergrindArenaDirector arenaDirector;

    [Header("Display")]
    [Min(0.05f)] public float refreshInterval = 0.15f;
    public float hintFadeSpeed = 5f;
    [Min(2f)] public float minimumHintDuration = 6f;

    private float refreshTimer;
    private CanvasGroup hintGroup;
    private TMP_Text hintTitleText;
    private TMP_Text hintBodyText;
    private Image panelImage;
    private string currentHintKey = string.Empty;
    private string currentTitle = string.Empty;
    private string currentBody = string.Empty;
    private float currentHintTimer;
    private Transform cachedArenaRoot;
    private Terminal[] cachedTerminals = System.Array.Empty<Terminal>();

    private bool sawMovementHint;
    private bool sawWeaponHint;
    private bool sawTerminalHint;
    private bool sawRewardHint;
    private bool sawShopHint;
    private bool sawBossHint;

    private void Start()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();
        if (arenaDirector == null)
            arenaDirector = FindAnyObjectByType<CybergrindArenaDirector>();

        BuildUI();
        RefreshHint();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = refreshInterval;
            RefreshHint();
        }

        if (hintGroup == null) return;
        bool suppress = player != null && (player.isUIActive || player.isDead);
        float targetAlpha = string.IsNullOrEmpty(currentHintKey) || suppress ? 0f : 1f;
        hintGroup.alpha = Mathf.Lerp(hintGroup.alpha, targetAlpha, Time.unscaledDeltaTime * hintFadeSpeed);
    }

    private void RefreshHint()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();
        if (arenaDirector == null)
            arenaDirector = FindAnyObjectByType<CybergrindArenaDirector>();

        if (!string.IsNullOrEmpty(currentHintKey))
        {
            currentHintTimer -= refreshInterval;
            if (currentHintTimer > 0f)
                return;

            MarkHintSeen(currentHintKey);
            currentHintKey = string.Empty;
        }

        string nextKey = string.Empty;
        string title = string.Empty;
        string body = string.Empty;
        string sectorLabel = arenaDirector != null ? arenaDirector.CurrentThemeLabel.ToUpperInvariant() : "ARENA";

        CharacterController controller = player != null ? player.GetComponent<CharacterController>() : null;
        float speed = controller != null ? new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude : 0f;

        if (!sawMovementHint && speed < 2f)
        {
            nextKey = "movement";
            title = "GUIDE";
            body = $"Press {ProjectStructureBindings.GetDisplayString(ProjectStructureAction.Guide)} at any time for controls and the run overview.";
        }
        else if (!sawWeaponHint && arenaDirector != null && arenaDirector.floor <= 2)
        {
            nextKey = "weapons";
            title = "WEAPONS";
            body = "Shoot with LMB. RMB uses the weapon ability; the pistol throws coins.";
        }
        else if (!sawTerminalHint && CountUnsolvedTerminals() > 0)
        {
            nextKey = "terminal";
            title = $"{sectorLabel} / TERMINALS";
            body = "Find a marked terminal, face it and press Interact. Complete it before leaving.";
        }
        else if (!sawRewardHint && arenaDirector != null && arenaDirector.HasPendingReward())
        {
            nextKey = "reward";
            title = "NEW GUN";
            body = "Pick up the weapon before leaving.";
        }
        else if (!sawShopHint && arenaDirector != null && arenaDirector.generator != null &&
                 arenaDirector.generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Shop)
        {
            nextKey = "shop";
            title = $"{sectorLabel} / SHOP";
            body = "Buy one item before moving to the next floor.";
        }
        else if (!sawBossHint && arenaDirector != null && arenaDirector.generator != null &&
                 arenaDirector.generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Boss)
        {
            nextKey = "boss";
            title = $"{sectorLabel} / BOSS";
            body = "Keep moving and attack after its move.";
        }

        if (nextKey == currentHintKey) return;

        currentHintKey = nextKey;
        currentHintTimer = string.IsNullOrEmpty(nextKey) ? 0f : minimumHintDuration;
        currentTitle = title;
        currentBody = body;
        if (hintTitleText != null) hintTitleText.text = currentTitle;
        if (hintBodyText != null) hintBodyText.text = currentBody;

        if (!string.IsNullOrEmpty(nextKey))
            ApplyHintVisual(nextKey);
    }

    private void MarkHintSeen(string hintKey)
    {
        switch (hintKey)
        {
            case "movement": sawMovementHint = true; break;
            case "weapons": sawWeaponHint = true; break;
            case "terminal": sawTerminalHint = true; break;
            case "reward": sawRewardHint = true; break;
            case "shop": sawShopHint = true; break;
            case "boss": sawBossHint = true; break;
        }
    }

    private int CountUnsolvedTerminals()
    {
        Transform root = arenaDirector != null && arenaDirector.generator != null ? arenaDirector.generator.CurrentArenaRoot : null;
        if (root != cachedArenaRoot)
        {
            cachedArenaRoot = root;
            cachedTerminals = root != null
                ? root.GetComponentsInChildren<Terminal>(true)
                : System.Array.Empty<Terminal>();
        }

        int unsolved = 0;
        for (int i = 0; i < cachedTerminals.Length; i++)
        {
            Terminal terminal = cachedTerminals[i];
            if (terminal == null || terminal.isSolved) continue;
            if (!terminal.name.StartsWith("PuzzleTerminal")) continue;
            unsolved++;
        }

        return unsolved;
    }

    private void BuildUI()
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;

        GameObject root = new GameObject("ProjectStructureHintOverlay");
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(0f, 0f);
        rootRect.pivot = new Vector2(0f, 0f);
        rootRect.anchoredPosition = new Vector2(18f, 18f);
        rootRect.sizeDelta = new Vector2(390f, 108f);

        Image panel = root.AddComponent<Image>();
        panel.color = new Color(0.018f, 0.03f, 0.038f, 0.92f);
        panelImage = panel;
        hintGroup = root.AddComponent<CanvasGroup>();
        hintGroup.alpha = 0f;

        GameObject accent = new GameObject("Accent");
        accent.transform.SetParent(root.transform, false);
        RectTransform accentRect = accent.AddComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(4f, 0f);
        Image accentImage = accent.AddComponent<Image>();
        accentImage.color = new Color(0.36f, 0.88f, 1f, 0.95f);

        hintTitleText = CreateText(root.transform, "HintTitle", 15f, new Vector2(0f, 1f), new Vector2(342f, 24f), new Vector2(22f, -14f), TextAlignmentOptions.TopLeft, Color.white);
        hintBodyText = CreateText(root.transform, "HintBody", 12.5f, new Vector2(0f, 1f), new Vector2(342f, 58f), new Vector2(22f, -42f), TextAlignmentOptions.TopLeft, new Color(0.82f, 0.9f, 0.96f));
    }

    private TMP_Text CreateText(Transform parent, string name, float fontSize, Vector2 anchor, Vector2 boxSize, Vector2 position, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = boxSize;

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        ProjectStructureUIRoot.ApplyDefaultFont(text);
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private void ApplyHintVisual(string hintKey)
    {
        Color accent = hintKey switch
        {
            "movement" => new Color(0.18f, 0.82f, 1f, 0.94f),
            "weapons" => new Color(0.38f, 0.72f, 1f, 0.94f),
            "terminal" => new Color(0.86f, 0.76f, 0.24f, 0.94f),
            "reward" => new Color(1f, 0.62f, 0.2f, 0.94f),
            "shop" => new Color(0.24f, 0.9f, 0.7f, 0.94f),
            "boss" => new Color(1f, 0.4f, 0.24f, 0.96f),
            _ => ResolveSectorAccent()
        };

        if (panelImage != null)
            panelImage.color = Color.Lerp(new Color(0.02f, 0.045f, 0.06f, 0.9f), accent * new Color(1f, 1f, 1f, 0.92f), 0.22f);
        if (hintTitleText != null)
            hintTitleText.color = Color.Lerp(Color.white, accent, 0.28f);
    }

    private Color ResolveSectorAccent()
    {
        int themeIndex = arenaDirector != null ? arenaDirector.CurrentThemeIndex : 0;
        switch (Math.Abs(themeIndex) % 4)
        {
            case 1 : return new Color(0.34f, 0.62f, 1f, 1f);
            case 2 : return new Color(1f, 0.56f, 0.22f, 1f);
            case 3 : return new Color(0.42f, 0.94f, 0.56f, 1f);
            default : return new Color(0.36f, 0.88f, 1f, 1f);
        }
    }
}
