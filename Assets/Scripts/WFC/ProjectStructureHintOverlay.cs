using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProjectStructureHintOverlay : MonoBehaviour
{
    public PlayerController player;
    public CybergrindArenaDirector arenaDirector;

    [Header("Display")]
    [Min(0.05f)] public float refreshInterval = 0.15f;
    public float hintFadeSpeed = 5f;

    private float refreshTimer;
    private CanvasGroup hintGroup;
    private TMP_Text hintTitleText;
    private TMP_Text hintBodyText;
    private Image panelImage;
    private string currentHintKey = string.Empty;
    private string currentTitle = string.Empty;
    private string currentBody = string.Empty;

    private bool sawMovementHint;
    private bool sawWeaponHint;
    private bool sawTerminalHint;
    private bool sawRewardHint;
    private bool sawShopHint;
    private bool sawBossHint;

    private void Start()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();
        if (arenaDirector == null)
            arenaDirector = FindAnyObjectByType<CybergrindArenaDirector>();

        BuildUI();
        RefreshHint();
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
        float targetAlpha = string.IsNullOrEmpty(currentHintKey) ? 0f : 1f;
        hintGroup.alpha = Mathf.Lerp(hintGroup.alpha, targetAlpha, Time.unscaledDeltaTime * hintFadeSpeed);
    }

    private void RefreshHint()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();
        if (arenaDirector == null)
            arenaDirector = FindAnyObjectByType<CybergrindArenaDirector>();

        string nextKey = string.Empty;
        string title = string.Empty;
        string body = string.Empty;
        string sectorLabel = arenaDirector != null ? arenaDirector.CurrentThemeLabel.ToUpperInvariant() : "SECTOR";
        string directiveTitle = arenaDirector != null ? arenaDirector.CurrentDirectiveTitle : "Directive";

        CharacterController controller = player != null ? player.GetComponent<CharacterController>() : null;
        float speed = controller != null ? new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude : 0f;

        if (!sawMovementHint && speed < 2f)
        {
            nextKey = "movement";
            title = "MOVE AGGRESSIVELY";
            body = "Dash with SHIFT, slide with CTRL/C, and keep momentum alive. This arena reads better once you move like you belong in it.";
        }
        else if (!sawWeaponHint && arenaDirector != null && arenaDirector.floor <= 2)
        {
            nextKey = "weapons";
            title = "VARIANT BUS";
            body = "1/2 swaps weapon family. Q/E cycles variants. Right click is not optional here; every variant has a special that changes how it solves a fight.";
        }
        else if (!sawTerminalHint && CountUnsolvedTerminals() > 0)
        {
            nextKey = "terminal";
            title = $"{sectorLabel} // MACHINE LOCK";
            body = $"{directiveTitle} is live here. Solve terminals under pressure, then collapse the remaining enemies to open the route forward.";
        }
        else if (!sawRewardHint && arenaDirector != null && arenaDirector.HasPendingReward())
        {
            nextKey = "reward";
            title = "CLAIM THE VARIANT";
            body = "Weapon rewards are floor milestones. Claim the pickup before you step into the exit so the run actually grows stronger.";
        }
        else if (!sawShopHint && arenaDirector != null && arenaDirector.generator != null &&
                 arenaDirector.generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Shop)
        {
            nextKey = "shop";
            title = $"{sectorLabel} // INTERCHANGE";
            body = $"{directiveTitle} stays in effect between floors. Use at least one station: Refit changes variant, Overclock buffs the weapon bus, Surge tunes movement, and Repair patches the hull.";
        }
        else if (!sawBossHint && arenaDirector != null && arenaDirector.generator != null &&
                 arenaDirector.generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Boss)
        {
            nextKey = "boss";
            title = $"{sectorLabel} // CHAMPION CHAMBER";
            body = "Bosses are pattern fights, not stat checks. Read the phase text, stay mobile, and punish the gaps after telegraphed bursts.";
        }

        if (nextKey == currentHintKey) return;

        currentHintKey = nextKey;
        currentTitle = title;
        currentBody = body;
        if (hintTitleText != null) hintTitleText.text = currentTitle;
        if (hintBodyText != null) hintBodyText.text = currentBody;

        if (string.IsNullOrEmpty(nextKey)) return;
        ApplyHintVisual(nextKey);
        switch (nextKey)
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
        Terminal[] terminals = root != null
            ? root.GetComponentsInChildren<Terminal>(true)
            : FindObjectsByType<Terminal>();

        int unsolved = 0;
        for (int i = 0; i < terminals.Length; i++)
        {
            Terminal terminal = terminals[i];
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
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(1f, 0f);
        rootRect.anchoredPosition = new Vector2(-18f, 22f);
        rootRect.sizeDelta = new Vector2(430f, 150f);

        Image panel = root.AddComponent<Image>();
        panel.color = new Color(0.02f, 0.045f, 0.06f, 0.9f);
        panelImage = panel;
        hintGroup = root.AddComponent<CanvasGroup>();
        hintGroup.alpha = 0f;

        hintTitleText = CreateText(root.transform, "HintTitle", 23f, new Vector2(0.5f, 0.73f), new Vector2(380f, 28f), TextAlignmentOptions.Center, Color.white);
        hintBodyText = CreateText(root.transform, "HintBody", 17f, new Vector2(0.5f, 0.4f), new Vector2(380f, 76f), TextAlignmentOptions.Center, new Color(0.82f, 0.9f, 0.96f));
    }

    private TMP_Text CreateText(Transform parent, string name, float fontSize, Vector2 anchor, Vector2 boxSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = boxSize;

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
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
