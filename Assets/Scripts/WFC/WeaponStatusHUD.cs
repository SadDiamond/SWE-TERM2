using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponStatusHUD : MonoBehaviour
{
    private static WeaponStatusHUD instance;
    public Gun gun;
    [Min(0.05f)] public float refreshInterval = 0.15f;
    [Min(0.2f)] public float momentDuration = 2.4f;

    private float refreshTimer;
    private float momentTimer;
    private TMP_Text familyText;
    private TMP_Text variantText;
    private TMP_Text detailText;
    private TMP_Text modifierText;
    private Image panelImage;
    private TMP_Text momentText;
    private TMP_Text momentDetailText;
    private GameObject momentRoot;
    private Image momentProgressFill;
    private float momentDurationActive;
    private string lastVariantDisplay = string.Empty;
    private string lastModifierStatus = string.Empty;
    private bool hasPrimedState;
    private float referenceRefreshTimer;

    private void Start()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (gun == null)
            gun = FindAnyObjectByType<Gun>();

        BuildUI();
        RefreshState();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        UpdateMomentState(Time.deltaTime);
        UpdateCachedReferences();

        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f) return;

        refreshTimer = refreshInterval;
        RefreshState();
    }

    private void RefreshState()
    {
        if (gun == null) return;

        if (familyText != null)
            familyText.text = gun.GetActiveFamilyLabel();
        if (variantText != null)
            variantText.text = gun.GetActiveDisplayName().ToUpperInvariant();
        if (detailText != null)
            detailText.text = gun.GetActiveStatsLine();
        string modifierStatus = gun.GetRunModifierStatus();
        if (modifierText != null)
            modifierText.text = modifierStatus == "No mods installed." ? string.Empty : modifierStatus;

        string currentVariantDisplay = gun.GetActiveDisplayName();
        if (hasPrimedState)
        {
            if (!string.Equals(currentVariantDisplay, lastVariantDisplay))
            {
                ShowArmoryMoment(
                    "GUN READY",
                    $"{currentVariantDisplay.ToUpperInvariant()} - {gun.GetActiveDescriptorLine()}",
                    ResolveAccentColor());
            }
            else if (!string.Equals(modifierStatus, lastModifierStatus) && modifierStatus != "No mods installed.")
            {
                ShowArmoryMoment(
                    "MOD INSTALLED",
                    modifierStatus,
                    new Color(1f, 0.68f, 0.2f, 1f));
            }
        }

        lastVariantDisplay = currentVariantDisplay;
        lastModifierStatus = modifierStatus;
        hasPrimedState = true;
    }

    private void UpdateCachedReferences(bool force = false)
    {
        referenceRefreshTimer -= Time.deltaTime;
        if (!force && referenceRefreshTimer > 0f)
            return;

        referenceRefreshTimer = 1f;
        if (gun == null)
            gun = FindAnyObjectByType<Gun>();
    }

    private void BuildUI()
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;

        GameObject root = new GameObject("WeaponStatusHUD");
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(1f, 0f);
        rootRect.anchoredPosition = new Vector2(-20f, 20f);
        rootRect.sizeDelta = new Vector2(330f, 106f);

        panelImage = root.AddComponent<Image>();
        panelImage.color = new Color(0.015f, 0.025f, 0.032f, 0.9f);

        familyText = CreateText(root.transform, "WeaponFamilyText", 11f, new Vector2(0.08f, 0.82f), new Vector2(278f, 18f), TextAlignmentOptions.Left, new Color(0.48f, 0.88f, 0.96f));
        variantText = CreateText(root.transform, "WeaponVariantText", 18f, new Vector2(0.08f, 0.6f), new Vector2(278f, 26f), TextAlignmentOptions.Left, Color.white);
        detailText = CreateText(root.transform, "WeaponDetailText", 10f, new Vector2(0.08f, 0.34f), new Vector2(278f, 18f), TextAlignmentOptions.Left, new Color(0.7f, 0.76f, 0.8f));
        modifierText = CreateText(root.transform, "WeaponModifierText", 9f, new Vector2(0.08f, 0.14f), new Vector2(278f, 18f), TextAlignmentOptions.Left, new Color(1f, 0.7f, 0.24f));

        momentRoot = new GameObject("WeaponMomentPanel");
        momentRoot.transform.SetParent(root.transform, false);
        RectTransform momentRect = momentRoot.AddComponent<RectTransform>();
        momentRect.anchorMin = new Vector2(0f, 1f);
        momentRect.anchorMax = new Vector2(1f, 1f);
        momentRect.pivot = new Vector2(0.5f, 0f);
        momentRect.anchoredPosition = new Vector2(0f, 10f);
        momentRect.sizeDelta = new Vector2(0f, 48f);
        Image momentImage = momentRoot.AddComponent<Image>();
        momentImage.color = new Color(0.08f, 0.12f, 0.16f, 0.94f);

        momentText = CreateText(momentRoot.transform, "WeaponMomentText", 11f, new Vector2(0.5f, 0.7f), new Vector2(290f, 16f), TextAlignmentOptions.Center, new Color(0.9f, 0.96f, 1f));
        momentDetailText = CreateText(momentRoot.transform, "WeaponMomentDetailText", 9f, new Vector2(0.5f, 0.3f), new Vector2(300f, 18f), TextAlignmentOptions.Center, new Color(0.8f, 0.9f, 0.96f));
        GameObject progress = new GameObject("WeaponMomentProgress");
        progress.transform.SetParent(momentRoot.transform, false);
        RectTransform progressRect = progress.AddComponent<RectTransform>();
        progressRect.anchorMin = new Vector2(0f, 0f);
        progressRect.anchorMax = new Vector2(1f, 0.05f);
        progressRect.offsetMin = Vector2.zero;
        progressRect.offsetMax = Vector2.zero;
        momentProgressFill = progress.AddComponent<Image>();
        momentProgressFill.type = Image.Type.Filled;
        momentProgressFill.fillMethod = Image.FillMethod.Horizontal;
        momentProgressFill.color = new Color(0.76f, 0.94f, 1f, 0.95f);
        momentRoot.SetActive(false);
    }

    public void ShowArmoryMoment(string title, string detail, Color accent, float duration = -1f)
    {
        if (momentRoot == null)
            return;

        if (momentText != null)
            momentText.text = title;
        if (momentDetailText != null)
            momentDetailText.text = detail;

        Image momentImage = momentRoot.GetComponent<Image>();
        if (momentImage != null)
            momentImage.color = Color.Lerp(new Color(0.06f, 0.09f, 0.12f, 0.94f), accent * new Color(1f, 1f, 1f, 0.94f), 0.35f);
        if (variantText != null)
            variantText.color = Color.Lerp(new Color(0.78f, 0.94f, 1f), accent, 0.32f);
        if (panelImage != null)
            panelImage.color = Color.Lerp(new Color(0.015f, 0.025f, 0.032f, 0.9f), accent * new Color(1f, 1f, 1f, 0.16f), 0.22f);

        momentRoot.SetActive(true);
        ProjectStructureUIRoot.BringToFront(momentRoot.transform);
        momentTimer = duration > 0f ? duration : momentDuration;
        momentDurationActive = momentTimer;
        if (momentProgressFill != null)
        {
            momentProgressFill.fillAmount = 1f;
            momentProgressFill.color = Color.Lerp(new Color(0.76f, 0.94f, 1f, 0.95f), accent, 0.35f);
        }
    }

    private void UpdateMomentState(float deltaTime)
    {
        if (momentRoot == null) return;

        if (momentTimer > 0f)
        {
            momentTimer -= deltaTime;
            if (momentProgressFill != null)
                momentProgressFill.fillAmount = Mathf.Clamp01(momentTimer / Mathf.Max(0.01f, momentDurationActive));
            if (momentTimer <= 0f)
            {
                momentRoot.SetActive(false);
                if (variantText != null)
                    variantText.color = new Color(0.78f, 0.94f, 1f);
                if (panelImage != null)
                    panelImage.color = new Color(0.015f, 0.025f, 0.032f, 0.9f);
            }
        }
    }

    private Color ResolveAccentColor()
    {
        if (gun == null) return new Color(0.7f, 0.92f, 1f, 1f);
        string family = gun.GetActiveFamilyLabel();
        return family.Contains("SHOTGUN")
            ? new Color(1f, 0.66f, 0.22f, 1f)
            : new Color(0.42f, 0.84f, 1f, 1f);
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
        ProjectStructureUIRoot.ApplyDefaultFont(text);
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }
}
