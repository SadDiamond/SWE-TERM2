using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatFeedbackHUD : MonoBehaviour
{
    private static CombatFeedbackHUD instance;

    private readonly Image[] hitLines = new Image[4];
    private TMP_Text statusText;
    private Image statusBack;

    private float hitTimer;
    private float killTimer;
    private string queuedStatus = string.Empty;
    private Color currentLineColor = Color.white;

    public static void RegisterHit(float damage, bool kill, Color accent, string targetLabel = "")
    {
        CombatFeedbackHUD hud = GetOrCreate();
        if (hud == null) return;
        hud.PushHit(damage, kill, accent, targetLabel);
    }

    public static CombatFeedbackHUD GetOrCreate()
    {
        if (instance != null) return instance;
        instance = FindAnyObjectByType<CombatFeedbackHUD>();
        if (instance != null)
        {
            instance.EnsureBuilt();
            return instance;
        }

        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return null;

        GameObject root = new GameObject("CombatFeedbackHUD");
        root.transform.SetParent(canvas.transform, false);
        instance = root.AddComponent<CombatFeedbackHUD>();
        instance.EnsureBuilt();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureBuilt();
    }

    private void Update()
    {
        if (hitTimer > 0f)
            hitTimer -= Time.deltaTime;
        if (killTimer > 0f)
            killTimer -= Time.deltaTime;

        float markerAlpha = Mathf.Clamp01(Mathf.Max(hitTimer / 0.12f, killTimer / 0.22f));
        Color markerColor = currentLineColor;
        markerColor.a = markerAlpha;
        for (int i = 0; i < hitLines.Length; i++)
        {
            if (hitLines[i] == null) continue;
            hitLines[i].color = markerColor;
            hitLines[i].enabled = markerAlpha > 0.01f;
        }

        if (statusText != null)
        {
            float statusAlpha = Mathf.Clamp01(killTimer / 0.4f);
            statusText.text = statusAlpha > 0.01f ? queuedStatus : string.Empty;
            Color textColor = new Color(1f, 0.89f, 0.7f, statusAlpha);
            statusText.color = textColor;
            if (statusBack != null)
            {
                statusBack.color = new Color(0.06f, 0.04f, 0.02f, statusAlpha * 0.72f);
                statusBack.enabled = statusAlpha > 0.01f;
            }
        }
    }

    private void PushHit(float damage, bool kill, Color accent, string targetLabel)
    {
        EnsureBuilt();
        hitTimer = 0.12f;
        if (kill)
        {
            killTimer = 0.4f;
            string damageLabel = damage >= 99f ? "HEAVY BREAK" : damage >= 40f ? "SHELL BROKEN" : "TARGET CUT";
            string target = string.IsNullOrWhiteSpace(targetLabel) ? "HOSTILE" : targetLabel.ToUpperInvariant();
            queuedStatus = $"{target} // {damageLabel}";
        }

        Color lineColor = Color.Lerp(accent, Color.white, kill ? 0.45f : 0.25f);
        lineColor.a = 1f;
        currentLineColor = lineColor;
        for (int i = 0; i < hitLines.Length; i++)
        {
            if (hitLines[i] != null)
                hitLines[i].color = lineColor;
        }
    }

    private void EnsureBuilt()
    {
        if (hitLines[0] != null && statusText != null) return;

        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;
        transform.SetParent(canvas.transform, false);

        RectTransform root = gameObject.GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(220f, 220f);

        BuildHitMarker();
        BuildKillStatus();
    }

    private void BuildHitMarker()
    {
        Vector2[] positions =
        {
            new Vector2(0f, 24f),
            new Vector2(24f, 0f),
            new Vector2(0f, -24f),
            new Vector2(-24f, 0f)
        };

        Vector2[] sizes =
        {
            new Vector2(4f, 18f),
            new Vector2(18f, 4f),
            new Vector2(4f, 18f),
            new Vector2(18f, 4f)
        };

        for (int i = 0; i < hitLines.Length; i++)
        {
            GameObject go = new GameObject("HitLine_" + i);
            go.transform.SetParent(transform, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = positions[i];
            rect.sizeDelta = sizes[i];
            Image image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            image.enabled = false;
            hitLines[i] = image;
        }
    }

    private void BuildKillStatus()
    {
        GameObject back = new GameObject("KillStatusBack");
        back.transform.SetParent(transform, false);
        RectTransform backRect = back.AddComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0.5f);
        backRect.anchorMax = new Vector2(0.5f, 0.5f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.anchoredPosition = new Vector2(0f, 68f);
        backRect.sizeDelta = new Vector2(280f, 34f);
        statusBack = back.AddComponent<Image>();
        statusBack.color = new Color(0.06f, 0.04f, 0.02f, 0f);
        statusBack.raycastTarget = false;
        statusBack.enabled = false;

        GameObject text = new GameObject("KillStatusText");
        text.transform.SetParent(back.transform, false);
        RectTransform textRect = text.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        statusText = text.AddComponent<TextMeshProUGUI>();
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.fontSize = 18f;
        statusText.text = string.Empty;
        statusText.raycastTarget = false;
        statusText.color = new Color(1f, 0.89f, 0.7f, 0f);
    }
}
