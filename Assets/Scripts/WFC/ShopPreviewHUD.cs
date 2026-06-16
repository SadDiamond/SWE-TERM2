using UnityEngine;

public class ShopPreviewHUD : MonoBehaviour
{
    [Min(0.05f)] public float refreshInterval = 0.08f;

    private PlayerController player;
    private GameObject root;
    private float refreshTimer;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
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
        if (player == null || root == null) return;

        if (player.isUIActive || player.isDead)
        {
            SetVisible(false);
            return;
        }

        SetVisible(false);
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
    }

    private void SetVisible(bool visible)
    {
        if (root != null)
            root.SetActive(visible);
    }
}
