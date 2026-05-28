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

    [Header("Legacy Runtime References")]
    public float fireRate = 0.15f;
    public float bulletSpeed = 50f;
    public float muzzleForwardOffset = 1.1f;
    public Transform gunBarrel;
    public GameObject bulletPrefab;
    public ParticleSystem muzzleFlash;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip shootSound;

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

        ApplyPreset(activePresetIndex);
    }

    private void Update()
    {
        HandleWeaponSwitching();
        HandleSwayAndRecoil();

        if (player != null && player.isUIActive) return;
        if (Mouse.current == null) return;

        bool wantsFire = Mouse.current.leftButton.wasPressedThisFrame;
        WeaponPreset preset = ActivePreset;
        if (preset != null && preset.archetype == WeaponArchetype.Magnet)
            wantsFire = Mouse.current.leftButton.isPressed;

        if (wantsFire && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + Mathf.Max(0.02f, fireRate);
            Shoot();
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
        ApplyPreset(GetPresetIndex(family, variant));
    }

    private void CycleVariant(int direction)
    {
        if (activeFamily == WeaponFamily.Handgun)
        {
            handgunVariant = Mod(handgunVariant + direction, 3);
            ApplyPreset(GetPresetIndex(WeaponFamily.Handgun, handgunVariant));
        }
        else
        {
            shotgunVariant = Mod(shotgunVariant + direction, 3);
            ApplyPreset(GetPresetIndex(WeaponFamily.Shotgun, shotgunVariant));
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
        WeaponPreset preset = ActivePreset;
        if (preset == null) return;

        activeFamily = preset.family;
        if (preset.family == WeaponFamily.Handgun)
            handgunVariant = activePresetIndex;
        else
            shotgunVariant = activePresetIndex - 3;

        fireRate = preset.fireRate;
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

    private void Shoot()
    {
        WeaponPreset preset = ActivePreset;
        if (preset == null) return;

        currentRecoilPosition -= new Vector3(0f, 0f, recoilForce * 0.1f);

        if (muzzleFlash != null) muzzleFlash.Play();
        if (audioSource != null && shootSound != null) audioSource.PlayOneShot(shootSound);

        Camera mainCam = Camera.main;
        Vector3 targetPoint = gunBarrel != null ? gunBarrel.position + gunBarrel.forward * 100f : transform.position + transform.forward * 100f;

        if (mainCam != null)
        {
            Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
            targetPoint = Physics.Raycast(ray, out RaycastHit hit, 1000f) ? hit.point : ray.GetPoint(100f);
        }

        int pellets = Mathf.Max(1, preset.pelletCount);
        GameObject[] spawnedProjectiles = new GameObject[pellets];
        for (int i = 0; i < pellets; i++)
        {
            Vector3 direction = CalculateShotDirection(targetPoint, preset.spreadDegrees);
            spawnedProjectiles[i] = SpawnProjectile(direction, preset, i);
        }

        IgnoreSiblingProjectileCollisions(spawnedProjectiles);
    }

    private Vector3 CalculateShotDirection(Vector3 targetPoint, float spreadDegrees)
    {
        Vector3 origin = GetProjectileSpawnPosition();
        Vector3 direction = (targetPoint - origin).normalized;
        if (spreadDegrees <= 0.001f) return direction;

        float yaw = UnityEngine.Random.Range(-spreadDegrees, spreadDegrees);
        float pitch = UnityEngine.Random.Range(-spreadDegrees, spreadDegrees);
        return Quaternion.Euler(pitch, yaw, 0f) * direction;
    }

    private GameObject SpawnProjectile(Vector3 direction, WeaponPreset preset, int pelletIndex)
    {
        if (bulletPrefab == null || gunBarrel == null) return null;

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

        if (gunBarrel != null)
            return gunBarrel.position + gunBarrel.forward * muzzleForwardOffset;

        return transform.position + transform.forward * muzzleForwardOffset;
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
