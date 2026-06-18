using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopPreviewHUD : MonoBehaviour
{
    private static ShopPreviewHUD instance;
    [Min(0.05f)] public float refreshInterval = 0.08f;

    private PlayerController player;
    private GameObject root;
    private Image panel;
    private Image accentBar;
    private TMP_Text titleText;
    private TMP_Text detailText;
    private TMP_Text actionText;
    private TMP_Text balanceText;
    private Image affordabilityMarker;
    private float refreshTimer;

    private void Start()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        player = FindAnyObjectByType<PlayerController>();
        BuildUI();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
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
        if (player == null || root == null) return;

        if (player.isUIActive || player.isDead)
        {
            SetVisible(false);
            return;
        }

        Interactable focused = player.FocusedInteractable;
        CybergrindShopStation station = focused as CybergrindShopStation;
        if (station == null)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        Color accent = station.GetPreviewAccent();
        panel.color = new Color(0.025f, 0.03f, 0.038f, 0.96f);
        accentBar.color = accent;
        titleText.text = station.GetPreviewTitle(player);
        titleText.color = accent;
        detailText.text = station.GetPreviewDetail(player);
        bool affordable = player.currency >= station.cost;
        actionText.text = affordable ? "Press E to confirm" : $"Requires {station.cost - player.currency} more coins";
        actionText.color = affordable ? new Color(0.9f, 0.94f, 0.97f) : new Color(1f, 0.48f, 0.4f);
        balanceText.text = $"Balance  {player.currency}";
        affordabilityMarker.color = affordable ? accent : new Color(0.95f, 0.28f, 0.22f, 1f);
    }

    private void BuildUI()
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;

        root = new GameObject("ShopPreviewHUD");
        root.transform.SetParent(canvas.transform, false);
        RectTransform rect = root.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-32f, 0f);
        rect.sizeDelta = new Vector2(440f, 164f);

        panel = root.AddComponent<Image>();
        panel.color = new Color(0.018f, 0.028f, 0.038f, 0.97f);
        panel.raycastTarget = false;

        GameObject accentObject = new GameObject("AccentBar");
        accentObject.transform.SetParent(root.transform, false);
        RectTransform accentRect = accentObject.AddComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(4f, 0f);
        accentBar = accentObject.AddComponent<Image>();
        accentBar.raycastTarget = false;

        GameObject headerObject = new GameObject("HeaderBand");
        headerObject.transform.SetParent(root.transform, false);
        RectTransform headerRect = headerObject.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0f, 42f);
        Image headerImage = headerObject.AddComponent<Image>();
        headerImage.color = new Color(0.055f, 0.065f, 0.078f, 0.98f);
        headerImage.raycastTarget = false;

        GameObject markerObject = new GameObject("AffordabilityMarker");
        markerObject.transform.SetParent(root.transform, false);
        RectTransform markerRect = markerObject.AddComponent<RectTransform>();
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(1f, 1f);
        markerRect.pivot = new Vector2(1f, 1f);
        markerRect.anchoredPosition = new Vector2(-16f, -15f);
        markerRect.sizeDelta = new Vector2(10f, 10f);
        affordabilityMarker = markerObject.AddComponent<Image>();
        affordabilityMarker.raycastTarget = false;

        titleText = CreateText(root.transform, "ServiceTitle", 17f, new Vector2(0f, 1f), new Vector2(280f, 26f), new Vector2(24f, -10f), TextAlignmentOptions.TopLeft);
        balanceText = CreateText(root.transform, "Balance", 11f, new Vector2(1f, 1f), new Vector2(110f, 22f), new Vector2(-38f, -12f), TextAlignmentOptions.TopRight);
        detailText = CreateText(root.transform, "ServiceDetail", 12f, new Vector2(0f, 1f), new Vector2(390f, 68f), new Vector2(24f, -54f), TextAlignmentOptions.TopLeft);
        detailText.color = new Color(0.8f, 0.84f, 0.88f);
        actionText = CreateText(root.transform, "ServiceAction", 12f, new Vector2(0f, 0f), new Vector2(390f, 24f), new Vector2(24f, 15f), TextAlignmentOptions.BottomLeft);
    }

    private TMP_Text CreateText(Transform parent, string name, float size, Vector2 anchor, Vector2 boxSize, Vector2 position, TextAlignmentOptions alignment)
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
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private void SetVisible(bool visible)
    {
        if (root != null)
            root.SetActive(visible);
    }
}
