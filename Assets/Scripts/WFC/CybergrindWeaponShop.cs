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

    public override void OnInteract(PlayerController player)
    {
        if (player == null) return;

        Gun gun = GetGun(player);
        if (gun == null)
        {
            promptMessage = "Weapon system unavailable";
            return;
        }

        CybergrindRunState runState = CybergrindRunState.GetOrCreate();
        if (!runState.IsWeaponUnlocked(presetIndex))
        {
            promptMessage = "Locked until you beat a boss";
            return;
        }

        if (!player.TrySpendCurrency(equipCost))
        {
            promptMessage = $"Need {equipCost} coins for refit";
            return;
        }

        gun.EquipPreset(presetIndex);
        promptMessage = $"Weapon equipped // {gun.GetPresetDisplayName(presetIndex)}";

        BossEncounterHUD hud = FindAnyObjectByType<BossEncounterHUD>();
        if (hud != null)
        {
            hud.ShowShopServiceBanner(
                CybergrindShopStation.ShopService.Refit,
                "WEAPON EQUIPPED",
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
        promptMessage = unlocked ? $"Equip weapon // {weaponName} ({equipCost} coins)" : $"{weaponName} // locked";
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
