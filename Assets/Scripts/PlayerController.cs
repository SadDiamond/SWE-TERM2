using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour, IDamageable
{
    [Header("UI")]
    public GameObject crosshair;
    public TMP_Text interactionPromptText;
    public TMP_Text healthText;
    public TMP_Text currencyText;
    public Image healthBarFill;
    public Image healthBarBack;
    public Image currencyPanel;
    public Color crosshairBaseColor = new Color(0.72f, 0.9f, 1f, 0.9f);
    public Color crosshairFocusColor = new Color(0.64f, 1f, 0.9f, 1f);
    public Color crosshairHostileColor = new Color(1f, 0.68f, 0.48f, 0.98f);
    public Color crosshairHitColor = new Color(1f, 0.88f, 0.64f, 1f);
    public Color crosshairKillColor = new Color(1f, 0.72f, 0.4f, 1f);

    [Header("Vitals")]
    public float maxHealth = 100f;
    public float damageInvulnerabilityTime = 0.2f;
    [Range(0.1f, 1f)] public float respawnHealthPercent = 0.75f;

    public float currentHealth { get; private set; }
    public int currency { get; private set; }
    private float damageInvulnerabilityTimer;

    [Header("Movement (Core)")]
    public float moveSpeed = 13.5f;
    public float groundAcceleration = 24f;
    public float groundDeceleration = 28f;
    public float airAcceleration = 13.5f;
    public float gravity = -29f;
    public float jumpHeight = 2.85f;
    public int maxJumps = 2; 
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;

    [Header("Movement (Dash)")]
    public float dashForce = 28f; // Pure momentum burst
    public float dashCooldown = 0.85f;
    public float dashDuration = 0.09f;
    public float dashTargetSpeed = 25f;
    public float dashExitSpeed = 13.5f;
    public float launchCarrySpeedLimit = 20f;

    [Header("Movement (Slide & Slam)")]
    public float slideBaseSpeed = 18f; // Just a bit faster than running
    public float slideFriction = 1.45f; // How fast you slow down while sliding
    public float fallSpeedToSlideBoost = 0.58f; // Hitting the ground hard speeds up your slide
    public float maxSlideStartSpeed = 22f;
    public float maxSlideJumpCarrySpeed = 26f;
    public float slideHeight = 1f;
    public float slideCooldown = 0.45f;
    public float slamSpeed = 40f; // How fast you plummet downwards
    public float postSlamSlideLockout = 0.25f;
    public float slamReleaseDelay = 0.12f;
    public float slideCameraDrop = 0.42f;
    public float slideGroundGrace = 0.12f;
    public float slideHoldSpeed = 14f;
    public float slideSteerStrength = 9f;
    private float defaultHeight;

    [Header("Movement (Limits)")]
    public float maxSpeedLimit = 29f; // Enough headroom for chaining movement
    public float groundedStopSpeed = 0.6f;

    [Header("FX & Polish")]
    public Camera playerCamera;
    public ParticleSystem slideDust; // Drag a particle system here!
    public ParticleSystem speedLines;
    public float overdriveSpeedThreshold = 24f;
    public float fallRespawnY = -18f;
    public float abyssRecoveryY = -8f;
    public bool enableAbyssRecovery = true;
    public Color dashBurstColor = new Color(0.58f, 0.9f, 1f, 0.9f);
    public Color slamBurstColor = new Color(1f, 0.72f, 0.42f, 0.92f);
    private float baseFOV;
    private Vector3 baseCameraLocalPos;
    private Vector3 lastSafePosition;
    private float safePositionTimer;
    private float slideLockoutTimer;
    private float slideCooldownTimer;
    private float slideGroundGraceTimer;
    private float moveSpeedBonus;
    private float dashForceBonus;
    private float jumpHeightBonus;
    private float maxHealthBonus;

    [Header("Damage Feedback")]
    public Color damageFlashColor = new Color(0.9f, 0.12f, 0.08f, 0.22f);
    public float damageFlashDuration = 0.26f;
    public float weaponKickDuration = 0.12f;

    [Header("Melee")]
    public float meleeRange = 2.1f;
    public float meleeRadius = 1.15f;
    public float meleeDamage = 28f;
    public float meleeCooldown = 0.42f;
    public float parryWindow = 0.16f;
    public float parryProjectileSpeed = 85f;
    public float parryDamageMultiplier = 1.6f;
    public float meleeHitBoost = 5.5f;
    public Color meleeBurstColor = new Color(1f, 0.86f, 0.48f, 0.95f);

    [Header("Look")]
    public float mouseSensitivity = 100f;
    public Transform cameraTransform;

    [Header("Inventory")]
    public System.Collections.Generic.List<CollectibleItem> inventory = new System.Collections.Generic.List<CollectibleItem>();

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;

    // Movement State
    public bool isGrounded; // Made public so JumpPad can see it if needed
    private int jumpsRemaining;
    private float dashCooldownTimer;
    private float dashTimer;
    private bool isSliding;
    private bool isSlamming;
    private bool slideRequiresRelease;
    private Vector3 momentum;
    private Vector3 dashVelocity;
    private float lastFrameVelocityY;
    private float disableGroundCheckTimer = 0f;
    private bool abyssRecoveredThisAirborneState;
    private bool transitionLocked;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float damageFlashTimer;
    private float damageKickTimer;
    private float weaponKickTimer;
    private float meleeCooldownTimer;
    private float parryTimer;
    private float crosshairFireTimer;
    private Image damageFlashOverlay;
    private RectTransform crosshairRect;
    private readonly RectTransform[] crosshairSegmentRects = new RectTransform[4];
    private readonly Image[] crosshairSegmentImages = new Image[4];
    private float crosshairHitTimer;
    private float crosshairKillTimer;
    private Color crosshairPulseColor;
    private Material cachedWorldFxMaterial;
    private Vector2 moveInputRaw;

    private float CurrentMoveSpeed => moveSpeed + moveSpeedBonus;
    private float CurrentDashForce => dashForce + dashForceBonus;
    private float CurrentJumpHeight => jumpHeight + jumpHeightBonus;
    private float CurrentMaxHealth => maxHealth + maxHealthBonus;
    public float EffectiveMaxHealth => CurrentMaxHealth;
    public float Health01 => CurrentMaxHealth <= 0.01f ? 0f : Mathf.Clamp01(currentHealth / CurrentMaxHealth);
    public bool DebugIsSliding => isSliding;
    public bool DebugIsSlamming => isSlamming;
    public float DebugDashTimer => dashTimer;
    public Vector3 DebugMomentum => momentum;
    public Vector3 DebugDashVelocity => dashVelocity;

    private const string MouseSensitivityPrefKey = "project_structure.mouse_sensitivity";
    private const string BaseFovPrefKey = "project_structure.base_fov";
    private const string MasterVolumePrefKey = "project_structure.master_volume";

    private Interactable currentInteractable;
    public Interactable FocusedInteractable => currentInteractable;
    public float interactionDistance = 3f;
    public LayerMask interactableLayer;
    private string transientPromptText;
    private float transientPromptTimer;

    [Header("State")]
    public bool isUIActive = false; // True when a puzzle or terminal screen is open
    public bool respawnOnDeath = false;
    public bool isDead = false;

    private float StandingOffset
    {
        get
        {
            if (controller == null)
                return 1.15f;

            return Mathf.Max(controller.height * 0.5f, controller.radius + 0.35f) + 0.08f;
        }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller != null)
            defaultHeight = controller.height;
        Cursor.lockState = CursorLockMode.Locked;
        currentHealth = CurrentMaxHealth;
        currency = 0;

        EnsureCameraReferences();
        if (playerCamera != null) baseFOV = playerCamera.fieldOfView;
        if (cameraTransform != null) baseCameraLocalPos = cameraTransform.localPosition;
        lastSafePosition = transform.position;
        EnsureVitalsHud();
        EnsureCrosshair();
        LoadSettings();
        if (playerCamera != null)
            playerCamera.fieldOfView = baseFOV;
        if (cameraTransform != null)
            cameraTransform.localPosition = baseCameraLocalPos;
        if (speedLines != null)
            speedLines.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (damageFlashOverlay != null)
            damageFlashOverlay.enabled = false;

        if (interactionPromptText != null)
        {
            interactionPromptText.gameObject.SetActive(false); // Hide text at start
        }

        RefreshVitalsUI();
    }

    void Update()
    {
        if (damageInvulnerabilityTimer > 0f)
            damageInvulnerabilityTimer -= Time.deltaTime;

        if (transientPromptTimer > 0f)
        {
            transientPromptTimer -= Time.deltaTime;
            if (transientPromptTimer <= 0f)
            {
                transientPromptText = string.Empty;
                if (currentInteractable != null)
                    ShowPrompt(currentInteractable.promptMessage);
                else if (interactionPromptText != null)
                    interactionPromptText.gameObject.SetActive(false);
            }
        }

        UpdateDamageFeedback();
        UpdateCrosshairVisual();
        if (meleeCooldownTimer > 0f) meleeCooldownTimer -= Time.deltaTime;
        if (parryTimer > 0f) parryTimer -= Time.deltaTime;

        if (isDead) return;

        if (isUIActive) return; // Don't move or interact if a puzzle is open

        if (transitionLocked)
        {
            HandleLook();
            return;
        }

        HandleMovement();
        HandleLook();
        HandleMelee();
        HandleInteraction();
    }

    public void ToggleUIMode(bool uiActive)
    {
        isUIActive = uiActive;
        if (uiActive)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (crosshair != null) crosshair.SetActive(false);
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            EnsureCrosshair();
            if (crosshair != null) crosshair.SetActive(true);
        }
    }

    public void SetTransitionLock(bool locked)
    {
        transitionLocked = locked;
        if (!locked) return;

        ClearMovementCarry(true);
    }

    // --- External Physics Methods ---
    public void LaunchPlayer(Vector3 launchVelocity)
    {
        CacheControllerDefaults();

        // Force the player to be airborne for a split second so friction/gravity doesn't instantly cancel the jump pad
        disableGroundCheckTimer = 0.2f;
        
        // Disable CharacterController snapping by physically pushing the player off the ground first
        if (controller != null && controller.enabled)
            controller.Move(Vector3.up * 0.1f);

        // Cancel downward gravity immediately
        if (velocity.y < 0) velocity.y = 0;
        
        // Add vertical height
        velocity.y = launchVelocity.y; // Override rather than add so double-jumping pads don't compound infinitely
        
        // Carry launch speed without stacking infinite horizontal acceleration.
        Vector3 launchPlanar = new Vector3(launchVelocity.x, 0f, launchVelocity.z);
        Vector3 currentPlanar = Vector3.ProjectOnPlane(momentum, Vector3.up);
        float carryLimit = Mathf.Clamp(launchCarrySpeedLimit + moveSpeedBonus, CurrentMoveSpeed + 4f, maxSpeedLimit);
        if (launchPlanar.sqrMagnitude > 0.001f)
        {
            if (currentPlanar.sqrMagnitude > 0.001f)
            {
                Vector3 launchDir = launchPlanar.normalized;
                float carriedAlongLaunch = Mathf.Max(0f, Vector3.Dot(currentPlanar, launchDir));
                float targetLaunchSpeed = Mathf.Clamp(Mathf.Max(launchPlanar.magnitude, carriedAlongLaunch), 0f, carryLimit);
                Vector3 sideways = currentPlanar - launchDir * Vector3.Dot(currentPlanar, launchDir);
                sideways = Vector3.ClampMagnitude(sideways, CurrentMoveSpeed * 0.35f);
                currentPlanar = launchDir * targetLaunchSpeed + sideways;
            }
            else
            {
                currentPlanar = Vector3.ClampMagnitude(launchPlanar, carryLimit);
            }
        }

        momentum = Vector3.ClampMagnitude(currentPlanar, carryLimit);
        
        // Put player in falling state so they aren't stuck sliding
        isSliding = false;
        isSlamming = false;
        dashTimer = 0f;
        dashVelocity = Vector3.zero;
        slideRequiresRelease = true;
        if (controller != null)
            controller.height = defaultHeight;

        // Force ground state false immediately so unity's CharacterController doesn't snap us back
        lastFrameVelocityY = velocity.y;
    }

    // --- Inventory System Methods ---
    public void PickUp(CollectibleItem item)
    {
        inventory.Add(item);
    }

    public bool HasKeycard(int requiredLevel)
    {
        foreach (var item in inventory)
        {
            if (item is Keycard keycard && keycard.accessLevel >= requiredLevel)
            {
                return true;
            }
        }
        return false; // Player doesn't have a keycard with that level
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        if (damageInvulnerabilityTimer > 0f) return;
        if (isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        damageInvulnerabilityTimer = damageInvulnerabilityTime;
        CybergrindRunState.GetOrCreate().RegisterDamageTaken(amount);
        TriggerDamageFeedback();
        RefreshVitalsUI();

        if (currentHealth <= 0f)
            HandleDeath();
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        currentHealth = Mathf.Min(CurrentMaxHealth, currentHealth + amount);
        RefreshVitalsUI();
    }

    public void AddCurrency(int amount)
    {
        if (amount <= 0) return;
        currency += amount;
        RefreshVitalsUI();
    }

    public bool TrySpendCurrency(int amount)
    {
        if (amount <= 0) return true;
        if (currency < amount) return false;

        currency -= amount;
        RefreshVitalsUI();
        return true;
    }

    void HandleInteraction()
    {
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        Interactable interactable = FindBestInteractable(ray);

        if (interactable == null)
        {
            ClearFocus();
            return;
        }

        if (currentInteractable != interactable)
        {
            if (currentInteractable != null) currentInteractable.OnLoseFocus();
            currentInteractable = interactable;
            currentInteractable.OnFocus();
            ShowPrompt(currentInteractable.promptMessage);
        }

        if (UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
        {
            currentInteractable.OnInteract(this);
            // Refresh prompt — interacting may have changed it (e.g. "Terminal Offline")
            if (currentInteractable != null) ShowPrompt(currentInteractable.promptMessage);
        }
    }

    private void HandleMelee()
    {
        if (UnityEngine.InputSystem.Keyboard.current == null) return;
        if (!UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame) return;
        if (meleeCooldownTimer > 0f) return;

        meleeCooldownTimer = meleeCooldown;
        parryTimer = parryWindow;

        bool parriedProjectile = TryParryProjectile();
        int hits = PerformMeleeStrike(parriedProjectile ? meleeDamage * 0.6f : meleeDamage);
        SpawnWorldBurst(
            transform.position + transform.forward * 1.2f + Vector3.up * 1f,
            parriedProjectile ? Color.white : meleeBurstColor,
            parriedProjectile ? 0.26f : 0.18f,
            0.22f,
            parriedProjectile ? 0.68f : 0.52f);

        if (!parriedProjectile && hits == 0)
            momentum += transform.forward * 1.2f;
    }

    private bool TryParryProjectile()
    {
        Vector3 center = cameraTransform.position + cameraTransform.forward * Mathf.Max(0.8f, meleeRange * 0.75f);
        Collider[] hits = Physics.OverlapSphere(center, meleeRadius, ~0, QueryTriggerInteraction.Ignore);
        bool parried = false;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;
            Projectile projectile = hits[i].GetComponentInParent<Projectile>();
            if (projectile == null || projectile.owner == gameObject) continue;

            if (ReflectProjectile(projectile))
                parried = true;
        }

        return parried;
    }

    public bool TryParryIncomingProjectile(Projectile projectile)
    {
        if (projectile == null || projectile.owner == gameObject) return false;
        if (parryTimer <= 0f) return false;

        return ReflectProjectile(projectile);
    }

    private bool ReflectProjectile(Projectile projectile)
    {
        if (projectile == null || cameraTransform == null) return false;

        projectile.Reflect(gameObject, cameraTransform.forward, parryProjectileSpeed, parryDamageMultiplier);
        parryTimer = 0f;
        meleeCooldownTimer = Mathf.Min(meleeCooldownTimer, meleeCooldown * 0.45f);
        SpawnWorldBurst(cameraTransform.position + cameraTransform.forward * 1.05f, Color.white, 0.22f, 0.18f, 0.72f);
        ShowTransientStatus("Parried", 0.45f);
        return true;
    }

    private int PerformMeleeStrike(float damage)
    {
        Vector3 center = cameraTransform.position + cameraTransform.forward * meleeRange;
        Collider[] hits = Physics.OverlapSphere(center, meleeRadius, ~0, QueryTriggerInteraction.Ignore);
        int hitCount = 0;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i];
            if (collider == null) continue;
            if (collider.transform.IsChildOf(transform)) continue;

            IDamageable damageable = collider.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable is PlayerController) continue;

            damageable.TakeDamage(damage);
            if (damageable is BasicEnemyAI enemy)
                enemy.ApplyMeleeStun(0.35f);

            if (collider.attachedRigidbody != null)
                collider.attachedRigidbody.AddForce(cameraTransform.forward * 5.5f, ForceMode.Impulse);

            Vector3 boost = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized * meleeHitBoost;
            momentum = Vector3.ClampMagnitude(momentum + boost, maxSpeedLimit);

            hitCount++;
        }

        return hitCount;
    }

    private Interactable FindBestInteractable(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance, interactableLayer);
        Interactable interactable = FindInteractableInHits(hits);
        if (interactable != null) return interactable;

        hits = Physics.RaycastAll(ray, interactionDistance);
        return FindInteractableInHits(hits);
    }

    private Interactable FindInteractableInHits(RaycastHit[] hits)
    {
        if (hits == null || hits.Length == 0) return null;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null) continue;

            Interactable interactable = collider.GetComponentInParent<Interactable>();
            if (interactable != null) return interactable;
        }

        return null;
    }

    void ClearFocus()
    {
        if (interactionPromptText != null && transientPromptTimer <= 0f)
            interactionPromptText.gameObject.SetActive(false);
        if (currentInteractable == null) return;
        currentInteractable.OnLoseFocus();
        currentInteractable = null;
    }

    void ShowPrompt(string message)
    {
        if (interactionPromptText == null) return;
        if (transientPromptTimer > 0f) return;
        string formatted = BuildInteractionPrompt(message);
        interactionPromptText.text = formatted;
        interactionPromptText.gameObject.SetActive(!string.IsNullOrEmpty(formatted));
    }

    public void ShowTransientStatus(string message, float duration = 1.2f)
    {
        if (interactionPromptText == null || string.IsNullOrWhiteSpace(message)) return;

        transientPromptText = message.Trim();
        transientPromptTimer = Mathf.Max(0.1f, duration);
        interactionPromptText.text = DecorateStatusPrompt(transientPromptText, true);
        interactionPromptText.gameObject.SetActive(true);
    }

    private string BuildInteractionPrompt(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        string trimmed = message.Trim();
        const string pressEPrefix = "Press E to ";
        if (trimmed.StartsWith(pressEPrefix, System.StringComparison.OrdinalIgnoreCase))
        {
            string action = trimmed.Substring(pressEPrefix.Length).Trim();
            return $"<color=#9BEFFF><b>[E]</b></color> <color=#EAFBFF>{action}</color>";
        }

        if (trimmed.StartsWith("Press E ", System.StringComparison.OrdinalIgnoreCase))
        {
            string action = trimmed.Substring("Press E ".Length).Trim();
            return $"<color=#9BEFFF><b>[E]</b></color> <color=#EAFBFF>{action}</color>";
        }

        if (ShouldShowInteractionKey(trimmed))
            return $"<color=#9BEFFF><b>[E]</b></color> <color=#EAFBFF>{trimmed}</color>";

        return DecorateStatusPrompt(trimmed, false);
    }

    private bool ShouldShowInteractionKey(string message)
    {
        if (string.IsNullOrEmpty(message))
            return false;

        if (message.Equals("DEAD", System.StringComparison.OrdinalIgnoreCase) ||
            message.Equals("YOU DIED", System.StringComparison.OrdinalIgnoreCase))
            return false;
        if (message.StartsWith("Need ", System.StringComparison.OrdinalIgnoreCase))
            return false;
        if (message.IndexOf("claimed", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (message.IndexOf("linked", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (message.IndexOf("spent", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (message.IndexOf("drained", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (message.IndexOf("offline", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (message.IndexOf("sealed", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (message.IndexOf("live", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        return true;
    }

    private string DecorateStatusPrompt(string message, bool emphasize = false)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        string trimmed = message.Trim();
        if (trimmed.Equals("YOU DIED", System.StringComparison.OrdinalIgnoreCase))
            return "<color=#FF7668><b>YOU DIED</b></color>";
        if (trimmed.StartsWith("Need ", System.StringComparison.OrdinalIgnoreCase))
            return emphasize
                ? $"<color=#FFC766><b>{trimmed}</b></color>"
                : $"<color=#FFC766>{trimmed}</color>";
        if (trimmed.IndexOf("core variant", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            trimmed.IndexOf("route punched deeper", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return emphasize
                ? $"<color=#FFD7A2><b>{trimmed}</b></color>"
                : $"<color=#FFD7A2>{trimmed}</color>";
        if (trimmed.IndexOf("claimed", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            trimmed.IndexOf("linked", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            trimmed.IndexOf("repaired", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            trimmed.IndexOf("tuned", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            trimmed.IndexOf("patch", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return emphasize
                ? $"<color=#8EFFE2><b>{trimmed}</b></color>"
                : $"<color=#8EFFE2>{trimmed}</color>";
        if (trimmed.IndexOf("spent", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            trimmed.IndexOf("drained", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            trimmed.IndexOf("sealed", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            trimmed.IndexOf("offline", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return $"<color=#AAB6C4>{trimmed}</color>";
        if (trimmed.IndexOf("live", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            trimmed.IndexOf("open", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            trimmed.IndexOf("clear", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return emphasize
                ? $"<color=#9BEFFF><b>{trimmed}</b></color>"
                : $"<color=#9BEFFF>{trimmed}</color>";
        if (trimmed.IndexOf("coin", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return emphasize
                ? $"<color=#FFD95E><b>{trimmed}</b></color>"
                : $"<color=#FFD95E>{trimmed}</color>";

        return trimmed;
    }

    void HandleMovement()
    {
        bool wasGrounded = isGrounded;

        if (disableGroundCheckTimer > 0)
        {
            disableGroundCheckTimer -= Time.deltaTime;
            isGrounded = false;
        }
        else
        {
            isGrounded = controller.isGrounded;
        }
        
        float fallSpeed = lastFrameVelocityY; // Capture before we reset it

        coyoteTimer = isGrounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - Time.deltaTime);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            jumpsRemaining = maxJumps;
            abyssRecoveredThisAirborneState = false;
        }

        bool landedThisFrame = isGrounded && !wasGrounded;
        if (landedThisFrame && isSlamming)
        {
            isSlamming = false;
            slideRequiresRelease = true;
            slideLockoutTimer = Mathf.Max(slideLockoutTimer, slamReleaseDelay);
            momentum = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(momentum, Vector3.up), Mathf.Max(CurrentMoveSpeed * 0.95f, slideBaseSpeed * 0.75f));
        }

        float previousDashTimer = dashTimer;
        if (dashCooldownTimer > 0) dashCooldownTimer -= Time.deltaTime;
        if (dashTimer > 0f) dashTimer = Mathf.Max(0f, dashTimer - Time.deltaTime);
        if (slideLockoutTimer > 0f) slideLockoutTimer -= Time.deltaTime;
        if (slideCooldownTimer > 0f) slideCooldownTimer -= Time.deltaTime;
        if (isSliding)
            slideGroundGraceTimer = isGrounded ? slideGroundGrace : Mathf.Max(0f, slideGroundGraceTimer - Time.deltaTime);
        TrackSafePosition();

        if (enableAbyssRecovery && transform.position.y < abyssRecoveryY && !abyssRecoveredThisAirborneState)
        {
            RecoverFromAbyss();
            return;
        }

        if (transform.position.y < fallRespawnY)
        {
            RespawnAtLastSafePosition();
            return;
        }

        // Wasd Input
        Vector2 input = new Vector2(
            UnityEngine.InputSystem.Keyboard.current.dKey.isPressed ? 1 :
            UnityEngine.InputSystem.Keyboard.current.aKey.isPressed ? -1 : 0,
            UnityEngine.InputSystem.Keyboard.current.wKey.isPressed ? 1 :
            UnityEngine.InputSystem.Keyboard.current.sKey.isPressed ? -1 : 0
        ).normalized;
        moveInputRaw = input;

        Vector3 inputDir = transform.right * input.x + transform.forward * input.y;
        bool slamPressedThisFrame =
            UnityEngine.InputSystem.Keyboard.current.ctrlKey.wasPressedThisFrame ||
            UnityEngine.InputSystem.Keyboard.current.cKey.wasPressedThisFrame;
        bool wantsToSlide = UnityEngine.InputSystem.Keyboard.current.ctrlKey.isPressed || UnityEngine.InputSystem.Keyboard.current.cKey.isPressed;

        if (previousDashTimer > 0f && dashTimer <= 0f)
            FinishDash(inputDir);

        if (jumpBufferTimer > 0f)
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
        if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            jumpBufferTimer = jumpBufferTime;

        if (UnityEngine.InputSystem.Keyboard.current.leftShiftKey.wasPressedThisFrame && dashCooldownTimer <= 0)
        {
            dashCooldownTimer = dashCooldown;
            Vector3 dashDir = inputDir.magnitude > 0.1f ? inputDir : transform.forward;
            float dashSpeed = Mathf.Clamp(dashTargetSpeed + dashForceBonus * 0.25f, CurrentMoveSpeed * 1.1f, CurrentMoveSpeed + 10f);
            dashVelocity = dashDir * dashSpeed;
            momentum = dashVelocity;
            dashTimer = dashDuration;
            ExitSlide(false);
            isSlamming = false;
            slideRequiresRelease = wantsToSlide;
            velocity.y = isGrounded ? Mathf.Min(velocity.y, 0f) : Mathf.Clamp(velocity.y, -4f, 4f);
            SpawnDashBurst();
        }

        // --- SLIDE & SLAM LOGIC ---
        if (!wantsToSlide)
            slideRequiresRelease = false;

        // Ground Slam (Mid-air drop)
        if (slamPressedThisFrame && !isGrounded && !isSlamming && !slideRequiresRelease)
        {
            ExitSlide(false);
            isSlamming = true;
            velocity.y = -slamSpeed; // Plunge straight down
            momentum = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(momentum, Vector3.up), Mathf.Max(CurrentMoveSpeed, dashExitSpeed));
        }

        if (wantsToSlide && isGrounded && !isSliding && !slideRequiresRelease && slideLockoutTimer <= 0f && slideCooldownTimer <= 0f)
        {
            isSliding = true;
            controller.height = slideHeight;
            slideGroundGraceTimer = slideGroundGrace;

            // If we fell from a great height, turn that fall speed into forward slide speed.
            float fallBoost = 0f;
            if (fallSpeed < -10f && !isSlamming)
            {
                fallBoost = Mathf.Abs(fallSpeed) * fallSpeedToSlideBoost;
            }

            float currentSpeed = momentum.magnitude;
            float slideLimit = Mathf.Max(maxSlideStartSpeed, slideBaseSpeed);
            float newSpeed = Mathf.Clamp(Mathf.Max(slideBaseSpeed, currentSpeed + fallBoost), slideBaseSpeed, slideLimit);
            
            // If we have no input direction, slide wherever we are looking
            Vector3 slideDir = inputDir.magnitude > 0.1f ? inputDir.normalized : transform.forward;
            momentum = slideDir * newSpeed;
            velocity.y = Mathf.Max(velocity.y, 0f);
        }
        else if ((!wantsToSlide || (!isGrounded && slideGroundGraceTimer <= 0f)) && isSliding)
        {
            if (!wantsToSlide)
            {
                ExitSlide(true);
            }
            else if (!isGrounded)
            {
                ExitSlide(false);
                slideRequiresRelease = true;
            }
        }

        // --- MOMENTUM & FRICTION LOGIC ---
        if (dashTimer > 0f)
        {
            momentum = dashVelocity;
        }
        else if (isGrounded)
        {
            if (isSliding)
            {
                momentum = Vector3.MoveTowards(momentum, Vector3.zero, slideFriction * CurrentMoveSpeed * 0.72f * Time.deltaTime);
                if (wantsToSlide)
                {
                    Vector3 slideDir = momentum.sqrMagnitude > 0.01f ? momentum.normalized : transform.forward;
                    if (inputDir.sqrMagnitude > 0.01f)
                        slideDir = Vector3.Slerp(slideDir, inputDir.normalized, slideSteerStrength * Time.deltaTime).normalized;

                    float sustainedSpeed = inputDir.sqrMagnitude > 0.01f ? slideHoldSpeed : slideHoldSpeed * 0.82f;
                    float speed = Mathf.Max(momentum.magnitude, Mathf.Min(sustainedSpeed, maxSlideStartSpeed));
                    momentum = slideDir * speed;
                }
            }
            else
            {
                ApplyGroundMovement(inputDir);
            }
        }
        else
        {
            ApplyAirMovement(inputDir);
        }

        // --- JUMP LOGIC ---
        bool canGroundJump = isGrounded || coyoteTimer > 0f;
        bool canAirJump = !canGroundJump && jumpsRemaining > 0;
        if (jumpBufferTimer > 0f && (canGroundJump || canAirJump))
        {
            velocity.y = Mathf.Sqrt(CurrentJumpHeight * -2f * gravity);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            if (canGroundJump)
                jumpsRemaining = Mathf.Max(0, maxJumps - 1);
            else
                jumpsRemaining--;
            isGrounded = false;

            if (isSliding)
            {
                ExitSlide(false);
                momentum = Vector3.ClampMagnitude(momentum, maxSlideJumpCarrySpeed);
                slideCooldownTimer = slideCooldown;
            }
        }

        // Gravity
        velocity.y += gravity * Time.deltaTime;
        lastFrameVelocityY = velocity.y;

        // --- HARD SPEED LIMIT ---
        momentum = Vector3.ClampMagnitude(momentum, maxSpeedLimit);

        // Apply Final Move
        Vector3 finalMove = momentum + (Vector3.up * velocity.y);
        CollisionFlags collisionFlags = controller.Move(finalMove * Time.deltaTime);
        if ((collisionFlags & CollisionFlags.Above) != 0 && velocity.y > 0f)
            velocity.y = 0f;
        if ((collisionFlags & CollisionFlags.Sides) != 0 && dashTimer > 0f)
            FinishDash(inputDir);
        if (dashTimer <= 0f && !isSliding)
            momentum = Vector3.ClampMagnitude(momentum, Mathf.Max(CurrentMoveSpeed * 1.05f, dashExitSpeed));

        if (playerCamera != null)
        {
            playerCamera.fieldOfView = baseFOV;
        }
        if (cameraTransform != null)
            cameraTransform.localPosition = isSliding
                ? baseCameraLocalPos + Vector3.down * slideCameraDrop
                : baseCameraLocalPos;

        if (slideDust != null)
        {
            // Play dust particles if sliding and actually moving, otherwise stop
            Vector3 actualGroundVel = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
            if (isSliding && isGrounded && actualGroundVel.magnitude > 2f)
            {
                if (!slideDust.isPlaying) slideDust.Play();
                
                // Sample the floor color dynamically!
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2.5f))
                {
                    Renderer floorRenderer = hit.collider.GetComponent<Renderer>();
                    if (floorRenderer != null && floorRenderer.material != null)
                    {
                        var main = slideDust.main;
                        Color floorColor = floorRenderer.material.color;
                        
                        // Mix the floor color with white so the dust stands out clearly against the ground!
                        Color mixedDustColor = Color.Lerp(floorColor, new Color(0.8f, 0.8f, 0.8f), 0.45f);
                        mixedDustColor.a = 0.5f; 
                        main.startColor = mixedDustColor;
                    }
                }
            }
            else
            {
                if (slideDust.isPlaying) slideDust.Stop();
            }
        }
    }

    private void TrackSafePosition()
    {
        if (!isGrounded || controller == null) return;
        safePositionTimer += Time.deltaTime;
        if (safePositionTimer < 0.15f) return;
        safePositionTimer = 0f;

        if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 3f))
        {
            if (!hit.collider.isTrigger)
                lastSafePosition = ResolveStandingPosition(new Vector3(transform.position.x, hit.point.y, transform.position.z));
        }
    }

    private void RespawnAtLastSafePosition()
    {
        controller.enabled = false;
        transform.position = ResolveStandingPosition(lastSafePosition);
        controller.enabled = true;
        ClearMovementCarry(true);
        lastSafePosition = transform.position;
    }

    private void RecoverFromAbyss()
    {
        abyssRecoveredThisAirborneState = true;
        Vector3 targetPosition = lastSafePosition + Vector3.up * 1.2f;

        controller.enabled = false;
        transform.position = ResolveStandingPosition(targetPosition);
        controller.enabled = true;
        dashTimer = 0f;
        dashVelocity = Vector3.zero;
        isSliding = false;
        isSlamming = false;
        slideRequiresRelease = false;
        slideGroundGraceTimer = 0f;
        if (controller != null)
            controller.height = defaultHeight;
        velocity = new Vector3(0f, Mathf.Sqrt((CurrentJumpHeight + 1.4f) * -2f * gravity), 0f);
        momentum = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(momentum, Vector3.up), CurrentMoveSpeed * 0.45f);
        lastSafePosition = transform.position;
    }

    private void ClearMovementCarry(bool clearCooldowns)
    {
        CacheControllerDefaults();
        velocity = Vector3.zero;
        momentum = Vector3.zero;
        dashVelocity = Vector3.zero;
        dashTimer = 0f;
        isSliding = false;
        isSlamming = false;
        slideRequiresRelease = false;
        slideGroundGraceTimer = 0f;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        disableGroundCheckTimer = 0f;
        abyssRecoveredThisAirborneState = false;

        if (clearCooldowns)
        {
            dashCooldownTimer = 0f;
            slideCooldownTimer = 0f;
            slideLockoutTimer = 0f;
        }

        if (controller != null)
            controller.height = defaultHeight;
    }

    private void CacheControllerDefaults()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();
        if (controller != null && defaultHeight <= 0.01f)
            defaultHeight = controller.height;
    }

    private void ExitSlide(bool applyCooldown)
    {
        if (!isSliding)
        {
            if (controller != null)
                controller.height = defaultHeight;
            return;
        }

        isSliding = false;
        slideGroundGraceTimer = 0f;
        if (controller != null)
            controller.height = defaultHeight;
        momentum = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(momentum, Vector3.up), Mathf.Max(CurrentMoveSpeed * 1.05f, slideBaseSpeed * 0.85f));
        if (applyCooldown)
            slideCooldownTimer = slideCooldown;
    }

    private void FinishDash(Vector3 inputDir)
    {
        if (dashVelocity.sqrMagnitude <= 0.0001f)
        {
            dashTimer = 0f;
            return;
        }

        Vector3 exitDir = dashVelocity.normalized;
        Vector3 planarMomentum = Vector3.ProjectOnPlane(momentum, Vector3.up);
        float exitSpeed = Mathf.Min(planarMomentum.magnitude, Mathf.Max(CurrentMoveSpeed, dashExitSpeed));
        if (inputDir.sqrMagnitude > 0.0001f)
            exitDir = Vector3.Slerp(exitDir, inputDir.normalized, isGrounded ? 0.25f : 0.12f).normalized;

        momentum = exitDir * exitSpeed;
        dashVelocity = Vector3.zero;
        dashTimer = 0f;
    }

    private void ApplyGroundMovement(Vector3 inputDir)
    {
        Vector3 horizontalMomentum = Vector3.ProjectOnPlane(momentum, Vector3.up);

        if (inputDir.sqrMagnitude <= 0.0001f)
        {
            float stopRate = groundDeceleration * Mathf.Max(CurrentMoveSpeed, 1f) * 1.65f * Time.deltaTime;
            horizontalMomentum = Vector3.MoveTowards(horizontalMomentum, Vector3.zero, stopRate);
            if (horizontalMomentum.magnitude <= groundedStopSpeed)
                horizontalMomentum = Vector3.zero;

            momentum = horizontalMomentum;
            return;
        }

        Vector3 desiredDir = inputDir.normalized;
        Vector3 desiredVelocity = desiredDir * CurrentMoveSpeed;
        float forwardSpeed = Vector3.Dot(horizontalMomentum, desiredDir);
        float angleDot = horizontalMomentum.sqrMagnitude > 0.01f ? Vector3.Dot(horizontalMomentum.normalized, desiredDir) : 1f;
        if (angleDot < 0.2f)
        {
            float redirectRate = groundDeceleration * CurrentMoveSpeed * (angleDot < -0.35f ? 3.2f : 2.35f) * Time.deltaTime;
            horizontalMomentum = Vector3.MoveTowards(horizontalMomentum, desiredVelocity, redirectRate);
            momentum = horizontalMomentum;
            return;
        }
        bool carryingSpeed = horizontalMomentum.magnitude > CurrentMoveSpeed && forwardSpeed > CurrentMoveSpeed * 0.9f;

        if (carryingSpeed)
        {
            Vector3 sideways = horizontalMomentum - desiredDir * forwardSpeed;
            sideways = Vector3.MoveTowards(sideways, Vector3.zero, groundDeceleration * CurrentMoveSpeed * 1.3f * Time.deltaTime);
            float retainedForward = Mathf.MoveTowards(forwardSpeed, CurrentMoveSpeed, groundDeceleration * CurrentMoveSpeed * 2.3f * Time.deltaTime);
            horizontalMomentum = desiredDir * retainedForward + sideways;
        }
        else
        {
            horizontalMomentum = Vector3.MoveTowards(horizontalMomentum, desiredVelocity, groundAcceleration * CurrentMoveSpeed * 3.2f * Time.deltaTime);
        }

        momentum = horizontalMomentum;
    }

    private void ApplyAirMovement(Vector3 inputDir)
    {
        Vector3 horizontalMomentum = Vector3.ProjectOnPlane(momentum, Vector3.up);
        float airCarryLimit = Mathf.Clamp(launchCarrySpeedLimit + moveSpeedBonus, CurrentMoveSpeed * 1.18f, maxSpeedLimit);

        if (inputDir.sqrMagnitude > 0.0001f)
        {
            Vector3 wishDir = inputDir.normalized;
            float currentSpeed = Vector3.Dot(horizontalMomentum, wishDir);
            float targetAirSpeed = Mathf.Min(Mathf.Max(CurrentMoveSpeed * 1.08f, currentSpeed), CurrentMoveSpeed * 1.2f);
            float addSpeed = Mathf.Max(0f, targetAirSpeed - currentSpeed);
            float accel = airAcceleration * CurrentMoveSpeed * 1.28f * Time.deltaTime;
            accel = Mathf.Min(accel, addSpeed);
            horizontalMomentum += wishDir * accel;

            Vector3 lateral = horizontalMomentum - (wishDir * Vector3.Dot(horizontalMomentum, wishDir));
            lateral = Vector3.MoveTowards(lateral, Vector3.zero, airAcceleration * 0.9f * Time.deltaTime);
            horizontalMomentum = wishDir * Vector3.Dot(horizontalMomentum, wishDir) + lateral;
        }
        else
        {
            horizontalMomentum = Vector3.MoveTowards(horizontalMomentum, Vector3.zero, airAcceleration * 0.75f * Time.deltaTime);
        }

        momentum = Vector3.ClampMagnitude(horizontalMomentum, airCarryLimit);
    }

    private void HandleDefeatAndRespawn()
    {
        HandleDeath();
    }

    private void HandleDeath()
    {
        if (isDead) return;

        if (respawnOnDeath)
        {
            RespawnAtLastSafePosition();
            float targetHealth = Mathf.Clamp(CurrentMaxHealth * respawnHealthPercent, 1f, CurrentMaxHealth);
            currentHealth = targetHealth;
            damageInvulnerabilityTimer = damageInvulnerabilityTime;
            RefreshVitalsUI();
            return;
        }

        isDead = true;
        currentHealth = 0f;
        RefreshVitalsUI();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (crosshair != null) crosshair.SetActive(false);
        if (interactionPromptText != null)
        {
            interactionPromptText.text = "YOU DIED";
            interactionPromptText.gameObject.SetActive(true);
        }
        if (controller != null) controller.enabled = false;
    }

    public void PrepareForRunReset()
    {
        isDead = false;
        isUIActive = false;
        transitionLocked = false;
        damageInvulnerabilityTimer = 0f;
        damageFlashTimer = 0f;
        damageKickTimer = 0f;
        ClearMovementCarry(true);
        if (controller != null)
        {
            controller.enabled = true;
            controller.height = defaultHeight;
        }

        currentHealth = CurrentMaxHealth;
        lastSafePosition = ResolveStandingPosition(transform.position);
        safePositionTimer = 0f;
        RefreshVitalsUI();
        ClearFocus();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        EnsureCrosshair();
        if (crosshair != null) crosshair.SetActive(true);
        if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);
    }

    public void NotifySpawnPlacement(Vector3 spawnPosition)
    {
        ClearMovementCarry(true);
        lastSafePosition = ResolveStandingPosition(spawnPosition);
        safePositionTimer = 0f;
        transform.position = lastSafePosition;
    }

    public void NotifyWeaponHit(Color accentColor, bool kill)
    {
        EnsureCrosshair();
        crosshairPulseColor = kill ? Color.Lerp(accentColor, crosshairKillColor, 0.55f) : Color.Lerp(accentColor, crosshairHitColor, 0.35f);
        crosshairHitTimer = 0.11f;
        if (kill)
            crosshairKillTimer = 0.26f;
    }

    public void NotifyWeaponFired(bool heavy)
    {
        crosshairFireTimer = Mathf.Max(crosshairFireTimer, heavy ? 0.14f : 0.1f);
    }

    public void RefreshVitalsUI()
    {
        if (healthText != null)
            healthText.text = $"HP {Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(CurrentMaxHealth)}";
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = Health01;
            healthBarFill.color = Color.Lerp(new Color(1f, 0.3f, 0.26f), new Color(0.34f, 1f, 0.6f), Health01);
        }

        if (currencyText != null)
            currencyText.text = $"COINS {currency}";
    }

    private void EnsureVitalsHud()
    {
        if (healthText != null && currencyText != null && healthBarFill != null)
            return;

        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null)
            return;

        Transform root = canvas.transform.Find("VitalsHUD");
        if (root == null)
        {
            GameObject hud = new GameObject("VitalsHUD");
            hud.transform.SetParent(canvas.transform, false);
            RectTransform rect = hud.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(14f, -12f);
            rect.sizeDelta = new Vector2(178f, 62f);

            Image panel = hud.AddComponent<Image>();
            panel.color = new Color(0.02f, 0.035f, 0.05f, 0.72f);

            GameObject healthBackObject = new GameObject("HealthBarBack");
            healthBackObject.transform.SetParent(hud.transform, false);
            RectTransform healthBackRect = healthBackObject.AddComponent<RectTransform>();
            healthBackRect.anchorMin = new Vector2(0f, 1f);
            healthBackRect.anchorMax = new Vector2(0f, 1f);
            healthBackRect.pivot = new Vector2(0f, 1f);
            healthBackRect.anchoredPosition = new Vector2(12f, -10f);
            healthBackRect.sizeDelta = new Vector2(148f, 10f);
            healthBarBack = healthBackObject.AddComponent<Image>();
            healthBarBack.color = new Color(0.14f, 0.18f, 0.22f, 0.96f);

            GameObject healthFillObject = new GameObject("HealthBarFill");
            healthFillObject.transform.SetParent(healthBackObject.transform, false);
            RectTransform healthFillRect = healthFillObject.AddComponent<RectTransform>();
            healthFillRect.anchorMin = new Vector2(0f, 0f);
            healthFillRect.anchorMax = new Vector2(1f, 1f);
            healthFillRect.offsetMin = new Vector2(2f, 2f);
            healthFillRect.offsetMax = new Vector2(-2f, -2f);
            healthBarFill = healthFillObject.AddComponent<Image>();
            healthBarFill.type = Image.Type.Filled;
            healthBarFill.fillMethod = Image.FillMethod.Horizontal;

            GameObject currencyBackObject = new GameObject("CurrencyBack");
            currencyBackObject.transform.SetParent(hud.transform, false);
            RectTransform currencyBackRect = currencyBackObject.AddComponent<RectTransform>();
            currencyBackRect.anchorMin = new Vector2(0f, 1f);
            currencyBackRect.anchorMax = new Vector2(0f, 1f);
            currencyBackRect.pivot = new Vector2(0f, 1f);
            currencyBackRect.anchoredPosition = new Vector2(12f, -42f);
            currencyBackRect.sizeDelta = new Vector2(94f, 18f);
            currencyPanel = currencyBackObject.AddComponent<Image>();
            currencyPanel.color = new Color(0.18f, 0.14f, 0.08f, 0.92f);

            if (healthText == null)
                healthText = CreateVitalsText(hud.transform, "HealthText", new Vector2(0f, 1f), new Vector2(12f, -24f), 13f, new Color(0.92f, 0.97f, 1f));
            if (currencyText == null)
                currencyText = CreateVitalsText(hud.transform, "CoinsText", new Vector2(0f, 1f), new Vector2(22f, -44f), 12f, new Color(1f, 0.87f, 0.46f));
            return;
        }

        if (healthText == null)
            healthText = root.Find("HealthText")?.GetComponent<TMP_Text>();
        if (healthText == null)
            healthText = root.Find("HullText")?.GetComponent<TMP_Text>();
        if (currencyText == null)
            currencyText = root.Find("CoinsText")?.GetComponent<TMP_Text>();
        if (healthBarBack == null)
            healthBarBack = root.Find("HealthBarBack")?.GetComponent<Image>();
        if (healthBarFill == null)
            healthBarFill = root.Find("HealthBarBack/HealthBarFill")?.GetComponent<Image>();
        if (currencyPanel == null)
            currencyPanel = root.Find("CurrencyBack")?.GetComponent<Image>();

        if (healthBarBack == null || healthBarFill == null)
        {
            GameObject healthBackObject = new GameObject("HealthBarBack");
            healthBackObject.transform.SetParent(root, false);
            RectTransform healthBackRect = healthBackObject.AddComponent<RectTransform>();
            healthBackRect.anchorMin = new Vector2(0f, 1f);
            healthBackRect.anchorMax = new Vector2(0f, 1f);
            healthBackRect.pivot = new Vector2(0f, 1f);
            healthBackRect.anchoredPosition = new Vector2(12f, -10f);
            healthBackRect.sizeDelta = new Vector2(148f, 10f);
            healthBarBack = healthBackObject.AddComponent<Image>();
            healthBarBack.color = new Color(0.14f, 0.18f, 0.22f, 0.96f);

            GameObject healthFillObject = new GameObject("HealthBarFill");
            healthFillObject.transform.SetParent(healthBackObject.transform, false);
            RectTransform healthFillRect = healthFillObject.AddComponent<RectTransform>();
            healthFillRect.anchorMin = new Vector2(0f, 0f);
            healthFillRect.anchorMax = new Vector2(1f, 1f);
            healthFillRect.offsetMin = new Vector2(2f, 2f);
            healthFillRect.offsetMax = new Vector2(-2f, -2f);
            healthBarFill = healthFillObject.AddComponent<Image>();
            healthBarFill.type = Image.Type.Filled;
            healthBarFill.fillMethod = Image.FillMethod.Horizontal;
        }

        if (currencyPanel == null)
        {
            GameObject currencyBackObject = new GameObject("CurrencyBack");
            currencyBackObject.transform.SetParent(root, false);
            RectTransform currencyBackRect = currencyBackObject.AddComponent<RectTransform>();
            currencyBackRect.anchorMin = new Vector2(0f, 1f);
            currencyBackRect.anchorMax = new Vector2(0f, 1f);
            currencyBackRect.pivot = new Vector2(0f, 1f);
            currencyBackRect.anchoredPosition = new Vector2(12f, -42f);
            currencyBackRect.sizeDelta = new Vector2(94f, 18f);
            currencyPanel = currencyBackObject.AddComponent<Image>();
            currencyPanel.color = new Color(0.18f, 0.14f, 0.08f, 0.92f);
        }
    }

    private TMP_Text CreateVitalsText(Transform parent, string name, Vector2 anchor, Vector2 position, float fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(156f, 20f);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Left;
        text.color = color;
        text.text = string.Empty;
        return text;
    }

    private void TriggerDamageFeedback()
    {
        damageFlashTimer = 0f;
        damageKickTimer = 0f;
    }

    private void UpdateDamageFeedback()
    {
        if (weaponKickTimer > 0f)
            weaponKickTimer -= Time.deltaTime;
        if (damageKickTimer > 0f)
            damageKickTimer -= Time.deltaTime;
        if (damageFlashTimer > 0f)
            damageFlashTimer -= Time.deltaTime;
    }

    private void EnsureDamageOverlay()
    {
        if (damageFlashOverlay == null)
            return;

        damageFlashOverlay.enabled = false;
        damageFlashOverlay.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, 0f);
    }

    public void ApplySettings(float sensitivity, float desiredBaseFov, float masterVolume, bool persist = true)
    {
        mouseSensitivity = Mathf.Clamp(sensitivity, 20f, 220f);
        baseFOV = Mathf.Clamp(desiredBaseFov, 70f, 120f);
        if (playerCamera != null)
            playerCamera.fieldOfView = baseFOV;
        AudioListener.volume = Mathf.Clamp01(masterVolume);

        if (!persist) return;

        PlayerPrefs.SetFloat(MouseSensitivityPrefKey, mouseSensitivity);
        PlayerPrefs.SetFloat(BaseFovPrefKey, baseFOV);
        PlayerPrefs.SetFloat(MasterVolumePrefKey, AudioListener.volume);
        PlayerPrefs.Save();
    }

    public float GetBaseFov()
    {
        return baseFOV;
    }

    public float GetMasterVolume()
    {
        return AudioListener.volume;
    }

    private void LoadSettings()
    {
        float savedSensitivity = PlayerPrefs.GetFloat(MouseSensitivityPrefKey, mouseSensitivity);
        float savedFov = PlayerPrefs.GetFloat(BaseFovPrefKey, baseFOV > 0f ? baseFOV : 90f);
        float savedVolume = PlayerPrefs.GetFloat(MasterVolumePrefKey, AudioListener.volume <= 0f ? 1f : AudioListener.volume);
        ApplySettings(savedSensitivity, savedFov, savedVolume, false);
    }

    private void EnsureSpeedLines()
    {
        if (speedLines != null && speedLines.isPlaying)
            speedLines.Stop();
    }

    private Material CreateSpeedLineMaterial()
    {
        return null;
    }

    private void UpdateSpeedLines()
    {
        if (speedLines != null && speedLines.isPlaying)
            speedLines.Stop();
    }

    private void SpawnDashBurst()
    {
        SpawnWorldBurst(transform.position + Vector3.up * 0.08f, dashBurstColor, 0.09f, 0.28f, 0.92f);
    }

    private void SpawnSlamBurst(float strength)
    {
        return;
    }

    private void SpawnWorldBurst(Vector3 center, Color color, float duration, float startRadius, float endRadius)
    {
        if (!gameObject.activeInHierarchy)
            return;

        StartCoroutine(WorldBurstRoutine(center, color, duration, startRadius, endRadius));
    }

    private System.Collections.IEnumerator WorldBurstRoutine(Vector3 center, Color color, float duration, float startRadius, float endRadius)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "MoveBurst";
        ring.transform.position = center;
        ring.transform.localScale = new Vector3(startRadius, 0.035f, startRadius);

        Collider col = ring.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        Renderer renderer = ring.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = GetWorldFxMaterial(color);

        float elapsed = 0f;
        while (elapsed < duration && ring != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float radius = Mathf.Lerp(startRadius, endRadius, t);
            ring.transform.localScale = new Vector3(radius, 0.035f, radius);

            if (renderer != null)
            {
                Color frameColor = color;
                frameColor.a *= 1f - t;
                ApplyWorldFxColor(renderer, frameColor);
            }

            yield return null;
        }

        if (ring != null)
            Destroy(ring);
    }

    private Material GetWorldFxMaterial(Color color)
    {
        if (cachedWorldFxMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            cachedWorldFxMaterial = new Material(shader)
            {
                name = "PlayerMoveBurst_Mat"
            };
        }

        Material mat = new Material(cachedWorldFxMaterial);
        ApplyWorldFxColor(mat, color);
        return mat;
    }

    private void ApplyWorldFxColor(Renderer renderer, Color color)
    {
        if (renderer == null || renderer.sharedMaterial == null)
            return;

        ApplyWorldFxColor(renderer.sharedMaterial, color);
    }

    private void ApplyWorldFxColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", color * 1.5f);
    }

    private void EnsureCrosshair()
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;

        if (crosshair == null)
        {
            bool createdNewCrosshair = false;
            Transform existingRoot = canvas.transform.Find("RuntimeCrosshair");
            if (existingRoot != null)
            {
                crosshair = existingRoot.gameObject;
                crosshairRect = crosshair.GetComponent<RectTransform>();
            }
            else
            {
                crosshair = new GameObject("RuntimeCrosshair");
                crosshair.transform.SetParent(canvas.transform, false);
                crosshairRect = crosshair.AddComponent<RectTransform>();
                crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
                crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
                crosshairRect.pivot = new Vector2(0.5f, 0.5f);
                crosshairRect.sizeDelta = new Vector2(84f, 84f);
                createdNewCrosshair = true;
            }

            if (createdNewCrosshair)
                BuildCrosshairSegments(crosshair.transform);
        }

        if (crosshairRect == null)
            crosshairRect = crosshair.GetComponent<RectTransform>();

        if (crosshairRect == null)
            return;

        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = canvas.transform.GetChild(i);
            if (child == null || child == crosshair.transform) continue;
            if (!child.name.Contains("Crosshair")) continue;
            child.gameObject.SetActive(false);
        }

        bool needsSegments = false;
        for (int i = 0; i < crosshairSegmentRects.Length; i++)
        {
            if (crosshairSegmentRects[i] == null || crosshairSegmentImages[i] == null)
            {
                needsSegments = true;
                break;
            }
        }

        if (!needsSegments) return;

        Image[] images = crosshair.GetComponentsInChildren<Image>(true);
        if (images.Length >= 4)
        {
            for (int i = 0; i < 4; i++)
            {
                crosshairSegmentImages[i] = images[i];
                crosshairSegmentRects[i] = images[i].rectTransform;
            }
            return;
        }

        BuildCrosshairSegments(crosshair.transform);
    }

    private void BuildCrosshairSegments(Transform root)
    {
        Vector2[] positions =
        {
            new Vector2(0f, 14f),
            new Vector2(14f, 0f),
            new Vector2(0f, -14f),
            new Vector2(-14f, 0f)
        };

        Vector2[] sizes =
        {
            new Vector2(3f, 13f),
            new Vector2(13f, 3f),
            new Vector2(3f, 13f),
            new Vector2(13f, 3f)
        };

        for (int i = 0; i < 4; i++)
        {
            GameObject go = new GameObject("CrosshairSegment_" + i);
            go.transform.SetParent(root, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = positions[i];
            rect.sizeDelta = sizes[i];
            Image image = go.AddComponent<Image>();
            image.color = crosshairBaseColor;
            image.raycastTarget = false;
            crosshairSegmentRects[i] = rect;
            crosshairSegmentImages[i] = image;
        }
    }

    private void UpdateCrosshairVisual()
    {
        EnsureCrosshair();
        if (crosshairRect == null || crosshair == null || !crosshair.activeSelf) return;

        if (crosshairHitTimer > 0f)
            crosshairHitTimer -= Time.deltaTime;
        if (crosshairKillTimer > 0f)
            crosshairKillTimer -= Time.deltaTime;
        if (crosshairFireTimer > 0f)
            crosshairFireTimer -= Time.deltaTime;

        float hit01 = Mathf.Clamp01(crosshairHitTimer / 0.11f);
        float kill01 = Mathf.Clamp01(crosshairKillTimer / 0.26f);
        float fire01 = Mathf.Clamp01(crosshairFireTimer / 0.14f);
        bool focused = currentInteractable != null && !isUIActive && !isDead;
        bool hostile = IsAimingAtHostile();

        Color targetColor = focused
            ? crosshairFocusColor
            : hostile ? crosshairHostileColor : crosshairBaseColor;
        if (hit01 > 0.01f || kill01 > 0.01f)
            targetColor = Color.Lerp(targetColor, crosshairPulseColor == default ? crosshairHitColor : crosshairPulseColor, Mathf.Max(hit01, kill01));

        float gap = 12f + fire01 * 2.5f + hit01 * 5f + kill01 * 8f;
        float length = 11f + fire01 * 1.2f + kill01 * 2.4f;
        float thickness = 3f + hit01 * 1.1f + kill01 * 1.3f;
        float scale = 1f + hit01 * 0.08f + kill01 * 0.1f;
        crosshairRect.localScale = Vector3.one * scale;

        Vector2[] positions =
        {
            new Vector2(0f, gap),
            new Vector2(gap, 0f),
            new Vector2(0f, -gap),
            new Vector2(-gap, 0f)
        };

        for (int i = 0; i < crosshairSegmentRects.Length; i++)
        {
            if (crosshairSegmentRects[i] == null || crosshairSegmentImages[i] == null) continue;
            crosshairSegmentRects[i].anchoredPosition = positions[i];
            crosshairSegmentRects[i].sizeDelta = i % 2 == 0
                ? new Vector2(thickness, length)
                : new Vector2(length, thickness);
            crosshairSegmentImages[i].color = targetColor;
        }
    }

    private bool IsAimingAtHostile()
    {
        if (cameraTransform == null || isUIActive || isDead)
            return false;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, 120f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null) continue;
            if (collider.transform.IsChildOf(transform)) continue;

            IDamageable damageable = collider.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable is PlayerController)
            {
                if (!collider.isTrigger) return false;
                continue;
            }

            if (damageable is BasicEnemyAI enemy)
                return !enemy.IsCombatResolved;

            if (damageable is Target target)
                return target != null && target.currentHealth > 0f;

            return true;
        }

        return false;
    }

    public void ApplyMobilityUpgrade(float moveBonusAmount, float dashBonusAmount, float jumpBonusAmount)
    {
        moveSpeedBonus += Mathf.Max(0f, moveBonusAmount);
        dashForceBonus += Mathf.Max(0f, dashBonusAmount);
        jumpHeightBonus += Mathf.Max(0f, jumpBonusAmount);
    }

    public void ApplyHullUpgrade(float maxHealthAmount, float healAmount)
    {
        maxHealthBonus += Mathf.Max(0f, maxHealthAmount);
        currentHealth = Mathf.Min(CurrentMaxHealth, currentHealth + Mathf.Max(0f, healAmount));
        RefreshVitalsUI();
    }

    public void ResetRunModifiers()
    {
        moveSpeedBonus = 0f;
        dashForceBonus = 0f;
        jumpHeightBonus = 0f;
        maxHealthBonus = 0f;
        PrepareForRunReset();
    }

    void HandleLook()
    {
        EnsureCameraReferences();
        if (cameraTransform == null || UnityEngine.InputSystem.Mouse.current == null)
            return;

        Vector2 mouseDelta = UnityEngine.InputSystem.Mouse.current.delta.ReadValue();

        // Removed the "SmoothDamp" because it often adds "Input Lag", making it feel floaty/slippery.
        // We divide sensitivity by 100 here so that 100 in the inspector = 1.0 multiplier.
        float multiplier = mouseSensitivity / 100f;
        float mouseX = mouseDelta.x * multiplier;
        float mouseY = mouseDelta.y * multiplier;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        float sideTilt = -moveInputRaw.x * (isGrounded ? 0.9f : 0.45f);
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, sideTilt);

        transform.Rotate(Vector3.up * mouseX);
    }

    private void EnsureCameraReferences()
    {
        if (cameraTransform == null)
        {
            if (playerCamera != null)
                cameraTransform = playerCamera.transform;
            else
            {
                Camera childCamera = GetComponentInChildren<Camera>(true);
                if (childCamera != null)
                {
                    playerCamera = childCamera;
                    cameraTransform = childCamera.transform;
                }
                else if (Camera.main != null && Camera.main.transform.IsChildOf(transform))
                {
                    playerCamera = Camera.main;
                    cameraTransform = Camera.main.transform;
                }
            }
        }

        if (playerCamera == null && cameraTransform != null)
            playerCamera = cameraTransform.GetComponent<Camera>();
    }

    private Vector3 ResolveStandingPosition(Vector3 referencePosition)
    {
        Vector3 resolved = referencePosition;
        float originLift = Mathf.Max(3f, StandingOffset + 1.5f);
        float rayDistance = Mathf.Max(8f, originLift + 8f);
        Vector3 rayOrigin = new Vector3(referencePosition.x, referencePosition.y + originLift, referencePosition.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.isTrigger)
        {
            resolved.y = hit.point.y + StandingOffset;
        }
        else
        {
            resolved.y = referencePosition.y + StandingOffset;
        }

        return resolved;
    }
}
