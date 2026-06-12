using System;
using UnityEngine;
using UnityEngine.InputSystem;

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
        new WeaponPreset { displayName = "Pistol - Marksman", family = WeaponFamily.Pistol, archetype = WeaponArchetype.Marksman, accentColor = new Color(0.0f, 0.62f, 0.9f), fireRate = 0.23f, bulletSpeed = 82f, damage = 24f, pelletCount = 1, spreadDegrees = 0.05f, recoilForce = 3.6f, modelScale = 0.78f },
        new WeaponPreset { displayName = "Pistol - Rail", family = WeaponFamily.Pistol, archetype = WeaponArchetype.Rail, accentColor = new Color(0.92f, 0.16f, 0.08f), fireRate = 0.7f, bulletSpeed = 115f, damage = 72f, pelletCount = 1, spreadDegrees = 0f, recoilForce = 8.8f, modelScale = 0.82f },
        new WeaponPreset { displayName = "Pistol - Splitter", family = WeaponFamily.Pistol, archetype = WeaponArchetype.Splitter, accentColor = new Color(0.1f, 0.85f, 0.28f), fireRate = 0.2f, bulletSpeed = 70f, damage = 11f, pelletCount = 3, spreadDegrees = 2.1f, recoilForce = 4.2f, modelScale = 0.74f },
        new WeaponPreset { displayName = "Shotgun - Core Eject", family = WeaponFamily.Shotgun, archetype = WeaponArchetype.CoreEject, accentColor = new Color(0.95f, 0.58f, 0.08f), fireRate = 0.78f, bulletSpeed = 55f, damage = 8.5f, pelletCount = 9, spreadDegrees = 5.2f, recoilForce = 8.8f, modelScale = 0.76f },
        new WeaponPreset { displayName = "Shotgun - Magnet", family = WeaponFamily.Shotgun, archetype = WeaponArchetype.Magnet, accentColor = new Color(0.78f, 0.08f, 0.75f), fireRate = 0.48f, bulletSpeed = 62f, damage = 6.5f, pelletCount = 6, spreadDegrees = 3.6f, recoilForce = 6.5f, modelScale = 0.72f },
        new WeaponPreset { displayName = "Shotgun - Slab", family = WeaponFamily.Shotgun, archetype = WeaponArchetype.Slab, accentColor = new Color(0.52f, 0.38f, 0.95f), fireRate = 1.05f, bulletSpeed = 105f, damage = 95f, pelletCount = 1, spreadDegrees = 0.08f, recoilForce = 11f, modelScale = 0.78f },
        new WeaponPreset { displayName = "Heavy - Mortar", family = WeaponFamily.Heavy, archetype = WeaponArchetype.Mortar, accentColor = new Color(1f, 0.42f, 0.22f), fireRate = 1.1f, bulletSpeed = 72f, damage = 54f, pelletCount = 1, spreadDegrees = 0.35f, recoilForce = 10.2f, modelScale = 0.92f },
        new WeaponPreset { displayName = "Heavy - Driver", family = WeaponFamily.Heavy, archetype = WeaponArchetype.Driver, accentColor = new Color(0.58f, 0.92f, 1f), fireRate = 0.92f, bulletSpeed = 128f, damage = 68f, pelletCount = 1, spreadDegrees = 0.02f, recoilForce = 10.8f, modelScale = 0.88f },
        new WeaponPreset { displayName = "Heavy - Arc", family = WeaponFamily.Heavy, archetype = WeaponArchetype.Arc, accentColor = new Color(0.84f, 0.78f, 0.28f), fireRate = 0.62f, bulletSpeed = 78f, damage = 36f, pelletCount = 2, spreadDegrees = 1.5f, recoilForce = 9.6f, modelScale = 0.9f }
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

        if (wantsFire && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + GetEffectiveFireRate(preset);
            ShootPrimary();
        }

        if (wantsAltFire && Time.time >= nextAltFireTime)
        {
            FireAlternate();
        }
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

        if (preset.family == WeaponFamily.Pistol)
            BuildHandgunModel(root, preset);
        else if (preset.family == WeaponFamily.Shotgun)
            BuildShotgunModel(root, preset);
        else
            BuildHeavyModel(root, preset);

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
        int variant = GetVariantForFamily(family);
        ApplyPreset(GetNextUnlockedPreset(GetPresetIndex(family, variant), family, 1));
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
            return "No active boosts.";

        string modText = passive != PassiveMod.None || alt != AltFireMod.None
            ? $"  mods {FormatPassiveMod(passive)} / {FormatAltMod(alt)}"
            : string.Empty;
        return $"Boosts: fire rate +{Mathf.Max(0, fireRatePercent)}%  damage +{Mathf.Max(0, damagePercent)}%  alt +{Mathf.Max(0, altPercent)}%{modText}";
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
        if (player != null)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                Vector3 planarVelocity = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
                float speed = planarVelocity.magnitude;
                if (speed > 0.1f)
                {
                    float bobTime = Time.time * (player.isGrounded ? 9.5f : 6f);
                    float bobAmount = Mathf.Clamp01(speed / 18f);
                    bobOffset = new Vector3(
                        Mathf.Sin(bobTime * 0.5f) * 0.012f * bobAmount,
                        Mathf.Abs(Mathf.Sin(bobTime)) * -0.02f * bobAmount,
                        0f);
                }
            }
        }

        transform.localPosition = currentRecoilPosition + bobOffset;

        if (Mouse.current == null || (player != null && player.isUIActive)) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float swayY = Mathf.Clamp(mouseDelta.x * swayMultiplier, -maxSwayAmount, maxSwayAmount);
        float swayX = Mathf.Clamp(-mouseDelta.y * swayMultiplier, -maxSwayAmount, maxSwayAmount);

        Quaternion targetRotation = Quaternion.Euler(swayX, swayY, 0f) * initialLocalRotation;
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

        Camera mainCam = Camera.main;
        Vector3 cameraPos = mainCam != null ? mainCam.transform.position : transform.position;
        Vector3 cameraForward = GetFireForward(mainCam, preset);

        if (preset.archetype == WeaponArchetype.Rail)
        {
            FirePiercingLine(cameraPos, cameraForward, baseDamage, preset, 0.1f, 3.4f);
            return;
        }

        if (preset.archetype == WeaponArchetype.Slab)
        {
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
            if (didHit)
            {
                hitPoint = hit.point;
                ApplyHitScanDamage(hit, baseDamage);
                ApplyWeaponOnHit(preset, hit, baseDamage);
            }

            Vector3 barrelPos = GetBarrelWorldPosition(cameraPos);
            Vector3 tracerDirection = (hitPoint - barrelPos).sqrMagnitude > 0.01f
                ? (hitPoint - barrelPos).normalized
                : direction;
            SpawnVisualTracer(barrelPos, tracerDirection, preset);
            if (didHit)
                SpawnImpactBurst(hitPoint, preset.accentColor, preset.archetype == WeaponArchetype.CoreEject ? 0.22f : 0.12f, 0.16f);
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
                FirePiercingLine(cameraPos, forward, baseDamage * 1.65f, preset, 4.5f, 2.2f);
                break;

            case WeaponArchetype.Rail:
                FirePiercingLine(cameraPos, forward, baseDamage * 1.2f, preset, 1.5f, 5f);
                break;

            case WeaponArchetype.Splitter:
                FireFanBurst(cameraPos, forward, preset, 9, Mathf.Max(4.8f, GetEffectiveSpread(preset) * 2.2f), baseDamage * 0.9f);
                break;

            case WeaponArchetype.CoreEject:
                FirePiercingLine(cameraPos, forward, baseDamage * 0.9f, preset, 5.5f, 1.4f);
                break;

            case WeaponArchetype.Magnet:
                TagEnemy(cameraPos, forward, preset);
                break;

            case WeaponArchetype.Slab:
                FireShockwave(cameraPos, forward, preset);
                break;

            case WeaponArchetype.Mortar:
                FirePiercingLine(cameraPos, forward, baseDamage * 0.9f, preset, 6.4f, 2.2f);
                break;

            case WeaponArchetype.Driver:
                FirePiercingLine(cameraPos, forward, baseDamage * 1.1f, preset, 2.8f, 4.6f);
                break;

            case WeaponArchetype.Arc:
                FireFanBurst(cameraPos, forward, preset, 5, 2.4f, baseDamage * 0.8f);
                break;
        }
    }

    public GameObject SpawnVisualTracer(Vector3 barrelPos, Vector3 direction, WeaponPreset preset)
    {
        if (bulletPrefab == null)
        {
            return SpawnProceduralTracer(barrelPos, direction, preset);
        }

        // Create visual tracer from barrel (will be seen but won't cause collision/damage)
        GameObject tracer = Instantiate(bulletPrefab, barrelPos, Quaternion.LookRotation(direction));
        DisableTracerCollisionAndDamage(tracer);

        Rigidbody rb = tracer.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.detectCollisions = false;
#pragma warning disable 0618
            rb.velocity = direction * preset.bulletSpeed;
#pragma warning restore 0618
        }

        BulletTrail trail = tracer.GetComponent<BulletTrail>();
        if (trail == null)
            trail = tracer.AddComponent<BulletTrail>();
        trail.Configure(preset.accentColor, GetTracerWidth(preset), GetTracerTime(preset));

        Renderer renderer = tracer.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            AssignRendererMaterial(renderer, GetFxMaterial(preset.accentColor, 2.4f));
            tracer.transform.localScale = Vector3.one * GetTracerCoreScale(preset);
        }

        // Short lifetime for visual effect. In edit-mode probes, leave cleanup to the caller.
        if (Application.isPlaying)
            Destroy(tracer, Mathf.Max(0.18f, GetTracerTime(preset) + 0.16f));
        return tracer;
    }

    private void DisableTracerCollisionAndDamage(GameObject tracer)
    {
        if (tracer == null) return;

        Projectile[] projectiles = tracer.GetComponentsInChildren<Projectile>(true);
        for (int i = 0; i < projectiles.Length; i++)
        {
            if (projectiles[i] == null) continue;
            projectiles[i].owner = player != null ? player.gameObject : gameObject;
            projectiles[i].enabled = false;
        }

        Collider[] colliders = tracer.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        Rigidbody[] bodies = tracer.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null) continue;
            bodies[i].useGravity = false;
            bodies[i].detectCollisions = false;
        }
    }

    private bool TryGetAimHit(Vector3 origin, Vector3 direction, out RaycastHit hit)
    {
        RaycastHit[] hits = hitScanRadius > 0.001f
            ? Physics.SphereCastAll(origin, hitScanRadius, direction, maxHitScanDistance, ~0, QueryTriggerInteraction.Ignore)
            : Physics.RaycastAll(origin, direction, maxHitScanDistance, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int h = 0; h < hits.Length; h++)
        {
            Collider hitCollider = hits[h].collider;
            if (hitCollider == null) continue;
            if (IsPlayerOwnedCollider(hitCollider)) continue;
            if (hitCollider.transform.IsChildOf(transform)) continue;

            hit = hits[h];
            return true;
        }

        hit = default;
        return false;
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
                ChainToNearbyTarget(hit, baseDamage * 0.55f, 5.8f, preset.accentColor);
                break;
            case WeaponArchetype.CoreEject:
                ApplySplashDamage(hit.point, 3.4f, baseDamage * 0.65f, hit.collider);
                break;
            case WeaponArchetype.Magnet:
                if (taggedTarget != null)
                    ApplySplashDamage(hit.point, 1.8f, baseDamage * 0.35f, hit.collider);
                break;
            case WeaponArchetype.Slab:
                ApplySplashDamage(hit.point, 2.1f, baseDamage * 0.5f, hit.collider);
                break;
            case WeaponArchetype.Mortar:
                ApplySplashDamage(hit.point, 4.6f, baseDamage * 0.85f, hit.collider);
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
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null) continue;
            if (IsPlayerOwnedCollider(hitCollider)) continue;
            if (hitCollider.transform.IsChildOf(transform)) continue;

            endPoint = hits[i].point;
            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                DealDamage(damageable, damage, preset.accentColor);
                hitAnyDamageable = true;
                SpawnImpactBurst(hits[i].point, preset.accentColor, 0.2f + splashRadius * 0.03f, 0.18f);
            }

            if (hitCollider.attachedRigidbody != null)
                hitCollider.attachedRigidbody.AddForce(direction * force, ForceMode.Impulse);

            if (damageable == null && !hitCollider.isTrigger)
                break;
        }

        if (splashRadius > 0.05f)
            ApplySplashDamage(endPoint, splashRadius, damage * 0.35f, null);

        SpawnVisualTracer(GetBarrelWorldPosition(origin), direction, preset);
        if (!hitAnyDamageable)
            SpawnImpactBurst(endPoint, preset.accentColor, 0.18f, 0.16f);
    }

    private void FireFanBurst(Vector3 cameraPos, Vector3 forward, WeaponPreset preset, int pelletCount, float spread, float damage)
    {
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 direction = ApplySpread(forward, spread);
            RaycastHit hit;
            Vector3 hitPoint = cameraPos + direction * maxHitScanDistance;
            if (TryGetAimHit(cameraPos, direction, out hit))
            {
                hitPoint = hit.point;
                ApplyHitScanDamage(hit, damage);
                SpawnImpactBurst(hitPoint, preset.accentColor, 0.12f, 0.14f);
            }

            SpawnVisualTracer(GetBarrelWorldPosition(cameraPos), direction, preset);
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
                SpawnImpactBurst(hit.point, preset.accentColor, 0.28f, 0.35f);
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
            SpawnImpactBurst(hits[i].bounds.center, color, 0.14f, 0.16f);
            break;
        }
    }

    private void DealDamage(IDamageable damageable, float damage, Color accentColor)
    {
        if (damageable == null || damageable is PlayerController)
            return;

        damageable.TakeDamage(damage);
        bool resolved = IsDamageableResolved(damageable);
        CombatFeedbackHUD.RegisterHit(damage, resolved, accentColor, GetDamageableLabel(damageable));
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

    private string GetDamageableLabel(IDamageable damageable)
    {
        return damageable switch
        {
            BasicEnemyAI enemy => enemy.displayName,
            Target target => target != null ? target.gameObject.name : "Target",
            MonoBehaviour behaviour => behaviour != null ? behaviour.gameObject.name : "Hostile",
            _ => "Hostile"
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
        if (preset.archetype == WeaponArchetype.Magnet && taggedTarget is MonoBehaviour taggedBehaviour && taggedBehaviour != null)
        {
            Vector3 target = taggedBehaviour.transform.position + Vector3.up * 0.8f;
            Vector3 origin = mainCam != null ? mainCam.transform.position : transform.position;
            return (target - origin).normalized;
        }

        return mainCam != null ? mainCam.transform.forward : transform.forward;
    }

    private void SpawnImpactBurst(Vector3 position, Color color, float scale, float lifetime)
    {
        Material mat = GetFxMaterial(color, 2.4f);
        int shardCount = Mathf.Clamp(Mathf.RoundToInt(5f + scale * 10f), 5, 13);
        SpawnImpactRing(position, color, Mathf.Max(0.18f, scale * 1.55f), lifetime * 1.4f);
        SpawnImpactCore(position, color, scale, lifetime);

        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = CreateFxPrimitive(
                "HitShard",
                PrimitiveType.Cube,
                position,
                UnityEngine.Random.rotation,
                new Vector3(0.035f, 0.035f, Mathf.Max(0.1f, scale * UnityEngine.Random.Range(0.65f, 1.25f))),
                mat);

            Vector3 scatter = UnityEngine.Random.onUnitSphere;
            scatter.y = Mathf.Abs(scatter.y) * 0.55f + 0.08f;
            Vector3 end = position + scatter.normalized * Mathf.Max(0.22f, scale * UnityEngine.Random.Range(1.0f, 2.1f));
            StartCoroutine(AnimateImpactShard(shard.transform, lifetime, end));
        }
    }

    private void SpawnImpactCore(Vector3 position, Color color, float scale, float lifetime)
    {
        Camera cam = Camera.main;
        Vector3 forward = cam != null ? cam.transform.forward : transform.forward;
        Vector3 up = cam != null ? cam.transform.up : Vector3.up;
        Material hot = GetFxMaterial(Color.Lerp(color, Color.white, 0.48f), 3.1f);
        float coreSize = Mathf.Max(0.12f, scale * 0.9f);

        GameObject core = CreateFxPrimitive(
            "HitCore",
            PrimitiveType.Cube,
            position - forward * 0.03f,
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
            "MuzzleBloom",
            PrimitiveType.Sphere,
            origin + forward * (length * 0.16f),
            Quaternion.identity,
            Vector3.one * (width * 2.2f),
            GetFxMaterial(Color.Lerp(preset.accentColor, Color.white, 0.35f), altFire ? 3.5f : 2.7f));
        StartCoroutine(AnimateFxScale(bloom.transform, bloom.transform.localScale, Vector3.zero, 0.05f));

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
        GameObject tracer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tracer.name = "ProceduralTracer";
        tracer.transform.position = barrelPos;
        tracer.transform.rotation = Quaternion.LookRotation(direction);
        tracer.transform.localScale = Vector3.one * GetTracerCoreScale(preset);
        Collider collider = tracer.GetComponent<Collider>();
        DestroyComponentSafe(collider);
        Renderer renderer = tracer.GetComponent<Renderer>();
        if (renderer != null)
            AssignRendererMaterial(renderer, GetFxMaterial(preset.accentColor, 2.4f));
        BulletTrail trail = tracer.AddComponent<BulletTrail>();
        trail.Configure(preset.accentColor, GetTracerWidth(preset), GetTracerTime(preset));
        StartCoroutine(MoveProceduralTracer(tracer.transform, direction, preset.bulletSpeed, Mathf.Max(0.12f, GetTracerTime(preset) + 0.08f)));
        return tracer;
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
            Destroy(tracer.gameObject);
    }

    private void SpawnImpactRing(Vector3 position, Color color, float radius, float lifetime)
    {
        GameObject ring = CreateFxPrimitive(
            "ImpactRing",
            PrimitiveType.Cylinder,
            position + Vector3.up * 0.025f,
            Quaternion.identity,
            new Vector3(radius * 0.3f, 0.018f, radius * 0.3f),
            GetFxMaterial(new Color(color.r, color.g, color.b, 0.55f), 1.8f));
        StartCoroutine(AnimateFxScale(ring.transform, ring.transform.localScale, new Vector3(radius, 0.018f, radius), lifetime));
    }

    private GameObject CreateFxPrimitive(string name, PrimitiveType type, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
    {
        GameObject fx = GameObject.CreatePrimitive(type);
        fx.name = name;
        fx.transform.position = position;
        fx.transform.rotation = rotation;
        fx.transform.localScale = scale;
        DestroyComponentSafe(fx.GetComponent<Collider>());

        Renderer renderer = fx.GetComponent<Renderer>();
        if (renderer != null)
            AssignRendererMaterial(renderer, material);

        return fx;
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
        if (Application.isPlaying) renderer.material = material;
        else renderer.sharedMaterial = material;
    }

    private System.Collections.IEnumerator AnimateFxScale(Transform target, Vector3 startScale, Vector3 endScale, float lifetime)
    {
        if (target == null) yield break;
        Renderer renderer = target.GetComponent<Renderer>();
        Material mat = renderer != null ? renderer.material : null;
        Color startColor = mat != null ? mat.color : Color.white;
        float elapsed = 0f;
        while (elapsed < lifetime && target != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
            target.localScale = Vector3.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, t));
            if (mat != null)
            {
                Color c = startColor;
                c.a *= 1f - t;
                SetMaterialColor(mat, c, c * 1.6f);
            }
            yield return null;
        }

        if (target != null)
            Destroy(target.gameObject);
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
            Destroy(shard.gameObject);
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
            Destroy(burst.gameObject);
    }

    private void SpawnInvisibleProjectile(Vector3 cameraPos, Vector3 direction, WeaponPreset preset)
    {
        if (bulletPrefab == null) return;

        // Create invisible projectile from camera for actual collision/damage detection
        GameObject projectile = Instantiate(bulletPrefab, cameraPos, Quaternion.LookRotation(direction));

        // Hide the renderer so it's invisible
        Renderer[] renderers = projectile.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
            r.enabled = false;

        // Remove trail renderer if it exists
        BulletTrail trail = projectile.GetComponent<BulletTrail>();
        if (trail != null)
            Destroy(trail);

        Projectile proj = projectile.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.Initialize(player != null ? player.gameObject : gameObject, GetEffectiveDamage(preset.damage));
        }

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
#pragma warning disable 0618
            rb.velocity = direction * preset.bulletSpeed;
#pragma warning restore 0618
        }
    }

    private Vector3 CalculateShotDirection(Vector3 origin, Vector3 targetPoint, float spreadDegrees)
    {
        Vector3 direction = (targetPoint - origin).normalized;
        return ApplySpread(direction, spreadDegrees);
    }

    private Vector3 ApplySpread(Vector3 direction, float spreadDegrees)
    {
        if (spreadDegrees <= 0.001f) return direction.normalized;

        float yaw = UnityEngine.Random.Range(-spreadDegrees, spreadDegrees);
        float pitch = UnityEngine.Random.Range(-spreadDegrees, spreadDegrees);
        return (Quaternion.Euler(pitch, yaw, 0f) * direction).normalized;
    }


    private GameObject SpawnProjectile(Vector3 direction, WeaponPreset preset, int pelletIndex)
    {
        // Retain legacy projectile spawn in case other systems rely on it. Spawn from camera for consistent hit-registration.
        if (bulletPrefab == null) return null;

        Vector3 spawnPosition = GetProjectileSpawnPosition() + direction * (0.08f * pelletIndex);
        GameObject bullet = Instantiate(bulletPrefab, spawnPosition, Quaternion.LookRotation(direction));

        Projectile projectile = bullet.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Initialize(player != null ? player.gameObject : gameObject, GetEffectiveDamage(preset.damage));
        }

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
#pragma warning disable 0618
            rb.velocity = direction * preset.bulletSpeed;
#pragma warning restore 0618
        }

        return bullet;
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

    private System.Collections.IEnumerator VisualTracerRoutine(Vector3 from, Vector3 to)
    {
        GameObject tracer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tracer.name = "VisualTracer";
        tracer.transform.position = from;
        tracer.transform.localScale = Vector3.one * 0.06f;
        Collider c = tracer.GetComponent<Collider>();
        if (c != null) Destroy(c);

        if (tracer.GetComponent<BulletTrail>() == null)
            tracer.AddComponent<BulletTrail>();

        float t = 0f;
        float duration = 0.06f; // very fast
        while (t < duration)
        {
            t += Time.deltaTime;
            tracer.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }

        tracer.transform.position = to;
        Destroy(tracer, 0.05f);
    }

    private void IgnoreSiblingProjectileCollisions(GameObject[] projectiles)
    {
        if (projectiles == null) return;

        for (int i = 0; i < projectiles.Length; i++)
        {
            if (projectiles[i] == null) continue;
            Collider a = projectiles[i].GetComponent<Collider>();
            if (a == null) continue;

            for (int j = i + 1; j < projectiles.Length; j++)
            {
                if (projectiles[j] == null) continue;
                Collider b = projectiles[j].GetComponent<Collider>();
                if (b != null)
                    Physics.IgnoreCollision(a, b, true);
            }
        }
    }

    private Vector3 GetProjectileSpawnPosition()
    {
        Camera cam = Camera.main;
        if (cam != null)
            return cam.transform.position + cam.transform.forward * muzzleForwardOffset;

        return transform.position + transform.forward * muzzleForwardOffset;
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
        pistolPassiveMod = PassiveMod.None;
        shotgunPassiveMod = PassiveMod.None;
        heavyPassiveMod = PassiveMod.None;
        pistolAltMod = AltFireMod.None;
        shotgunAltMod = AltFireMod.None;
        heavyAltMod = AltFireMod.None;

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
            WeaponArchetype.Marksman => "Fast pistol for clean single shots.",
            WeaponArchetype.Rail => "Slow pistol that punches through a line.",
            WeaponArchetype.Splitter => "Burst pistol for close clean-up.",
            WeaponArchetype.CoreEject => "Wide shotgun that hits groups hard.",
            WeaponArchetype.Magnet => "Shotgun that stays on a tagged target.",
            WeaponArchetype.Slab => "Single heavy slug with a big hit.",
            WeaponArchetype.Mortar => "Heavy launcher with blast damage.",
            WeaponArchetype.Driver => "Heavy driver for straight-line pressure.",
            WeaponArchetype.Arc => "Heavy weapon that jumps damage between targets.",
            _ => "Reliable weapon setup."
        };
    }

    private static string GetAltDescriptor(WeaponArchetype archetype)
    {
        return archetype switch
        {
            WeaponArchetype.Marksman => "to fire a charged line.",
            WeaponArchetype.Rail => "to fire a wider beam.",
            WeaponArchetype.Splitter => "to fire a wide fan burst.",
            WeaponArchetype.CoreEject => "to fire a bigger blast.",
            WeaponArchetype.Magnet => "to tag a target.",
            WeaponArchetype.Slab => "to fire a short shockwave.",
            WeaponArchetype.Mortar => "to fire a big splash round.",
            WeaponArchetype.Driver => "to fire a piercing driver shot.",
            WeaponArchetype.Arc => "to fire a short arc burst.",
            _ => "to fire the alt shot."
        };
    }

    private void BuildHandgunModel(Transform root, WeaponPreset preset)
    {
        AddPart(root, "Grip", new Vector3(-0.05f, -0.38f, 0.12f), new Vector3(0.26f, 0.72f, 0.22f), Quaternion.Euler(-12f, 0f, 0f), bodyMaterial);
        AddPart(root, "Receiver", new Vector3(0f, -0.12f, 0.45f), new Vector3(0.48f, 0.3f, 0.82f), Quaternion.identity, bodyMaterial);
        AddPart(root, "Slide", new Vector3(0f, 0.08f, 0.48f), new Vector3(0.54f, 0.18f, 1.0f), Quaternion.identity, bodyMaterial);
        AddPart(root, "Barrel", new Vector3(0.26f, -0.12f, 1.03f), new Vector3(0.12f, 0.12f, 0.45f), Quaternion.identity, accentMaterial);
        AddPart(root, "Sight", new Vector3(0f, 0.22f, 0.83f), new Vector3(0.12f, 0.08f, 0.18f), Quaternion.identity, accentMaterial);

        if (preset.archetype == WeaponArchetype.Rail)
        {
            AddPart(root, "RailCoil", new Vector3(0f, 0.12f, 0.72f), new Vector3(0.66f, 0.1f, 0.82f), Quaternion.identity, accentMaterial);
            AddPart(root, "RailCompensator", new Vector3(0f, 0.02f, 1.08f), new Vector3(0.62f, 0.24f, 0.24f), Quaternion.identity, bodyMaterial);
        }
        else if (preset.archetype == WeaponArchetype.Splitter)
        {
            AddPart(root, "SplitterForkL", new Vector3(-0.18f, -0.05f, 1.0f), new Vector3(0.12f, 0.12f, 0.42f), Quaternion.identity, accentMaterial);
            AddPart(root, "SplitterForkR", new Vector3(0.18f, -0.05f, 1.0f), new Vector3(0.12f, 0.12f, 0.42f), Quaternion.identity, accentMaterial);
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

        if (preset.archetype == WeaponArchetype.Magnet)
        {
            AddPart(root, "MagnetCoil", new Vector3(0f, 0.12f, 1.08f), new Vector3(0.76f, 0.16f, 0.58f), Quaternion.identity, accentMaterial);
        }
        else if (preset.archetype == WeaponArchetype.Slab)
        {
            AddPart(root, "SlugRail", new Vector3(0f, 0.26f, 1.0f), new Vector3(0.18f, 0.1f, 1.1f), Quaternion.identity, accentMaterial);
            AddPart(root, "SlabWeight", new Vector3(0f, -0.18f, 0.78f), new Vector3(0.7f, 0.22f, 0.48f), Quaternion.identity, bodyMaterial);
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
        }
        else if (preset.archetype == WeaponArchetype.Driver)
        {
            AddPart(root, "DriverForkL", new Vector3(-0.18f, 0.02f, 1.82f), new Vector3(0.08f, 0.08f, 0.48f), Quaternion.identity, accentMaterial);
            AddPart(root, "DriverForkR", new Vector3(0.18f, 0.02f, 1.82f), new Vector3(0.08f, 0.08f, 0.48f), Quaternion.identity, accentMaterial);
        }
        else if (preset.archetype == WeaponArchetype.Arc)
        {
            AddPart(root, "ArcCap", new Vector3(0f, 0.28f, 1.36f), new Vector3(0.56f, 0.14f, 0.56f), Quaternion.identity, accentMaterial);
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
        Material material = new Material(FindUrpShader(true)) { name = "RuntimeWeaponFX" };
        SetMaterialColor(material, color, color * Mathf.Max(0f, emissionStrength));
        return material;
    }

    private Shader FindUrpShader(bool unlit)
    {
        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader;
    }
}
