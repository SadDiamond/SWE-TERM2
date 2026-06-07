using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Gun : MonoBehaviour
{
    public enum WeaponFamily
    {
        Handgun,
        Shotgun
    }

    public enum WeaponArchetype
    {
        Marksman,
        Rail,
        Splitter,
        CoreEject,
        Magnet,
        Slab
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
        new WeaponPreset { displayName = "Handgun - Marksman", family = WeaponFamily.Handgun, archetype = WeaponArchetype.Marksman, accentColor = new Color(0.0f, 0.62f, 0.9f), fireRate = 0.23f, bulletSpeed = 82f, damage = 24f, pelletCount = 1, spreadDegrees = 0.05f, recoilForce = 3.6f, modelScale = 0.78f },
        new WeaponPreset { displayName = "Handgun - Rail", family = WeaponFamily.Handgun, archetype = WeaponArchetype.Rail, accentColor = new Color(0.92f, 0.16f, 0.08f), fireRate = 0.7f, bulletSpeed = 115f, damage = 72f, pelletCount = 1, spreadDegrees = 0f, recoilForce = 8.8f, modelScale = 0.82f },
        new WeaponPreset { displayName = "Handgun - Splitter", family = WeaponFamily.Handgun, archetype = WeaponArchetype.Splitter, accentColor = new Color(0.1f, 0.85f, 0.28f), fireRate = 0.2f, bulletSpeed = 70f, damage = 11f, pelletCount = 3, spreadDegrees = 2.1f, recoilForce = 4.2f, modelScale = 0.74f },
        new WeaponPreset { displayName = "Shotgun - Core Eject", family = WeaponFamily.Shotgun, archetype = WeaponArchetype.CoreEject, accentColor = new Color(0.95f, 0.58f, 0.08f), fireRate = 0.78f, bulletSpeed = 55f, damage = 8.5f, pelletCount = 9, spreadDegrees = 5.2f, recoilForce = 8.8f, modelScale = 0.76f },
        new WeaponPreset { displayName = "Shotgun - Magnet", family = WeaponFamily.Shotgun, archetype = WeaponArchetype.Magnet, accentColor = new Color(0.78f, 0.08f, 0.75f), fireRate = 0.48f, bulletSpeed = 62f, damage = 6.5f, pelletCount = 6, spreadDegrees = 3.6f, recoilForce = 6.5f, modelScale = 0.72f },
        new WeaponPreset { displayName = "Shotgun - Slab", family = WeaponFamily.Shotgun, archetype = WeaponArchetype.Slab, accentColor = new Color(0.52f, 0.38f, 0.95f), fireRate = 1.05f, bulletSpeed = 105f, damage = 95f, pelletCount = 1, spreadDegrees = 0.08f, recoilForce = 11f, modelScale = 0.78f }
    };

    [Range(0, 5)] public int activePresetIndex = 0;
    public bool removeLegacyChildMeshes = true;
    public bool restrictToUnlockedWeapons = true;

    [Header("Legacy Runtime References")]
    public float fireRate = 0.15f;
    public float bulletSpeed = 50f;
    public float maxHitScanDistance = 1000f;
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
    private WeaponFamily activeFamily = WeaponFamily.Handgun;
    private int handgunVariant;
    private int shotgunVariant;
    private IDamageable taggedTarget;
    private float taggedTargetTimer;
    private float nextAltFireTime;
    private float runFireRateMultiplier = 1f;
    private float runDamageMultiplier = 1f;
    private float runAltCooldownMultiplier = 1f;
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
        root.localPosition = preset.family == WeaponFamily.Handgun ? new Vector3(0.34f, -0.46f, 0.72f) : new Vector3(0.42f, -0.52f, 0.76f);
        root.localRotation = Quaternion.Euler(-2f, -4f, 0f);
        root.localScale = Vector3.one * Mathf.Max(0.1f, preset.modelScale * 0.34f);

        if (removeLegacyChildMeshes)
            ClearLegacyVisualChildren(root);

        if (preset.family == WeaponFamily.Handgun)
            BuildHandgunModel(root, preset);
        else
            BuildShotgunModel(root, preset);

        if (gunBarrel == null)
        {
            GameObject barrel = new GameObject("GeneratedBarrel");
            barrel.transform.SetParent(root, false);
            barrel.transform.localPosition = preset.family == WeaponFamily.Handgun ? new Vector3(0.24f, -0.1f, 1.0f) : new Vector3(0.22f, -0.06f, 1.48f);
            gunBarrel = barrel.transform;
        }
    }

    private void HandleWeaponSwitching()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) SetFamily(WeaponFamily.Handgun);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SetFamily(WeaponFamily.Shotgun);

        bool lookingAtInteractable = IsLookingAtInteractable();
        if (Keyboard.current.qKey.wasPressedThisFrame)
            CycleVariant(-1);
        if (Keyboard.current.eKey.wasPressedThisFrame && !lookingAtInteractable)
            CycleVariant(1);
    }

    private void SetFamily(WeaponFamily family)
    {
        activeFamily = family;
        int variant = family == WeaponFamily.Handgun ? handgunVariant : shotgunVariant;
        ApplyPreset(GetNextUnlockedPreset(GetPresetIndex(family, variant), family, 1));
    }

    private void CycleVariant(int direction)
    {
        if (activeFamily == WeaponFamily.Handgun)
        {
            handgunVariant = Mod(handgunVariant + direction, 3);
            ApplyPreset(GetNextUnlockedPreset(GetPresetIndex(WeaponFamily.Handgun, handgunVariant), WeaponFamily.Handgun, direction));
        }
        else
        {
            shotgunVariant = Mod(shotgunVariant + direction, 3);
            ApplyPreset(GetNextUnlockedPreset(GetPresetIndex(WeaponFamily.Shotgun, shotgunVariant), WeaponFamily.Shotgun, direction));
        }
    }

    private int GetPresetIndex(WeaponFamily family, int variant)
    {
        int familyOffset = family == WeaponFamily.Handgun ? 0 : 3;
        return Mathf.Clamp(familyOffset + Mod(variant, 3), 0, presets.Length - 1);
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
        if (presets == null || index < 0 || index >= presets.Length) return "Unindexed Armature";
        return string.IsNullOrWhiteSpace(presets[index].displayName) ? $"Dormant Variant {index + 1}" : presets[index].displayName;
    }

    public string GetPresetGuideText(int index)
    {
        if (presets == null || index < 0 || index >= presets.Length) return "New weapon equipped.";

        WeaponPreset preset = presets[index];
        return $"{GetArchetypeLabel(preset.archetype)}: {GetPrimaryDescriptor(preset.archetype)} Right click {GetAltDescriptor(preset.archetype)}";
    }

    public string GetActiveDisplayName()
    {
        return GetPresetDisplayName(activePresetIndex);
    }

    public string GetActiveFamilyLabel()
    {
        WeaponPreset preset = ActivePreset;
        return preset == null ? "ARMORY" : (preset.family == WeaponFamily.Handgun ? "HANDGUN BUS" : "SHOTGUN BUS");
    }

    public string GetActiveVariantLabel()
    {
        WeaponPreset preset = ActivePreset;
        return preset == null ? "ARMORY LINK" : GetArchetypeLabel(preset.archetype).ToUpperInvariant();
    }

    public string GetActiveDescriptorLine()
    {
        WeaponPreset preset = ActivePreset;
        if (preset == null) return "Armory link unavailable.";
        return $"{GetPrimaryDescriptor(preset.archetype)} Right click {GetAltDescriptor(preset.archetype)}";
    }

    public string GetRunModifierStatus()
    {
        int fireRatePercent = Mathf.RoundToInt((1f - runFireRateMultiplier) * 100f);
        int damagePercent = Mathf.RoundToInt((runDamageMultiplier - 1f) * 100f);
        int altPercent = Mathf.RoundToInt((1f - runAltCooldownMultiplier) * 100f);

        if (fireRatePercent <= 0 && damagePercent <= 0 && altPercent <= 0)
            return "Bus state nominal.";

        return $"Overclock // cycle +{Mathf.Max(0, fireRatePercent)}%  damage +{Mathf.Max(0, damagePercent)}%  alt +{Mathf.Max(0, altPercent)}%";
    }

    public string GetActiveStatsLine()
    {
        WeaponPreset preset = ActivePreset;
        if (preset == null) return string.Empty;

        float damage = GetEffectiveDamage(preset.damage);
        float shotsPerSecond = 1f / Mathf.Max(0.02f, GetEffectiveFireRate(preset));
        string pelletText = preset.pelletCount > 1 ? $"  pellets {preset.pelletCount}" : string.Empty;
        return $"Output {damage:0.#}  cycle {shotsPerSecond:0.0}/s{pelletText}";
    }

    private bool IsPresetUnlocked(int index)
    {
        return !restrictToUnlockedWeapons || CybergrindRunState.GetOrCreate().IsWeaponUnlocked(index);
    }

    private int GetNextUnlockedPreset(int desiredIndex, WeaponFamily family, int direction)
    {
        if (!restrictToUnlockedWeapons || presets == null || presets.Length == 0) return desiredIndex;

        int familyStart = family == WeaponFamily.Handgun ? 0 : 3;
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
        if (preset.family == WeaponFamily.Handgun)
            handgunVariant = activePresetIndex;
        else
            shotgunVariant = activePresetIndex - 3;

        fireRate = GetEffectiveFireRate(preset);
        bulletSpeed = preset.bulletSpeed;
        recoilForce = preset.recoilForce;

        EnsureMaterials();
        SetMaterialColor(accentMaterial, preset.accentColor, preset.accentColor * 0.55f);

        RebuildModel();
    }

    private void HandleSwayAndRecoil()
    {
        currentRecoilPosition = Vector3.Lerp(currentRecoilPosition, initialLocalPosition, Time.deltaTime * recoilRecoverySpeed);
        transform.localPosition = currentRecoilPosition;

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

        if (muzzleFlash != null) muzzleFlash.Play();
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
            Vector3 direction = ApplySpread(cameraForward, preset.spreadDegrees);
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
            SpawnImpactBurst(hitPoint, preset.accentColor, preset.archetype == WeaponArchetype.CoreEject ? 0.22f : 0.12f, 0.16f);
        }
    }

    private void FireAlternate()
    {
        WeaponPreset preset = ActivePreset;
        if (preset == null) return;
        float baseDamage = GetEffectiveDamage(preset.damage);

        float cooldown = Mathf.Max(0.3f, preset.fireRate * 2.2f * runAltCooldownMultiplier);
        nextAltFireTime = Time.time + cooldown;
        currentRecoilPosition -= new Vector3(0f, 0f, recoilForce * 0.14f);

        if (muzzleFlash != null) muzzleFlash.Play();
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
                FireFanBurst(cameraPos, forward, preset, 9, Mathf.Max(4.8f, preset.spreadDegrees * 2.2f), baseDamage * 0.9f);
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
        }
    }

    private void SpawnVisualTracer(Vector3 barrelPos, Vector3 direction, WeaponPreset preset)
    {
        if (bulletPrefab == null) return;

        // Create visual tracer from barrel (will be seen but won't cause collision/damage)
        GameObject tracer = Instantiate(bulletPrefab, barrelPos, Quaternion.LookRotation(direction));
        Projectile projectile = tracer.GetComponent<Projectile>();
        if (projectile != null)
            projectile.enabled = false;

        Collider tracerCollider = tracer.GetComponent<Collider>();
        if (tracerCollider != null)
            tracerCollider.enabled = false;

        Rigidbody rb = tracer.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = false;
#pragma warning disable 0618
            rb.velocity = direction * preset.bulletSpeed;
#pragma warning restore 0618
        }

        // Make sure the tracer has a trail renderer
        if (tracer.GetComponent<BulletTrail>() == null)
            tracer.AddComponent<BulletTrail>();

        // Short lifetime for visual effect
        Destroy(tracer, 2f);
    }

    private bool TryGetAimHit(Vector3 origin, Vector3 direction, out RaycastHit hit)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxHitScanDistance, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int h = 0; h < hits.Length; h++)
        {
            Collider hitCollider = hits[h].collider;
            if (hitCollider == null) continue;
            if (player != null && (hitCollider.gameObject == player.gameObject || hitCollider.transform.IsChildOf(player.transform))) continue;
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
            if (player != null && (hitCollider.gameObject == player.gameObject || hitCollider.transform.IsChildOf(player.transform))) continue;
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
            if (player != null && (col.gameObject == player.gameObject || col.transform.IsChildOf(player.transform))) continue;

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
        return Mathf.Max(0.02f, preset.fireRate * runFireRateMultiplier);
    }

    private float GetEffectiveDamage(float baseDamage)
    {
        return baseDamage * runDamageMultiplier;
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
        GameObject burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        burst.name = "HitBurst";
        burst.transform.position = position;
        burst.transform.localScale = Vector3.one * Mathf.Max(0.05f, scale);
        Collider collider = burst.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = burst.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(FindUrpShader(true));
            mat.color = color;
            if (mat.HasProperty(EmissionColorId))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(EmissionColorId, color * 1.6f);
            }
            renderer.material = mat;
        }

        StartCoroutine(ScaleBurstDown(burst.transform, lifetime));
    }

    private System.Collections.IEnumerator ScaleBurstDown(Transform burst, float lifetime)
    {
        if (burst == null) yield break;
        Vector3 startScale = burst.localScale;
        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
            burst.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
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
            proj.Initialize(player != null ? player.gameObject : gameObject, preset.damage);
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
            projectile.Initialize(player != null ? player.gameObject : gameObject, preset.damage);
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
            fireRate = GetEffectiveFireRate(preset);
    }

    public void ResetRunModifiers()
    {
        runFireRateMultiplier = 1f;
        runDamageMultiplier = 1f;
        runAltCooldownMultiplier = 1f;

        WeaponPreset preset = ActivePreset;
        if (preset != null)
            fireRate = GetEffectiveFireRate(preset);
    }

    private static string GetArchetypeLabel(WeaponArchetype archetype)
    {
        return archetype switch
        {
            WeaponArchetype.CoreEject => "Core Eject",
            _ => archetype.ToString()
        };
    }

    private static string GetPrimaryDescriptor(WeaponArchetype archetype)
    {
        return archetype switch
        {
            WeaponArchetype.Marksman => "Precise sidearm. Shots can arc into a second target.",
            WeaponArchetype.Rail => "Piercing hand cannon built for lane clears.",
            WeaponArchetype.Splitter => "Burst pistol that spreads pressure across a crowd.",
            WeaponArchetype.CoreEject => "Explosive shotgun that splashes through clustered targets.",
            WeaponArchetype.Magnet => "Tracking shotgun that rewards keeping pressure on a tagged target.",
            WeaponArchetype.Slab => "Heavy slug gun with brutal single-shot authority.",
            _ => "Armory variant tuned for general pressure."
        };
    }

    private static string GetAltDescriptor(WeaponArchetype archetype)
    {
        return archetype switch
        {
            WeaponArchetype.Marksman => "fires an overload line.",
            WeaponArchetype.Rail => "punches a heavier beam through the lane.",
            WeaponArchetype.Splitter => "unleashes a wide fan burst.",
            WeaponArchetype.CoreEject => "detonates a heavier blast line.",
            WeaponArchetype.Magnet => "tags a target so shots bend toward it.",
            WeaponArchetype.Slab => "fires a short concussion shockwave.",
            _ => "fires a special pattern."
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

    private Shader FindUrpShader(bool unlit)
    {
        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader;
    }
}
