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

    public override void OnInteract(PlayerController player)
    {
        if (player == null) return;
        if (spent)
        {
            promptMessage = "Empty";
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
                bannerTitle = "PATCHED UP";
                bannerDetail = $"Recovered {healAmount} hull. Back to work.";
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
                gun.EquipPreset(presetIndex);
                promptMessage = $"{gun.GetPresetDisplayName(presetIndex)} equipped";
                bannerTitle = "LOADOUT CHANGED";
                bannerDetail = $"{gun.GetPresetDisplayName(presetIndex)} equipped. {gun.GetActiveDescriptorLine()}";
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
                player.Heal(Mathf.Max(12, healAmount / 3));
                promptMessage = overclockGun.GetRunModifierStatus();
                bannerTitle = "WEAPON BOOSTED";
                bannerDetail = $"{overclockGun.GetRunModifierStatus()} Use it while the floor is still hot.";
                success = true;
                break;

            case ShopService.Surge:
                if (!player.TrySpendCurrency(cost))
                {
                    promptMessage = $"Need {cost} coins";
                    return;
                }
                player.ApplyMobilityUpgrade(moveSpeedBonus, dashBonus, jumpBonus);
                promptMessage = "Movement boosted";
                bannerTitle = "MOBILITY TUNED";
                bannerDetail = $"+{moveSpeedBonus:0.0} move  +{dashBonus:0.0} dash  +{jumpBonus:0.0} jump";
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
            promptMessage = "Empty";
            return;
        }

        Gun gun = GetGun(null);
        switch (service)
        {
            case ShopService.Repair:
                promptMessage = $"Heal up // +{healAmount} HP ({cost} coins)";
                break;
            case ShopService.Refit:
                string weaponName = gun != null ? gun.GetPresetDisplayName(presetIndex) : $"Variant {presetIndex + 1}";
                promptMessage = $"Swap weapon // {weaponName} ({cost} coins)";
                break;
            case ShopService.Overclock:
                int fireRatePercent = Mathf.RoundToInt(fireRateBoostPercent * 100f);
                int damagePercent = Mathf.RoundToInt(damageBoostPercent * 100f);
                promptMessage = $"Boost weapon // +{fireRatePercent}% fire rate, +{damagePercent}% damage ({cost} coins)";
                break;
            case ShopService.Surge:
                promptMessage = cost <= 0
                    ? "Tune movement // speed, dash, jump (free)"
                    : $"Tune movement // speed, dash, jump ({cost} coins)";
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

        displayRenderer.material.color = service switch
        {
            ShopService.Repair => new Color(0.2f, 0.95f, 0.72f),
            ShopService.Refit => new Color(0.4f, 0.72f, 1f),
            ShopService.Overclock => new Color(1f, 0.62f, 0.18f),
            _ => new Color(0.8f, 0.52f, 1f)
        };
    }
}
