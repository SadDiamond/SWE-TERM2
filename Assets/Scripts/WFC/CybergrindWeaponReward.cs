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
    private Transform orbitRoot;
    private Transform innerCore;
    private Transform baseHalo;
    private float guideTimer;
    private float revealTimer;
    private Vector3 revealStartPosition;
    private string weaponName = "Weapon";
    private string guideText = "";
    private static Canvas rewardCanvas;
    private static GameObject rewardPanel;
    private static TMP_Text rewardTitleText;
    private static TMP_Text rewardBodyText;
    private static TMP_Text rewardTimerText;
    private static TMP_Text rewardControlsText;
    private static Image rewardProgressFill;
    private static Image rewardPanelImage;
    private static CybergrindWeaponReward activeRewardGuide;
    private static float activeGuideHideAt;

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
        promptMessage = isBossReward ? "Boss gun dropping in" : "Pick up gun - " + weaponName;
        ApplyRewardMaterial();
        BuildRewardCrown();
        base.Start();
    }

    protected override void Update()
    {
        base.Update();
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
                promptMessage = isBossReward ? "Pick up boss gun - " + weaponName : "Pick up gun - " + weaponName;
            }
        }
        else if (!IsClaimed)
        {
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = basePosition + Vector3.up * bob;
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        AnimateRewardModel();

        if (activeRewardGuide == this)
        {
            guideTimer = Mathf.Max(0f, activeGuideHideAt - Time.unscaledTime);
            if (Time.unscaledTime >= activeGuideHideAt)
            {
                activeRewardGuide = null;
                guideTimer = 0f;
                if (rewardPanel != null)
                    rewardPanel.SetActive(false);
            }
        }
        else if (guideTimer > 0f)
            guideTimer = Mathf.Max(0f, guideTimer - Time.unscaledDeltaTime);
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
        activeGuideHideAt = Time.unscaledTime + guideDuration;
        promptMessage = isBossReward ? "Boss gun ready. The way down is open." : "Gun ready. Head for the exit.";
        SpawnPickupBurst();

        WeaponStatusHUD armoryHud = FindAnyObjectByType<WeaponStatusHUD>();
        if (armoryHud != null)
        {
            string title = "GUN READY";
            string detail = $"{weaponName.ToUpperInvariant()} - {guideText}";
            armoryHud.ShowArmoryMoment(title, detail, GetRewardColor(presetIndex), isBossReward ? 3.4f : 2.8f);
        }

        BossEncounterHUD encounterHud = FindAnyObjectByType<BossEncounterHUD>();
        if (encounterHud != null)
        {
            encounterHud.ShowSystemBanner(
                "GUN TAKEN",
                isBossReward
                    ? $"{weaponName.ToUpperInvariant()} is ready. The next drop is open."
                    : $"{weaponName.ToUpperInvariant()} is ready. Exit is live.",
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
            guideText = isBossReward ? "Boss gun added." : "New gun added.";
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
        Color color = Mathf.Clamp(index, 0, 8) switch
        {
            1 => new Color(0.95f, 0.18f, 0.08f, 1f),
            2 => new Color(0.1f, 0.95f, 0.28f, 1f),
            3 => new Color(1f, 0.58f, 0.08f, 1f),
            4 => new Color(0.85f, 0.1f, 0.85f, 1f),
            5 => new Color(0.62f, 0.48f, 1f, 1f),
            6 => new Color(1f, 0.42f, 0.22f, 1f),
            7 => new Color(0.58f, 0.92f, 1f, 1f),
            8 => new Color(0.84f, 0.78f, 0.28f, 1f),
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

        GameObject baseObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObject.name = "RewardBaseHalo";
        baseObject.transform.SetParent(transform, false);
        baseObject.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        baseObject.transform.localScale = isBossReward ? new Vector3(1.65f, 0.045f, 1.65f) : new Vector3(1.25f, 0.035f, 1.25f);
        if (mat != null && baseObject.TryGetComponent<Renderer>(out Renderer baseRenderer)) baseRenderer.sharedMaterial = mat;
        Collider baseCollider = baseObject.GetComponent<Collider>();
        if (baseCollider != null) Destroy(baseCollider);
        baseHalo = baseObject.transform;

        orbitRoot = new GameObject("RewardOrbitRoot").transform;
        orbitRoot.SetParent(transform, false);
        orbitRoot.localPosition = Vector3.up * (isBossReward ? 1.05f : 0.92f);

        GameObject orbitA = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        orbitA.name = "RewardOrbitA";
        orbitA.transform.SetParent(orbitRoot, false);
        orbitA.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        orbitA.transform.localScale = isBossReward ? new Vector3(1.18f, 0.025f, 1.18f) : new Vector3(0.92f, 0.018f, 0.92f);
        if (mat != null && orbitA.TryGetComponent<Renderer>(out Renderer orbitARenderer)) orbitARenderer.sharedMaterial = mat;
        Collider orbitACollider = orbitA.GetComponent<Collider>();
        if (orbitACollider != null) Destroy(orbitACollider);

        GameObject orbitB = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        orbitB.name = "RewardOrbitB";
        orbitB.transform.SetParent(orbitRoot, false);
        orbitB.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        orbitB.transform.localScale = isBossReward ? new Vector3(0.88f, 0.02f, 0.88f) : new Vector3(0.66f, 0.016f, 0.66f);
        if (mat != null && orbitB.TryGetComponent<Renderer>(out Renderer orbitBRenderer)) orbitBRenderer.sharedMaterial = mat;
        Collider orbitBCollider = orbitB.GetComponent<Collider>();
        if (orbitBCollider != null) Destroy(orbitBCollider);

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cube);
        core.name = "RewardInnerCore";
        core.transform.SetParent(transform, false);
        core.transform.localPosition = Vector3.up * (isBossReward ? 0.98f : 0.86f);
        core.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        core.transform.localScale = isBossReward ? new Vector3(0.42f, 0.42f, 0.42f) : new Vector3(0.32f, 0.32f, 0.32f);
        if (mat != null && core.TryGetComponent<Renderer>(out Renderer coreRenderer)) coreRenderer.sharedMaterial = mat;
        Collider coreCollider = core.GetComponent<Collider>();
        if (coreCollider != null) Destroy(coreCollider);
        innerCore = core.transform;

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

    private void AnimateRewardModel()
    {
        if (IsClaimed) return;

        if (orbitRoot != null)
        {
            orbitRoot.localRotation = Quaternion.Euler(
                Mathf.Sin(Time.time * 1.3f) * 10f,
                Time.time * (isBossReward ? 72f : 54f),
                Mathf.Cos(Time.time * 1.1f) * 8f);
        }

        if (innerCore != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 5.6f) * (isBossReward ? 0.12f : 0.08f);
            innerCore.localScale = Vector3.one * (isBossReward ? 0.42f : 0.32f) * pulse;
            innerCore.localRotation = Quaternion.Euler(Time.time * 34f, 45f + Time.time * 72f, Time.time * 18f);
        }

        if (baseHalo != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 3.2f) * 0.05f;
            Vector3 baseScale = isBossReward ? new Vector3(1.65f, 0.045f, 1.65f) : new Vector3(1.25f, 0.035f, 1.25f);
            baseHalo.localScale = new Vector3(baseScale.x * pulse, baseScale.y, baseScale.z * pulse);
        }
    }

    private void SpawnPickupBurst()
    {
        if (!Application.isPlaying) return;

        Color color = GetRewardColor(presetIndex);
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
        mat.color = color;
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 2f);
        }

        int count = isBossReward ? 12 : 8;
        for (int i = 0; i < count; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "RewardPickupShard";
            shard.transform.position = transform.position + Vector3.up * 0.85f;
            shard.transform.rotation = Random.rotation;
            shard.transform.localScale = isBossReward ? new Vector3(0.08f, 0.08f, 0.42f) : new Vector3(0.055f, 0.055f, 0.32f);
            if (shard.TryGetComponent<Renderer>(out Renderer renderer)) renderer.material = mat;
            Collider collider = shard.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) + 0.35f;
            StartCoroutine(AnimatePickupShard(shard.transform, dir.normalized * (isBossReward ? 2.6f : 1.9f), isBossReward ? 0.42f : 0.32f));
        }
    }

    private System.Collections.IEnumerator AnimatePickupShard(Transform shard, Vector3 offset, float lifetime)
    {
        if (shard == null) yield break;

        Vector3 start = shard.position;
        Vector3 startScale = shard.localScale;
        float elapsed = 0f;
        while (elapsed < lifetime && shard != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
            shard.position = Vector3.Lerp(start, start + offset, Mathf.SmoothStep(0f, 1f, t));
            shard.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        if (shard != null)
            Destroy(shard.gameObject);
    }

    private void LateUpdate()
    {
        EnsureRewardGuideUI();
        RefreshRewardGuideUI();

        if (activeRewardGuide == this && guideTimer <= 0f)
        {
            activeRewardGuide = null;
            if (rewardPanel != null)
                rewardPanel.SetActive(false);
        }
    }

    private static void EnsureRewardGuideUI()
    {
        rewardCanvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (rewardCanvas == null) return;

        if (rewardPanel != null)
        {
            if (rewardPanel.transform.parent != rewardCanvas.transform)
                rewardPanel.transform.SetParent(rewardCanvas.transform, false);
            return;
        }

        for (int i = rewardCanvas.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = rewardCanvas.transform.GetChild(i);
            if (child == null || child.name != "ArenaWeaponRewardGuidePanel") continue;
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        rewardPanel = new GameObject("ArenaWeaponRewardGuidePanel");
        rewardPanel.transform.SetParent(rewardCanvas.transform, false);
        RectTransform panelRect = rewardPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 20f);
        panelRect.sizeDelta = new Vector2(540f, 104f);

        rewardPanelImage = rewardPanel.AddComponent<Image>();
        rewardPanelImage.color = new Color(0.018f, 0.028f, 0.038f, 0.95f);

        GameObject accent = new GameObject("RewardAccent");
        accent.transform.SetParent(rewardPanel.transform, false);
        RectTransform accentRect = accent.AddComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(4f, 0f);
        Image accentImage = accent.AddComponent<Image>();
        accentImage.color = new Color(0.74f, 0.95f, 1f, 0.98f);

        rewardTitleText = CreateGuideText(rewardPanel.transform, "RewardTitle", 15f, new Vector2(0f, 1f), new Vector2(486f, 20f), new Vector2(18f, -12f), TextAlignmentOptions.TopLeft, Color.white);
        rewardBodyText = CreateGuideText(rewardPanel.transform, "RewardBody", 10.5f, new Vector2(0f, 1f), new Vector2(486f, 34f), new Vector2(18f, -34f), TextAlignmentOptions.TopLeft, new Color(0.84f, 0.9f, 0.96f));
        rewardControlsText = CreateGuideText(rewardPanel.transform, "RewardControls", 9.5f, new Vector2(0f, 0f), new Vector2(420f, 16f), new Vector2(18f, 12f), TextAlignmentOptions.BottomLeft, new Color(0.7f, 0.82f, 0.9f));
        rewardTimerText = CreateGuideText(rewardPanel.transform, "RewardTimer", 9.5f, new Vector2(1f, 0f), new Vector2(88f, 16f), new Vector2(-16f, 12f), TextAlignmentOptions.BottomRight, new Color(0.72f, 0.86f, 0.92f));

        GameObject progressBack = new GameObject("RewardProgressBack");
        progressBack.transform.SetParent(rewardPanel.transform, false);
        RectTransform backRect = progressBack.AddComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(1f, 0f);
        backRect.pivot = new Vector2(0.5f, 0f);
        backRect.anchoredPosition = new Vector2(0f, 0f);
        backRect.sizeDelta = new Vector2(-20f, 4f);
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

    private static TMP_Text CreateGuideText(Transform parent, string name, float size, Vector2 anchor, Vector2 boxSize, Vector2 position, TextAlignmentOptions alignment, Color color)
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
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static void RefreshRewardGuideUI()
    {
        if (rewardPanel == null) return;

        if (activeRewardGuide == null || Time.unscaledTime >= activeGuideHideAt || activeRewardGuide.guideTimer <= 0f)
        {
            activeRewardGuide = null;
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
            rewardTitleText.text = "NEW GUN - " + activeRewardGuide.weaponName.ToUpperInvariant();
            rewardTitleText.color = Color.Lerp(Color.white, accent, 0.28f);
        }
        if (rewardBodyText != null)
        {
            rewardBodyText.text = activeRewardGuide.guideText + " Pick it up before leaving.";
            rewardBodyText.color = new Color(0.84f, 0.9f, 0.96f);
        }
        if (rewardControlsText != null)
            rewardControlsText.text = "1/2 SWITCH   Q/E VARIANT   RMB ABILITY";
        if (rewardProgressFill != null)
        {
            rewardProgressFill.fillAmount = Mathf.Clamp01(activeRewardGuide.guideTimer / Mathf.Max(0.01f, activeRewardGuide.guideDuration));
            rewardProgressFill.color = Color.Lerp(new Color(0.74f, 0.95f, 1f, 0.98f), accent, 0.35f);
        }
        if (rewardTimerText != null)
        {
            rewardTimerText.text = $"{Mathf.CeilToInt(activeRewardGuide.guideTimer)}s";
            rewardTimerText.color = Color.Lerp(new Color(0.72f, 0.86f, 0.92f), accent, 0.22f);
        }
    }
}
