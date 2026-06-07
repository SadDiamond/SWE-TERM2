using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CybergrindWeaponReward : Interactable
{
    public int presetIndex;
    public bool isBossReward;
    public Transform exitTransform;
    public float bobHeight = 0.18f;
    public float bobSpeed = 2.4f;
    public float spinSpeed = 90f;
    public float guideDuration = 5f;

    public bool IsClaimed { get; private set; }

    private Vector3 basePosition;
    private Gun cachedGun;
    private Renderer cachedRenderer;
    private Collider cachedCollider;
    private float guideTimer;
    private float revealTimer;
    private Vector3 revealStartPosition;
    private string weaponName = "Weapon";
    private string guideText = "";
    private static Canvas rewardCanvas;
    private static GameObject rewardPanel;
    private static TMP_Text rewardTitleText;
    private static TMP_Text rewardBodyText;
    private static Image rewardProgressFill;
    private static Image rewardPanelImage;
    private static CybergrindWeaponReward activeRewardGuide;

    protected override void Start()
    {
        cachedRenderer = GetComponent<Renderer>();
        cachedCollider = GetComponent<Collider>();
        highlightRenderer = highlightRenderer != null ? highlightRenderer : cachedRenderer;
        interactionRange = 4.2f;
        basePosition = transform.position;
        if (isBossReward)
        {
            revealTimer = 0.95f;
            revealStartPosition = basePosition + Vector3.down * 1.8f;
            transform.position = revealStartPosition;
            guideDuration = Mathf.Max(guideDuration, 6.4f);
            if (cachedCollider != null)
                cachedCollider.enabled = false;
        }
        RefreshWeaponLabels();
        promptMessage = isBossReward ? "Core variant cradle aligning" : "Claim variant // " + weaponName;
        ApplyRewardMaterial();
        BuildRewardCrown();
        base.Start();
    }

    private void Update()
    {
        if (!IsClaimed && revealTimer > 0f)
        {
            revealTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(revealTimer / 0.95f);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.Lerp(revealStartPosition, basePosition, eased);
            transform.Rotate(Vector3.up, spinSpeed * 0.55f * Time.deltaTime, Space.World);

            if (revealTimer <= 0f)
            {
                transform.position = basePosition;
                if (cachedCollider != null)
                    cachedCollider.enabled = true;
                promptMessage = isBossReward ? "Claim core variant // " + weaponName : "Claim variant // " + weaponName;
            }
        }
        else if (!IsClaimed)
        {
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = basePosition + Vector3.up * bob;
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        if (guideTimer > 0f)
            guideTimer -= Time.deltaTime;
    }

    public override void OnInteract(PlayerController player)
    {
        if (IsClaimed || player == null) return;

        Gun gun = GetGun(player);
        CybergrindRunState runState = CybergrindRunState.GetOrCreate();
        runState.UnlockWeapon(presetIndex);
        if (gun != null)
        {
            gun.EquipPreset(presetIndex);
            weaponName = gun.GetPresetDisplayName(presetIndex);
        }

        IsClaimed = true;
        guideTimer = guideDuration;
        activeRewardGuide = this;
        promptMessage = isBossReward ? "Core variant linked. Inner shell split open." : "Variant claimed. Step into the descent gate.";

        WeaponStatusHUD armoryHud = FindAnyObjectByType<WeaponStatusHUD>();
        if (armoryHud != null)
        {
            string title = isBossReward ? "CORE VARIANT LINKED" : "VARIANT CLAIMED";
            string detail = $"{weaponName.ToUpperInvariant()} // {guideText}";
            armoryHud.ShowArmoryMoment(title, detail, GetRewardColor(presetIndex), isBossReward ? 3.4f : 2.8f);
        }

        BossEncounterHUD encounterHud = FindAnyObjectByType<BossEncounterHUD>();
        if (encounterHud != null)
        {
            encounterHud.ShowSystemBanner(
                isBossReward ? "CORE VARIANT LINKED" : "VARIANT CLAIMED",
                isBossReward
                    ? $"{weaponName.ToUpperInvariant()} online. Descent route punched deeper."
                    : $"{weaponName.ToUpperInvariant()} online. Descent gate is now live.",
                Color.Lerp(new Color(0.05f, 0.08f, 0.12f, 0.95f), GetRewardColor(presetIndex) * new Color(1f, 1f, 1f, 0.9f), 0.42f),
                isBossReward ? 3.4f : 2.6f);
        }

        if (cachedRenderer != null) cachedRenderer.enabled = false;
        if (cachedCollider != null) cachedCollider.enabled = false;
        for (int i = 0; i < transform.childCount; i++)
            transform.GetChild(i).gameObject.SetActive(false);
    }

    private Gun GetGun(PlayerController player)
    {
        if (cachedGun != null) return cachedGun;
        cachedGun = player.GetComponentInChildren<Gun>(true);
        if (cachedGun == null)
            cachedGun = FindAnyObjectByType<Gun>();
        return cachedGun;
    }

    private void RefreshWeaponLabels()
    {
        Gun gun = FindAnyObjectByType<Gun>();
        if (gun != null)
        {
            weaponName = gun.GetPresetDisplayName(presetIndex);
            guideText = gun.GetPresetGuideText(presetIndex);
        }
        else
        {
            guideText = isBossReward ? "Champion chamber reward linked." : "New weapon equipped.";
        }
    }

    private void ApplyRewardMaterial()
    {
        if (cachedRenderer == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        Color color = GetRewardColor(presetIndex);
        mat.color = color;
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 1.5f);
        }
        cachedRenderer.material = mat;
    }

    private Color GetRewardColor(int index)
    {
        Color color = Mathf.Clamp(index, 0, 5) switch
        {
            1 => new Color(0.95f, 0.18f, 0.08f, 1f),
            2 => new Color(0.1f, 0.95f, 0.28f, 1f),
            3 => new Color(1f, 0.58f, 0.08f, 1f),
            4 => new Color(0.85f, 0.1f, 0.85f, 1f),
            5 => new Color(0.62f, 0.48f, 1f, 1f),
            _ => new Color(0.1f, 0.85f, 1f, 1f)
        };

        return isBossReward ? Color.Lerp(color, Color.white, 0.2f) : color;
    }

    private void BuildRewardCrown()
    {
        Material mat = cachedRenderer != null ? cachedRenderer.sharedMaterial : null;
        int bladeCount = isBossReward ? 6 : 4;
        float radius = isBossReward ? 0.78f : 0.62f;
        float height = isBossReward ? 1.28f : 1.15f;

        for (int i = 0; i < bladeCount; i++)
        {
            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "RewardBlade_" + i;
            blade.transform.SetParent(transform, false);
            float yaw = i * (360f / bladeCount);
            blade.transform.localPosition = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, height, radius);
            blade.transform.localRotation = Quaternion.Euler(0f, yaw, isBossReward ? 28f : 22f);
            blade.transform.localScale = isBossReward ? new Vector3(0.1f, 0.92f, 0.1f) : new Vector3(0.08f, 0.72f, 0.08f);
            if (mat != null && blade.TryGetComponent<Renderer>(out Renderer r)) r.material = mat;
            Collider c = blade.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }
    }

    private void OnGUI()
    {
        EnsureRewardGuideUI();
        RefreshRewardGuideUI();
    }

    private void LateUpdate()
    {
        if (activeRewardGuide == this && guideTimer <= 0f)
            activeRewardGuide = null;
    }

    private static void EnsureRewardGuideUI()
    {
        if (rewardPanel != null) return;

        rewardCanvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (rewardCanvas == null) return;

        rewardPanel = new GameObject("WeaponRewardGuidePanel");
        rewardPanel.transform.SetParent(rewardCanvas.transform, false);
        RectTransform panelRect = rewardPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 28f);
        panelRect.sizeDelta = new Vector2(620f, 170f);

        rewardPanelImage = rewardPanel.AddComponent<Image>();
        rewardPanelImage.color = new Color(0.02f, 0.04f, 0.055f, 0.94f);

        rewardTitleText = CreateGuideText(rewardPanel.transform, "RewardTitle", 28f, new Vector2(0.5f, 0.78f), new Vector2(560f, 34f), TextAlignmentOptions.Center, Color.white);
        rewardBodyText = CreateGuideText(rewardPanel.transform, "RewardBody", 19f, new Vector2(0.5f, 0.46f), new Vector2(560f, 92f), TextAlignmentOptions.Center, new Color(0.84f, 0.9f, 0.96f));

        GameObject progressBack = new GameObject("RewardProgressBack");
        progressBack.transform.SetParent(rewardPanel.transform, false);
        RectTransform backRect = progressBack.AddComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.14f, 0.08f);
        backRect.anchorMax = new Vector2(0.86f, 0.15f);
        backRect.offsetMin = Vector2.zero;
        backRect.offsetMax = Vector2.zero;
        Image backImage = progressBack.AddComponent<Image>();
        backImage.color = new Color(0.08f, 0.1f, 0.13f, 0.96f);

        GameObject progressFill = new GameObject("RewardProgressFill");
        progressFill.transform.SetParent(progressBack.transform, false);
        RectTransform fillRect = progressFill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        rewardProgressFill = progressFill.AddComponent<Image>();
        rewardProgressFill.type = Image.Type.Filled;
        rewardProgressFill.fillMethod = Image.FillMethod.Horizontal;
        rewardProgressFill.fillAmount = 1f;
        rewardProgressFill.color = new Color(0.74f, 0.95f, 1f, 0.98f);

        rewardPanel.SetActive(false);
    }

    private static TMP_Text CreateGuideText(Transform parent, string name, float size, Vector2 anchor, Vector2 boxSize, TextAlignmentOptions alignment, Color color)
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

    private static void RefreshRewardGuideUI()
    {
        if (rewardPanel == null) return;

        if (activeRewardGuide == null || activeRewardGuide.guideTimer <= 0f)
        {
            rewardPanel.SetActive(false);
            return;
        }

        rewardPanel.SetActive(true);
        ProjectStructureUIRoot.BringToFront(rewardPanel.transform);
        Color accent = activeRewardGuide.GetRewardColor(activeRewardGuide.presetIndex);
        if (rewardPanelImage != null)
            rewardPanelImage.color = Color.Lerp(new Color(0.02f, 0.04f, 0.055f, 0.94f), accent * new Color(1f, 1f, 1f, 0.92f), activeRewardGuide.isBossReward ? 0.4f : 0.26f);
        if (rewardTitleText != null)
        {
            rewardTitleText.text = (activeRewardGuide.isBossReward ? "CORE VARIANT ACQUIRED // " : "WEAPON ACQUIRED // ") + activeRewardGuide.weaponName.ToUpperInvariant();
            rewardTitleText.color = Color.Lerp(Color.white, accent, 0.28f);
        }
        if (rewardBodyText != null)
        {
            rewardBodyText.text =
                activeRewardGuide.guideText + "\n\n" +
                (activeRewardGuide.isBossReward
                    ? "Champion chamber broken. Descend deeper.\n1/2 switch family   Q/E cycle variants   Left click fire   Right click special"
                    : "1/2 switch family   Q/E cycle variants   Left click fire   Right click special");
            rewardBodyText.color = new Color(0.84f, 0.9f, 0.96f);
        }
        if (rewardProgressFill != null)
        {
            rewardProgressFill.fillAmount = Mathf.Clamp01(activeRewardGuide.guideTimer / Mathf.Max(0.01f, activeRewardGuide.guideDuration));
            rewardProgressFill.color = Color.Lerp(new Color(0.74f, 0.95f, 1f, 0.98f), accent, 0.35f);
        }
    }
}
