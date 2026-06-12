using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopPreviewHUD : MonoBehaviour
{
    [Min(0.05f)] public float refreshInterval = 0.08f;

    private PlayerController player;
    private Gun gun;
    private GameObject root;
    private Image panelImage;
    private TMP_Text titleText;
    private TMP_Text detailText;
    private float refreshTimer;
    private Interactable lastInteractable;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        gun = FindAnyObjectByType<Gun>();
        BuildUI();
        SetVisible(false);
    }

    private void Update()
    {
        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f) return;

        refreshTimer = refreshInterval;
        RefreshState();
    }

    private void RefreshState()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();
        if (gun == null)
            gun = FindAnyObjectByType<Gun>();
        if (player == null || root == null) return;

        if (player.isUIActive || player.isDead)
        {
            SetVisible(false);
            return;
        }

        Interactable interactable = player.FocusedInteractable;
        if (interactable == null)
        {
            lastInteractable = null;
            SetVisible(false);
            return;
        }

        string title = string.Empty;
        string detail = string.Empty;
        Color accent = new Color(0.08f, 0.12f, 0.16f, 1f);

        if (interactable is CybergrindShopStation || interactable is CybergrindWeaponShop)
        {
            lastInteractable = null;
            SetVisible(false);
            return;
        }
        else
        {
            lastInteractable = null;
            SetVisible(false);
            return;
        }

        if (titleText != null)
            titleText.text = title;
        if (detailText != null)
            detailText.text = detail;
        if (panelImage != null)
            panelImage.color = Color.Lerp(new Color(0.025f, 0.04f, 0.055f, 0.9f), accent * new Color(1f, 1f, 1f, 0.92f), 0.2f);

        if (interactable != lastInteractable)
            ProjectStructureUIRoot.BringToFront(root.transform);

        lastInteractable = interactable;
        SetVisible(true);
    }

    private void BuildUI()
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;

        root = new GameObject("ShopPreviewHUD");
        root.transform.SetParent(canvas.transform, false);
        RectTransform rect = root.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-14f, 14f);
        rect.sizeDelta = new Vector2(268f, 82f);

        panelImage = root.AddComponent<Image>();
        panelImage.color = new Color(0.025f, 0.04f, 0.055f, 0.9f);

        titleText = CreateText(root.transform, "ShopPreviewTitle", 11.5f, new Vector2(0.5f, 0.74f), new Vector2(238f, 18f), TextAlignmentOptions.Center, Color.white);
        detailText = CreateText(root.transform, "ShopPreviewDetail", 8.5f, new Vector2(0.5f, 0.38f), new Vector2(238f, 44f), TextAlignmentOptions.Center, new Color(0.84f, 0.9f, 0.96f));
    }

    private TMP_Text CreateText(Transform parent, string name, float size, Vector2 anchor, Vector2 boxSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = boxSize;

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private void SetVisible(bool visible)
    {
        if (root != null)
            root.SetActive(visible);
    }
}
