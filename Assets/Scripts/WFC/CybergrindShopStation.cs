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
            promptMessage = "Station spent";
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
                    promptMessage = $"Need {cost} coins for shell repair";
                    return;
                }
                player.Heal(healAmount);
                promptMessage = $"Shell repaired (+{healAmount} HP)";
                bannerTitle = "SHELL RESEALED";
                bannerDetail = $"Hull restored by {healAmount}. Re-enter the route with full pressure.";
                success = true;
                break;

            case ShopService.Refit:
                Gun gun = GetGun(player);
                if (gun == null)
                {
                    promptMessage = "Armory link unavailable";
                    return;
                }
                if (!player.TrySpendCurrency(cost))
                {
                    promptMessage = $"Need {cost} coins for refit";
                    return;
                }
                gun.EquipPreset(presetIndex);
                promptMessage = $"Refit applied // {gun.GetPresetDisplayName(presetIndex)}";
                bannerTitle = "VARIANT BUS REWIRED";
                bannerDetail = $"{gun.GetPresetDisplayName(presetIndex)} linked. {gun.GetActiveDescriptorLine()}";
                success = true;
                break;

            case ShopService.Overclock:
                Gun overclockGun = GetGun(player);
                if (overclockGun == null)
                {
                    promptMessage = "Armory link unavailable";
                    return;
                }
                if (!player.TrySpendCurrency(cost))
                {
                    promptMessage = $"Need {cost} coins for overclock";
                    return;
                }
                overclockGun.ApplyWeaponOverclock(fireRateBoostPercent, damageBoostPercent, altCooldownBoostPercent);
                player.Heal(Mathf.Max(12, healAmount / 3));
                promptMessage = overclockGun.GetRunModifierStatus();
                bannerTitle = "BUS OVERCLOCKED";
                bannerDetail = $"{overclockGun.GetRunModifierStatus()} Keep pressure high before the lattice cools.";
                success = true;
                break;

            case ShopService.Surge:
                if (!player.TrySpendCurrency(cost))
                {
                    promptMessage = $"Need {cost} coins for surge sync";
                    return;
                }
                player.ApplyMobilityUpgrade(moveSpeedBonus, dashBonus, jumpBonus);
                promptMessage = "Mobility lattice tuned";
                bannerTitle = "MOBILITY LATTICE TUNED";
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
            promptMessage = "Station spent";
            return;
        }

        Gun gun = GetGun(null);
        switch (service)
        {
            case ShopService.Repair:
                promptMessage = $"Repair shell // +{healAmount} HP ({cost} coins)";
                break;
            case ShopService.Refit:
                string weaponName = gun != null ? gun.GetPresetDisplayName(presetIndex) : $"Variant {presetIndex + 1}";
                promptMessage = $"Refit // {weaponName} ({cost} coins)";
                break;
            case ShopService.Overclock:
                int fireRatePercent = Mathf.RoundToInt(fireRateBoostPercent * 100f);
                int damagePercent = Mathf.RoundToInt(damageBoostPercent * 100f);
                promptMessage = $"Overclock // +{fireRatePercent}% cycle, +{damagePercent}% damage ({cost} coins)";
                break;
            case ShopService.Surge:
                promptMessage = cost <= 0
                    ? $"Surge sync // +move +dash +jump (free)"
                    : $"Surge sync // +move +dash +jump ({cost} coins)";
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
