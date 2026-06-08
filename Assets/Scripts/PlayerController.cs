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
    public float groundDeceleration = 11f;
    public float airAcceleration = 10f;
    public float gravity = -29f;
    public float jumpHeight = 2.85f;
    public int maxJumps = 2; 
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;

    [Header("Movement (Dash)")]
    public float dashForce = 28f; // Pure momentum burst
    public float dashCooldown = 0.85f;

    [Header("Movement (Slide & Slam)")]
    public float slideBaseSpeed = 18f; // Just a bit faster than running
    public float slideFriction = 1.95f; // How fast you slow down while sliding
    public float fallSpeedToSlideBoost = 0.58f; // Hitting the ground hard speeds up your slide
    public float maxSlideJumpCarrySpeed = 26f;
    public float slideHeight = 1f;
    public float slideCooldown = 0.45f;
    public float slamSpeed = 40f; // How fast you plummet downwards
    public float postSlamSlideLockout = 0.25f;
    private float defaultHeight;

    [Header("Movement (Limits)")]
    public float maxSpeedLimit = 42f; // Enough headroom for chaining movement

    [Header("FX & Polish")]
    public Camera playerCamera;
    public ParticleSystem slideDust; // Drag a particle system here!
    public ParticleSystem speedLines;
    public float maxSpeedFOV = 100f; // Zoom out FOV when going fast!
    public float slideCameraDrop = 0.5f; // Dip the camera down when sliding
    public float overdriveSpeedThreshold = 24f;
    public float fallRespawnY = -18f;
    public float abyssRecoveryY = -8f;
    public bool enableAbyssRecovery = true;
    private float baseFOV;
    private Vector3 baseCameraLocalPos;
    private Vector3 lastSafePosition;
    private float safePositionTimer;
    private float slideLockoutTimer;
    private float slideCooldownTimer;
    private float moveSpeedBonus;
    private float dashForceBonus;
    private float jumpHeightBonus;
    private float maxHealthBonus;

    [Header("Damage Feedback")]
    public Color damageFlashColor = new Color(0.9f, 0.12f, 0.08f, 0.22f);
    public float damageFlashDuration = 0.26f;
    public float damageLookKick = 2.8f;
    public float damageFovKick = 3.5f;
    public float weaponKickDuration = 0.12f;
    public float weaponLookKick = 0.7f;
    public float weaponCameraKickBack = 0.075f;
    public float weaponCameraKickDown = 0.035f;

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
    private bool isSliding;
    private bool isSlamming;
    private Vector3 momentum;
    private float lastFrameVelocityY;
    private float disableGroundCheckTimer = 0f;
    private bool abyssRecoveredThisAirborneState;
    private bool transitionLocked;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float damageFlashTimer;
    private float damageKickTimer;
    private float weaponKickTimer;
    private float crosshairFireTimer;
    private Image damageFlashOverlay;
    private RectTransform crosshairRect;
    private readonly RectTransform[] crosshairSegmentRects = new RectTransform[4];
    private readonly Image[] crosshairSegmentImages = new Image[4];
    private float crosshairHitTimer;
    private float crosshairKillTimer;
    private Color crosshairPulseColor;

    private float CurrentMoveSpeed => moveSpeed + moveSpeedBonus;
    private float CurrentDashForce => dashForce + dashForceBonus;
    private float CurrentJumpHeight => jumpHeight + jumpHeightBonus;
    private float CurrentMaxHealth => maxHealth + maxHealthBonus;
    public float EffectiveMaxHealth => CurrentMaxHealth;
    public float Health01 => CurrentMaxHealth <= 0.01f ? 0f : Mathf.Clamp01(currentHealth / CurrentMaxHealth);

    private const string MouseSensitivityPrefKey = "project_structure.mouse_sensitivity";
    private const string BaseFovPrefKey = "project_structure.base_fov";
    private const string MasterVolumePrefKey = "project_structure.master_volume";

    private Interactable currentInteractable;
    public float interactionDistance = 3f;
    public LayerMask interactableLayer;
    private string transientPromptText;
    private float transientPromptTimer;

    [Header("State")]
    public bool isUIActive = false; // True when a puzzle or terminal screen is open
    public bool respawnOnDeath = false;
    public bool isDead = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        defaultHeight = controller.height;
        Cursor.lockState = CursorLockMode.Locked;
        currentHealth = CurrentMaxHealth;
        currency = 0;

        // Auto-assign camera if we forgot, and log its starting positions for FX
        if (playerCamera == null && cameraTransform != null) playerCamera = cameraTransform.GetComponent<Camera>();
        if (playerCamera != null) baseFOV = playerCamera.fieldOfView;
        if (cameraTransform != null) baseCameraLocalPos = cameraTransform.localPosition;
        lastSafePosition = transform.position;
        EnsureVitalsHud();
        EnsureCrosshair();
        EnsureSpeedLines();
        EnsureDamageOverlay();
        LoadSettings();

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

        if (isDead) return;

        if (isUIActive) return; // Don't move or interact if a puzzle is open

        if (transitionLocked)
        {
            HandleLook();
            return;
        }

        HandleMovement();
        HandleLook();
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

        isSliding = false;
        isSlamming = false;
        momentum = Vector3.zero;
        velocity = Vector3.zero;
        if (controller != null)
            controller.height = defaultHeight;
    }

    // --- External Physics Methods ---
    public void LaunchPlayer(Vector3 launchVelocity)
    {
        Debug.Log($"[PlayerController] LaunchPlayer called with velocity: {launchVelocity}");
        
        // Force the player to be airborne for a split second so friction/gravity doesn't instantly cancel the jump pad
        disableGroundCheckTimer = 0.2f;
        
        // Disable CharacterController snapping by physically pushing the player off the ground first
        controller.Move(Vector3.up * 0.1f);

        // Cancel downward gravity immediately
        if (velocity.y < 0) velocity.y = 0;
        
        // Add vertical height
        velocity.y = launchVelocity.y; // Override rather than add so double-jumping pads don't compound infinitely
        
        // Add forward/horizontal momentum
        momentum += new Vector3(launchVelocity.x, 0, launchVelocity.z);
        
        // Put player in falling state so they aren't stuck sliding
        isSliding = false;
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
            message.Equals("HULL BREACH", System.StringComparison.OrdinalIgnoreCase))
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
        if (trimmed.Equals("HULL BREACH", System.StringComparison.OrdinalIgnoreCase))
            return "<color=#FF7668><b>HULL BREACH</b></color>";
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
            isSlamming = false; // FIX: Always reset slam state when touching the ground!
            abyssRecoveredThisAirborneState = false;
        }

        if (dashCooldownTimer > 0) dashCooldownTimer -= Time.deltaTime;
        if (slideLockoutTimer > 0f) slideLockoutTimer -= Time.deltaTime;
        if (slideCooldownTimer > 0f) slideCooldownTimer -= Time.deltaTime;
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

        Vector3 inputDir = transform.right * input.x + transform.forward * input.y;
        if (jumpBufferTimer > 0f)
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
        if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            jumpBufferTimer = jumpBufferTime;

        // --- DASH LOGIC ---
        // Dash now ADDS a massive burst of momentum rather than overriding control
        if (UnityEngine.InputSystem.Keyboard.current.leftShiftKey.wasPressedThisFrame && dashCooldownTimer <= 0)
        {
            dashCooldownTimer = dashCooldown;
            Vector3 dashDir = inputDir.magnitude > 0.1f ? inputDir : transform.forward;
            
            // Add momentum (retaining previous velocity)
            momentum += dashDir * CurrentDashForce;
            velocity.y = 0f; // Brief hover effect
        }

        // --- SLIDE & SLAM LOGIC ---
        bool wantsToSlide = UnityEngine.InputSystem.Keyboard.current.ctrlKey.isPressed || UnityEngine.InputSystem.Keyboard.current.cKey.isPressed;

        // Ground Slam (Mid-air drop)
        if (wantsToSlide && !isGrounded && !isSlamming)
        {
            isSlamming = true;
            velocity.y = -slamSpeed; // Plunge straight down
        }

        if (wantsToSlide && isGrounded && !isSliding && slideLockoutTimer <= 0f && slideCooldownTimer <= 0f)
        {
            isSliding = true;
            controller.height = slideHeight;

            // If we fell from a great height (or slammed), turn that fall speed into forward slide speed!
            float fallBoost = 0f;
            if (fallSpeed < -10f && !isSlamming)
            {
                // Multiplier makes massive slams give crazy slide speed
                fallBoost = Mathf.Abs(fallSpeed) * fallSpeedToSlideBoost;
            }
            if (isSlamming)
            {
                slideLockoutTimer = postSlamSlideLockout;
                isSlamming = false;
                isSliding = false;
                controller.height = defaultHeight;
                momentum *= 0.65f;
                slideCooldownTimer = Mathf.Max(slideCooldownTimer, postSlamSlideLockout);
                return;
            }

            float currentSpeed = momentum.magnitude;
            float newSpeed = Mathf.Max(slideBaseSpeed, currentSpeed + fallBoost); 
            
            // If we have no input direction, slide wherever we are looking
            Vector3 slideDir = inputDir.magnitude > 0.1f ? inputDir.normalized : transform.forward;
            momentum = slideDir * newSpeed;
            velocity.y = Mathf.Max(velocity.y, 0f);
        }
        else if ((!wantsToSlide || !isGrounded) && isSliding)
        {
            // Stop sliding 
            if (!wantsToSlide)
            {
                isSliding = false;
                controller.height = defaultHeight;
                slideCooldownTimer = slideCooldown;
            }
        }

        // --- MOMENTUM & FRICTION LOGIC ---
        if (isGrounded)
        {
            if (isSliding)
            {
                momentum = Vector3.Lerp(momentum, Vector3.zero, slideFriction * 0.85f * Time.deltaTime);
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
                isSliding = false; 
                controller.height = defaultHeight;
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
        controller.Move(finalMove * Time.deltaTime);

        // --- FX & POLISH (FOV, Camera Drop, Particles) ---
        if (playerCamera != null)
        {
            // Fix: Use actual physical speed (ignoring falling) to calculate FOV scaling. 
            // This fixes the bug where running into a wall keeps FOV zoomed out!
            Vector3 actualGroundVel = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
            float speedRatio = actualGroundVel.magnitude / maxSpeedLimit;
            
            float targetFOV = isSlamming ? maxSpeedFOV : Mathf.Lerp(baseFOV, maxSpeedFOV, speedRatio);
            
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, 10f * Time.deltaTime);

            float targetCamY = isSliding ? baseCameraLocalPos.y - slideCameraDrop : baseCameraLocalPos.y;
            float weaponKick01 = Mathf.Clamp01(weaponKickTimer / Mathf.Max(0.01f, weaponKickDuration));
            Vector3 recoilOffset = new Vector3(0f, -weaponCameraKickDown * weaponKick01, -weaponCameraKickBack * weaponKick01);
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition, 
                new Vector3(baseCameraLocalPos.x, targetCamY, baseCameraLocalPos.z) + recoilOffset, 
                15f * Time.deltaTime
            );
        }

        UpdateSpeedLines();

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
                lastSafePosition = transform.position;
        }
    }

    private void RespawnAtLastSafePosition()
    {
        controller.enabled = false;
        transform.position = lastSafePosition + Vector3.up * 1.0f;
        controller.enabled = true;
        velocity = Vector3.zero;
        momentum = Vector3.zero;
        isSliding = false;
        isSlamming = false;
        controller.height = defaultHeight;
    }

    private void RecoverFromAbyss()
    {
        abyssRecoveredThisAirborneState = true;
        Vector3 targetPosition = lastSafePosition + Vector3.up * 1.2f;
        CybergrindArenaGenerator generator = FindAnyObjectByType<CybergrindArenaGenerator>();
        if (generator != null && generator.TryGetRecoveryPosition(transform.position, out Vector3 recoveryPosition))
            targetPosition = recoveryPosition;

        controller.enabled = false;
        transform.position = targetPosition;
        controller.enabled = true;
        velocity = new Vector3(0f, Mathf.Sqrt((CurrentJumpHeight + 1.4f) * -2f * gravity), 0f);
        momentum *= 0.55f;
    }

    private void ApplyGroundMovement(Vector3 inputDir)
    {
        if (inputDir.sqrMagnitude <= 0.0001f)
        {
            momentum = Vector3.MoveTowards(momentum, Vector3.zero, groundDeceleration * CurrentMoveSpeed * 0.95f * Time.deltaTime);
            return;
        }

        Vector3 desiredDir = inputDir.normalized;
        Vector3 desiredVelocity = desiredDir * CurrentMoveSpeed;
        float forwardSpeed = Vector3.Dot(momentum, desiredDir);
        bool carryingSpeed = momentum.magnitude > CurrentMoveSpeed && forwardSpeed > CurrentMoveSpeed * 0.9f;

        if (carryingSpeed)
        {
            Vector3 sideways = Vector3.ProjectOnPlane(momentum, desiredDir);
            sideways = Vector3.MoveTowards(sideways, Vector3.zero, groundDeceleration * CurrentMoveSpeed * 0.85f * Time.deltaTime);
            float retainedForward = Mathf.MoveTowards(forwardSpeed, CurrentMoveSpeed, groundDeceleration * CurrentMoveSpeed * 1.2f * Time.deltaTime);
            momentum = desiredDir * retainedForward + sideways;
        }
        else
        {
            momentum = Vector3.MoveTowards(momentum, desiredVelocity, groundAcceleration * CurrentMoveSpeed * 2.1f * Time.deltaTime);
        }
    }

    private void ApplyAirMovement(Vector3 inputDir)
    {
        if (inputDir.sqrMagnitude > 0.0001f)
        {
            Vector3 wishDir = inputDir.normalized;
            float currentSpeed = Vector3.Dot(momentum, wishDir);
            float targetAirSpeed = Mathf.Max(CurrentMoveSpeed, momentum.magnitude);
            float addSpeed = Mathf.Max(0f, targetAirSpeed - currentSpeed);
            float accel = airAcceleration * CurrentMoveSpeed * 1.7f * Time.deltaTime;
            accel = Mathf.Min(accel, addSpeed);
            momentum += wishDir * accel;

            Vector3 lateral = Vector3.ProjectOnPlane(momentum, wishDir);
            momentum = wishDir * Vector3.Dot(momentum, wishDir) + Vector3.MoveTowards(lateral, lateral * 0.92f, airAcceleration * 0.15f * Time.deltaTime);
        }
        else
        {
            momentum *= 1f - Mathf.Clamp01(Time.deltaTime * 0.08f);
        }
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
            interactionPromptText.text = "HULL BREACH";
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
        velocity = Vector3.zero;
        momentum = Vector3.zero;
        isSliding = false;
        isSlamming = false;
        abyssRecoveredThisAirborneState = false;
        if (controller != null)
        {
            controller.enabled = true;
            controller.height = defaultHeight;
        }

        currentHealth = CurrentMaxHealth;
        lastSafePosition = transform.position;
        safePositionTimer = 0f;
        RefreshVitalsUI();
        ClearFocus();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        EnsureCrosshair();
        if (crosshair != null) crosshair.SetActive(true);
        if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);
        if (damageFlashOverlay != null)
        {
            damageFlashOverlay.enabled = false;
            damageFlashOverlay.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, 0f);
        }
    }

    public void NotifySpawnPlacement(Vector3 spawnPosition)
    {
        velocity = Vector3.zero;
        momentum = Vector3.zero;
        lastSafePosition = spawnPosition;
        safePositionTimer = 0f;
        abyssRecoveredThisAirborneState = false;
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
        weaponKickTimer = Mathf.Max(weaponKickTimer, weaponKickDuration * (heavy ? 1.15f : 1f));
        crosshairFireTimer = Mathf.Max(crosshairFireTimer, heavy ? 0.14f : 0.1f);
        xRotation = Mathf.Clamp(xRotation - (heavy ? weaponLookKick * 1.35f : weaponLookKick), -80f, 80f);
    }

    private void RefreshVitalsUI()
    {
        if (healthText != null)
            healthText.text = $"HULL {Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(CurrentMaxHealth)}";

        if (currencyText != null)
            currencyText.text = $"COINS {currency}";
    }

    private void EnsureVitalsHud()
    {
        if (healthText != null && currencyText != null)
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
            rect.anchoredPosition = new Vector2(22f, -18f);
            rect.sizeDelta = new Vector2(240f, 88f);

            Image panel = hud.AddComponent<Image>();
            panel.color = new Color(0.02f, 0.035f, 0.05f, 0.72f);

            if (healthText == null)
                healthText = CreateVitalsText(hud.transform, "HullText", new Vector2(0f, 1f), new Vector2(18f, -16f), 26f, new Color(0.92f, 0.97f, 1f));
            if (currencyText == null)
                currencyText = CreateVitalsText(hud.transform, "CoinsText", new Vector2(0f, 1f), new Vector2(18f, -48f), 22f, new Color(1f, 0.87f, 0.46f));
            return;
        }

        if (healthText == null)
            healthText = root.Find("HullText")?.GetComponent<TMP_Text>();
        if (currencyText == null)
            currencyText = root.Find("CoinsText")?.GetComponent<TMP_Text>();
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
        rect.sizeDelta = new Vector2(210f, 28f);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Left;
        text.color = color;
        text.text = string.Empty;
        return text;
    }

    private void TriggerDamageFeedback()
    {
        damageFlashTimer = damageFlashDuration;
        damageKickTimer = damageFlashDuration;
        xRotation = Mathf.Clamp(xRotation - damageLookKick, -80f, 80f);
    }

    private void UpdateDamageFeedback()
    {
        if (weaponKickTimer > 0f)
            weaponKickTimer -= Time.deltaTime;

        if (playerCamera != null && damageKickTimer > 0f)
        {
            damageKickTimer -= Time.deltaTime;
            float normalized = Mathf.Clamp01(damageKickTimer / Mathf.Max(0.01f, damageFlashDuration));
            float target = baseFOV + damageFovKick * normalized;
            playerCamera.fieldOfView = Mathf.Max(playerCamera.fieldOfView, target);
        }

        if (damageFlashOverlay == null) return;

        if (damageFlashTimer > 0f)
        {
            damageFlashTimer -= Time.deltaTime;
            float normalized = Mathf.Clamp01(damageFlashTimer / Mathf.Max(0.01f, damageFlashDuration));
            Color color = damageFlashColor;
            color.a *= normalized;
            damageFlashOverlay.color = color;
            damageFlashOverlay.enabled = color.a > 0.001f;
            return;
        }

        if (!damageFlashOverlay.enabled) return;
        damageFlashOverlay.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, 0f);
        damageFlashOverlay.enabled = false;
    }

    private void EnsureDamageOverlay()
    {
        if (damageFlashOverlay != null) return;

        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("DamageFlashOverlay");
        if (existing != null)
        {
            damageFlashOverlay = existing.GetComponent<Image>();
            return;
        }

        GameObject overlay = new GameObject("DamageFlashOverlay");
        overlay.transform.SetParent(canvas.transform, false);
        RectTransform rect = overlay.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        damageFlashOverlay = overlay.AddComponent<Image>();
        damageFlashOverlay.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, 0f);
        damageFlashOverlay.raycastTarget = false;
        damageFlashOverlay.enabled = false;
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
        if (speedLines != null || cameraTransform == null) return;

        GameObject go = new GameObject("SpeedLines");
        go.transform.SetParent(cameraTransform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        speedLines = go.AddComponent<ParticleSystem>();
        var main = speedLines.main;
        main.loop = true;
        main.startLifetime = 0.18f;
        main.startSpeed = 13f;
        main.startSize = 0.018f;
        main.maxParticles = 140;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startColor = new Color(0.45f, 0.75f, 0.85f, 0.18f);

        var emission = speedLines.emission;
        emission.rateOverTime = 0f;

        var shape = speedLines.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 38f;
        shape.radius = 0.9f;
        shape.position = new Vector3(0f, 0f, 0.55f);

        var renderer = speedLines.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 4.0f;
        renderer.velocityScale = 0.08f;
        renderer.material = CreateSpeedLineMaterial();
    }

    private Material CreateSpeedLineMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader) { name = "SpeedLines_URP_Unlit" };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", new Color(0.45f, 0.75f, 0.85f, 0.18f));
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", new Color(0.45f, 0.75f, 0.85f, 0.18f));
        return material;
    }

    private void UpdateSpeedLines()
    {
        if (speedLines == null || controller == null) return;

        Vector3 groundVelocity = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
        float speed01 = Mathf.InverseLerp(overdriveSpeedThreshold, maxSpeedLimit, groundVelocity.magnitude);
        var emission = speedLines.emission;
        emission.rateOverTime = Mathf.Lerp(0f, 120f, speed01);

        if (speed01 > 0.05f && !speedLines.isPlaying)
            speedLines.Play();
        else if (speed01 <= 0.01f && speedLines.isPlaying)
            speedLines.Stop();
    }

    private void EnsureCrosshair()
    {
        if (crosshair == null)
        {
            Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
            if (canvas == null) return;

            crosshair = new GameObject("RuntimeCrosshair");
            crosshair.transform.SetParent(canvas.transform, false);
            crosshairRect = crosshair.AddComponent<RectTransform>();
            crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
            crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairRect.pivot = new Vector2(0.5f, 0.5f);
            crosshairRect.sizeDelta = new Vector2(84f, 84f);

            BuildCrosshairSegments(crosshair.transform);
            return;
        }

        if (crosshairRect == null)
            crosshairRect = crosshair.GetComponent<RectTransform>();

        if (crosshairRect == null)
            return;

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

        float speed = controller != null ? new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude : 0f;
        float speed01 = Mathf.InverseLerp(0f, maxSpeedLimit, speed);
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

        float gap = 12f + speed01 * 8f + fire01 * 4.5f + hit01 * 5f + kill01 * 9f;
        float length = 11f + speed01 * 3.5f + fire01 * 1.6f + kill01 * 3f;
        float thickness = 3f + fire01 * 0.7f + hit01 * 1.2f + kill01 * 1.4f;
        float scale = 1f + speed01 * 0.05f + fire01 * 0.05f + hit01 * 0.08f + kill01 * 0.12f;
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
        Vector2 mouseDelta = UnityEngine.InputSystem.Mouse.current.delta.ReadValue();

        // Removed the "SmoothDamp" because it often adds "Input Lag", making it feel floaty/slippery.
        // We divide sensitivity by 100 here so that 100 in the inspector = 1.0 multiplier.
        float multiplier = mouseSensitivity / 100f;
        float mouseX = mouseDelta.x * multiplier;
        float mouseY = mouseDelta.y * multiplier;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }
}
