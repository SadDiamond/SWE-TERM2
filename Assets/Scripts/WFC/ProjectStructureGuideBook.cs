using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ProjectStructureGuideBook : MonoBehaviour
{
    public static bool HasBeenOpenedThisSession { get; private set; }

    private PlayerController player;
    private GameObject root;
    private TMP_Text chapterText;
    private TMP_Text leftPageText;
    private TMP_Text rightPageText;
    private TMP_Text footerText;
    private int pageIndex;
    private bool isOpen;
    private float previousTimeScale = 1f;

    private const int PageCount = 4;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        BuildUI();
        SetVisible(false);
    }

    private void Update()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();

        if (ProjectStructureBindings.WasPressedThisFrame(ProjectStructureAction.Guide))
        {
            if (isOpen)
                CloseGuide();
            else if (player == null || !player.isUIActive)
                OpenGuide();
            return;
        }

        if (!isOpen || Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseGuide();
            return;
        }

        if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
        {
            pageIndex = (pageIndex + 1) % PageCount;
            RefreshPage();
        }
        else if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
        {
            pageIndex = (pageIndex - 1 + PageCount) % PageCount;
            RefreshPage();
        }
    }

    private void OnDisable()
    {
        if (isOpen)
            CloseGuide();
    }

    private void OpenGuide()
    {
        if (root == null)
            BuildUI();

        HasBeenOpenedThisSession = true;
        isOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (player != null)
            player.ToggleUIMode(true);
        SetVisible(true);
        RefreshPage();
    }

    private void CloseGuide()
    {
        isOpen = false;
        Time.timeScale = previousTimeScale;
        if (player != null)
            player.ToggleUIMode(false);
        SetVisible(false);
    }

    private void BuildUI()
    {
        if (root != null)
            return;

        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null)
            return;

        root = new GameObject("GuideBookOverlay");
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        Image shade = root.AddComponent<Image>();
        shade.color = new Color(0.005f, 0.01f, 0.016f, 0.88f);

        GameObject book = new GameObject("Book");
        book.transform.SetParent(root.transform, false);
        RectTransform bookRect = book.AddComponent<RectTransform>();
        bookRect.anchorMin = bookRect.anchorMax = new Vector2(0.5f, 0.5f);
        bookRect.pivot = new Vector2(0.5f, 0.5f);
        bookRect.sizeDelta = new Vector2(1040f, 610f);
        Image bookImage = book.AddComponent<Image>();
        bookImage.color = new Color(0.035f, 0.055f, 0.065f, 0.99f);
        Outline outline = book.AddComponent<Outline>();
        outline.effectColor = new Color(0.28f, 0.82f, 0.94f, 0.42f);
        outline.effectDistance = new Vector2(2f, -2f);

        CreatePanel(book.transform, "LeftPage", new Vector2(0.025f, 0.09f), new Vector2(0.49f, 0.86f));
        CreatePanel(book.transform, "RightPage", new Vector2(0.51f, 0.09f), new Vector2(0.975f, 0.86f));

        GameObject spine = new GameObject("Spine");
        spine.transform.SetParent(book.transform, false);
        RectTransform spineRect = spine.AddComponent<RectTransform>();
        spineRect.anchorMin = new Vector2(0.497f, 0.09f);
        spineRect.anchorMax = new Vector2(0.503f, 0.86f);
        spineRect.offsetMin = spineRect.offsetMax = Vector2.zero;
        spine.AddComponent<Image>().color = new Color(0.2f, 0.72f, 0.82f, 0.24f);

        chapterText = CreateText(book.transform, "Chapter", 30f, new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.97f), TextAlignmentOptions.Center, new Color(0.72f, 0.96f, 1f));
        leftPageText = CreateText(book.transform, "LeftText", 19f, new Vector2(0.055f, 0.13f), new Vector2(0.46f, 0.81f), TextAlignmentOptions.TopLeft, Color.white);
        rightPageText = CreateText(book.transform, "RightText", 19f, new Vector2(0.54f, 0.13f), new Vector2(0.945f, 0.81f), TextAlignmentOptions.TopLeft, Color.white);
        footerText = CreateText(book.transform, "Footer", 15f, new Vector2(0.05f, 0.015f), new Vector2(0.95f, 0.075f), TextAlignmentOptions.Center, new Color(0.62f, 0.76f, 0.82f));
    }

    private void CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0.055f, 0.075f, 0.08f, 0.96f);
    }

    private TMP_Text CreateText(Transform parent, string name, float size, Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(8f, 8f);
        rect.offsetMax = new Vector2(-8f, -8f);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        ProjectStructureUIRoot.ApplyDefaultFont(text);
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private void RefreshPage()
    {
        if (chapterText == null)
            return;

        switch (pageIndex)
        {
            case 0:
                chapterText.text = "THE RUN";
                leftPageText.text = "COMBAT FLOORS\n\nClear every enemy.\n\nFind each marked terminal and complete its short input challenge.\n\nWhen both tasks are finished, collect the weapon reward at the marked exit.";
                rightPageText.text = "FLOOR ORDER\n\n2 combat floors\nShop\n2 combat floors\nBoss\n\nThe shop allows one purchase. Boss floors skip terminals. Follow the objective at the top of the HUD.";
                break;
            case 1:
                chapterText.text = "MOVEMENT";
                leftPageText.text = $"MOVE\n{Binding(ProjectStructureAction.MoveForward)} {Binding(ProjectStructureAction.MoveLeft)} {Binding(ProjectStructureAction.MoveBackward)} {Binding(ProjectStructureAction.MoveRight)}\n\nJUMP\n{Binding(ProjectStructureAction.Jump)}\n\nDASH\n{Binding(ProjectStructureAction.Dash)}\n\nSLIDE / SLAM\n{Binding(ProjectStructureAction.Slide)}";
                rightPageText.text = $"GRAPPLE\n{Binding(ProjectStructureAction.Grapple)}\n\nHold to launch the hook and release to pull. Keep moving to carry momentum through the swing.\n\nSlide on the ground. Press slide in the air to slam. Jumping from a slide carries more speed.";
                break;
            case 2:
                chapterText.text = "COMBAT";
                leftPageText.text = $"FIRE\n{Binding(ProjectStructureAction.Fire)}\n\nWEAPON ABILITY\n{Binding(ProjectStructureAction.AltFire)}\n\nWEAPON SLOTS\n{Binding(ProjectStructureAction.WeaponSlot1)} / {Binding(ProjectStructureAction.WeaponSlot2)} / {Binding(ProjectStructureAction.WeaponSlot3)}";
                rightPageText.text = $"VARIANTS\n{Binding(ProjectStructureAction.VariantPrev)} / {Binding(ProjectStructureAction.VariantNext)}\n\nThe pistol ability throws coins. Shoot a coin to redirect the shot. Nearby coins are chained before the shot selects an enemy.\n\nNew guns and mods only last for the current run.";
                break;
            default:
                chapterText.text = "OBJECTIVES";
                leftPageText.text = $"INTERACT\n{Binding(ProjectStructureAction.Interact)}\n\nFace a terminal, shop station, reward or exit and use Interact.\n\nTerminal screens only show the controls needed for that challenge. A bright timing bar means act now.";
                rightPageText.text = "HUD\n\nThe objective is shown at the top.\nHealth, speed, coins and dash charges are shown near the bottom.\n\nThe last two enemies are marked through walls. Rewards and exits are marked after the floor is cleared.";
                break;
        }

        footerText.text = $"A / D or LEFT / RIGHT turn page    {Binding(ProjectStructureAction.Guide)} close    ESC close    {pageIndex + 1}/{PageCount}";
    }

    private string Binding(ProjectStructureAction action)
    {
        return ProjectStructureBindings.GetDisplayString(action).ToUpperInvariant();
    }

    private void SetVisible(bool visible)
    {
        if (root == null)
            return;
        root.SetActive(visible);
        if (visible)
            ProjectStructureUIRoot.BringToFront(root.transform);
    }
}
