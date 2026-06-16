using UnityEngine;

public class CybergrindShopStation : Interactable
{
    public enum ShopService
    {
        Repair,
        Refit,
        Overclock,
        Surge
    }

    [Header("Shop")]
    public ShopService service;
    public int cost = 3;
    public int presetIndex;
    public int healAmount = 35;
    public float moveSpeedBonus = 1.75f;
    public float dashBonus = 4f;
    public float jumpBonus = 0.3f;
    public float fireRateBoostPercent = 0.14f;
    public float damageBoostPercent = 0.1f;
    public float altCooldownBoostPercent = 0.18f;
    public Gun.PassiveMod passiveMod = Gun.PassiveMod.RapidFeed;
    public Gun.AltFireMod altFireMod = Gun.AltFireMod.QuickCharge;
    public bool singleUse = true;
    public Renderer displayRenderer;

    private Gun cachedGun;
    private bool spent;

    protected override void Start()
    {
        base.Start();
        displayRenderer = displayRenderer != null ? displayRenderer : GetComponent<Renderer>();
        ApplyServiceVisual();
        RefreshPrompt();
    }

    public string GetServiceTitle()
    {
        return service switch
        {
            ShopService.Repair => "HEAL",
            ShopService.Refit => "GUN",
            ShopService.Overclock => "MOD",
            _ => "MOVE"
        };
    }

    public Color GetPreviewAccent()
    {
        return service switch
        {
            ShopService.Repair => new Color(0.2f, 0.95f, 0.72f, 1f),
            ShopService.Refit => new Color(0.4f, 0.72f, 1f, 1f),
            ShopService.Overclock => new Color(1f, 0.62f, 0.18f, 1f),
            _ => new Color(0.8f, 0.52f, 1f, 1f)
        };
    }

    public string GetPreviewTitle()
    {
        return GetPreviewTitle(null);
    }

    public string GetPreviewTitle(PlayerController player)
    {
        if (spent)
            return $"{GetServiceTitle()} - USED";

        string title = cost <= 0
            ? $"{GetServiceTitle()} - FREE"
            : $"{GetServiceTitle()} - {cost} COINS";
        if (player != null && cost > 0 && player.currency < cost)
            return $"{title} - NEED {cost - player.currency}";
        return title;
    }

    public string GetPreviewDetail()
    {
        return GetPreviewDetail(null);
    }

    public string GetPreviewDetail(PlayerController player)
    {
        if (spent)
            return "Already used.";

        Gun gun = GetGun(player);
        return service switch
        {
            ShopService.Repair => $"Restore {healAmount} HP.",
            ShopService.Refit => BuildRefitDetail(gun),
            ShopService.Overclock => BuildOverclockDetail(gun),
            _ => $"+{moveSpeedBonus:0.0} speed  +{dashBonus:0.0} dash  +{jumpBonus:0.0} jump."
        };
    }

    public override void OnInteract(PlayerController player)
    {
        if (player == null) return;
        if (spent)
        {
            promptMessage = "Used";
            return;
        }

        bool success = false;
        string bannerTitle = string.Empty;
        string bannerDetail = string.Empty;
        switch (service)
        {
            case ShopService.Repair:
                if (!player.TrySpendCurrency(cost))
                {
                    promptMessage = $"Need {cost} coins";
                    return;
                }
                player.Heal(healAmount);
                promptMessage = $"Healed +{healAmount}";
                bannerTitle = "HEALED";
                bannerDetail = $"+{healAmount} HP";
                success = true;
                break;

            case ShopService.Refit:
                Gun gun = GetGun(player);
                if (gun == null)
                {
                    promptMessage = "No weapon found";
                    return;
                }
                if (!player.TrySpendCurrency(cost))
                {
                    promptMessage = $"Need {cost} coins";
                    return;
                }
                CybergrindRunState.GetOrCreate().UnlockWeapon(presetIndex);
                gun.EquipPreset(presetIndex);
                promptMessage = $"{gun.GetPresetDisplayName(presetIndex)} ready";
                bannerTitle = "GUN READY";
                bannerDetail = $"{gun.GetPresetDisplayName(presetIndex)}. {gun.GetActiveDescriptorLine()}";
                success = true;
                break;

            case ShopService.Overclock:
                Gun overclockGun = GetGun(player);
                if (overclockGun == null)
                {
                    promptMessage = "No weapon found";
                    return;
                }
                if (!player.TrySpendCurrency(cost))
                {
                    promptMessage = $"Need {cost} coins";
                    return;
                }
                overclockGun.ApplyWeaponOverclock(fireRateBoostPercent, damageBoostPercent, altCooldownBoostPercent);
                overclockGun.ApplyWeaponMod(overclockGun.GetActiveFamily(), passiveMod, altFireMod);
                player.Heal(Mathf.Max(12, healAmount / 3));
                promptMessage = overclockGun.GetRunModifierStatus();
                bannerTitle = "MOD INSTALLED";
                bannerDetail = $"{overclockGun.GetModPreviewLine(overclockGun.GetActiveFamily(), passiveMod, altFireMod)}. {overclockGun.GetRunModifierStatus()}";
                success = true;
                break;

            case ShopService.Surge:
                if (!player.TrySpendCurrency(cost))
                {
                    promptMessage = $"Need {cost} coins";
                    return;
                }
                player.ApplyMobilityUpgrade(moveSpeedBonus, dashBonus, jumpBonus);
                promptMessage = "Move kit upgraded";
                bannerTitle = "MOVE KIT";
                bannerDetail = $"+{moveSpeedBonus:0.0} speed  +{dashBonus:0.0} dash  +{jumpBonus:0.0} jump";
                success = true;
                break;
        }

        if (!success) return;

        if (singleUse)
        {
            spent = true;
            ApplySpentVisual();
        }

        CybergrindArenaDirector director = FindAnyObjectByType<CybergrindArenaDirector>();
        if (director != null)
            director.NotifyShopInteraction();

        BossEncounterHUD hud = FindAnyObjectByType<BossEncounterHUD>();
        if (hud != null)
            hud.ShowShopServiceBanner(service, bannerTitle, bannerDetail);

        ProjectStructureAudioDirector audioDirector = FindAnyObjectByType<ProjectStructureAudioDirector>();
        if (audioDirector != null)
            audioDirector.PlayShopServiceCue(service);
    }

    public override void OnFocus()
    {
        RefreshPrompt();
        base.OnFocus();
    }

    private void RefreshPrompt()
    {
        if (spent)
        {
            promptMessage = "Used";
            return;
        }

        Gun gun = GetGun(null);
        switch (service)
        {
            case ShopService.Repair:
                promptMessage = $"Heal +{healAmount} HP - {cost} coins";
                break;
            case ShopService.Refit:
                string weaponName = gun != null ? gun.GetPresetDisplayName(presetIndex) : $"Weapon {presetIndex + 1}";
                bool unlocked = CybergrindRunState.GetOrCreate().IsWeaponUnlocked(presetIndex);
                promptMessage = unlocked
                    ? $"Equip {weaponName} - {cost} coins"
                    : $"Add {weaponName} - {cost} coins";
                break;
            case ShopService.Overclock:
                int fireRatePercent = Mathf.RoundToInt(fireRateBoostPercent * 100f);
                int damagePercent = Mathf.RoundToInt(damageBoostPercent * 100f);
                promptMessage = $"Install mod - +{fireRatePercent}% rate, +{damagePercent}% damage - {cost} coins";
                break;
            case ShopService.Surge:
                promptMessage = cost <= 0
                    ? "Move kit - speed, dash, jump - free"
                    : $"Move kit - speed, dash, jump - {cost} coins";
                break;
        }
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

    private void ApplySpentVisual()
    {
        if (displayRenderer == null || displayRenderer.material == null) return;
        Color color = displayRenderer.material.color;
        displayRenderer.material.color = Color.Lerp(color, Color.black, 0.58f);
    }

    private void ApplyServiceVisual()
    {
        if (displayRenderer == null || displayRenderer.material == null) return;

        displayRenderer.material.color = GetPreviewAccent();
    }

    private string BuildRefitDetail(Gun gun)
    {
        string weaponName = gun != null ? gun.GetPresetDisplayName(presetIndex) : $"Weapon {presetIndex + 1}";
        bool unlocked = CybergrindRunState.GetOrCreate().IsWeaponUnlocked(presetIndex);
        string descriptor = gun != null ? gun.GetPresetGuideText(presetIndex) : "Switch to a different weapon for this run.";
        return unlocked
            ? $"{weaponName}. Equips it now. {descriptor}"
            : $"{weaponName}. Adds it for this run and equips it.";
    }

    private string BuildOverclockDetail(Gun gun)
    {
        int fireRatePercent = Mathf.RoundToInt(fireRateBoostPercent * 100f);
        int damagePercent = Mathf.RoundToInt(damageBoostPercent * 100f);
        int altPercent = Mathf.RoundToInt(altCooldownBoostPercent * 100f);
        string gunName = gun != null ? gun.GetActiveDisplayName() : "your current weapon";
        string modLine = gun != null
            ? gun.GetModPreviewLine(gun.GetActiveFamily(), passiveMod, altFireMod)
            : "Adds one passive mod and one special mod.";
        return $"{gunName}. {modLine}. +{fireRatePercent}% rate  +{damagePercent}% damage  +{altPercent}% special recharge. Restores a little HP.";
    }
}
