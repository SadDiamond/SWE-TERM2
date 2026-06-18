using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyPriorityHUD : MonoBehaviour
{
    private static EnemyPriorityHUD instance;
    [Min(0.05f)] public float refreshInterval = 0.08f;
    public float edgePadding = 46f;

    private readonly List<MarkerWidget> widgets = new List<MarkerWidget>();
    private float refreshTimer;
    private Camera targetCamera;
    private CybergrindArenaDirector arenaDirector;
    private Transform cachedArenaRoot;
    private BasicEnemyAI[] cachedEnemies = System.Array.Empty<BasicEnemyAI>();

    private sealed class MarkerWidget
    {
        public GameObject root;
        public RectTransform rect;
        public Image diamond;
        public TMP_Text label;
    }

    private void Start()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        targetCamera = Camera.main;
        arenaDirector = FindAnyObjectByType<CybergrindArenaDirector>();
        BuildWidgets(2);
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;

        refreshTimer = refreshInterval;
        RefreshMarkers();
    }

    private void RefreshMarkers()
    {
        RefreshEnemyCache();
        int widgetIndex = 0;
        for (int i = 0; i < cachedEnemies.Length; i++)
        {
            BasicEnemyAI enemy = cachedEnemies[i];
            if (enemy == null || enemy.IsCombatResolved || !enemy.gameObject.activeInHierarchy)
                continue;

            if (!enemy.name.Contains("Enemy") && !enemy.name.Contains("Boss"))
                continue;

            if (!enemy.IsPriorityTarget)
                continue;

            if (widgetIndex >= widgets.Count)
                break;

            UpdateWidget(widgets[widgetIndex], enemy);
            widgetIndex++;
        }

        for (int i = widgetIndex; i < widgets.Count; i++)
            widgets[i].root.SetActive(false);
    }

    private void RefreshEnemyCache()
    {
        if (arenaDirector == null)
            arenaDirector = FindAnyObjectByType<CybergrindArenaDirector>();

        Transform root = arenaDirector != null && arenaDirector.generator != null
            ? arenaDirector.generator.CurrentArenaRoot
            : null;
        if (root == cachedArenaRoot)
            return;

        cachedArenaRoot = root;
        cachedEnemies = root != null
            ? root.GetComponentsInChildren<BasicEnemyAI>(true)
            : System.Array.Empty<BasicEnemyAI>();
    }

    private void UpdateWidget(MarkerWidget widget, BasicEnemyAI enemy)
    {
        if (widget == null || enemy == null || targetCamera == null)
            return;

        Vector3 world = enemy.transform.position + Vector3.up * 2.8f;
        Vector3 viewport = targetCamera.WorldToViewportPoint(world);
        bool behind = viewport.z < 0f;
        if (behind)
        {
            viewport.x = 1f - viewport.x;
            viewport.y = 1f - viewport.y;
            viewport.z = 0f;
        }

        viewport.x = Mathf.Clamp01(viewport.x);
        viewport.y = Mathf.Clamp01(viewport.y);

        RectTransform canvasRect = widget.rect.parent as RectTransform;
        Vector2 size = canvasRect != null ? canvasRect.rect.size : new Vector2(Screen.width, Screen.height);
        Vector2 pos = new Vector2(
            Mathf.Lerp(edgePadding, size.x - edgePadding, viewport.x),
            Mathf.Lerp(edgePadding, size.y - edgePadding, viewport.y));

        widget.rect.anchoredPosition = pos;
        widget.diamond.color = behind ? new Color(1f, 0.78f, 0.28f, 0.98f) : new Color(1f, 0.9f, 0.44f, 0.98f);
        widget.label.text = "LAST";
        widget.root.SetActive(true);
    }

    private void BuildWidgets(int count)
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;

        for (int i = 0; i < count; i++)
        {
            GameObject root = new GameObject($"EnemyPriorityMarker_{i}");
            root.transform.SetParent(canvas.transform, false);
            RectTransform rect = root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(88f, 36f);

            GameObject diamondGo = new GameObject("Diamond");
            diamondGo.transform.SetParent(root.transform, false);
            RectTransform diamondRect = diamondGo.AddComponent<RectTransform>();
            diamondRect.anchorMin = new Vector2(0.5f, 0.5f);
            diamondRect.anchorMax = new Vector2(0.5f, 0.5f);
            diamondRect.sizeDelta = new Vector2(22f, 22f);
            diamondRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image diamond = diamondGo.AddComponent<Image>();
            diamond.color = new Color(1f, 0.9f, 0.44f, 0.98f);

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform, false);
            RectTransform labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, 18f);
            labelRect.sizeDelta = new Vector2(72f, 24f);
            TMP_Text label = labelGo.AddComponent<TextMeshProUGUI>();
            ProjectStructureUIRoot.ApplyDefaultFont(label);
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 16f;
            label.color = new Color(1f, 0.95f, 0.76f, 0.98f);

            widgets.Add(new MarkerWidget
            {
                root = root,
                rect = rect,
                diamond = diamond,
                label = label
            });
        }
    }

    private void SetVisible(bool visible)
    {
        for (int i = 0; i < widgets.Count; i++)
            widgets[i].root.SetActive(visible);
    }
}
