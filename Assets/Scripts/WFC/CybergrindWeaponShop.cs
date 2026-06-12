using UnityEngine;

public class CybergrindWeaponShop : Interactable
{
    [Header("Shop")]
    public int presetIndex;
    public int equipCost = 3;
    public Renderer displayRenderer;

    private Gun cachedGun;

    protected override void Start()
    {
        base.Start();
        displayRenderer = displayRenderer != null ? displayRenderer : GetComponent<Renderer>();
        RefreshPrompt();
    }

    public Color GetPreviewAccent()
    {
        return new Color(0.42f, 0.78f, 1f, 1f);
    }

    public string GetPreviewTitle()
    {
        return GetPreviewTitle(null);
    }

    public string GetPreviewTitle(PlayerController player)
    {
        bool unlocked = CybergrindRunState.GetOrCreate().IsWeaponUnlocked(presetIndex);
        string title = unlocked
            ? $"SWITCH // {equipCost} COINS"
            : $"ADD // {equipCost} COINS";
        if (player != null && equipCost > 0 && player.currency < equipCost)
            return $"{title} // NEED {equipCost - player.currency}";
        return title;
    }

    public string GetPreviewDetail()
    {
        return GetPreviewDetail(null);
    }

    public string GetPreviewDetail(PlayerController player)
    {
        Gun gun = GetGun(player);
        string weaponName = gun != null ? gun.GetPresetDisplayName(presetIndex) : $"Weapon {presetIndex + 1}";
        if (!CybergrindRunState.GetOrCreate().IsWeaponUnlocked(presetIndex))
            return $"{weaponName}. Adds it for this run and equips it.";

        string descriptor = gun != null ? gun.GetPresetGuideText(presetIndex) : "Switch to a different weapon.";
        return $"{weaponName}. Equips it now. {descriptor}";
    }

    public override void OnInteract(PlayerController player)
    {
        if (player == null) return;

        Gun gun = GetGun(player);
        if (gun == null)
        {
            promptMessage = "Weapon system unavailable";
            return;
        }

        if (!player.TrySpendCurrency(equipCost))
        {
            promptMessage = $"Need {equipCost} coins for refit";
            return;
        }

        CybergrindRunState runState = CybergrindRunState.GetOrCreate();
        if (!runState.IsWeaponUnlocked(presetIndex))
            runState.UnlockWeapon(presetIndex);

        gun.EquipPreset(presetIndex);
        promptMessage = $"{gun.GetPresetDisplayName(presetIndex)} ready";

        CybergrindArenaDirector director = FindAnyObjectByType<CybergrindArenaDirector>();
        if (director != null)
            director.NotifyShopInteraction();

        BossEncounterHUD hud = FindAnyObjectByType<BossEncounterHUD>();
        if (hud != null)
        {
            hud.ShowShopServiceBanner(
                CybergrindShopStation.ShopService.Refit,
                "WEAPON READY",
                $"{gun.GetPresetDisplayName(presetIndex)} active. {gun.GetActiveDescriptorLine()}");
        }

        ProjectStructureAudioDirector audioDirector = FindAnyObjectByType<ProjectStructureAudioDirector>();
        if (audioDirector != null)
            audioDirector.PlayShopServiceCue(CybergrindShopStation.ShopService.Refit);
    }

    public override void OnFocus()
    {
        RefreshPrompt();
        base.OnFocus();
    }

    private void RefreshPrompt()
    {
        Gun gun = GetGun(null);
        string weaponName = gun != null ? gun.GetPresetDisplayName(presetIndex) : $"Weapon {presetIndex + 1}";
        bool unlocked = CybergrindRunState.GetOrCreate().IsWeaponUnlocked(presetIndex);
        promptMessage = unlocked ? $"Equip // {weaponName} ({equipCost} coins)" : $"Add // {weaponName} ({equipCost} coins)";
    }

    private Gun GetGun(PlayerController player)
    {
        if (cachedGun != null) return cachedGun;

        if (player != null)
            cachedGun = player.GetComponentInChildren<Gun>(true);

        if (cachedGun == null)
            cachedGun = FindAnyObjectByType<Gun>();

        return cachedGun;
    }
}
