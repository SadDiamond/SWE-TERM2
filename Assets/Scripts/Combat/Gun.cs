using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public class Gun : MonoBehaviour
{
    public enum WeaponFamily
    {
        Pistol,
        Shotgun,
        Heavy
    }

    public enum WeaponArchetype
    {
        Marksman,
        Rail,
        Splitter,
        CoreEject,
        Magnet,
        Slab,
        Mortar,
        Driver,
        Arc
    }

    public enum PassiveMod
    {
        None,
        SharpenedRounds,
        Stabilizer,
        RapidFeed
    }

    public enum AltFireMod
    {
        None,
        QuickCharge,
        Overload
    }

    [Serializable]
    public class WeaponPreset
    {
        public string displayName;
        public WeaponFamily family;
        public WeaponArchetype archetype;
        public Color accentColor = Color.cyan;
        public float fireRate = 0.25f;
        public float bulletSpeed = 65f;
        public float damage = 18f;
        public int pelletCount = 1;
        public float spreadDegrees = 0.2f;
        public float recoilForce = 5f;
        public float modelScale = 1f;
    }

    [Header("Weapon Presets")]
    public WeaponPreset[] presets =
    {
        new WeaponPreset { displayName = "Vesper", family = WeaponFamily.Pistol, archetype = WeaponArchetype.Marksman, accentColor = new Color(0.05f, 0.72f, 1f), fireRate = 0.2f, bulletSpeed = 90f, damage = 23f, pelletCount = 1, spreadDegrees = 0.02f, recoilForce = 3.2f, modelScale = 0.78f },
        new WeaponPreset { displayName = "Redline", family = WeaponFamily.Pistol, archetype = WeaponArchetype.Rail, accentColor = new Color(1f, 0.16f, 0.08f), fireRate = 0.72f, bulletSpeed = 130f, damage = 76f, pelletCount = 1, spreadDegrees = 0f, recoilForce = 9.2f, modelScale = 0.84f },
        new WeaponPreset { displayName = "Trident", family = WeaponFamily.Pistol, archetype = WeaponArchetype.Splitter, accentColor = new Color(0.18f, 0.95f, 0.38f), fireRate = 0.19f, bulletSpeed = 74f, damage = 10f, pelletCount = 3, spreadDegrees = 1.8f, recoilForce = 3.8f, modelScale = 0.76f },
        new WeaponPreset { displayName = "Kiln", family = WeaponFamily.Shotgun, archetype = WeaponArchetype.CoreEject, accentColor = new Color(1f, 0.55f, 0.06f), fireRate = 0.76f, bulletSpeed = 58f, damage = 8.5f, pelletCount = 10, spreadDegrees = 5f, recoilForce = 9f, modelScale = 0.78f },
        new WeaponPreset { displayName = "Lodestar", family = WeaponFamily.Shotgun, archetype = WeaponArchetype.Magnet, accentColor = new Color(0.92f, 0.16f, 0.82f), fireRate = 0.42f, bulletSpeed = 68f, damage = 6.2f, pelletCount = 6, spreadDegrees = 3.2f, recoilForce = 6f, modelScale = 0.74f },
        new WeaponPreset { displayName = "Breach", family = WeaponFamily.Shotgun, archetype = WeaponArchetype.Slab, accentColor = new Color(0.64f, 0.48f, 1f), fireRate = 1.02f, bulletSpeed = 115f, damage = 98f, pelletCount = 1, spreadDegrees = 0f, recoilForce = 12f, modelScale = 0.82f },
        new WeaponPreset { displayName = "Cinder", family = WeaponFamily.Heavy, archetype = WeaponArchetype.Mortar, accentColor = new Color(1f, 0.34f, 0.12f), fireRate = 1.08f, bulletSpeed = 72f, damage = 52f, pelletCount = 1, spreadDegrees = 0.25f, recoilForce = 10f, modelScale = 0.94f },
        new WeaponPreset { displayName = "Pile Driver", family = WeaponFamily.Heavy, archetype = WeaponArchetype.Driver, accentColor = new Color(0.45f, 0.94f, 1f), fireRate = 0.88f, bulletSpeed = 140f, damage = 70f, pelletCount = 1, spreadDegrees = 0f, recoilForce = 11f, modelScale = 0.9f },
        new WeaponPreset { displayName = "Tempest", family = WeaponFamily.Heavy, archetype = WeaponArchetype.Arc, accentColor = new Color(0.95f, 0.86f, 0.22f), fireRate = 0.56f, bulletSpeed = 82f, damage = 34f, pelletCount = 2, spreadDegrees = 1.2f, recoilForce = 8.8f, modelScale = 0.92f }
    };

    [Range(0, 8)] public int activePresetIndex = 0;
    public bool removeLegacyChildMeshes = true;
    public bool restrictToUnlockedWeapons = true;

    [Header("Legacy Runtime References")]
    public float fireRate = 0.15f;
    public float bulletSpeed = 50f;
    public float maxHitScanDistance = 1000f;
    [Min(0f)] public float hitScanRadius = 0.16f;
    public float muzzleForwardOffset = 1.1f;
    public Transform gunBarrel;
    public GameObject bulletPrefab;
    public ParticleSystem muzzleFlash;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip shootSound;
    public AudioClip altShootSound;

    [Header("Sway Settings")]
    public float swaySmooth = 8f;
    public float swayMultiplier = 2f;
    public float maxSwayAmount = 5f;
    public float bobFrequency = 10.5f;
    public float bobSpeedReference = 16f;
    public float bobAmplitudeX = 0.026f;
    public float bobAmplitudeY = 0.04f;
    public float bobAmplitudeZ = 0.006f;
    public float bobRotationAmount = 0.55f;
    public float airBobFrequency = 3.2f;
    public float airBobAmplitudeY = 0.018f;
    public float airBobAmplitudeZ = 0.01f;
    public float airBobRotationAmount = 0.22f;

    [Header("Recoil Settings")]
    public float recoilForce = 5f;
    public float recoilRecoverySpeed = 10f;

    private const string GeneratedModelName = "_GeneratedLowPolyWeapon";
    private PlayerController player;
    private float nextTimeToFire;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalPosition;
    private Vector3 currentRecoilPosition;
    private Material bodyMaterial;
    private Material accentMaterial;
    private WeaponFamily activeFamily = WeaponFamily.Pistol;
    private int pistolVariant;
    private int shotgunVariant;
    private int heavyVariant;
    private IDamageable taggedTarget;
    private float taggedTargetTimer;
    private float nextAltFireTime;
    private float runFireRateMultiplier = 1f;
    private float runDamageMultiplier = 1f;
    private float runAltCooldownMultiplier = 1f;
    private PassiveMod pistolPassiveMod = PassiveMod.None;
    private PassiveMod shotgunPassiveMod = PassiveMod.None;
    private PassiveMod heavyPassiveMod = PassiveMod.None;
    private AltFireMod pistolAltMod = AltFireMod.None;
    private AltFireMod shotgunAltMod = AltFireMod.None;
    private AltFireMod heavyAltMod = AltFireMod.None;
    private ProjectStructureAudioDirector combatAudioDirector;
    private readonly List<Transform> tridentNeedles = new List<Transform>();
    private readonly List<WeaponAbilityObject> vesperCoins = new List<WeaponAbilityObject>();
    private readonly List<WeaponAbilityObject> cinderBombs = new List<WeaponAbilityObject>();
    private readonly Dictionary<int, Material> fxMaterialCache = new Dictionary<int, Material>();
    private readonly Dictionary<PrimitiveType, Stack<GameObject>> fxPrimitivePool = new Dictionary<PrimitiveType, Stack<GameObject>>();
    private readonly Dictionary<GameObject, PrimitiveType> fxPrimitiveTypes = new Dictionary<GameObject, PrimitiveType>();
    private Transform fxPoolRoot;
    private WeaponAbilityObject lodestarAnchor;
    private Vector3? firstTetherPoint;
    private float redlineChargeStart = -1f;
    private float tempestStormTimer;
    private float tempestPulseTimer;
    private Transform redlineChargeFx;
    private bool queuedPrimaryFire;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private WeaponPreset ActivePreset
    {
        get
        {
            if (presets == null || presets.Length == 0) return null;
            activePresetIndex = Mathf.Clamp(activePresetIndex, 0, presets.Length - 1);
            return presets[activePresetIndex];
        }
    }

    private void Start()
    {
        player = GetComponentInParent<PlayerController>();
        if (gunBarrel == null && Camera.main != null)
            gunBarrel = Camera.main.transform;

        initialLocalRotation = transform.localRotation;
        initialLocalPosition = transform.localPosition;
        currentRecoilPosition = initialLocalPosition;

        if (restrictToUnlockedWeapons && !IsPresetUnlocked(activePresetIndex))
            activePresetIndex = CybergrindRunState.GetOrCreate().GetFirstUnlockedPreset();

        ApplyPreset(activePresetIndex);
    }

    private void Update()
    {
        HandleWeaponSwitching();
        HandleSwayAndRecoil();
        if (taggedTargetTimer > 0f)
            taggedTargetTimer -= Time.deltaTime;
        else
            taggedTarget = null;

        if (player != null && player.isUIActive) return;
        if (Mouse.current == null) return;

        bool wantsFire = Mouse.current.leftButton.wasPressedThisFrame;
        WeaponPreset preset = ActivePreset;
        if (preset != null && preset.archetype == WeaponArchetype.Magnet)
            wantsFire = Mouse.current.leftButton.isPressed;

        bool wantsAltFire = Mouse.current.rightButton.wasPressedThisFrame;

        if (preset != null && preset.archetype == WeaponArchetype.Rail)
        {
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                redlineChargeStart = Time.time;
                BeginRedlineChargeFx(preset);
            }
            if (redlineChargeStart >= 0f)
                UpdateRedlineChargeFx(Mathf.Clamp01((Time.time - redlineChargeStart) / 1.35f), preset);
            if (Mouse.current.rightButton.wasReleasedThisFrame && redlineChargeStart >= 0f)
            {
                FireRedlineCharge(Mathf.Clamp01((Time.time - redlineChargeStart) / 1.35f));
                redlineChargeStart = -1f;
                if (redlineChargeFx != null) Destroy(redlineChargeFx.gameObject);
            }
            wantsAltFire = false;
        }

        UpdatePersistentAbilities(preset);

        if (wantsFire && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + GetEffectiveFireRate(preset);
            queuedPrimaryFire = true;
        }

        if (wantsAltFire && Time.time >= nextAltFireTime)
        {
            FireAlternate();
        }
    }

    private void LateUpdate()
    {
        if (!queuedPrimaryFire) return;
        queuedPrimaryFire = false;
        if (player != null && player.isUIActive) return;
        ShootPrimary();
    }

    [ContextMenu("Rebuild Low Poly Weapon Model")]
    public void RebuildModel()
    {
        EnsureMaterials();
        ClearGeneratedModel();

        WeaponPreset preset = ActivePreset;
        if (preset == null) return;

        Transform root = new GameObject(GeneratedModelName).transform;
        root.SetParent(transform, false);
        root.localPosition = preset.family == WeaponFamily.Pistol
            ? new Vector3(0.34f, -0.46f, 0.72f)
            : preset.family == WeaponFamily.Shotgun
                ? new Vector3(0.42f, -0.52f, 0.76f)
                : new Vector3(0.46f, -0.56f, 0.82f);
        root.localRotation = Quaternion.Euler(-2f, -4f, 0f);
        root.localScale = Vector3.one * Mathf.Max(0.1f, preset.modelScale * 0.34f);

        if (removeLegacyChildMeshes)
            ClearLegacyVisualChildren(root);

        BuildWeaponWithFlexibleBuilder(root, preset);

        BuildInstalledModModel(root, preset);

        if (gunBarrel == null)
        {
            GameObject barrel = new GameObject("GeneratedBarrel");
            barrel.transform.SetParent(root, false);
            barrel.transform.localPosition = preset.family == WeaponFamily.Pistol
                ? new Vector3(0.24f, -0.1f, 1.0f)
                : preset.family == WeaponFamily.Shotgun
                    ? new Vector3(0.22f, -0.06f, 1.48f)
                    : new Vector3(0.18f, -0.04f, 1.72f);
            gunBarrel = barrel.transform;
        }
    }

    private void BuildInstalledModModel(Transform root, WeaponPreset preset)
    {
        PassiveMod passive = GetPassiveMod(preset.family);
        AltFireMod alt = GetAltMod(preset.family);
        if (passive == PassiveMod.None && alt == AltFireMod.None)
            return;

        Vector3 mount = preset.family switch
        {
            WeaponFamily.Shotgun => new Vector3(0f, 0.18f, 0.58f),
            WeaponFamily.Heavy => new Vector3(0f, 0.28f, 0.68f),
            _ => new Vector3(0f, 0.22f, 0.45f)
        };

        if (passive != PassiveMod.None)
        {
            switch (passive)
            {
                case PassiveMod.SharpenedRounds:
                    AddPart(root, "InstalledMod_DamageCore", mount + new Vector3(0.34f, 0f, 0.1f), new Vector3(0.1f, 0.3f, 0.34f), Quaternion.identity, accentMaterial);
                    AddPart(root, "InstalledMod_DamageNeedle", mount + new Vector3(0.34f, 0.18f, 0.32f), new Vector3(0.08f, 0.46f, 0.08f), Quaternion.Euler(18f, 0f, 0f), accentMaterial);
                    break;
                case PassiveMod.Stabilizer:
                    AddPart(root, "InstalledMod_StabilizerL", mount + new Vector3(-0.36f, -0.02f, 0.28f), new Vector3(0.09f, 0.14f, 0.72f), Quaternion.identity, accentMaterial);
                    AddPart(root, "InstalledMod_StabilizerR", mount + new Vector3(0.36f, -0.02f, 0.28f), new Vector3(0.09f, 0.14f, 0.72f), Quaternion.identity, accentMaterial);
                    break;
                case PassiveMod.RapidFeed:
                    AddPart(root, "InstalledMod_FeedRail", mount + new Vector3(0f, -0.18f, 0.08f), new Vector3(0.58f, 0.08f, 0.34f), Quaternion.identity, accentMaterial);
                    AddPart(root, "InstalledMod_FeedCellA", mount + new Vector3(-0.2f, -0.28f, 0.1f), new Vector3(0.12f, 0.16f, 0.2f), Quaternion.identity, accentMaterial);
                    AddPart(root, "InstalledMod_FeedCellB", mount + new Vector3(0.2f, -0.28f, 0.1f), new Vector3(0.12f, 0.16f, 0.2f), Quaternion.identity, accentMaterial);
                    break;
            }
        }

        if (alt != AltFireMod.None)
        {
            if (alt == AltFireMod.Overload)
            {
                AddPart(root, "InstalledMod_OverloadSpine", mount + new Vector3(0f, 0.34f, 0.22f), new Vector3(0.16f, 0.42f, 0.18f), Quaternion.identity, accentMaterial);
                AddPart(root, "InstalledMod_OverloadCap", mount + new Vector3(0f, 0.58f, 0.22f), new Vector3(0.44f, 0.08f, 0.36f), Quaternion.identity, accentMaterial);
            }
            else
            {
                AddPart(root, "InstalledMod_QuickChargeL", mount + new Vector3(-0.18f, 0.34f, 0.22f), new Vector3(0.08f, 0.38f, 0.16f), Quaternion.identity, accentMaterial);
                AddPart(root, "InstalledMod_QuickChargeR", mount + new Vector3(0.18f, 0.34f, 0.22f), new Vector3(0.08f, 0.38f, 0.16f), Quaternion.identity, accentMaterial);
            }
        }
    }

    private void HandleWeaponSwitching()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) SetFamily(WeaponFamily.Pistol);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SetFamily(WeaponFamily.Shotgun);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) SetFamily(WeaponFamily.Heavy);

        bool lookingAtInteractable = IsLookingAtInteractable();
        if (Keyboard.current.qKey.wasPressedThisFrame)
            CycleVariant(-1);
        if (Keyboard.current.eKey.wasPressedThisFrame && !lookingAtInteractable)
            CycleVariant(1);
    }

    private void SetFamily(WeaponFamily family)
    {
        activeFamily = family;
        SetVariantForFamily(family, 0);
        ApplyPreset(GetNextUnlockedPreset(GetPresetIndex(family, 0), family, 1));
    }

    private void CycleVariant(int direction)
    {
        SetVariantForFamily(activeFamily, GetVariantForFamily(activeFamily) + direction);
        ApplyPreset(GetNextUnlockedPreset(GetPresetIndex(activeFamily, GetVariantForFamily(activeFamily)), activeFamily, direction));
    }

    private int GetPresetIndex(WeaponFamily family, int variant)
    {
        int familyOffset = GetFamilyOffset(family);
        return Mathf.Clamp(familyOffset + Mod(variant, 3), 0, presets.Length - 1);
    }

    private int GetFamilyOffset(WeaponFamily family)
    {
        return family switch
        {
            WeaponFamily.Pistol => 0,
            WeaponFamily.Shotgun => 3,
            WeaponFamily.Heavy => 6,
            _ => 0
        };
    }

    private int GetVariantForFamily(WeaponFamily family)
    {
        return family switch
        {
            WeaponFamily.Pistol => pistolVariant,
            WeaponFamily.Shotgun => shotgunVariant,
            WeaponFamily.Heavy => heavyVariant,
            _ => 0
        };
    }

    private void SetVariantForFamily(WeaponFamily family, int variant)
    {
        switch (family)
        {
            case WeaponFamily.Pistol:
                pistolVariant = Mod(variant, 3);
                break;
            case WeaponFamily.Shotgun:
                shotgunVariant = Mod(variant, 3);
                break;
            case WeaponFamily.Heavy:
                heavyVariant = Mod(variant, 3);
                break;
        }
    }

    private int Mod(int value, int divisor)
    {
        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    public void EquipPreset(int index)
    {
        if (restrictToUnlockedWeapons && !IsPresetUnlocked(index)) return;
        ApplyPreset(index);
    }

    public string GetPresetDisplayName(int index)
    {
        if (presets == null || index < 0 || index >= presets.Length) return "Unknown Weapon";
        return string.IsNullOrWhiteSpace(presets[index].displayName) ? $"Weapon {index + 1}" : presets[index].displayName;
    }

    public string GetPresetGuideText(int index)
    {
        if (presets == null || index < 0 || index >= presets.Length) return "New weapon equipped.";

        WeaponPreset preset = presets[index];
        return $"{GetPrimaryDescriptor(preset.archetype)} Right click {GetAltDescriptor(preset.archetype)}";
    }

    public string GetActiveDisplayName()
    {
        return GetPresetDisplayName(activePresetIndex);
    }

    public string GetActiveFamilyLabel()
    {
        WeaponPreset preset = ActivePreset;
        return preset == null ? "WEAPON" : preset.family switch
        {
            WeaponFamily.Pistol => "PISTOL",
            WeaponFamily.Shotgun => "SHOTGUN",
            WeaponFamily.Heavy => "HEAVY",
            _ => "WEAPON"
        };
    }

    public WeaponFamily GetActiveFamily()
    {
        return activeFamily;
    }

    public string GetActiveVariantLabel()
    {
        WeaponPreset preset = ActivePreset;
        return preset == null ? "READY" : GetArchetypeLabel(preset.archetype).ToUpperInvariant();
    }

    public string GetActiveDescriptorLine()
    {
        WeaponPreset preset = ActivePreset;
        if (preset == null) return "No weapon ready.";
        return $"{GetPrimaryDescriptor(preset.archetype)} Right click {GetAltDescriptor(preset.archetype)}";
    }

    public string GetRunModifierStatus()
    {
        int fireRatePercent = Mathf.RoundToInt((1f - runFireRateMultiplier) * 100f);
        int damagePercent = Mathf.RoundToInt((runDamageMultiplier - 1f) * 100f);
        int altPercent = Mathf.RoundToInt((1f - runAltCooldownMultiplier) * 100f);
        PassiveMod passive = GetPassiveMod(activeFamily);
        AltFireMod alt = GetAltMod(activeFamily);

        if (fireRatePercent <= 0 && damagePercent <= 0 && altPercent <= 0 && passive == PassiveMod.None && alt == AltFireMod.None)
            return "No mods installed.";

        string modText = passive != PassiveMod.None || alt != AltFireMod.None
            ? $"  mods {FormatPassiveMod(passive)} / {FormatAltMod(alt)}"
            : string.Empty;
        return $"Mods: rate +{Mathf.Max(0, fireRatePercent)}%  damage +{Mathf.Max(0, damagePercent)}%  special +{Mathf.Max(0, altPercent)}%{modText}";
    }

    public string GetActiveStatsLine()
    {
        WeaponPreset preset = ActivePreset;
        if (preset == null) return string.Empty;

        float damage = GetEffectiveDamage(preset.damage);
        float shotsPerSecond = 1f / Mathf.Max(0.02f, GetEffectiveFireRate(preset));
        string pelletText = preset.pelletCount > 1 ? $"  pellets {preset.pelletCount}" : string.Empty;
        string spreadText = GetEffectiveSpread(preset) > 0.05f ? $"  spread {GetEffectiveSpread(preset):0.#}" : string.Empty;
        return $"Damage {damage:0.#}  rate {shotsPerSecond:0.0}/s{pelletText}{spreadText}";
    }

    private bool IsPresetUnlocked(int index)
    {
        return !restrictToUnlockedWeapons || CybergrindRunState.GetOrCreate().IsWeaponUnlocked(index);
    }

    private int GetNextUnlockedPreset(int desiredIndex, WeaponFamily family, int direction)
    {
        if (!restrictToUnlockedWeapons || presets == null || presets.Length == 0) return desiredIndex;

        int familyStart = GetFamilyOffset(family);
        int familyCount = Mathf.Min(3, presets.Length - familyStart);
        if (familyCount <= 0)
            return CybergrindRunState.GetOrCreate().GetFirstUnlockedPreset();

        int variant = Mod(desiredIndex - familyStart, familyCount);
        int step = direction < 0 ? -1 : 1;

        for (int i = 0; i < familyCount; i++)
        {
            int candidate = familyStart + Mod(variant + (i * step), familyCount);
            if (IsPresetUnlocked(candidate)) return candidate;
        }

        return CybergrindRunState.GetOrCreate().GetFirstUnlockedPreset();
    }

    private bool IsLookingAtInteractable()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        return Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 3.2f) &&
               hit.collider.GetComponent<Interactable>() != null;
    }

    private void ApplyPreset(int index)
    {
        if (presets == null || presets.Length == 0) return;
        activePresetIndex = Mathf.Clamp(index, 0, presets.Length - 1);
        if (restrictToUnlockedWeapons && !IsPresetUnlocked(activePresetIndex))
            activePresetIndex = CybergrindRunState.GetOrCreate().GetFirstUnlockedPreset();

        WeaponPreset preset = ActivePreset;
        if (preset == null) return;

        activeFamily = preset.family;
        SetVariantForFamily(preset.family, activePresetIndex - GetFamilyOffset(preset.family));

        fireRate = GetEffectiveFireRate(preset);
        bulletSpeed = preset.bulletSpeed;
        recoilForce = preset.recoilForce;

        EnsureMaterials();
        Color accent = GetEffectiveAccentColor(preset.accentColor);
        SetMaterialColor(accentMaterial, accent, accent * 0.65f);

        RebuildModel();
    }

    private void HandleSwayAndRecoil()
    {
        currentRecoilPosition = Vector3.Lerp(currentRecoilPosition, initialLocalPosition, Time.deltaTime * recoilRecoverySpeed);
        Vector3 bobOffset = Vector3.zero;
        Quaternion bobRotation = Quaternion.identity;
        if (player != null)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                Vector3 planarVelocity = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
                float speed = planarVelocity.magnitude;
                if (speed > 0.1f)
                {
                    float bobAmount = Mathf.Clamp01(speed / Mathf.Max(1f, bobSpeedReference));
                    if (player.isGrounded && !player.DebugIsSliding)
                    {
                        float bobTime = Time.time * bobFrequency;
                        bobOffset = new Vector3(
                            Mathf.Sin(bobTime * 0.5f) * bobAmplitudeX * bobAmount,
                            Mathf.Abs(Mathf.Sin(bobTime)) * -bobAmplitudeY * bobAmount,
                            Mathf.Sin(bobTime) * bobAmplitudeZ * bobAmount);
                        bobRotation = Quaternion.Euler(
                            Mathf.Sin(bobTime) * bobRotationAmount * 0.55f * bobAmount,
                            0f,
                            Mathf.Sin(bobTime * 0.5f) * bobRotationAmount * bobAmount);
                    }
                    else
                    {
                        float airTime = Time.time * airBobFrequency;
                        bobOffset = new Vector3(
                            Mathf.Sin(airTime * 0.65f) * bobAmplitudeX * 0.25f * bobAmount,
                            Mathf.Sin(airTime) * airBobAmplitudeY * bobAmount,
                            Mathf.Cos(airTime * 0.8f) * airBobAmplitudeZ * bobAmount);
                        bobRotation = Quaternion.Euler(
                            Mathf.Sin(airTime * 0.9f) * airBobRotationAmount * bobAmount,
                            0f,
                            Mathf.Sin(airTime * 0.45f) * airBobRotationAmount * bobAmount);
                    }
                }
            }
        }

        transform.localPosition = currentRecoilPosition + bobOffset;

        if (Mouse.current == null || (player != null && player.isUIActive)) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float swayY = Mathf.Clamp(mouseDelta.x * swayMultiplier, -maxSwayAmount, maxSwayAmount);
        float swayX = Mathf.Clamp(-mouseDelta.y * swayMultiplier, -maxSwayAmount, maxSwayAmount);

        Quaternion targetRotation = bobRotation * Quaternion.Euler(swayX, swayY, 0f) * initialLocalRotation;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, swaySmooth * Time.deltaTime);
    }

    private void ShootPrimary()
    {
        WeaponPreset preset = ActivePreset;
        if (preset == null) return;
        float baseDamage = GetEffectiveDamage(preset.damage);

        currentRecoilPosition -= new Vector3(0f, 0f, recoilForce * 0.1f);
        if (player != null)
            player.NotifyWeaponFired(preset.archetype == WeaponArchetype.Rail || preset.archetype == WeaponArchetype.Slab);

        if (muzzleFlash != null) muzzleFlash.Play();
        SpawnMuzzleBurst(GetBarrelWorldPosition(transform.position), preset);
        if (audioSource != null && shootSound != null) audioSource.PlayOneShot(shootSound);

        Camera mainCam = player != null && player.cameraTransform != null
            ? player.cameraTransform.GetComponent<Camera>()
            : null;
        if (mainCam == null) mainCam = Camera.main;
        Vector3 cameraPos = mainCam != null ? mainCam.transform.position : transform.position;
        Vector3 cameraForward = GetFireForward(mainCam, preset);

        if (preset.archetype == WeaponArchetype.Mortar)
        {
            SpawnCinderBomb(cameraPos, cameraForward, preset);
            return;
        }

        if (preset.archetype == WeaponArchetype.Rail)
        {
            cameraForward = ResolveAssistedShotDirection(cameraPos, cameraForward);
            FirePiercingLine(cameraPos, cameraForward, baseDamage, preset, 0.1f, 3.4f);
            return;
        }

        if (preset.archetype == WeaponArchetype.Slab)
        {
            cameraForward = ResolveAssistedShotDirection(cameraPos, cameraForward);
            FirePiercingLine(cameraPos, cameraForward, baseDamage, preset, 2.6f, 1.1f);
            return;
        }

        int pellets = Mathf.Max(1, preset.pelletCount);
        for (int i = 0; i < pellets; i++)
        {
            Vector3 direction = ApplySpread(cameraForward, GetEffectiveSpread(preset));
            Vector3 hitPoint = cameraPos + direction * maxHitScanDistance;
            RaycastHit hit;
            bool didHit = TryGetAimHit(cameraPos, direction, out hit);
            bool hitCoin = false;
            if (didHit)
            {
                hitPoint = hit.point;
                WeaponAbilityObject abilityObject = hit.collider.GetComponentInParent<WeaponAbilityObject>();
                if (abilityObject != null)
                {
                    hitCoin = abilityObject.kind == WeaponAbilityObject.Kind.Coin;
                    abilityObject.Hit(baseDamage, direction);
                }
                ApplyHitScanDamage(hit, baseDamage);
                ApplyWeaponOnHit(preset, hit, baseDamage);
                if (preset.archetype == WeaponArchetype.Splitter && tridentNeedles.Count < 24)
                    AddEmbeddedNeedle(hit.point, hit.normal, preset.accentColor);
            }

            Vector3 barrelPos = GetBarrelWorldPosition(cameraPos);
            Vector3 tracerDirection = (hitPoint - barrelPos).sqrMagnitude > 0.01f
                ? (hitPoint - barrelPos).normalized
                : direction;
            SpawnHitScanTrail(barrelPos, hitPoint, preset, hitCoin ? 0.055f : 0.018f);
            if (didHit && !hitCoin)
                SpawnImpactBurst(hitPoint, hit.normal, tracerDirection, preset.accentColor, preset.archetype == WeaponArchetype.CoreEject ? 0.22f : 0.12f, 0.16f);
        }
    }

    private void FireAlternate()
    {
        WeaponPreset preset = ActivePreset;
        if (preset == null) return;
        float baseDamage = GetEffectiveDamage(preset.damage);

        float cooldown = Mathf.Max(0.3f, preset.fireRate * 2.2f * GetEffectiveAltCooldownMultiplier(preset));
        nextAltFireTime = Time.time + cooldown;
        currentRecoilPosition -= new Vector3(0f, 0f, recoilForce * 0.14f);
        if (player != null)
            player.NotifyWeaponFired(true);

        if (muzzleFlash != null) muzzleFlash.Play();
        SpawnMuzzleBurst(GetBarrelWorldPosition(transform.position), preset, true);
        if (audioSource != null)
        {
            if (altShootSound != null) audioSource.PlayOneShot(altShootSound);
            else if (shootSound != null) audioSource.PlayOneShot(shootSound);
        }

        Camera mainCam = Camera.main;
        Vector3 cameraPos = mainCam != null ? mainCam.transform.position : transform.position;
        Vector3 forward = mainCam != null ? mainCam.transform.forward : transform.forward;

        switch (preset.archetype)
        {
            case WeaponArchetype.Marksman:
                nextAltFireTime = Time.time + 0.01f;
                vesperCoins.RemoveAll(coin => coin == null);
                if (vesperCoins.Count < 4)
                {
                    Vector3 inheritedVelocity = player != null ? player.WorldVelocity : Vector3.zero;
                    vesperCoins.Add(SpawnAbilityObject(
                        WeaponAbilityObject.Kind.Coin,
                        cameraPos + forward * 0.9f,
                        inheritedVelocity + forward * 7f + Vector3.up * 6f,
                        0.132f,
                        preset.accentColor));
                }
                break;

            case WeaponArchetype.Rail:
                FirePiercingLine(cameraPos, forward, baseDamage * 1.2f, preset, 1.5f, 5f);
                break;

            case WeaponArchetype.Splitter:
                RecallTridentNeedles(cameraPos, preset, baseDamage);
                break;

            case WeaponArchetype.CoreEject:
                SpawnAbilityObject(WeaponAbilityObject.Kind.Core, cameraPos + forward * 1.5f, forward * 11f + Vector3.up * 3f, 0.34f, preset.accentColor);
                break;

            case WeaponArchetype.Magnet:
                PlaceLodestarAnchor(cameraPos, forward, preset);
                break;

            case WeaponArchetype.Slab:
                StartCoroutine(BreachGuard(preset));
                break;

            case WeaponArchetype.Mortar:
                if (cinderBombs.Count > 0) DetonateCinderBombs(preset, baseDamage);
                else SpawnCinderBomb(cameraPos, forward, preset);
                break;

            case WeaponArchetype.Driver:
                PlaceTetherSpike(cameraPos, forward, preset, baseDamage);
                break;

            case WeaponArchetype.Arc:
                tempestStormTimer = 5f;
                tempestPulseTimer = 0f;
                SpawnRadialAbilityBurst(cameraPos + forward * 2f, 3f, preset.accentColor, 12);
                break;
        }
    }

    private void FireCoreDetonation(Vector3 origin, Vector3 direction, WeaponPreset preset, float damage)
    {
        Vector3 point = origin + direction * 18f;
        if (TryGetAimHit(origin, direction, out RaycastHit hit))
            point = hit.point;
        float radius = preset.archetype == WeaponArchetype.Mortar ? 7.2f : 5.2f;
        ApplySplashDamage(point, radius, damage, null);
        SpawnVisualTracer(GetBarrelWorldPosition(origin), (point - GetBarrelWorldPosition(origin)).normalized, preset);
        SpawnImpactBurst(point, preset.accentColor, radius * 0.1f, 0.34f);
        SpawnRadialAbilityBurst(point, radius, preset.accentColor, preset.archetype == WeaponArchetype.Mortar ? 16 : 10);
    }

    private WeaponAbilityObject SpawnAbilityObject(WeaponAbilityObject.Kind kind, Vector3 position, Vector3 velocity, float size, Color color)
    {
        PrimitiveType primitive = kind == WeaponAbilityObject.Kind.Bomb
                ? PrimitiveType.Capsule
                : PrimitiveType.Sphere;
        GameObject go = kind == WeaponAbilityObject.Kind.Coin
            ? CreateCoinObject(size, color)
            : GameObject.CreatePrimitive(primitive);
        go.name = $"Ability_{kind}";
        go.transform.position = position;
        if (kind != WeaponAbilityObject.Kind.Coin)
            go.transform.localScale = kind == WeaponAbilityObject.Kind.Bomb
                ? new Vector3(size, size * 1.5f, size)
                : Vector3.one * size;
        if (kind == WeaponAbilityObject.Kind.Coin)
        {
            Vector3 facing = Camera.main != null ? Camera.main.transform.forward : transform.forward;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, facing);
        }
        else
            go.GetComponent<Renderer>().sharedMaterial = GetFxMaterial(color, 1.8f);
        Rigidbody body = go.AddComponent<Rigidbody>();
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.linearVelocity = velocity;
        if (kind == WeaponAbilityObject.Kind.Coin)
        {
            body.maxAngularVelocity = 40f;
            body.angularVelocity = new Vector3(16f, 22f, 12f);
            Collider coinCollider = go.GetComponent<Collider>();
            if (coinCollider != null && player != null)
            {
                Collider[] ownerColliders = player.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < ownerColliders.Length; i++)
                    if (ownerColliders[i] != null) Physics.IgnoreCollision(coinCollider, ownerColliders[i], true);
            }
        }
        WeaponAbilityObject ability = go.AddComponent<WeaponAbilityObject>();
        ability.kind = kind;
        ability.owner = this;
        ability.lifetime = kind == WeaponAbilityObject.Kind.Coin ? 3.5f : 12f;
        return ability;
    }

    private GameObject CreateCoinObject(float diameter, Color accent)
    {
        GameObject root = new GameObject("CoinRoot");
        float halfThickness = diameter * 0.055f;
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(diameter, halfThickness * 2f, diameter);

        Material dark = GetFxMaterial(new Color(0.012f, 0.025f, 0.032f, 1f), 0.18f);
        Material rim = GetFxMaterial(Color.Lerp(accent, Color.white, 0.12f), 3.8f);
        Material center = GetFxMaterial(Color.white, 5.5f);

        CreateCoinPart(root.transform, "CoinBody", PrimitiveType.Cylinder, Vector3.zero,
            Quaternion.identity, new Vector3(diameter, halfThickness, diameter), dark);

        const int rimSegments = 10;
        float radius = diameter * 0.46f;
        for (int i = 0; i < rimSegments; i++)
        {
            if (i == 1 || i == 2) continue;
            float angle = i * (360f / rimSegments);
            float radians = angle * Mathf.Deg2Rad;
            Vector3 localPosition = new Vector3(Mathf.Sin(radians) * radius, 0f, Mathf.Cos(radians) * radius);
            CreateCoinPart(root.transform, $"Rim_{i:00}", PrimitiveType.Cube, localPosition,
                Quaternion.Euler(0f, angle, 0f), new Vector3(diameter * 0.26f, halfThickness * 2.5f, diameter * 0.055f), rim);
        }

        for (int face = -1; face <= 1; face += 2)
        {
            CreateCoinPart(root.transform, face > 0 ? "CenterFront" : "CenterBack", PrimitiveType.Cylinder,
                Vector3.up * (halfThickness * 1.15f * face), Quaternion.identity,
                new Vector3(diameter * 0.17f, halfThickness * 0.25f, diameter * 0.17f), center);
        }

        for (int i = 0; i < 3; i++)
        {
            float angle = 120f * i;
            float radians = angle * Mathf.Deg2Rad;
            Vector3 localPosition = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * diameter * 0.19f;
            localPosition.y = halfThickness * 1.3f;
            CreateCoinPart(root.transform, $"FaceGroove_{i}", PrimitiveType.Cube, localPosition,
                Quaternion.Euler(0f, angle, 0f), new Vector3(diameter * 0.028f, halfThickness * 0.25f, diameter * 0.24f), rim);
        }

        TrailRenderer trail = root.AddComponent<TrailRenderer>();
        trail.time = 0.11f;
        trail.minVertexDistance = 0.025f;
        trail.alignment = LineAlignment.View;
        trail.startWidth = diameter * 0.11f;
        trail.endWidth = 0f;
        trail.sharedMaterial = rim;
        trail.startColor = new Color(accent.r, accent.g, accent.b, 0.62f);
        trail.endColor = new Color(accent.r, accent.g, accent.b, 0f);
        return root;
    }

    private void CreateCoinPart(Transform parent, string name, PrimitiveType primitive, Vector3 localPosition,
        Quaternion localRotation, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;
        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null) partCollider.enabled = false;
        DestroyComponentSafe(partCollider);
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
    }

    public void HandleAbilityObjectHit(WeaponAbilityObject ability, float damage, Vector3 direction)
    {
        if (ability == null) return;
        WeaponPreset preset = ActivePreset;
        Color color = preset != null ? preset.accentColor : Color.white;
        switch (ability.kind)
        {
            case WeaponAbilityObject.Kind.Coin:
                StartCoroutine(ResolveCoinChain(ability, damage, direction, preset, color));
                break;
            case WeaponAbilityObject.Kind.Core:
                float coreMultiplier = preset != null && preset.archetype == WeaponArchetype.Rail ? 4f : 2.4f;
                ApplySplashDamage(ability.transform.position, 6f, damage * coreMultiplier, null);
                ApplyRadialForce(ability.transform.position, 6f, 14f);
                SpawnRadialAbilityBurst(ability.transform.position, 6f, color, 14);
                ChainDetonateExplosives(ability.transform.position, 8f, ability);
                if (player != null)
                {
                    float distance = Vector3.Distance(player.transform.position, ability.transform.position);
                    if (distance < 6f) player.TakeDamage(damage * Mathf.Lerp(0.75f, 0.1f, distance / 6f));
                }
                TriggerHeavyImpact(0.09f, 0.2f);
                Destroy(ability.gameObject);
                break;
            case WeaponAbilityObject.Kind.Bomb:
                Rigidbody bombBody = ability.GetComponent<Rigidbody>();
                if (bombBody != null) bombBody.AddForce(direction.normalized * 9f, ForceMode.Impulse);
                SpawnImpactBurst(ability.transform.position, color, 0.18f, 0.1f);
                break;
        }
    }

    private System.Collections.IEnumerator ResolveCoinChain(WeaponAbilityObject struckCoin, float damage, Vector3 shotDirection, WeaponPreset preset, Color color)
    {
        if (struckCoin == null) yield break;
        Vector3 chainPoint = struckCoin.transform.position;
        List<WeaponAbilityObject> remainingCoins = new List<WeaponAbilityObject>();
        for (int i = 0; i < vesperCoins.Count; i++)
        {
            WeaponAbilityObject coin = vesperCoins[i];
            if (coin != null && coin != struckCoin)
                remainingCoins.Add(coin);
        }

        int chainedCoins = 1;
        if (player != null) player.NotifyHeavyWeaponImpact(0.065f);
        yield return new WaitForSecondsRealtime(0.035f);
        while (remainingCoins.Count > 0)
        {
            int nearestIndex = 0;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < remainingCoins.Count; i++)
            {
                float distance = (remainingCoins[i].transform.position - chainPoint).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearestIndex = i;
            }

            WeaponAbilityObject nextCoin = remainingCoins[nearestIndex];
            remainingCoins.RemoveAt(nearestIndex);
            if (!nextCoin.TryResolve())
                continue;
            Vector3 nextPoint = nextCoin.transform.position;
            SpawnHitScanTrail(chainPoint, nextPoint, preset, 0.065f);
            SpawnImpactBurst(nextPoint, color, 0.16f, 0.11f);
            chainPoint = nextPoint;
            chainedCoins++;
            vesperCoins.Remove(nextCoin);
            Destroy(nextCoin.gameObject);
            yield return new WaitForSecondsRealtime(0.045f);
        }

        Collider[] targets = Physics.OverlapSphere(chainPoint, 24f, ~0, QueryTriggerInteraction.Ignore);
        HashSet<IDamageable> evaluatedTargets = new HashSet<IDamageable>();
        IDamageable best = null;
        Vector3 bestPoint = chainPoint + shotDirection * 24f;
        int bestPriority = int.MaxValue;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < targets.Length; i++)
        {
            if (IsPlayerOwnedCollider(targets[i])) continue;
            IDamageable candidate = targets[i].GetComponentInParent<IDamageable>();
            if (candidate == null || candidate is PlayerController) continue;
            if (!evaluatedTargets.Add(candidate)) continue;
            int priority = GetCoinRicochetPriority(candidate);
            float distance = Vector3.Distance(chainPoint, targets[i].bounds.center);
            if (priority > bestPriority || (priority == bestPriority && distance >= bestDistance)) continue;
            best = candidate;
            bestPoint = targets[i].bounds.center;
            bestPriority = priority;
            bestDistance = distance;
        }

        float baseMultiplier = preset != null && preset.archetype == WeaponArchetype.Rail ? 3.4f : 2.2f;
        float chainMultiplier = baseMultiplier + (chainedCoins - 1) * 0.55f;
        if (best != null)
            DealDamage(best, damage * chainMultiplier, color);
        SpawnHitScanTrail(chainPoint, bestPoint, preset, 0.075f);
        TriggerHeavyImpact(chainMultiplier > 3f ? 0.085f : 0.045f, chainMultiplier > 3f ? 0.16f : 0.1f);
        vesperCoins.Remove(struckCoin);
        Destroy(struckCoin.gameObject);
    }

    private int GetCoinRicochetPriority(IDamageable candidate)
    {
        if (candidate == taggedTarget && taggedTargetTimer > 0f)
            return 1;
        if (candidate is BasicEnemyAI enemy)
            return enemy.isBoss ? 2 : 3;
        if (candidate is Target)
            return 4;
        return 5;
    }

    public void HandleCoinGlint(WeaponAbilityObject coin, bool subtle)
    {
        if (coin == null) return;
        WeaponPreset preset = ActivePreset;
        Color color = preset != null ? preset.accentColor : new Color(0.2f, 0.85f, 1f);
        Color glintColor = Color.Lerp(color, Color.white, 0.9f);
        SpawnCoinGlint2D(coin, glintColor, subtle);
    }

    private void SpawnCoinGlint2D(WeaponAbilityObject coin, Color color, bool subtle)
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;
        GameObject graphic = new GameObject(subtle ? "CoinGlintSubtle" : "CoinGlintFlash", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rootRect = graphic.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        rootRect.SetAsLastSibling();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(100f, 100f);
        rootRect.localScale = Vector3.one * (subtle ? 0.72f : 1f);
        CanvasGroup group = graphic.GetComponent<CanvasGroup>();

        CreateCoinGlintRay(rootRect, "SlashA", new Vector2(subtle ? 58f : 92f, subtle ? 4f : 7f), Vector2.zero, color, 45f);
        CreateCoinGlintRay(rootRect, "SlashB", new Vector2(subtle ? 58f : 92f, subtle ? 4f : 7f), Vector2.zero, color, -45f);
        CreateCoinGlintRay(rootRect, "Core", new Vector2(subtle ? 12f : 18f, subtle ? 12f : 18f), Vector2.zero, Color.white, 45f);
        if (!subtle)
            CreateCoinGlintRay(rootRect, "CyanAfterimage", new Vector2(52f, 3f), new Vector2(0f, -5f),
                new Color(0.08f, 0.82f, 1f, 0.9f), 0f);

        if (subtle)
            StartCoroutine(AnimatePersistentCoinGlint(rootRect, group, coin));
        else
            StartCoroutine(AnimateCoinGlintFlash(rootRect, group, coin, 0.2f));
    }

    private static void CreateCoinGlintRay(RectTransform parent, string name, Vector2 size, Vector2 position, Color color, float rotation)
    {
        GameObject ray = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = ray.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        Image image = ray.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private System.Collections.IEnumerator AnimateCoinGlintFlash(RectTransform glint, CanvasGroup group, WeaponAbilityObject coin, float duration)
    {
        if (glint == null) yield break;
        Vector3 fullScale = glint.localScale;
        float elapsed = 0f;
        while (elapsed < duration && glint != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float pulse = Mathf.Sin(t * Mathf.PI);
            glint.localScale = fullScale * Mathf.Lerp(0.25f, 1.2f, pulse);
            if (group != null)
                group.alpha = UpdateCoinGlintScreenPosition(glint, coin) ? pulse : 0f;
            yield return null;
        }

        if (group != null)
            Destroy(group.gameObject);
    }

    private System.Collections.IEnumerator AnimatePersistentCoinGlint(RectTransform glint, CanvasGroup group, WeaponAbilityObject coin)
    {
        if (glint == null) yield break;
        Vector3 baseScale = glint.localScale;
        while (glint != null && coin != null)
        {
            float pulse = 0.5f + Mathf.Sin(Time.time * 8f) * 0.5f;
            glint.localScale = baseScale * Mathf.Lerp(0.92f, 1.08f, pulse);
            if (group != null)
                group.alpha = UpdateCoinGlintScreenPosition(glint, coin) ? Mathf.Lerp(0.18f, 0.42f, pulse) : 0f;
            yield return null;
        }

        if (group != null)
            Destroy(group.gameObject);
    }

    private bool UpdateCoinGlintScreenPosition(RectTransform glint, WeaponAbilityObject coin)
    {
        Camera camera = player != null && player.cameraTransform != null
            ? player.cameraTransform.GetComponent<Camera>()
            : null;
        if (camera == null) camera = Camera.main;
        if (glint == null || coin == null || camera == null)
            return false;

        Vector3 screenPoint = camera.WorldToScreenPoint(coin.transform.position);
        if (screenPoint.z <= 0f)
            return false;

        glint.position = new Vector3(screenPoint.x, screenPoint.y, 0f);
        glint.localRotation = Quaternion.identity;
        return true;
    }

    public void HandleAbilityObjectCollision(WeaponAbilityObject ability, Collision collision)
    {
        if (ability == null) return;
        if (ability.kind == WeaponAbilityObject.Kind.Coin)
        {
            if (collision != null && IsPlayerOwnedCollider(collision.collider)) return;
            if (!ability.TryResolve()) return;
            vesperCoins.Remove(ability);
            Destroy(ability.gameObject);
            return;
        }
        if (ability.kind != WeaponAbilityObject.Kind.Core) return;

        WeaponPreset kiln = presets != null && presets.Length > 3 ? presets[3] : ActivePreset;
        float damage = kiln != null ? GetEffectiveDamage(kiln.damage) * 2f : 40f;
        Vector3 point = ability.transform.position;
        ApplySplashDamage(point, 5.5f, damage, null);
        if (player != null)
        {
            float distance = Vector3.Distance(player.transform.position, point);
            if (distance < 5.5f)
                player.TakeDamage(damage * Mathf.Lerp(0.8f, 0.15f, distance / 5.5f));
        }
        ApplyRadialForce(point, 5.5f, 16f);
        SpawnRadialAbilityBurst(point, 5.5f, kiln != null ? kiln.accentColor : Color.red, 14);
        TriggerHeavyImpact(0.06f, 0.16f);
        Destroy(ability.gameObject);
    }

    private void RecallTridentNeedles(Vector3 destination, WeaponPreset preset, float damage)
    {
        for (int i = tridentNeedles.Count - 1; i >= 0; i--)
        {
            Transform needle = tridentNeedles[i];
            if (needle == null) continue;
            Vector3 origin = needle.position;
            Vector3 direction = (destination - origin).normalized;
            float distance = Vector3.Distance(origin, destination);
            RaycastHit[] hits = Physics.SphereCastAll(origin, 0.28f, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int h = 0; h < hits.Length; h++)
            {
                IDamageable target = hits[h].collider.GetComponentInParent<IDamageable>();
                if (target != null && !(target is PlayerController)) DealDamage(target, damage * 0.75f, preset.accentColor);
            }
            SpawnVisualTracer(origin, direction, preset);
            Destroy(needle.gameObject);
        }
        SpawnRadialAbilityBurst(destination, 2.2f, preset.accentColor, 9);
        tridentNeedles.Clear();
    }

    private void AddEmbeddedNeedle(Vector3 point, Vector3 normal, Color color)
    {
        GameObject needle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        needle.name = "TridentEmbeddedNeedle";
        needle.transform.position = point + normal * 0.08f;
        needle.transform.rotation = Quaternion.LookRotation(normal) * Quaternion.Euler(90f, 0f, 0f);
        needle.transform.localScale = new Vector3(0.035f, 0.22f, 0.035f);
        Collider collider = needle.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        needle.GetComponent<Renderer>().sharedMaterial = GetFxMaterial(Color.Lerp(color, Color.white, 0.25f), 2.8f);
        tridentNeedles.Add(needle.transform);
        SpawnImpactBurst(point, normal, -normal, color, 0.08f, 0.12f);
    }

    private void PlaceLodestarAnchor(Vector3 origin, Vector3 direction, WeaponPreset preset)
    {
        if (lodestarAnchor != null) Destroy(lodestarAnchor.gameObject);
        Vector3 point = origin + direction * 14f;
        if (TryGetAimHit(origin, direction, out RaycastHit hit)) point = hit.point;
        lodestarAnchor = SpawnAbilityObject(WeaponAbilityObject.Kind.Anchor, point, Vector3.zero, 0.28f, preset.accentColor);
        Rigidbody body = lodestarAnchor.GetComponent<Rigidbody>();
        body.isKinematic = true;
        lodestarAnchor.lifetime = 8f;
        SpawnRadialAbilityBurst(point, 4f, preset.accentColor, 10);
    }

    private System.Collections.IEnumerator BreachGuard(WeaponPreset preset)
    {
        float end = Time.time + 0.42f;
        bool reflected = false;
        while (Time.time < end)
        {
            if (player != null)
            {
                Collider[] hits = Physics.OverlapSphere(player.transform.position + player.transform.forward * 1.2f, 2.4f);
                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i].GetComponentInParent<Projectile>() != null)
                        reflected = true;
                }
            }
            yield return null;
        }
        Vector3 center = player != null ? player.transform.position + player.transform.forward * 2.5f : transform.position;
        if (reflected)
        {
            nextTimeToFire = 0f;
            ApplySplashDamage(center, 4.2f, GetEffectiveDamage(preset.damage) * 0.85f, null);
        }
        ApplyRadialForce(center, reflected ? 6f : 3.5f, reflected ? 24f : 11f);
        LaunchAbilityObjects(center, player != null ? player.transform.forward : transform.forward, reflected ? 18f : 9f, 6f);
        SpawnRadialAbilityBurst(center, reflected ? 4.2f : 2.4f, preset.accentColor, reflected ? 14 : 7);
        TriggerHeavyImpact(reflected ? 0.08f : 0.025f, reflected ? 0.18f : 0.08f);
    }

    private void SpawnCinderBomb(Vector3 origin, Vector3 direction, WeaponPreset preset)
    {
        WeaponAbilityObject bomb = SpawnAbilityObject(WeaponAbilityObject.Kind.Bomb, origin + direction * 1.5f, direction * 18f + Vector3.up * 5f, 0.42f, preset.accentColor);
        cinderBombs.Add(bomb);
    }

    private void DetonateCinderBombs(WeaponPreset preset, float damage)
    {
        for (int i = cinderBombs.Count - 1; i >= 0; i--)
            if (cinderBombs[i] != null) DetonateAbilityObject(cinderBombs[i], 7.2f, damage * 1.6f, preset.accentColor);
        cinderBombs.Clear();
    }

    private void DetonateAbilityObject(WeaponAbilityObject ability, float radius, float damage, Color color)
    {
        if (ability == null) return;
        Vector3 point = ability.transform.position;
        ApplySplashDamage(point, radius, damage, null);
        ApplyRadialForce(point, radius, 16f);
        SpawnRadialAbilityBurst(point, radius, color, 16);
        TriggerHeavyImpact(0.055f, 0.14f);
        Destroy(ability.gameObject);
    }

    private void PlaceTetherSpike(Vector3 origin, Vector3 direction, WeaponPreset preset, float damage)
    {
        Vector3 point = origin + direction * 22f;
        if (TryGetAimHit(origin, direction, out RaycastHit hit)) point = hit.point;
        if (!firstTetherPoint.HasValue)
        {
            firstTetherPoint = point;
            SpawnAbilityObject(WeaponAbilityObject.Kind.Spike, point, Vector3.zero, 0.2f, preset.accentColor).GetComponent<Rigidbody>().isKinematic = true;
            return;
        }
        Vector3 start = firstTetherPoint.Value;
        Vector3 line = point - start;
        StartCoroutine(ActiveTether(start, point, preset, damage));
        SpawnRadialAbilityBurst(point, 2f, preset.accentColor, 8);
        firstTetherPoint = null;
    }

    private void FireRedlineCharge(float charge)
    {
        WeaponPreset preset = ActivePreset;
        if (preset == null || Time.time < nextAltFireTime) return;
        nextAltFireTime = Time.time + Mathf.Lerp(0.5f, 1.5f, charge);
        Camera cam = Camera.main;
        Vector3 origin = cam != null ? cam.transform.position : transform.position;
        Vector3 direction = cam != null ? cam.transform.forward : transform.forward;
        FirePiercingLine(origin, direction, GetEffectiveDamage(preset.damage) * Mathf.Lerp(1.1f, 3.4f, charge), preset, Mathf.Lerp(0.5f, 3f, charge), Mathf.Lerp(4f, 16f, charge));
        SpawnMuzzleBurst(GetBarrelWorldPosition(origin), preset, true);
        if (charge > 0.55f) TriggerHeavyImpact(Mathf.Lerp(0.025f, 0.075f, charge), Mathf.Lerp(0.08f, 0.18f, charge));
        if (player != null) player.NotifyWeaponFired(true);
    }

    private void BeginRedlineChargeFx(WeaponPreset preset)
    {
        if (redlineChargeFx != null) Destroy(redlineChargeFx.gameObject);
        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "RedlineChargeIndicator";
        Collider collider = orb.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        orb.GetComponent<Renderer>().sharedMaterial = GetFxMaterial(preset.accentColor, 3.2f);
        redlineChargeFx = orb.transform;
    }

    private void UpdateRedlineChargeFx(float charge, WeaponPreset preset)
    {
        if (redlineChargeFx == null) return;
        redlineChargeFx.position = GetBarrelWorldPosition(transform.position);
        float pulse = 0.08f + charge * 0.28f + Mathf.Sin(Time.unscaledTime * Mathf.Lerp(8f, 28f, charge)) * 0.025f;
        redlineChargeFx.localScale = Vector3.one * pulse;
        if (charge > 0.98f && player != null) player.NotifyHeavyWeaponImpact(0.035f);
    }

    private void UpdatePersistentAbilities(WeaponPreset preset)
    {
        if (lodestarAnchor != null)
        {
            Collider[] hits = Physics.OverlapSphere(lodestarAnchor.transform.position, 7f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsPlayerOwnedCollider(hits[i])) continue;
                Vector3 pull = (lodestarAnchor.transform.position - hits[i].bounds.center);
                if (hits[i].attachedRigidbody != null) hits[i].attachedRigidbody.AddForce(pull.normalized * 18f, ForceMode.Acceleration);
                BasicEnemyAI enemy = hits[i].GetComponentInParent<BasicEnemyAI>();
                if (enemy != null)
                    enemy.transform.position += pull.normalized * Mathf.Min(2.8f * Time.deltaTime, pull.magnitude * 0.12f);
            }
        }
        if (tempestStormTimer <= 0f || preset == null) return;
        tempestStormTimer -= Time.deltaTime;
        tempestPulseTimer -= Time.deltaTime;
        if (tempestPulseTimer > 0f || player == null) return;
        tempestPulseTimer = 0.28f;
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null || controller.velocity.magnitude < 9f) return;
        SpawnRadialAbilityBurst(player.transform.position + Vector3.up * 0.8f, 2.2f, preset.accentColor, 6);
        player.NotifyHeavyWeaponImpact(0.028f);
        Collider[] nearby = Physics.OverlapSphere(player.transform.position, 3.2f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < nearby.Length; i++)
        {
            WeaponAbilityObject conductive = nearby[i].GetComponentInParent<WeaponAbilityObject>();
            if (conductive != null && conductive.conductive)
            {
                conductive.Hit(GetEffectiveDamage(preset.damage) * 0.7f, (conductive.transform.position - player.transform.position).normalized);
                continue;
            }
            IDamageable target = nearby[i].GetComponentInParent<IDamageable>();
            if (target != null && !(target is PlayerController)) DealDamage(target, GetEffectiveDamage(preset.damage) * 0.45f, preset.accentColor);
        }
    }

    private void ApplyRadialForce(Vector3 center, float radius, float force)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
            if (hits[i].attachedRigidbody != null) hits[i].attachedRigidbody.AddExplosionForce(force, center, radius, 0.5f, ForceMode.Impulse);
    }

    private void ChainDetonateExplosives(Vector3 center, float radius, WeaponAbilityObject source)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            WeaponAbilityObject ability = hits[i].GetComponentInParent<WeaponAbilityObject>();
            if (ability == null || ability == source || ability.kind != WeaponAbilityObject.Kind.Bomb) continue;
            ability.Hit(ActivePreset != null ? GetEffectiveDamage(ActivePreset.damage) : 30f, (ability.transform.position - center).normalized);
        }
    }

    private void LaunchAbilityObjects(Vector3 center, Vector3 forward, float force, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            WeaponAbilityObject ability = hits[i].GetComponentInParent<WeaponAbilityObject>();
            Rigidbody body = ability != null ? ability.GetComponent<Rigidbody>() : null;
            if (body == null || body.isKinematic) continue;
            body.AddForce((forward + Vector3.up * 0.18f).normalized * force, ForceMode.Impulse);
        }
    }

    private System.Collections.IEnumerator ActiveTether(Vector3 start, Vector3 end, WeaponPreset preset, float damage)
    {
        float elapsed = 0f;
        float tick = 0f;
        Vector3 line = end - start;
        while (elapsed < 6f)
        {
            elapsed += Time.deltaTime;
            tick -= Time.deltaTime;
            if (tick <= 0f)
            {
                tick = 0.22f;
                RaycastHit[] hits = Physics.SphereCastAll(start, 0.48f, line.normalized, line.magnitude, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits.Length; i++)
                {
                    IDamageable target = hits[i].collider.GetComponentInParent<IDamageable>();
                    if (target != null && !(target is PlayerController)) DealDamage(target, damage * 0.28f, preset.accentColor);
                    WeaponAbilityObject explosive = hits[i].collider.GetComponentInParent<WeaponAbilityObject>();
                    if (explosive != null && explosive.kind == WeaponAbilityObject.Kind.Bomb)
                        explosive.Hit(damage * 0.6f, line.normalized);
                }
                SpawnVisualTracer(start, line.normalized, preset);
            }
            yield return null;
        }
        SpawnRadialAbilityBurst(end, 1.5f, preset.accentColor, 6);
    }

    private void TriggerHeavyImpact(float hitStopDuration, float shakeAmount)
    {
        if (player != null) player.NotifyHeavyWeaponImpact(shakeAmount);
        if (hitStopDuration > 0f) StartCoroutine(ImpactPause(hitStopDuration));
    }

    private System.Collections.IEnumerator ImpactPause(float duration)
    {
        float previousScale = Time.timeScale;
        Time.timeScale = Mathf.Min(previousScale, 0.08f);
        yield return new WaitForSecondsRealtime(duration);
        if (Time.timeScale <= 0.081f) Time.timeScale = previousScale;
    }

    private void FireArcPulse(Vector3 origin, Vector3 direction, WeaponPreset preset, float damage)
    {
        Vector3 center = origin + direction * 8f;
        if (TryGetAimHit(origin, direction, out RaycastHit aimHit))
            center = aimHit.point;
        Collider[] candidates = Physics.OverlapSphere(center, 9f, ~0, QueryTriggerInteraction.Ignore);
        int struck = 0;
        for (int i = 0; i < candidates.Length && struck < 6; i++)
        {
            Collider candidate = candidates[i];
            if (candidate == null || IsPlayerOwnedCollider(candidate)) continue;
            IDamageable target = candidate.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            DealDamage(target, damage * Mathf.Pow(0.82f, struck), preset.accentColor);
            SpawnImpactBurst(candidate.bounds.center, preset.accentColor, 0.2f, 0.16f);
            Vector3 arcDirection = (candidate.bounds.center - center).normalized;
            SpawnVisualTracer(center, arcDirection, preset);
            struck++;
        }
        SpawnImpactBurst(center, preset.accentColor, 0.46f, 0.24f);
        SpawnRadialAbilityBurst(center, 4.5f, preset.accentColor, 8);
        TriggerHeavyImpact(0.04f, 0.1f);
    }

    private void SpawnRadialAbilityBurst(Vector3 center, float radius, Color color, int spokes)
    {
        Material material = GetFxMaterial(color, 2.2f);
        for (int i = 0; i < spokes; i++)
        {
            float angle = (Mathf.PI * 2f * i) / spokes;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0.12f, Mathf.Sin(angle)).normalized;
            GameObject spoke = CreateFxPrimitive(
                "WeaponAbilitySpoke",
                PrimitiveType.Cube,
                center + direction * radius * 0.5f,
                Quaternion.LookRotation(direction),
                new Vector3(0.045f, 0.045f, radius),
                material);
            StartCoroutine(ScaleBurstDown(spoke.transform, 0.28f, new Vector3(0.01f, 0.01f, radius * 1.35f)));
        }
    }

    public GameObject SpawnVisualTracer(Vector3 barrelPos, Vector3 direction, WeaponPreset preset)
    {
        return SpawnProceduralTracer(barrelPos, direction, preset);
    }

    private bool TryGetAimHit(Vector3 origin, Vector3 direction, out RaycastHit hit)
    {
        direction.Normalize();
        RaycastHit exactHit = default;
        bool hasExactHit = false;
        float obstructionDistance = maxHitScanDistance;
        RaycastHit[] exactHits = Physics.RaycastAll(origin, direction, maxHitScanDistance, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(exactHits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < exactHits.Length; i++)
        {
            Collider hitCollider = exactHits[i].collider;
            if (hitCollider == null) continue;
            if (IsPlayerOwnedCollider(hitCollider)) continue;
            if (hitCollider.transform.IsChildOf(transform)) continue;
            exactHit = exactHits[i];
            hasExactHit = true;
            obstructionDistance = exactHits[i].distance;
            break;
        }

        if (hasExactHit && IsValidAssistedTarget(exactHit.collider))
        {
            hit = exactHit;
            return true;
        }

        float assistRadius = Mathf.Max(0.08f, hitScanRadius);
        RaycastHit[] assistedHits = Physics.SphereCastAll(origin, assistRadius, direction, obstructionDistance + assistRadius, ~0, QueryTriggerInteraction.Ignore);
        float bestScore = float.MaxValue;
        RaycastHit bestHit = default;
        bool foundTarget = false;
        for (int i = 0; i < assistedHits.Length; i++)
        {
            Collider candidate = assistedHits[i].collider;
            if (candidate == null || IsPlayerOwnedCollider(candidate) || candidate.transform.IsChildOf(transform)) continue;
            if (!IsValidAssistedTarget(candidate)) continue;

            Vector3 targetPoint = candidate.bounds.ClosestPoint(origin + direction * assistedHits[i].distance);
            float forwardDistance = Vector3.Dot(targetPoint - origin, direction);
            if (forwardDistance <= 0f || forwardDistance > obstructionDistance + assistRadius) continue;
            float perpendicular = Vector3.Distance(origin + direction * forwardDistance, targetPoint);
            float score = perpendicular * 5f + forwardDistance * 0.001f;
            if (score >= bestScore) continue;
            bestScore = score;
            bestHit = assistedHits[i];
            foundTarget = true;
        }

        if (foundTarget)
        {
            hit = bestHit;
            return true;
        }

        if (hasExactHit)
        {
            hit = exactHit;
            return true;
        }

        hit = default;
        return false;
    }

    private bool IsValidAssistedTarget(Collider collider)
    {
        return collider != null &&
               (collider.GetComponentInParent<IDamageable>() != null ||
                collider.GetComponentInParent<WeaponAbilityObject>() != null);
    }

    private Vector3 ResolveAssistedShotDirection(Vector3 origin, Vector3 direction)
    {
        if (!TryGetAimHit(origin, direction, out RaycastHit hit)) return direction.normalized;
        if (!IsValidAssistedTarget(hit.collider)) return direction.normalized;
        Vector3 target = hit.collider.bounds.ClosestPoint(origin);
        if ((target - origin).sqrMagnitude < 0.01f) target = hit.point;
        return (target - origin).normalized;
    }

    private void ApplyHitScanDamage(RaycastHit hit, float damage)
    {
        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        DealDamage(damageable, damage, ActivePreset != null ? ActivePreset.accentColor : Color.white);

        if (hit.rigidbody != null)
            hit.rigidbody.AddForceAtPosition(-hit.normal * 2.8f, hit.point, ForceMode.Impulse);
    }

    private void ApplyWeaponOnHit(WeaponPreset preset, RaycastHit hit, float baseDamage)
    {
        switch (preset.archetype)
        {
            case WeaponArchetype.Marksman:
            case WeaponArchetype.CoreEject:
            case WeaponArchetype.Magnet:
            case WeaponArchetype.Slab:
            case WeaponArchetype.Mortar:
                break;
            case WeaponArchetype.Arc:
                ChainToNearbyTarget(hit, baseDamage * 0.65f, 7.2f, preset.accentColor);
                break;
        }
    }

    private void FirePiercingLine(Vector3 origin, Vector3 direction, float damage, WeaponPreset preset, float splashRadius, float force)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxHitScanDistance, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Vector3 endPoint = origin + direction * maxHitScanDistance;
        bool hitAnyDamageable = false;
        bool hitCoinAbility = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null) continue;
            if (IsPlayerOwnedCollider(hitCollider)) continue;
            if (hitCollider.transform.IsChildOf(transform)) continue;

            endPoint = hits[i].point;
            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            WeaponAbilityObject abilityObject = hitCollider.GetComponentInParent<WeaponAbilityObject>();
            if (abilityObject != null)
            {
                hitCoinAbility |= abilityObject.kind == WeaponAbilityObject.Kind.Coin;
                abilityObject.Hit(damage, direction);
            }
            if (damageable != null)
            {
                DealDamage(damageable, damage, preset.accentColor);
                hitAnyDamageable = true;
                SpawnImpactBurst(hits[i].point, hits[i].normal, direction, preset.accentColor, 0.2f + splashRadius * 0.03f, 0.18f);
            }

            if (hitCollider.attachedRigidbody != null)
                hitCollider.attachedRigidbody.AddForce(direction * force, ForceMode.Impulse);

            if (damageable == null && abilityObject == null && !hitCollider.isTrigger)
                break;
        }

        SpawnHitScanTrail(GetBarrelWorldPosition(origin), endPoint, preset);
        if (!hitAnyDamageable && !hitCoinAbility)
            SpawnImpactBurst(endPoint, -direction, direction, preset.accentColor, 0.18f, 0.16f);
    }

    private void FireFanBurst(Vector3 cameraPos, Vector3 forward, WeaponPreset preset, int pelletCount, float spread, float damage)
    {
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 direction = ApplySpread(forward, spread);
            RaycastHit hit;
            Vector3 hitPoint = cameraPos + direction * maxHitScanDistance;
            bool hitCoin = false;
            if (TryGetAimHit(cameraPos, direction, out hit))
            {
                hitPoint = hit.point;
                WeaponAbilityObject abilityObject = hit.collider.GetComponentInParent<WeaponAbilityObject>();
                if (abilityObject != null)
                {
                    hitCoin = abilityObject.kind == WeaponAbilityObject.Kind.Coin;
                    abilityObject.Hit(damage, direction);
                }
                ApplyHitScanDamage(hit, damage);
                if (!hitCoin)
                    SpawnImpactBurst(hitPoint, hit.normal, direction, preset.accentColor, 0.12f, 0.14f);
            }

            SpawnHitScanTrail(GetBarrelWorldPosition(cameraPos), hitPoint, preset);
        }
    }

    private void TagEnemy(Vector3 origin, Vector3 direction, WeaponPreset preset)
    {
        if (TryGetAimHit(origin, direction, out RaycastHit hit))
        {
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                taggedTarget = damageable;
                taggedTargetTimer = 8f;
                SpawnImpactBurst(hit.point, hit.normal, direction, preset.accentColor, 0.28f, 0.35f);
            }
        }
    }

    private void FireShockwave(Vector3 origin, Vector3 forward, WeaponPreset preset)
    {
        Vector3 center = origin + forward * 3.5f;
        float shockDamage = GetEffectiveDamage(preset.damage) * 0.65f;
        Collider[] hits = Physics.OverlapSphere(center, 3.8f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;
            if (IsPlayerOwnedCollider(hit)) continue;
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;
            DealDamage(damageable, shockDamage, preset.accentColor);
            if (hit.attachedRigidbody != null)
                hit.attachedRigidbody.AddExplosionForce(8f, center, 4.5f, 0.6f, ForceMode.Impulse);
        }

        SpawnImpactBurst(center, preset.accentColor, 0.42f, 0.24f);
    }

    private void ApplySplashDamage(Vector3 center, float radius, float damage, Collider directHit)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null) continue;
            if (IsPlayerOwnedCollider(col)) continue;

            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            float dealt = col == directHit ? damage : damage * 0.7f;
            DealDamage(damageable, dealt, ActivePreset != null ? ActivePreset.accentColor : Color.white);
        }
    }

    private void ChainToNearbyTarget(RaycastHit hit, float damage, float radius, Color color)
    {
        Collider[] hits = Physics.OverlapSphere(hit.point, radius, ~0, QueryTriggerInteraction.Ignore);
        IDamageable original = hit.collider.GetComponentInParent<IDamageable>();
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsPlayerOwnedCollider(hits[i])) continue;
            IDamageable candidate = hits[i].GetComponentInParent<IDamageable>();
            if (candidate == null || candidate == original) continue;
            DealDamage(candidate, damage, color);
            SpawnImpactBurst(hits[i].bounds.center, -transform.forward, transform.forward, color, 0.14f, 0.16f);
            break;
        }
    }

    private void DealDamage(IDamageable damageable, float damage, Color accentColor)
    {
        if (damageable == null || damageable is PlayerController)
            return;

        damageable.TakeDamage(damage);
        bool resolved = IsDamageableResolved(damageable);
        if (player != null)
            player.NotifyWeaponHit(accentColor, resolved);
        if (combatAudioDirector == null)
            combatAudioDirector = FindAnyObjectByType<ProjectStructureAudioDirector>();
        if (combatAudioDirector != null)
            combatAudioDirector.PlayCombatImpactCue(resolved, damage);
    }

    private bool IsPlayerOwnedCollider(Collider collider)
    {
        if (player == null || collider == null)
            return false;

        PlayerController colliderPlayer = collider.GetComponentInParent<PlayerController>();
        if (colliderPlayer != null && colliderPlayer == player)
            return true;

        return collider.gameObject == player.gameObject ||
               collider.transform == player.transform ||
               collider.transform.IsChildOf(player.transform) ||
               player.transform.IsChildOf(collider.transform);
    }

    private bool IsDamageableResolved(IDamageable damageable)
    {
        return damageable switch
        {
            BasicEnemyAI enemy => enemy.IsCombatResolved,
            Target target => target == null || target.currentHealth <= 0f,
            PlayerController playerTarget => playerTarget.Health01 <= 0f,
            MonoBehaviour behaviour => behaviour == null || !behaviour.isActiveAndEnabled || !behaviour.gameObject.activeInHierarchy,
            _ => false
        };
    }

    private float GetEffectiveFireRate(WeaponPreset preset)
    {
        if (preset == null) return Mathf.Max(0.02f, fireRate);
        float multiplier = runFireRateMultiplier;
        if (GetPassiveMod(preset.family) == PassiveMod.RapidFeed)
            multiplier *= 0.86f;
        return Mathf.Max(0.02f, preset.fireRate * multiplier);
    }

    private float GetEffectiveDamage(float baseDamage)
    {
        float multiplier = runDamageMultiplier;
        PassiveMod passive = GetPassiveMod(activeFamily);
        AltFireMod alt = GetAltMod(activeFamily);
        if (passive == PassiveMod.SharpenedRounds)
            multiplier *= 1.16f;
        if (alt == AltFireMod.Overload)
            multiplier *= 1.08f;
        return baseDamage * multiplier;
    }

    private float GetEffectiveSpread(WeaponPreset preset)
    {
        if (preset == null) return 0f;
        float spread = preset.spreadDegrees;
        if (GetPassiveMod(preset.family) == PassiveMod.Stabilizer)
            spread *= 0.68f;
        return spread;
    }

    private float GetEffectiveAltCooldownMultiplier(WeaponPreset preset)
    {
        float multiplier = runAltCooldownMultiplier;
        if (preset != null && GetAltMod(preset.family) == AltFireMod.QuickCharge)
            multiplier *= 0.78f;
        if (preset != null && GetAltMod(preset.family) == AltFireMod.Overload)
            multiplier *= 1.12f;
        return multiplier;
    }

    private Color GetEffectiveAccentColor(Color baseColor)
    {
        float boost = Mathf.Clamp01((runDamageMultiplier - 1f) * 1.6f + (1f - runFireRateMultiplier) * 1.2f);
        Color color = Color.Lerp(baseColor, Color.white, boost * 0.38f);
        color.a = baseColor.a;
        return color;
    }

    private Vector3 GetFireForward(Camera mainCam, WeaponPreset preset)
    {
        if (preset.archetype == WeaponArchetype.Magnet && lodestarAnchor != null)
        {
            Vector3 target = lodestarAnchor.transform.position;
            Vector3 origin = mainCam != null ? mainCam.transform.position : transform.position;
            return (target - origin).normalized;
        }

        return mainCam != null ? mainCam.transform.forward : transform.forward;
    }

    private void SpawnImpactBurst(Vector3 position, Color color, float scale, float lifetime)
    {
        SpawnImpactBurst(position, Vector3.up, transform.forward, color, scale, lifetime);
    }

    private void SpawnImpactBurst(Vector3 position, Vector3 normal, Vector3 incomingDirection, Color color, float scale, float lifetime)
    {
        Material mat = GetFxMaterial(color, 2.4f);
        int shardCount = Mathf.Clamp(Mathf.RoundToInt(5f + scale * 10f), 5, 13);
        Vector3 surfaceNormal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        Vector3 shotDirection = incomingDirection.sqrMagnitude > 0.001f ? incomingDirection.normalized : -surfaceNormal;
        SpawnImpactRing(position, surfaceNormal, color, Mathf.Max(0.18f, scale * 1.55f), lifetime * 1.4f);
        SpawnImpactCore(position, surfaceNormal, shotDirection, color, scale, lifetime);
        SpawnImpactStreak(position, surfaceNormal, shotDirection, color, Mathf.Max(0.3f, scale * 2.4f), lifetime * 0.72f);
        SpawnImpactCrown(position, surfaceNormal, shotDirection, color, scale, lifetime);

        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = CreateFxPrimitive(
                "HitShard",
                PrimitiveType.Cube,
                position,
                UnityEngine.Random.rotation,
                new Vector3(0.035f, 0.035f, Mathf.Max(0.1f, scale * UnityEngine.Random.Range(0.65f, 1.25f))),
                mat);

            Vector3 tangent = Vector3.Cross(surfaceNormal, UnityEngine.Random.onUnitSphere);
            if (tangent.sqrMagnitude < 0.001f)
                tangent = Vector3.Cross(surfaceNormal, Vector3.right);
            Vector3 scatter = (surfaceNormal * UnityEngine.Random.Range(0.2f, 0.65f) + tangent.normalized * UnityEngine.Random.Range(-1f, 1f) - shotDirection * UnityEngine.Random.Range(0.05f, 0.24f)).normalized;
            Vector3 end = position + scatter.normalized * Mathf.Max(0.22f, scale * UnityEngine.Random.Range(1.0f, 2.1f));
            StartCoroutine(AnimateImpactShard(shard.transform, lifetime, end));
        }
    }

    private void SpawnImpactCrown(Vector3 position, Vector3 normal, Vector3 incomingDirection, Color color, float scale, float lifetime)
    {
        Camera cam = Camera.main;
        Vector3 surfaceNormal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        Vector3 shotDirection = incomingDirection.sqrMagnitude > 0.001f ? incomingDirection.normalized : -surfaceNormal;
        Vector3 tangent = Vector3.ProjectOnPlane(-shotDirection, surfaceNormal);
        if (tangent.sqrMagnitude < 0.001f)
            tangent = cam != null ? Vector3.ProjectOnPlane(cam.transform.right, surfaceNormal) : Vector3.Cross(surfaceNormal, Vector3.right);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(surfaceNormal, tangent).normalized;

        int spokeCount = scale > 0.2f ? 4 : 3;
        float spokeLength = Mathf.Max(0.2f, scale * 1.9f);
        float spokeWidth = Mathf.Max(0.028f, scale * 0.16f);
        Material hot = GetFxMaterial(Color.Lerp(color, Color.white, 0.55f), 3.4f);

        for (int i = 0; i < spokeCount; i++)
        {
            float normalizedIndex = spokeCount == 1 ? 0f : i / (float)(spokeCount - 1);
            float spread = Mathf.Lerp(-36f, 36f, normalizedIndex);
            Vector3 spokeDir = (Quaternion.AngleAxis(spread, surfaceNormal) * tangent).normalized;
            GameObject spoke = CreateFxPrimitive(
                "ImpactSpoke",
                PrimitiveType.Cube,
                position + surfaceNormal * 0.05f + spokeDir * (spokeLength * 0.28f),
                Quaternion.LookRotation(spokeDir, surfaceNormal),
                new Vector3(spokeWidth, spokeWidth, spokeLength),
                hot);
            StartCoroutine(AnimateFxScale(spoke.transform, spoke.transform.localScale, new Vector3(spokeWidth * 0.25f, spokeWidth * 0.25f, 0f), Mathf.Max(0.04f, lifetime * 0.7f)));
        }

        GameObject plate = CreateFxPrimitive(
            "ImpactPlate",
            PrimitiveType.Cube,
            position + surfaceNormal * 0.03f,
            Quaternion.LookRotation(tangent, surfaceNormal),
            new Vector3(Mathf.Max(0.14f, scale * 0.95f), Mathf.Max(0.018f, scale * 0.08f), Mathf.Max(0.08f, scale * 0.42f)),
            GetFxMaterial(new Color(color.r, color.g, color.b, 0.52f), 2.1f));
        StartCoroutine(AnimateFxScale(plate.transform, plate.transform.localScale, new Vector3(plate.transform.localScale.x * 1.45f, plate.transform.localScale.y, plate.transform.localScale.z * 0.35f), Mathf.Max(0.045f, lifetime * 0.8f)));

        GameObject spark = CreateFxPrimitive(
            "ImpactSpark",
            PrimitiveType.Cube,
            position + surfaceNormal * 0.05f + bitangent * Mathf.Max(0.03f, scale * 0.12f),
            Quaternion.LookRotation(surfaceNormal, tangent),
            new Vector3(Mathf.Max(0.022f, scale * 0.09f), Mathf.Max(0.022f, scale * 0.09f), Mathf.Max(0.18f, scale * 0.78f)),
            hot);
        StartCoroutine(AnimateFxScale(spark.transform, spark.transform.localScale, Vector3.zero, Mathf.Max(0.035f, lifetime * 0.48f)));
    }

    private void SpawnImpactCore(Vector3 position, Vector3 normal, Vector3 incomingDirection, Color color, float scale, float lifetime)
    {
        Camera cam = Camera.main;
        Vector3 forward = normal.sqrMagnitude > 0.001f ? normal.normalized : (cam != null ? cam.transform.forward : transform.forward);
        Vector3 up = cam != null ? cam.transform.up : Vector3.up;
        if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.92f)
            up = Vector3.Cross(forward, incomingDirection).sqrMagnitude > 0.001f ? Vector3.Cross(forward, incomingDirection).normalized : Vector3.up;
        Material hot = GetFxMaterial(Color.Lerp(color, Color.white, 0.48f), 3.1f);
        float coreSize = Mathf.Max(0.12f, scale * 0.9f);

        GameObject core = CreateFxPrimitive(
            "HitCore",
            PrimitiveType.Cube,
            position + forward * 0.035f,
            Quaternion.LookRotation(forward, up),
            new Vector3(coreSize * 0.28f, coreSize * 0.28f, coreSize * 1.2f),
            hot);
        StartCoroutine(AnimateFxScale(core.transform, core.transform.localScale, Vector3.zero, Mathf.Max(0.05f, lifetime * 0.65f)));

        GameObject slashA = CreateFxPrimitive(
            "HitSlashA",
            PrimitiveType.Cube,
            position,
            Quaternion.LookRotation(forward, up) * Quaternion.Euler(0f, 0f, 35f),
            new Vector3(coreSize * 1.35f, coreSize * 0.07f, coreSize * 0.08f),
            hot);
        StartCoroutine(AnimateFxScale(slashA.transform, slashA.transform.localScale, Vector3.zero, Mathf.Max(0.045f, lifetime * 0.55f)));

        GameObject slashB = CreateFxPrimitive(
            "HitSlashB",
            PrimitiveType.Cube,
            position,
            Quaternion.LookRotation(forward, up) * Quaternion.Euler(0f, 0f, -55f),
            new Vector3(coreSize * 0.95f, coreSize * 0.055f, coreSize * 0.08f),
            hot);
        StartCoroutine(AnimateFxScale(slashB.transform, slashB.transform.localScale, Vector3.zero, Mathf.Max(0.04f, lifetime * 0.5f)));
    }

    private void SpawnImpactStreak(Vector3 position, Vector3 normal, Vector3 incomingDirection, Color color, float length, float lifetime)
    {
        Vector3 surfaceNormal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        Vector3 shotDirection = incomingDirection.sqrMagnitude > 0.001f ? incomingDirection.normalized : -surfaceNormal;
        Vector3 tangent = Vector3.ProjectOnPlane(-shotDirection, surfaceNormal);
        if (tangent.sqrMagnitude < 0.001f)
            tangent = Vector3.Cross(surfaceNormal, Vector3.right);
        tangent.Normalize();

        GameObject streak = CreateFxPrimitive(
            "ImpactStreak",
            PrimitiveType.Cube,
            position + surfaceNormal * 0.055f + tangent * (length * 0.22f),
            Quaternion.LookRotation(tangent, surfaceNormal),
            new Vector3(Mathf.Max(0.035f, length * 0.045f), Mathf.Max(0.035f, length * 0.045f), length),
            GetFxMaterial(Color.Lerp(color, Color.white, 0.3f), 2.8f));
        StartCoroutine(AnimateFxScale(streak.transform, streak.transform.localScale, Vector3.zero, Mathf.Max(0.04f, lifetime)));
    }

    private void SpawnMuzzleBurst(Vector3 origin, WeaponPreset preset, bool altFire = false)
    {
        if (preset == null) return;

        float length = preset.family == WeaponFamily.Heavy ? 0.92f : preset.family == WeaponFamily.Shotgun ? 0.66f : 0.46f;
        float width = preset.family == WeaponFamily.Heavy ? 0.14f : preset.family == WeaponFamily.Shotgun ? 0.11f : 0.075f;
        if (altFire)
        {
            length *= 1.35f;
            width *= 1.25f;
        }

        Camera cam = Camera.main;
        Vector3 forward = cam != null ? cam.transform.forward : transform.forward;
        Vector3 right = cam != null ? cam.transform.right : transform.right;
        Vector3 up = cam != null ? cam.transform.up : transform.up;
        Material mat = GetFxMaterial(preset.accentColor, altFire ? 3.2f : 2.4f);

        GameObject flash = CreateFxPrimitive(
            "MuzzleSlash",
            PrimitiveType.Cube,
            origin + forward * (length * 0.35f),
            Quaternion.LookRotation(forward, up),
            new Vector3(width, width, length),
            mat);
        StartCoroutine(AnimateFxScale(flash.transform, flash.transform.localScale, Vector3.zero, 0.055f));

        GameObject cross = CreateFxPrimitive(
            "MuzzleCross",
            PrimitiveType.Cube,
            origin + forward * (length * 0.18f),
            Quaternion.LookRotation(forward, up) * Quaternion.Euler(0f, 0f, 90f),
            new Vector3(width * 0.7f, width * 0.7f, length * 0.62f),
            mat);
        StartCoroutine(AnimateFxScale(cross.transform, cross.transform.localScale, Vector3.zero, 0.045f));

        GameObject bloom = CreateFxPrimitive(
            "MuzzlePlate",
            PrimitiveType.Cube,
            origin + forward * (length * 0.16f),
            Quaternion.LookRotation(forward, up),
            new Vector3(width * 2.4f, width * 0.24f, width * 0.72f),
            GetFxMaterial(Color.Lerp(preset.accentColor, Color.white, 0.35f), altFire ? 3.5f : 2.7f));
        StartCoroutine(AnimateFxScale(bloom.transform, bloom.transform.localScale, Vector3.zero, 0.05f));

        GameObject throat = CreateFxPrimitive(
            "MuzzleThroat",
            PrimitiveType.Cube,
            origin + forward * (length * 0.08f),
            Quaternion.LookRotation(forward, up),
            new Vector3(width * 0.62f, width * 0.62f, length * 0.38f),
            GetFxMaterial(Color.white, altFire ? 4.2f : 3.4f));
        StartCoroutine(AnimateFxScale(throat.transform, throat.transform.localScale, Vector3.zero, 0.04f));

        for (int i = -1; i <= 1; i += 2)
        {
            GameObject petal = CreateFxPrimitive(
                "MuzzlePetal",
                PrimitiveType.Cube,
                origin + forward * (length * 0.14f) + right * (width * 1.18f * i),
                Quaternion.LookRotation(forward, up) * Quaternion.Euler(0f, 0f, 42f * i),
                new Vector3(width * 1.18f, width * 0.18f, length * 0.34f),
                GetFxMaterial(Color.Lerp(preset.accentColor, Color.white, 0.24f), altFire ? 3.4f : 2.8f));
            StartCoroutine(AnimateFxScale(petal.transform, petal.transform.localScale, Vector3.zero, 0.045f));
        }

        if (preset.family == WeaponFamily.Shotgun || altFire)
        {
            for (int i = -1; i <= 1; i++)
            {
                GameObject side = CreateFxPrimitive(
                    "MuzzleSideFlash",
                    PrimitiveType.Cube,
                    origin + forward * 0.18f + right * (i * width * 1.9f),
                    Quaternion.LookRotation((forward + right * i * 0.12f).normalized, up),
                    new Vector3(width * 0.46f, width * 0.46f, length * 0.48f),
                    mat);
                StartCoroutine(AnimateFxScale(side.transform, side.transform.localScale, Vector3.zero, 0.045f));
            }
        }
    }

    private GameObject SpawnProceduralTracer(Vector3 barrelPos, Vector3 direction, WeaponPreset preset)
    {
        float coreScale = GetTracerCoreScale(preset);
        GameObject tracer = CreateFxPrimitive(
            "ProceduralTracer",
            PrimitiveType.Cube,
            barrelPos,
            Quaternion.LookRotation(direction),
            new Vector3(coreScale * 0.42f, coreScale * 0.42f, coreScale * 3.4f),
            GetFxMaterial(preset.accentColor, 2.4f));
        BulletTrail trail = tracer.GetComponent<BulletTrail>();
        if (trail == null) trail = tracer.AddComponent<BulletTrail>();
        trail.Configure(preset.accentColor, GetTracerWidth(preset), GetTracerTime(preset));
        TrailRenderer trailRenderer = tracer.GetComponent<TrailRenderer>();
        if (trailRenderer != null)
        {
            trailRenderer.enabled = true;
            trailRenderer.Clear();
        }
        float visualTracerSpeed = Mathf.Max(220f, preset.bulletSpeed * 2.5f);
        StartCoroutine(MoveProceduralTracer(tracer.transform, direction, visualTracerSpeed, Mathf.Max(0.12f, GetTracerTime(preset) + 0.08f)));
        return tracer;
    }

    private GameObject SpawnHitScanTrail(Vector3 start, Vector3 end, WeaponPreset preset, float lifetime = 0.018f)
    {
        Vector3 delta = end - start;
        float distance = delta.magnitude;
        if (distance < 0.02f) return null;
        Vector3 direction = delta / distance;
        float width = GetTracerWidth(preset);
        GameObject glow = CreateFxPrimitive(
            "HitScanGlow",
            PrimitiveType.Cube,
            start + delta * 0.5f,
            Quaternion.LookRotation(direction),
            new Vector3(width * 1.95f, width * 1.95f, distance),
            GetFxMaterial(new Color(preset.accentColor.r, preset.accentColor.g, preset.accentColor.b, 0.42f), 2.2f));
        StartCoroutine(FadeHitScanTrail(glow.transform, width * 1.95f, distance, Mathf.Max(0.014f, lifetime * 1.2f)));

        GameObject streak = CreateFxPrimitive(
            "HitScanStreak",
            PrimitiveType.Cube,
            start + delta * 0.5f,
            Quaternion.LookRotation(direction),
            new Vector3(width * 0.82f, width * 0.82f, distance),
            GetFxMaterial(Color.Lerp(preset.accentColor, Color.white, 0.32f), 3.6f));
        StartCoroutine(FadeHitScanTrail(streak.transform, width * 0.82f, distance, Mathf.Max(0.012f, lifetime)));
        return glow;
    }

    private System.Collections.IEnumerator FadeHitScanTrail(Transform streak, float width, float distance, float lifetime)
    {
        float elapsed = 0f;
        while (elapsed < lifetime && streak != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);
            streak.localScale = new Vector3(width * (1f - t), width * (1f - t), distance);
            yield return null;
        }
        if (streak != null) ReleaseFxPrimitive(streak.gameObject);
    }

    private System.Collections.IEnumerator MoveProceduralTracer(Transform tracer, Vector3 direction, float speed, float lifetime)
    {
        float elapsed = 0f;
        while (elapsed < lifetime && tracer != null)
        {
            elapsed += Time.deltaTime;
            tracer.position += direction * speed * Time.deltaTime;
            yield return null;
        }

        if (tracer != null)
            ReleaseFxPrimitive(tracer.gameObject);
    }

    private void SpawnImpactRing(Vector3 position, Vector3 normal, Color color, float radius, float lifetime)
    {
        Vector3 surfaceNormal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        GameObject ring = CreateFxPrimitive(
            "ImpactRing",
            PrimitiveType.Cylinder,
            position + surfaceNormal * 0.035f,
            Quaternion.FromToRotation(Vector3.up, surfaceNormal),
            new Vector3(radius * 0.3f, 0.018f, radius * 0.3f),
            GetFxMaterial(new Color(color.r, color.g, color.b, 0.55f), 1.8f));
        StartCoroutine(AnimateFxScale(ring.transform, ring.transform.localScale, new Vector3(radius, 0.018f, radius), lifetime));
    }

    private GameObject CreateFxPrimitive(string name, PrimitiveType type, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
    {
        GameObject fx = AcquireFxPrimitive(type);
        fx.name = name;
        fx.transform.position = position;
        fx.transform.rotation = rotation;
        fx.transform.localScale = scale;

        Renderer renderer = fx.GetComponent<Renderer>();
        if (renderer != null)
        {
            AssignRendererMaterial(renderer, material);
            renderer.SetPropertyBlock(null);
        }

        return fx;
    }

    private GameObject AcquireFxPrimitive(PrimitiveType type)
    {
        if (!fxPrimitivePool.TryGetValue(type, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            fxPrimitivePool[type] = pool;
        }

        GameObject fx = null;
        while (pool.Count > 0 && fx == null)
            fx = pool.Pop();

        if (fx == null)
        {
            fx = GameObject.CreatePrimitive(type);
            Collider collider = fx.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            DestroyComponentSafe(collider);
            fxPrimitiveTypes[fx] = type;
        }

        fx.transform.SetParent(null, false);
        fx.SetActive(true);
        return fx;
    }

    private void ReleaseFxPrimitive(GameObject fx)
    {
        if (fx == null) return;
        if (!fxPrimitiveTypes.TryGetValue(fx, out PrimitiveType type))
        {
            Destroy(fx);
            return;
        }

        TrailRenderer trail = fx.GetComponent<TrailRenderer>();
        if (trail != null)
        {
            trail.Clear();
            trail.enabled = false;
        }

        if (fxPoolRoot == null)
        {
            GameObject root = new GameObject("_WeaponFxPool");
            root.transform.SetParent(transform, false);
            fxPoolRoot = root.transform;
        }

        fx.SetActive(false);
        fx.transform.SetParent(fxPoolRoot, false);
        fxPrimitivePool[type].Push(fx);
    }

    private void DestroyComponentSafe(Component component)
    {
        if (component == null) return;
        if (Application.isPlaying) Destroy(component);
        else DestroyImmediate(component);
    }

    private void AssignRendererMaterial(Renderer renderer, Material material)
    {
        if (renderer == null || material == null) return;
        renderer.sharedMaterial = material;
    }

    private System.Collections.IEnumerator AnimateFxScale(Transform target, Vector3 startScale, Vector3 endScale, float lifetime)
    {
        if (target == null) yield break;
        Renderer renderer = target.GetComponent<Renderer>();
        Material mat = renderer != null ? renderer.sharedMaterial : null;
        Color startColor = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
        MaterialPropertyBlock properties = renderer != null ? new MaterialPropertyBlock() : null;
        float elapsed = 0f;
        while (elapsed < lifetime && target != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
            target.localScale = Vector3.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, t));
            if (renderer != null && properties != null)
            {
                Color c = startColor;
                c.a *= 1f - t;
                properties.SetColor(BaseColorId, c);
                properties.SetColor(ColorId, c);
                properties.SetColor(EmissionColorId, c * 1.6f);
                renderer.SetPropertyBlock(properties);
            }
            yield return null;
        }

        if (target != null)
            ReleaseFxPrimitive(target.gameObject);
    }

    private float GetTracerWidth(WeaponPreset preset)
    {
        if (preset == null) return 0.05f;
        return preset.family switch
        {
            WeaponFamily.Shotgun => 0.045f,
            WeaponFamily.Heavy => 0.09f,
            _ => 0.055f
        };
    }

    private float GetTracerTime(WeaponPreset preset)
    {
        if (preset == null) return 0.055f;
        return preset.family switch
        {
            WeaponFamily.Shotgun => 0.04f,
            WeaponFamily.Heavy => 0.075f,
            _ => 0.055f
        };
    }

    private float GetTracerCoreScale(WeaponPreset preset)
    {
        if (preset == null) return 0.045f;
        return preset.family switch
        {
            WeaponFamily.Shotgun => 0.035f,
            WeaponFamily.Heavy => 0.08f,
            _ => 0.045f
        };
    }

    private System.Collections.IEnumerator AnimateImpactShard(Transform shard, float lifetime, Vector3 endPosition)
    {
        if (shard == null) yield break;
        Vector3 start = shard.position;
        Vector3 startScale = shard.localScale;
        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
            shard.position = Vector3.Lerp(start, endPosition, t);
            shard.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        if (shard != null)
            ReleaseFxPrimitive(shard.gameObject);
    }

    private System.Collections.IEnumerator ScaleBurstDown(Transform burst, float lifetime)
    {
        yield return ScaleBurstDown(burst, lifetime, Vector3.zero);
    }

    private System.Collections.IEnumerator ScaleBurstDown(Transform burst, float lifetime, Vector3 targetScale)
    {
        if (burst == null) yield break;
        Vector3 startScale = burst.localScale;
        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
            burst.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        if (burst != null)
            ReleaseFxPrimitive(burst.gameObject);
    }

    private Vector3 ApplySpread(Vector3 direction, float spreadDegrees)
    {
        if (spreadDegrees <= 0.001f) return direction.normalized;

        float yaw = UnityEngine.Random.Range(-spreadDegrees, spreadDegrees);
        float pitch = UnityEngine.Random.Range(-spreadDegrees, spreadDegrees);
        return (Quaternion.Euler(pitch, yaw, 0f) * direction).normalized;
    }

    // Returns the world position to use as the visual barrel origin. Falls back to provided fallback if needed.
    private Vector3 GetBarrelWorldPosition(Vector3 fallback)
    {
        // Prefer an explicitly assigned barrel that is not the camera transform
        if (gunBarrel != null && gunBarrel != Camera.main?.transform)
            return gunBarrel.position;

        // If a generated barrel exists as a child from RebuildModel, use that
        Transform genBarrel = transform.Find("GeneratedBarrel");
        if (genBarrel != null)
            return genBarrel.position;

        // If camera is available, approximate a muzzle point slightly in front of it
        Camera cam = Camera.main;
        if (cam != null)
            return cam.transform.position + cam.transform.forward * muzzleForwardOffset;

        return fallback;
    }

    public void ApplyWeaponOverclock(float fireRateReductionPercent, float damageIncreasePercent, float altCooldownReductionPercent)
    {
        runFireRateMultiplier *= Mathf.Clamp(1f - fireRateReductionPercent, 0.55f, 1f);
        runDamageMultiplier *= 1f + Mathf.Max(0f, damageIncreasePercent);
        runAltCooldownMultiplier *= Mathf.Clamp(1f - altCooldownReductionPercent, 0.5f, 1f);

        WeaponPreset preset = ActivePreset;
        if (preset != null)
        {
            fireRate = GetEffectiveFireRate(preset);
            RebuildModel();
        }
    }

    public void ApplyWeaponMod(WeaponFamily family, PassiveMod passiveMod, AltFireMod altMod)
    {
        SetPassiveMod(family, passiveMod);
        SetAltMod(family, altMod);

        WeaponPreset preset = ActivePreset;
        if (preset != null)
        {
            fireRate = GetEffectiveFireRate(preset);
            RebuildModel();
        }
    }

    public string GetModPreviewLine(WeaponFamily family, PassiveMod passiveMod, AltFireMod altMod)
    {
        return $"{GetFamilyLabel(family)}: {FormatPassiveMod(passiveMod)} / {FormatAltMod(altMod)}";
    }

    public void ResetRunModifiers()
    {
        runFireRateMultiplier = 1f;
        runDamageMultiplier = 1f;
        runAltCooldownMultiplier = 1f;
        pistolVariant = 0;
        shotgunVariant = 0;
        heavyVariant = 0;
        pistolPassiveMod = PassiveMod.None;
        shotgunPassiveMod = PassiveMod.None;
        heavyPassiveMod = PassiveMod.None;
        pistolAltMod = AltFireMod.None;
        shotgunAltMod = AltFireMod.None;
        heavyAltMod = AltFireMod.None;
        activeFamily = WeaponFamily.Pistol;
        activePresetIndex = CybergrindRunState.StartingWeaponPreset;
        taggedTarget = null;
        taggedTargetTimer = 0f;
        nextAltFireTime = 0f;
        nextTimeToFire = 0f;

        if (restrictToUnlockedWeapons)
            ApplyPreset(CybergrindRunState.GetOrCreate().GetFirstUnlockedPreset());
        else
        {
            WeaponPreset preset = ActivePreset;
            if (preset != null)
                fireRate = GetEffectiveFireRate(preset);
        }
    }

    private static string GetArchetypeLabel(WeaponArchetype archetype)
    {
        return archetype switch
        {
            WeaponArchetype.CoreEject => "Core Eject",
            WeaponArchetype.Mortar => "Mortar",
            WeaponArchetype.Driver => "Driver",
            WeaponArchetype.Arc => "Arc",
            _ => archetype.ToString()
        };
    }

    private PassiveMod GetPassiveMod(WeaponFamily family)
    {
        return family switch
        {
            WeaponFamily.Shotgun => shotgunPassiveMod,
            WeaponFamily.Heavy => heavyPassiveMod,
            _ => pistolPassiveMod
        };
    }

    private AltFireMod GetAltMod(WeaponFamily family)
    {
        return family switch
        {
            WeaponFamily.Shotgun => shotgunAltMod,
            WeaponFamily.Heavy => heavyAltMod,
            _ => pistolAltMod
        };
    }

    private void SetPassiveMod(WeaponFamily family, PassiveMod mod)
    {
        switch (family)
        {
            case WeaponFamily.Shotgun:
                shotgunPassiveMod = mod;
                break;
            case WeaponFamily.Heavy:
                heavyPassiveMod = mod;
                break;
            default:
                pistolPassiveMod = mod;
                break;
        }
    }

    private void SetAltMod(WeaponFamily family, AltFireMod mod)
    {
        switch (family)
        {
            case WeaponFamily.Shotgun:
                shotgunAltMod = mod;
                break;
            case WeaponFamily.Heavy:
                heavyAltMod = mod;
                break;
            default:
                pistolAltMod = mod;
                break;
        }
    }

    private static string FormatPassiveMod(PassiveMod mod)
    {
        return mod switch
        {
            PassiveMod.SharpenedRounds => "damage",
            PassiveMod.Stabilizer => "tight spread",
            PassiveMod.RapidFeed => "faster fire",
            _ => "no passive"
        };
    }

    private static string FormatAltMod(AltFireMod mod)
    {
        return mod switch
        {
            AltFireMod.QuickCharge => "quick special",
            AltFireMod.Overload => "hard special",
            _ => "no special mod"
        };
    }

    private static string GetFamilyLabel(WeaponFamily family)
    {
        return family switch
        {
            WeaponFamily.Shotgun => "Shotgun",
            WeaponFamily.Heavy => "Heavy",
            _ => "Pistol"
        };
    }

    private static string GetPrimaryDescriptor(WeaponArchetype archetype)
    {
        return archetype switch
        {
            WeaponArchetype.Marksman => "Precision sidearm; hits arc into a nearby target.",
            WeaponArchetype.Rail => "Hand cannon; every shot penetrates an enemy line.",
            WeaponArchetype.Splitter => "Three-prong repeater for mobile close pressure.",
            WeaponArchetype.CoreEject => "Room-clearing scattergun with explosive impacts.",
            WeaponArchetype.Magnet => "Automatic flechette gun that tracks a marked target.",
            WeaponArchetype.Slab => "One brutal breaching slug with impact splash.",
            WeaponArchetype.Mortar => "Long-range demolition cannon with large blast damage.",
            WeaponArchetype.Driver => "Hypervelocity lance built to skewer packed targets.",
            WeaponArchetype.Arc => "Twin-bolt conductor that chains through crowds.",
            _ => "Reliable weapon setup."
        };
    }

    private static string GetAltDescriptor(WeaponArchetype archetype)
    {
        return archetype switch
        {
            WeaponArchetype.Marksman => "for a high-damage precision lance.",
            WeaponArchetype.Rail => "for a wide overpenetrating beam.",
            WeaponArchetype.Splitter => "for a nine-round suppression fan.",
            WeaponArchetype.CoreEject => "to detonate an ejected core at the reticle.",
            WeaponArchetype.Magnet => "to mark a target for guided primary fire.",
            WeaponArchetype.Slab => "to discharge a close-range kinetic shockwave.",
            WeaponArchetype.Mortar => "to airburst a demolition shell at the reticle.",
            WeaponArchetype.Driver => "for a high-force siege lance.",
            WeaponArchetype.Arc => "to discharge a six-target storm pulse.",
            _ => "to fire the alt shot."
        };
    }

    private void BuildWeaponWithFlexibleBuilder(Transform root, WeaponPreset preset)
    {
        WeaponModelBuilder b = new WeaponModelBuilder(root);
        Material dark = bodyMaterial;
        Material glow = accentMaterial;

        switch (preset.archetype)
        {
            case WeaponArchetype.Marksman:
                b.Box("VesperGrip", new Vector3(-0.04f, -0.38f, 0.05f), new Vector3(0.24f, 0.72f, 0.24f), dark, new Vector3(-14f, 0f, 0f));
                b.Box("VesperFrame", new Vector3(0f, -0.08f, 0.48f), new Vector3(0.46f, 0.28f, 0.92f), dark);
                b.Cylinder("VesperSuppressor", new Vector3(0f, 0f, 1.18f), 0.12f, 0.82f, dark, new Vector3(90f, 0f, 0f));
                b.Box("VesperSight", new Vector3(0f, 0.22f, 0.58f), new Vector3(0.06f, 0.18f, 0.5f), glow, new Vector3(-6f, 0f, 0f));
                b.MirroredBox("VesperRail", new Vector3(0f, 0.04f, 0.82f), new Vector3(0.06f, 0.1f, 0.72f), 0.25f, glow);
                break;

            case WeaponArchetype.Rail:
                b.Box("RedlineGrip", new Vector3(0f, -0.42f, 0.1f), new Vector3(0.3f, 0.76f, 0.28f), dark, new Vector3(-10f, 0f, 0f));
                b.Box("RedlineBreech", new Vector3(0f, -0.02f, 0.5f), new Vector3(0.68f, 0.4f, 0.72f), dark);
                b.Cylinder("RedlineCore", new Vector3(0f, 0.05f, 0.9f), 0.16f, 1.35f, glow, new Vector3(90f, 0f, 0f));
                b.Coil("RedlineCoil", new Vector3(0f, 0.05f, 0.92f), 0.3f, 0.2f, 5, dark);
                b.MirroredBox("RedlineFork", new Vector3(0f, 0.04f, 1.48f), new Vector3(0.1f, 0.16f, 0.62f), 0.28f, dark, new Vector3(0f, 5f, 0f));
                break;

            case WeaponArchetype.Splitter:
                b.Box("TridentGrip", new Vector3(0f, -0.4f, 0.12f), new Vector3(0.26f, 0.7f, 0.25f), dark, new Vector3(-12f, 0f, 0f));
                b.Sphere("TridentCell", new Vector3(0f, -0.05f, 0.52f), new Vector3(0.58f, 0.4f, 0.7f), dark);
                b.Cylinder("TridentCenter", new Vector3(0f, 0.12f, 1.15f), 0.075f, 1.1f, glow, new Vector3(90f, 0f, 0f));
                b.Cylinder("TridentLeft", new Vector3(-0.24f, -0.04f, 1.1f), 0.075f, 1.0f, glow, new Vector3(90f, 0f, -4f));
                b.Cylinder("TridentRight", new Vector3(0.24f, -0.04f, 1.1f), 0.075f, 1.0f, glow, new Vector3(90f, 0f, 4f));
                break;

            case WeaponArchetype.CoreEject:
                b.Box("KilnStock", new Vector3(0f, -0.2f, 0.05f), new Vector3(0.5f, 0.52f, 0.62f), dark, new Vector3(8f, 0f, 0f));
                b.Cylinder("KilnChamber", new Vector3(0f, 0f, 0.72f), 0.36f, 0.72f, glow, new Vector3(90f, 0f, 0f));
                b.MirroredBox("KilnHeatShield", new Vector3(0f, 0.08f, 1.08f), new Vector3(0.16f, 0.62f, 1.2f), 0.42f, dark, new Vector3(0f, 0f, 8f));
                b.Cylinder("KilnMuzzle", new Vector3(0f, 0.02f, 1.55f), 0.28f, 0.72f, dark, new Vector3(90f, 0f, 0f));
                b.Sphere("KilnCore", new Vector3(0f, 0.02f, 0.72f), Vector3.one * 0.34f, glow);
                break;

            case WeaponArchetype.Magnet:
                b.Box("LodestarBody", new Vector3(0f, -0.08f, 0.62f), new Vector3(0.58f, 0.42f, 1.1f), dark);
                b.Box("LodestarGrip", new Vector3(0f, -0.5f, 0.3f), new Vector3(0.28f, 0.64f, 0.26f), dark, new Vector3(-9f, 0f, 0f));
                b.MirroredBox("LodestarArm", new Vector3(0f, 0.12f, 1.2f), new Vector3(0.14f, 0.48f, 1.28f), 0.48f, dark, new Vector3(0f, -8f, -10f));
                b.Coil("LodestarField", new Vector3(0f, 0.08f, 1.14f), 0.4f, 0.22f, 5, glow);
                b.Sphere("LodestarLens", new Vector3(0f, 0.1f, 1.74f), new Vector3(0.28f, 0.28f, 0.2f), glow);
                break;

            case WeaponArchetype.Slab:
                b.Box("BreachBlock", new Vector3(0f, -0.02f, 0.72f), new Vector3(0.86f, 0.58f, 1.3f), dark);
                b.Box("BreachGrip", new Vector3(0f, -0.56f, 0.24f), new Vector3(0.32f, 0.7f, 0.3f), dark, new Vector3(-8f, 0f, 0f));
                b.Box("BreachRam", new Vector3(0f, 0.06f, 1.48f), new Vector3(0.34f, 0.34f, 1.22f), glow);
                b.Box("BreachMuzzle", new Vector3(0f, 0.04f, 1.98f), new Vector3(1.0f, 0.62f, 0.34f), dark);
                b.MirroredBox("BreachBrace", new Vector3(0f, -0.28f, 0.8f), new Vector3(0.16f, 0.18f, 1.0f), 0.48f, glow);
                break;

            case WeaponArchetype.Mortar:
                b.Box("CinderRear", new Vector3(0f, -0.08f, 0.3f), new Vector3(0.7f, 0.52f, 0.82f), dark);
                b.Cylinder("CinderDrum", new Vector3(0f, -0.02f, 0.9f), 0.48f, 0.62f, glow, new Vector3(0f, 0f, 90f));
                b.Cylinder("CinderTube", new Vector3(0f, 0.06f, 1.5f), 0.3f, 1.28f, dark, new Vector3(90f, 0f, 0f));
                b.MirroredBox("CinderCage", new Vector3(0f, 0.12f, 1.28f), new Vector3(0.12f, 0.72f, 1.42f), 0.48f, glow, new Vector3(0f, 0f, 8f));
                b.Box("CinderGrip", new Vector3(0f, -0.58f, 0.42f), new Vector3(0.3f, 0.7f, 0.3f), dark, new Vector3(-8f, 0f, 0f));
                break;

            case WeaponArchetype.Driver:
                b.Box("DriverRear", new Vector3(0f, -0.04f, 0.38f), new Vector3(0.82f, 0.48f, 0.86f), dark);
                b.Cylinder("DriverRam", new Vector3(0f, 0.04f, 1.28f), 0.19f, 1.9f, glow, new Vector3(90f, 0f, 0f));
                b.Coil("DriverCoil", new Vector3(0f, 0.04f, 1.18f), 0.34f, 0.25f, 6, dark);
                b.MirroredBox("DriverRail", new Vector3(0f, 0.02f, 1.48f), new Vector3(0.1f, 0.16f, 1.6f), 0.38f, glow);
                b.Box("DriverBrace", new Vector3(0f, -0.38f, 0.62f), new Vector3(1.05f, 0.18f, 0.56f), dark);
                break;

            case WeaponArchetype.Arc:
                b.Sphere("TempestReactor", new Vector3(0f, 0.02f, 0.72f), new Vector3(0.72f, 0.62f, 0.82f), glow);
                b.Box("TempestHousing", new Vector3(0f, -0.12f, 0.48f), new Vector3(0.82f, 0.42f, 0.92f), dark);
                b.MirroredBox("TempestProng", new Vector3(0f, 0.18f, 1.48f), new Vector3(0.16f, 0.2f, 1.52f), 0.42f, glow, new Vector3(0f, -8f, 0f));
                b.Cylinder("TempestCapL", new Vector3(-0.42f, 0.18f, 2.0f), 0.16f, 0.32f, dark, new Vector3(90f, 0f, 0f));
                b.Cylinder("TempestCapR", new Vector3(0.42f, 0.18f, 2.0f), 0.16f, 0.32f, dark, new Vector3(90f, 0f, 0f));
                b.Box("TempestGrip", new Vector3(0f, -0.56f, 0.34f), new Vector3(0.3f, 0.68f, 0.3f), dark, new Vector3(-8f, 0f, 0f));
                break;
        }
    }

    private void BuildHandgunModel(Transform root, WeaponPreset preset)
    {
        AddPart(root, "Grip", new Vector3(-0.05f, -0.38f, 0.12f), new Vector3(0.26f, 0.72f, 0.22f), Quaternion.Euler(-12f, 0f, 0f), bodyMaterial);
        AddPart(root, "Receiver", new Vector3(0f, -0.12f, 0.45f), new Vector3(0.48f, 0.3f, 0.82f), Quaternion.identity, bodyMaterial);
        AddPart(root, "Slide", new Vector3(0f, 0.08f, 0.48f), new Vector3(0.54f, 0.18f, 1.0f), Quaternion.identity, bodyMaterial);
        AddPart(root, "Barrel", new Vector3(0.26f, -0.12f, 1.03f), new Vector3(0.12f, 0.12f, 0.45f), Quaternion.identity, accentMaterial);
        AddPart(root, "Sight", new Vector3(0f, 0.22f, 0.83f), new Vector3(0.12f, 0.08f, 0.18f), Quaternion.identity, accentMaterial);

        if (preset.archetype == WeaponArchetype.Marksman)
        {
            AddPart(root, "VesperShroud", new Vector3(0f, 0.04f, 1.1f), new Vector3(0.3f, 0.22f, 0.68f), Quaternion.identity, bodyMaterial);
            AddPart(root, "VesperBladeSight", new Vector3(0f, 0.3f, 0.56f), new Vector3(0.05f, 0.22f, 0.52f), Quaternion.Euler(-8f, 0f, 0f), accentMaterial);
        }
        else if (preset.archetype == WeaponArchetype.Rail)
        {
            AddPart(root, "RailCoil", new Vector3(0f, 0.12f, 0.72f), new Vector3(0.66f, 0.1f, 0.82f), Quaternion.identity, accentMaterial);
            AddPart(root, "RailCompensator", new Vector3(0f, 0.02f, 1.08f), new Vector3(0.62f, 0.24f, 0.24f), Quaternion.identity, bodyMaterial);
            AddPart(root, "RailSpine", new Vector3(0f, 0.3f, 0.66f), new Vector3(0.12f, 0.32f, 1.16f), Quaternion.identity, bodyMaterial);
        }
        else if (preset.archetype == WeaponArchetype.Splitter)
        {
            AddPart(root, "SplitterForkL", new Vector3(-0.18f, -0.05f, 1.0f), new Vector3(0.12f, 0.12f, 0.42f), Quaternion.identity, accentMaterial);
            AddPart(root, "SplitterForkR", new Vector3(0.18f, -0.05f, 1.0f), new Vector3(0.12f, 0.12f, 0.42f), Quaternion.identity, accentMaterial);
            AddPart(root, "SplitterForkC", new Vector3(0f, 0.18f, 1.02f), new Vector3(0.1f, 0.1f, 0.48f), Quaternion.identity, accentMaterial);
            AddPart(root, "SplitterCell", new Vector3(0f, -0.24f, 0.55f), new Vector3(0.62f, 0.2f, 0.34f), Quaternion.identity, bodyMaterial);
        }
    }

    private void BuildShotgunModel(Transform root, WeaponPreset preset)
    {
        AddPart(root, "Stock", new Vector3(0f, -0.14f, -0.1f), new Vector3(0.46f, 0.38f, 0.58f), Quaternion.Euler(8f, 0f, 0f), bodyMaterial);
        AddPart(root, "Receiver", new Vector3(0f, -0.08f, 0.55f), new Vector3(0.56f, 0.34f, 0.72f), Quaternion.identity, bodyMaterial);
        AddPart(root, "UpperBarrel", new Vector3(0.16f, 0.04f, 1.25f), new Vector3(0.16f, 0.16f, 1.35f), Quaternion.identity, accentMaterial);
        AddPart(root, "LowerBarrel", new Vector3(-0.16f, 0.04f, 1.25f), new Vector3(0.16f, 0.16f, 1.35f), Quaternion.identity, accentMaterial);
        AddPart(root, "Pump", new Vector3(0f, -0.22f, 1.05f), new Vector3(0.62f, 0.18f, 0.62f), Quaternion.identity, bodyMaterial);
        AddPart(root, "Grip", new Vector3(0f, -0.48f, 0.36f), new Vector3(0.26f, 0.58f, 0.24f), Quaternion.Euler(-10f, 0f, 0f), bodyMaterial);

        if (preset.archetype == WeaponArchetype.CoreEject)
        {
            AddPart(root, "KilnChamber", new Vector3(0f, 0.18f, 0.56f), new Vector3(0.74f, 0.48f, 0.62f), Quaternion.identity, accentMaterial);
            AddPart(root, "KilnVentL", new Vector3(-0.42f, 0.06f, 0.72f), new Vector3(0.12f, 0.58f, 0.48f), Quaternion.Euler(0f, 0f, -12f), bodyMaterial);
            AddPart(root, "KilnVentR", new Vector3(0.42f, 0.06f, 0.72f), new Vector3(0.12f, 0.58f, 0.48f), Quaternion.Euler(0f, 0f, 12f), bodyMaterial);
        }
        else if (preset.archetype == WeaponArchetype.Magnet)
        {
            AddPart(root, "MagnetCoil", new Vector3(0f, 0.12f, 1.08f), new Vector3(0.76f, 0.16f, 0.58f), Quaternion.identity, accentMaterial);
            AddPart(root, "MagnetArmL", new Vector3(-0.48f, 0.16f, 1.2f), new Vector3(0.14f, 0.5f, 0.9f), Quaternion.Euler(0f, -8f, -10f), bodyMaterial);
            AddPart(root, "MagnetArmR", new Vector3(0.48f, 0.16f, 1.2f), new Vector3(0.14f, 0.5f, 0.9f), Quaternion.Euler(0f, 8f, 10f), bodyMaterial);
        }
        else if (preset.archetype == WeaponArchetype.Slab)
        {
            AddPart(root, "SlugRail", new Vector3(0f, 0.26f, 1.0f), new Vector3(0.18f, 0.1f, 1.1f), Quaternion.identity, accentMaterial);
            AddPart(root, "SlabWeight", new Vector3(0f, -0.18f, 0.78f), new Vector3(0.7f, 0.22f, 0.48f), Quaternion.identity, bodyMaterial);
            AddPart(root, "BreachMuzzle", new Vector3(0f, 0.02f, 1.86f), new Vector3(0.86f, 0.46f, 0.34f), Quaternion.identity, bodyMaterial);
        }
    }

    private void BuildHeavyModel(Transform root, WeaponPreset preset)
    {
        AddPart(root, "Body", new Vector3(0f, -0.02f, 0.82f), new Vector3(0.68f, 0.38f, 1.12f), Quaternion.identity, bodyMaterial);
        AddPart(root, "RearBlock", new Vector3(0f, -0.12f, 0.18f), new Vector3(0.56f, 0.42f, 0.62f), Quaternion.identity, bodyMaterial);
        AddPart(root, "Grip", new Vector3(0f, -0.5f, 0.44f), new Vector3(0.28f, 0.62f, 0.26f), Quaternion.Euler(-9f, 0f, 0f), bodyMaterial);
        AddPart(root, "Barrel", new Vector3(0f, 0.02f, 1.58f), new Vector3(0.24f, 0.24f, 1.18f), Quaternion.identity, accentMaterial);
        AddPart(root, "TopRail", new Vector3(0f, 0.22f, 0.98f), new Vector3(0.18f, 0.08f, 1.34f), Quaternion.identity, accentMaterial);

        if (preset.archetype == WeaponArchetype.Mortar)
        {
            AddPart(root, "Drum", new Vector3(0f, -0.2f, 0.94f), new Vector3(0.74f, 0.24f, 0.52f), Quaternion.identity, accentMaterial);
            AddPart(root, "MortarCageL", new Vector3(-0.44f, 0.12f, 1.25f), new Vector3(0.12f, 0.72f, 1.15f), Quaternion.Euler(0f, 0f, -8f), bodyMaterial);
            AddPart(root, "MortarCageR", new Vector3(0.44f, 0.12f, 1.25f), new Vector3(0.12f, 0.72f, 1.15f), Quaternion.Euler(0f, 0f, 8f), bodyMaterial);
        }
        else if (preset.archetype == WeaponArchetype.Driver)
        {
            AddPart(root, "DriverForkL", new Vector3(-0.18f, 0.02f, 1.82f), new Vector3(0.08f, 0.08f, 0.48f), Quaternion.identity, accentMaterial);
            AddPart(root, "DriverForkR", new Vector3(0.18f, 0.02f, 1.82f), new Vector3(0.08f, 0.08f, 0.48f), Quaternion.identity, accentMaterial);
            AddPart(root, "DriverRam", new Vector3(0f, -0.04f, 1.45f), new Vector3(0.42f, 0.42f, 1.72f), Quaternion.identity, bodyMaterial);
            AddPart(root, "DriverBrace", new Vector3(0f, -0.34f, 0.74f), new Vector3(1.0f, 0.16f, 0.5f), Quaternion.identity, accentMaterial);
        }
        else if (preset.archetype == WeaponArchetype.Arc)
        {
            AddPart(root, "ArcCap", new Vector3(0f, 0.28f, 1.36f), new Vector3(0.56f, 0.14f, 0.56f), Quaternion.identity, accentMaterial);
            AddPart(root, "ArcProngL", new Vector3(-0.36f, 0.16f, 1.72f), new Vector3(0.16f, 0.16f, 0.92f), Quaternion.Euler(0f, -8f, 0f), accentMaterial);
            AddPart(root, "ArcProngR", new Vector3(0.36f, 0.16f, 1.72f), new Vector3(0.16f, 0.16f, 0.92f), Quaternion.Euler(0f, 8f, 0f), accentMaterial);
            AddPart(root, "ArcBridge", new Vector3(0f, 0.34f, 1.86f), new Vector3(0.82f, 0.12f, 0.18f), Quaternion.identity, bodyMaterial);
        }
    }

    private void AddPart(Transform root, string name, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(root, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
    }

    private void ClearGeneratedModel()
    {
        Transform old = transform.Find(GeneratedModelName);
        if (old == null) return;

        if (gunBarrel != null && gunBarrel.IsChildOf(old))
            gunBarrel = null;

        if (Application.isPlaying)
            Destroy(old.gameObject);
        else
            DestroyImmediate(old.gameObject);
    }

    private void ClearLegacyVisualChildren(Transform newRoot)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == newRoot) continue;
            if (child.GetComponentInChildren<ParticleSystem>(true) != null) continue;
            if (child.GetComponentInChildren<Camera>(true) != null) continue;
            if (child.GetComponentInChildren<Renderer>(true) == null) continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private void EnsureMaterials()
    {
        if (bodyMaterial == null)
        {
            bodyMaterial = new Material(FindUrpShader(false)) { name = "Weapon Dark Body" };
            SetMaterialColor(bodyMaterial, new Color(0.035f, 0.038f, 0.042f), Color.black);
        }

        if (accentMaterial == null)
        {
            accentMaterial = new Material(FindUrpShader(false)) { name = "Weapon Accent" };
            SetMaterialColor(accentMaterial, Color.cyan, Color.cyan * 0.45f);
        }
    }

    private void SetMaterialColor(Material material, Color baseColor, Color emission)
    {
        if (material == null) return;
        if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, baseColor);
        if (material.HasProperty(ColorId)) material.SetColor(ColorId, baseColor);

        if (emission.maxColorComponent > 0f)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty(EmissionColorId)) material.SetColor(EmissionColorId, emission);
        }
    }

    private Material GetFxMaterial(Color color, float emissionStrength)
    {
        Color32 packed = color;
        int emissionStep = Mathf.RoundToInt(Mathf.Max(0f, emissionStrength) * 10f);
        int key = packed.r;
        key = (key * 397) ^ packed.g;
        key = (key * 397) ^ packed.b;
        key = (key * 397) ^ packed.a;
        key = (key * 397) ^ emissionStep;
        if (fxMaterialCache.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        Material material = new Material(FindUrpShader(true)) { name = "RuntimeWeaponFX" };
        SetMaterialColor(material, color, color * Mathf.Max(0f, emissionStrength));
        fxMaterialCache[key] = material;
        return material;
    }

    private void OnDestroy()
    {
        foreach (Material material in fxMaterialCache.Values)
        {
            if (material != null)
                Destroy(material);
        }
        fxMaterialCache.Clear();
    }

    private Shader FindUrpShader(bool unlit)
    {
        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader;
    }
}
