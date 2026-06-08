using System;
using UnityEngine;
using UnityEngine.UI;

public class ProjectStructureAtmosphereHUD : MonoBehaviour
{
    public PlayerController player;
    public CybergrindArenaDirector arenaDirector;

    [Header("Refresh")]
    [Min(0.02f)] public float refreshInterval = 0.05f;

    private float refreshTimer;
    private Image vignetteOverlay;
    private Image scanlineOverlay;
    private Image modeTintOverlay;
    private Texture2D scanlineTexture;

    private void Start()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();
        if (arenaDirector == null)
            arenaDirector = FindAnyObjectByType<CybergrindArenaDirector>();

        EnsureOverlay();
        RefreshOverlay();
    }

    private void Update()
    {
        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f) return;

        refreshTimer = refreshInterval;
        RefreshOverlay();
    }

    private void EnsureOverlay()
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;

        vignetteOverlay = EnsureImage(canvas.transform, "ProjectStructureVignette", 0);
        modeTintOverlay = EnsureImage(canvas.transform, "ProjectStructureModeTint", 1);
        scanlineOverlay = EnsureImage(canvas.transform, "ProjectStructureScanlines", 2);

        if (scanlineTexture == null)
            scanlineTexture = BuildScanlineTexture();
        if (scanlineOverlay != null && scanlineTexture != null)
            scanlineOverlay.sprite = Sprite.Create(scanlineTexture, new Rect(0f, 0f, scanlineTexture.width, scanlineTexture.height), new Vector2(0.5f, 0.5f));
    }

    private Image EnsureImage(Transform parent, string name, int siblingIndex)
    {
        Transform existing = parent.Find(name);
        Image image;
        if (existing == null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image = go.AddComponent<Image>();
            image.raycastTarget = false;
        }
        else
        {
            image = existing.GetComponent<Image>();
        }

        if (image != null)
            image.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, parent.childCount - 1));

        return image;
    }

    private void RefreshOverlay()
    {
        if (vignetteOverlay == null || scanlineOverlay == null || modeTintOverlay == null)
            return;

        float health01 = 1f;
        float speed01 = 0f;
        bool isBoss = false;
        bool isShop = false;
        int themeIndex = 0;

        if (player != null)
        {
            health01 = player.Health01;
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                Vector3 planar = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
                speed01 = Mathf.InverseLerp(player.overdriveSpeedThreshold, player.maxSpeedLimit, planar.magnitude);
            }
        }

        if (arenaDirector != null && arenaDirector.generator != null)
        {
            isBoss = arenaDirector.generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Boss;
            isShop = arenaDirector.generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Shop;
            themeIndex = arenaDirector.CurrentThemeIndex;
        }

        float lowHealth = 1f - health01;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (isBoss ? 5.4f : 2.8f));
        float vignetteAlpha = 0.12f + lowHealth * 0.34f + speed01 * 0.05f + (isBoss ? 0.07f : 0f);
        vignetteOverlay.color = new Color(0.02f, 0.03f, 0.04f, Mathf.Clamp01(vignetteAlpha));

        Color sectorTint = ResolveSectorTint(themeIndex);
        Color tint = isBoss
            ? Color.Lerp(new Color(0.22f, 0.05f, 0.04f, 0.05f + pulse * 0.06f), new Color(sectorTint.r, sectorTint.g, sectorTint.b, 0.08f + pulse * 0.05f), 0.45f)
            : isShop
                ? Color.Lerp(new Color(0.03f, 0.12f, 0.1f, 0.05f), new Color(sectorTint.r, sectorTint.g, sectorTint.b, 0.05f), 0.5f)
                : new Color(sectorTint.r, sectorTint.g, sectorTint.b, 0.03f + speed01 * 0.04f);
        modeTintOverlay.color = tint;

        float scanAlpha = lowHealth > 0.72f ? Mathf.Lerp(0f, 0.008f, Mathf.InverseLerp(0.72f, 1f, lowHealth)) : 0f;
        scanlineOverlay.color = new Color(0.7f, 0.86f, 0.92f, Mathf.Clamp01(scanAlpha));
        RectTransform rect = scanlineOverlay.rectTransform;
        if (rect != null)
            rect.anchoredPosition = new Vector2(0f, Mathf.Repeat(Time.unscaledTime * 3f, 24f));
    }

    private Texture2D BuildScanlineTexture()
    {
        const int width = 8;
        const int height = 64;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point,
            name = "ProjectStructureScanlineTexture"
        };

        for (int y = 0; y < height; y++)
        {
            float alpha = y % 4 == 0 ? 0.35f : (y % 2 == 0 ? 0.12f : 0.03f);
            Color color = new Color(1f, 1f, 1f, alpha);
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, color);
        }

        texture.Apply();
        return texture;
    }

    private Color ResolveSectorTint(int themeIndex)
    {
        switch (Math.Abs(themeIndex) % 4)
        {
            case 1: return new Color(0.05f, 0.13f, 0.24f);
            case 2: return new Color(0.18f, 0.08f, 0.04f);
            case 3: return new Color(0.05f, 0.15f, 0.08f);
            default: return new Color(0.02f, 0.07f, 0.1f);
        }
    }
}
