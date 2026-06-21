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

    [Header("Vitals")]
    public float maxHealth = 100f;
    public float damageInvulnerabilityTime = 0.2f;
    [Range(0.1f, 1f)] public float respawnHealthPercent = 0.75f;

    public float currentHealth { get; private set; }
    public int currency { get; private set; }
    private float damageInvulnerabilityTimer;

    [Header("Movement (Core)")]
    public float moveSpeed = 10.8f;
    public float groundAcceleration = 24f;
    public float groundDeceleration = 38f;
    public float airAcceleration = 15f;
    public float gravity = -29f;
    public float jumpHeight = 2.85f;
    public int maxJumps = 2; 
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;

    [Header("Movement (Dash)")]
    public float dashForce = 24f;
    public float dashCooldown = 0.78f;
    public float dashDuration = 0.18f;
    public float dashTargetSpeed = 23f;
    public float dashExitSpeed = 7f;
    public float launchCarrySpeedLimit = 22f;
    [Range(0.2f, 1f)] public float dashNoInputExitMultiplier = 0.62f;
    [Range(1, 5)] public int maxDashCharges = 3;

    [Header("Movement (Slide & Slam)")]
    public float slideBaseSpeed = 16.2f;
    public float slideFriction = 0.34f;
    public float fallSpeedToSlideBoost = 0.58f; // Hitting the ground hard speeds up your slide
    public float maxSlideStartSpeed = 18.8f;
    public float maxSlideJumpCarrySpeed = 23f;
    public float slideJumpSpeedMultiplier = 1.24f;
    public float slideJumpVerticalMultiplier = 1.08f;
    public float slideHeight = 1f;
    public float slideCooldown = 0.45f;
    public float slamSpeed = 40f; // How fast you plummet downwards
    public float postSlamSlideLockout = 0.25f;
    public float slamReleaseDelay = 0.12f;
    [Min(0f)] public float slamJumpWindow = 0.32f;
    [Min(1f)] public float slamJumpVerticalBoost = 1.3f;
    [Min(0f)] public float slamJumpChainWindow = 1.35f;
    [Range(0f, 0.12f)] public float slamJumpChainHeightBonus = 0.04f;
    [Range(0, 8)] public int maxSlamJumpChain = 5;
    public float slideCameraDrop = 0.42f;
    public float slideGroundGrace = 0.12f;
    public float slideHoldSpeed = 16.2f;
    public float slideSteerStrength = 18f;
    public float slideMinDuration = 0.7f;
    public float slideMinHoldSpeed = 13.5f;
    [Header("Movement (Slide Jump Chain)")]
    public float slideJumpChainWindow = 2f;
    [Range(0f, 0.2f)] public float slideJumpChainBonus = 0.08f;
    [Min(0f)] public float heldJumpLandingDelay = 0.14f;
    public float airTurnDamping = 1.8f;
    public float airNoInputBrake = 0.15f;
    [Range(0.05f, 0.45f)] public float airControlImpulseScale = 0.18f;
    public float groundReleaseBrakeMultiplier = 0.72f;
    private float defaultHeight;
    private Vector3 defaultControllerCenter;

    [Header("Movement (Limits)")]
    public float maxSpeedLimit = 26f;
    public float groundedStopSpeed = 0.2f;

    [Header("Movement (Wall)")]
    public float wallSlideMaxFallSpeed = 9.5f;
    public float wallSlideGravityScale = 0.34f;
    public float wallRunGravityScale = 0.18f;
    public float wallRunMinSpeed = 9f;
    public float wallRunTargetSpeed = 16.5f;
    public float wallRunAcceleration = 34f;
    public float wallRunSidePull = 5f;
    public float wallRunMinInputDot = 0.1f;
    public float wallRunDuration = 1.05f;
    public float wallJumpAwayForce = 10.5f;
    public float wallJumpUpForce = 9.2f;
    public float wallDetachCooldown = 0.18f;
    public float wallRunCameraTilt = 7.5f;
    public float wallTransitionDuration = 0.14f;
    [Range(0f, 1f)] public float wallReleaseCarryPreservation = 0.82f;
    [Range(0f, 1f)] public float wallRunCarryPreservation = 0.88f;

    [Header("Movement (Grapple)")]
    public float grappleRange = 46f;
    [Min(0.5f)] public float grappleMinRopeLength = 0.9f;
    [Min(0f)] public float grappleRopeSlack = 0.12f;
    [Range(0.5f, 1f)] public float grappleConstraintElasticity = 0.86f;
    [Min(0.1f)] public float grappleConstraintMaxCorrection = 0.9f;
    [Min(0f)] public float grapplePullAcceleration = 105f;
    [Min(0f)] public float grappleInitialPullSpeed = 10f;
    [Min(0f)] public float grapplePullSpeed = 30f;
    [Min(0f)] public float grapplePullRampDuration = 0.28f;
    [Min(0f)] public float grappleReelSpeed = 16f;
    [Min(0.5f)] public float grappleAutoReleaseDistance = 1.15f;
    public float grappleAirSteer = 9f;
    [Range(0f, 1f)] public float grappleGravityScale = 0.78f;
    public float grappleJumpBoost = 5.2f;
    public float grappleJumpForwardBoost = 2.8f;
    public float grappleJumpCooldown = 0.24f;
    public float grappleHookSpeed = 88f;
    public float grappleHookRadius = 0.11f;
    public float grappleCooldown = 0.2f;
    [Min(0f)] public float grappleTargetGraceDuration = 0.1f;
    public float grappleLatchBoost = 11.5f;
    public float grappleLatchUpBoost = 1.8f;
    public float grappleReleaseForwardBoost = 4.2f;
    public float grappleReleaseUpBoost = 1.2f;
    public float grappleLatchShake = 0.12f;
    [Range(0f, 1f)] public float grappleCarryPreservation = 0.92f;
    public LayerMask grappleSurfaceMask = ~0;
    public Color grappleLineColor = new Color(0.08f, 0.09f, 0.11f, 0.96f);
    public Color grappleReticleColor = new Color(0.9f, 0.96f, 1f, 0.92f);
    [Min(0f)] public float grappleAssistWorldRadius = 0.32f;
    public float grappleLedgeProbeHeight = 1.6f;
    [Range(0f, 1f)] public float grappleLedgePreference = 0.24f;
    [Range(0f, 1f)] public float grappleVerticalPreference = 0.18f;
    [Min(0f)] public float grappleReticleFadeSpeed = 8f;
    [Min(0f)] public float grappleReticleScaleBoost = 0.18f;

    [Header("Movement (Grapple Visuals)")]
    public float grappleLaunchVisualDuration = 0.08f;
    public float grappleHandRecoverSpeed = 10f;
    public Color grappleViewBodyColor = new Color(0.08f, 0.1f, 0.14f, 1f);
    public Color grappleViewAccentColor = new Color(0.7f, 0.92f, 1f, 1f);
    public Color grappleAnchorColor = new Color(0.86f, 0.97f, 1f, 0.96f);
    public Color grappleAnchorPulseColor = new Color(0.4f, 0.95f, 1f, 0.8f);
    public float grappleLineWidthActive = 0.028f;
    public float grappleLineWidthIdle = 0.02f;

    [Header("FX & Polish")]
    public Camera playerCamera;
    public ParticleSystem slideDust; // Drag a particle system here!
    public ParticleSystem speedLines;
    public float overdriveSpeedThreshold = 24f;
    public float overdriveFovBonus = 10f;
    public float overdriveShakeAmount = 0.045f;
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
    private float slideTimer;
    private float slamJumpTimer;
    private float slamJumpChainTimer;
    private int slamJumpChainCount;
    private float slideJumpChainTimer;
    private int slideJumpChain;
    private float groundedHoldTimer;
    private float moveSpeedBonus;
    private float dashForceBonus;
    private float jumpHeightBonus;
    private float maxHealthBonus;
    private float wallRunTimer;
    private float wallDetachTimer;
    private float wallMovementBlend;
    private float wallReleaseBlendTimer;

    [Header("Damage Feedback")]
    public Color damageFlashColor = new Color(0.9f, 0.12f, 0.08f, 0.22f);
    public float damageFlashDuration = 0.26f;
    [Range(0f, 0.55f)] public float lowHealthVignetteAlpha = 0.34f;
    public float weaponKickDuration = 0.12f;

    [Header("Look")]
    public float mouseSensitivity = 100f;
    [Min(0f)] public float cameraTiltSmoothSpeed = 16f;
    [Min(0.001f)] public float cameraPositionSmoothTime = 0.045f;
    [Min(0f)] public float cameraFovSmoothSpeed = 10f;
    [Min(0f)] public float impactShakeFrequency = 42f;
    [Min(0f)] public float cameraShakeSmoothSpeed = 18f;
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
    private int dashCharges;
    private bool isSliding;
    private bool isSlamming;
    private bool isWallSliding;
    private bool isWallRunning;
    private bool slideRequiresRelease;
    private Vector3 momentum;
    private Vector3 lastSideHitNormal;
    private float lastSideHitTime;
    private Vector3 activeWallNormal;
    private Vector3 activeWallDirection;
    private Vector3 wallReleaseNormal;
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
    private float weaponImpactShakeTimer;
    private float weaponImpactShakeAmount;
    private float crosshairFireTimer;
    private Image damageFlashOverlay;
    private Sprite damageVignetteSprite;
    private RectTransform crosshairRect;
    private RectTransform grappleReticleRect;
    private Image grappleReticleImage;
    private readonly RectTransform[] crosshairSegmentRects = new RectTransform[4];
    private readonly Image[] crosshairSegmentImages = new Image[4];
    private Material cachedWorldFxMaterial;
    private Vector2 moveInputRaw;
    private Vector3 cameraLocalPositionVelocity;
    private float currentCameraTilt;
    private Vector3 currentCameraShakeOffset;
    private ParticleSystem runtimeSlideGroundFx;
    private ParticleSystem runtimeSlideAirFx;
    private LineRenderer grappleLine;
    private Material cachedGrappleLineMaterial;
    private Sprite cachedGrappleReticleSprite;
    private Sprite cachedGrappleAnchorSprite;
    private float grappleCooldownTimer;
    private bool grappleHeldLastFrame;
    private float activeGrappleRopeLength;
    private float grappleLaunchVisualTimer;
    private float grappleVisualPulse;
    private float grappleActiveTimer;
    private float grappleJumpCooldownTimer;
    private float grappleTargetGraceTimer;
    private float grappleReticleFade;
    private GrappleState grappleState;
    private GrappleTarget aimedGrappleTarget;
    private GrappleTarget launchedGrappleTarget;
    private GrappleTarget activeGrappleTarget;
    private GrappleHookProjectile activeGrappleHook;
    private GrappleHookProjectile pooledGrappleHook;
#if UNITY_EDITOR
    private bool debugGrappleHeldForTest;
#endif
    private Transform grappleViewRoot;
    private Transform grappleHandPivot;
    private Transform grappleLauncherMuzzle;
    private Transform grappleLauncherBody;
    private Transform grappleLauncherRail;
    private Transform grappleAnchorVisual;
    private SpriteRenderer grappleAnchorRenderer;
    private SpriteRenderer grappleAnchorPulseRenderer;
    private Material cachedGrappleViewBodyMaterial;
    private Material cachedGrappleViewAccentMaterial;
    private Material cachedGrappleAnchorMaterial;

    private float CurrentMoveSpeed => moveSpeed + moveSpeedBonus;
    private float CurrentDashForce => dashForce + dashForceBonus;
    private float CurrentJumpHeight => jumpHeight + jumpHeightBonus;
    private float CurrentMaxHealth => maxHealth + maxHealthBonus;
    public float EffectiveMaxHealth => CurrentMaxHealth;
    public float Health01 => CurrentMaxHealth <= 0.01f ? 0f : Mathf.Clamp01(currentHealth / CurrentMaxHealth);
    public float PlanarSpeed => new Vector3(momentum.x, 0f, momentum.z).magnitude;
    public Vector3 WorldVelocity => momentum + Vector3.up * velocity.y;
    public int SlideJumpChain => slideJumpChain;
    public bool DebugIsSliding => isSliding;
    public bool DebugIsSlamming => isSlamming;
    public float DebugDashTimer => dashTimer;
    public Vector3 DebugMomentum => momentum;
    public Vector3 DebugDashVelocity => dashVelocity;
    public int DashCharges => Mathf.Clamp(dashCharges, 0, MaxDashCharges);
    public int MaxDashCharges => Mathf.Clamp(maxDashCharges, 1, 5);
    public float DashRecharge01 => DashCharges >= MaxDashCharges || dashCooldown <= 0.01f
        ? 1f
        : 1f - Mathf.Clamp01(dashCooldownTimer / dashCooldown);
    public bool IsGrappling => activeGrappleTarget.isValid;
    public bool IsGrappleHookInFlight => activeGrappleHook != null && !activeGrappleHook.IsLatched;

    private const string MouseSensitivityPrefKey = "project_structure.mouse_sensitivity";
    private const string BaseFovPrefKey = "project_structure.base_fov";
    private const string MasterVolumePrefKey = "project_structure.master_volume";
    private const int GrappleLineSegments = 12;
    private readonly RaycastHit[] grappleAimHits = new RaycastHit[12];

    private enum GrappleState
    {
        Idle,
        Firing,
        Latched,
        Retracting,
        Cooldown
    }

    private struct GrappleTarget
    {
        public bool isValid;
        public bool isAssisted;
        public bool isLedgeSnap;
        public bool pullsPlayer;
        public Vector3 point;
        public Vector3 normal;
        public Collider collider;
        public Transform anchorTransform;
        public Vector3 localPoint;
        public IGrappleMassTarget massTarget;
    }

    private struct MovementInputState
    {
        public Vector2 moveAxes;
        public Vector3 moveDirection;
        public bool jumpPressed;
        public bool jumpHeld;
        public bool dashPressed;
        public bool slidePressed;
        public bool slideHeld;
        public bool grappleHeld;
        public bool grapplePressed;
    }

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
        ProjectStructureBindings.EnsureLoaded();
        controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            defaultHeight = controller.height;
            defaultControllerCenter = controller.center;
        }
        ApplyMovementTuningDefaults();
        dashCharges = MaxDashCharges;
        wallRunTimer = wallRunDuration;
        Cursor.lockState = CursorLockMode.Locked;
        currentHealth = CurrentMaxHealth;
        currency = 0;

        EnsureCameraReferences();
        EnsureSpeedLines();
        EnsureSlideParticles();
        EnsureGrappleViewModel();
        if (playerCamera != null) baseFOV = playerCamera.fieldOfView;
        if (cameraTransform != null) baseCameraLocalPos = cameraTransform.localPosition;
        currentCameraTilt = 0f;
        currentCameraShakeOffset = Vector3.zero;
        lastSafePosition = transform.position;
        EnsureVitalsHud();
        EnsureCrosshair();
        EnsureDamageOverlay();
        LoadSettings();
        if (playerCamera != null)
            playerCamera.fieldOfView = baseFOV;
        if (cameraTransform != null)
            cameraTransform.localPosition = baseCameraLocalPos;
        if (speedLines != null)
            speedLines.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        StopSlideParticles(true);
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
            StopGrapple();
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
        float carryLimit = slideJumpChain > 0
            ? GetActiveSpeedLimit()
            : Mathf.Clamp(launchCarrySpeedLimit + moveSpeedBonus, CurrentMoveSpeed + 4f, maxSpeedLimit);
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
        {
            controller.height = defaultHeight;
            controller.center = defaultControllerCenter;
        }

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

    public void TriggerGameOverDeath()
    {
        if (isDead)
            return;

        bool previousRespawnMode = respawnOnDeath;
        respawnOnDeath = false;
        damageInvulnerabilityTimer = 0f;
        currentHealth = 0f;
        RefreshVitalsUI();
        HandleDeath();
        respawnOnDeath = previousRespawnMode;
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

        if (ProjectStructureBindings.WasPressedThisFrame(ProjectStructureAction.Interact))
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
            return $"<color=#9BEFFF><b>[{ProjectStructureBindings.GetDisplayString(ProjectStructureAction.Interact)}]</b></color> <color=#EAFBFF>{action}</color>";
        }

        if (trimmed.StartsWith("Press E ", System.StringComparison.OrdinalIgnoreCase))
        {
            string action = trimmed.Substring("Press E ".Length).Trim();
            return $"<color=#9BEFFF><b>[{ProjectStructureBindings.GetDisplayString(ProjectStructureAction.Interact)}]</b></color> <color=#EAFBFF>{action}</color>";
        }

        if (ShouldShowInteractionKey(trimmed))
            return $"<color=#9BEFFF><b>[{ProjectStructureBindings.GetDisplayString(ProjectStructureAction.Interact)}]</b></color> <color=#EAFBFF>{trimmed}</color>";

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
        float fallSpeed = lastFrameVelocityY;
        UpdateGroundStateAndLanding(wasGrounded, fallSpeed);
        float previousDashTimer = dashTimer;
        UpdateMovementTimers(Time.deltaTime);
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

        MovementInputState input = CollectMovementInputState();
        aimedGrappleTarget = FindAimedGrappleTarget();

        if (previousDashTimer > 0f && dashTimer <= 0f)
            FinishDash(input.moveDirection);

        if (activeGrappleTarget.isValid)
        {
            if (IsGrappleTargetStillValid(activeGrappleTarget))
                grappleTargetGraceTimer = grappleTargetGraceDuration;
            else
                grappleTargetGraceTimer = Mathf.Max(0f, grappleTargetGraceTimer - Time.deltaTime);

            if (grappleTargetGraceTimer <= 0f)
                BeginGrappleReleaseRetract(activeGrappleTarget, true);
            else if (!input.grappleHeld)
                ReleaseGrapplePreservingMomentum();
        }
        if (activeGrappleHook != null && grappleState == GrappleState.Firing && !input.grappleHeld)
            CancelGrappleHook();

        if (input.grapplePressed && !activeGrappleTarget.isValid && activeGrappleHook == null)
            TryStartGrapple();

        if (input.jumpPressed || (input.jumpHeld && isGrounded && groundedHoldTimer >= heldJumpLandingDelay))
            jumpBufferTimer = jumpBufferTime;

        if (input.dashPressed && dashTimer <= 0f && dashCharges > 0 && !activeGrappleTarget.isValid)
        {
            dashCharges = Mathf.Max(0, dashCharges - 1);
            if (dashCharges < MaxDashCharges && dashCooldownTimer <= 0f)
                dashCooldownTimer = dashCooldown;
            Vector3 dashDir = input.moveDirection.magnitude > 0.1f ? input.moveDirection : transform.forward;
            float dashSpeed = Mathf.Clamp(
                Mathf.Max(dashTargetSpeed, CurrentDashForce) + dashForceBonus * 0.15f,
                CurrentMoveSpeed * 2.1f,
                CurrentMoveSpeed * 2.55f);
            dashVelocity = dashDir * dashSpeed;
            momentum = dashVelocity;
            dashTimer = Mathf.Max(dashDuration, 0.2f);
            ExitSlide(false);
            isSlamming = false;
            slideRequiresRelease = input.slideHeld;
            velocity.y = isGrounded ? Mathf.Min(velocity.y, 0f) : Mathf.Clamp(velocity.y, -4f, 4f);
            SpawnDashBurst();
        }
        UpdateSlideAndSlamState(input, fallSpeed);

        SolveHorizontalVelocity(input, Time.deltaTime);
        ProcessBufferedJump(input);
        ApplyVerticalVelocity(Time.deltaTime);
        ResolveMovementCollision(input.moveDirection);

        UpdateCameraPresentation(Time.deltaTime);

        UpdateSpeedLines();

        UpdateSlideParticles();
        UpdateGrappleViewModel();
        UpdateGrappleVisuals();
    }

    private GrappleTarget FindAimedGrappleTarget()
    {
        GrappleTarget bestTarget = default;
        if (cameraTransform == null)
            return bestTarget;

        Vector3 origin = cameraTransform.position;
        Vector3 forward = cameraTransform.forward;

        if (Physics.Raycast(origin, forward, out RaycastHit directHit, grappleRange, grappleSurfaceMask, QueryTriggerInteraction.Ignore) &&
            TryBuildGrappleTarget(directHit, out bestTarget))
        {
            return bestTarget;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0.01f, grappleAssistWorldRadius),
            forward,
            grappleAimHits,
            grappleRange,
            grappleSurfaceMask,
            QueryTriggerInteraction.Ignore);
        float bestScore = float.MinValue;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit sampleHit = grappleAimHits[i];
            if (!TryBuildGrappleTarget(sampleHit, out GrappleTarget sampleTarget))
                continue;

            sampleTarget.isAssisted = true;
            float score = ScoreGrappleTarget(forward, sampleTarget);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = sampleTarget;
            }
        }

        return bestTarget;
    }

    private bool TryStartGrapple()
    {
        if (grappleCooldownTimer > 0f || grappleState == GrappleState.Firing || grappleState == GrappleState.Retracting)
            return false;
        if (cameraTransform == null)
            return false;

        Vector3 launchOrigin = cameraTransform.position + cameraTransform.forward * Mathf.Max(0.04f, grappleHookRadius * 0.55f);
        Vector3 launchDirection = aimedGrappleTarget.isValid
            ? (GetGrappleAnchorPoint(aimedGrappleTarget) - launchOrigin).normalized
            : cameraTransform.forward;
        launchedGrappleTarget = aimedGrappleTarget;
        activeGrappleTarget = default;
        activeGrappleRopeLength = grappleRange;
        grappleLaunchVisualTimer = grappleLaunchVisualDuration;
        grappleVisualPulse = 1f;
        grappleActiveTimer = 0f;
        grappleJumpCooldownTimer = 0f;
        grappleTargetGraceTimer = grappleTargetGraceDuration;
        grappleState = GrappleState.Firing;
        SpawnGrappleHook(launchOrigin, launchDirection);
        EnsureGrappleLine();
        if (grappleLine != null)
            grappleLine.enabled = true;
        return true;
    }

    private MovementInputState CollectMovementInputState()
    {
        bool grappleHeld = ProjectStructureBindings.IsPressed(ProjectStructureAction.Grapple);
#if UNITY_EDITOR
        grappleHeld |= debugGrappleHeldForTest;
#endif
        MovementInputState input = new MovementInputState
        {
            moveAxes = ProjectStructureBindings.ReadMovementVector(),
            jumpPressed = ProjectStructureBindings.WasPressedThisFrame(ProjectStructureAction.Jump),
            jumpHeld = ProjectStructureBindings.IsPressed(ProjectStructureAction.Jump),
            dashPressed = ProjectStructureBindings.WasPressedThisFrame(ProjectStructureAction.Dash),
            slidePressed = ProjectStructureBindings.WasPressedThisFrame(ProjectStructureAction.Slide),
            slideHeld = ProjectStructureBindings.IsPressed(ProjectStructureAction.Slide),
            grappleHeld = grappleHeld
        };

        moveInputRaw = input.moveAxes;
        input.moveDirection = transform.right * input.moveAxes.x + transform.forward * input.moveAxes.y;
        input.grapplePressed = input.grappleHeld && !grappleHeldLastFrame;
        grappleHeldLastFrame = input.grappleHeld;
        if (jumpBufferTimer > 0f)
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
        return input;
    }

    private void UpdateGroundStateAndLanding(bool wasGrounded, float fallSpeed)
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

        coyoteTimer = isGrounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - Time.deltaTime);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            jumpsRemaining = maxJumps;
            abyssRecoveredThisAirborneState = false;
            wallRunTimer = wallRunDuration;
            wallDetachTimer = 0f;
            ClearWallState();
        }

        bool landedThisFrame = isGrounded && !wasGrounded;
        groundedHoldTimer = isGrounded
            ? (landedThisFrame ? 0f : groundedHoldTimer + Time.deltaTime)
            : 0f;
        if (landedThisFrame && isSlamming)
        {
            isSlamming = false;
            slideRequiresRelease = true;
            slideLockoutTimer = Mathf.Max(slideLockoutTimer, slamReleaseDelay);
            slamJumpTimer = slamJumpWindow;
            slamJumpChainTimer = slamJumpChainWindow;
            momentum = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(momentum, Vector3.up), Mathf.Max(CurrentMoveSpeed * 0.95f, slideBaseSpeed * 0.75f));
        }
    }

    private void UpdateMovementTimers(float deltaTime)
    {
        RechargeDashCharges(deltaTime);
        if (grappleCooldownTimer > 0f)
            grappleCooldownTimer = Mathf.Max(0f, grappleCooldownTimer - deltaTime);
        if (grappleCooldownTimer <= 0f && !activeGrappleTarget.isValid && activeGrappleHook == null && grappleState == GrappleState.Cooldown)
            grappleState = GrappleState.Idle;
        if (grappleJumpCooldownTimer > 0f)
            grappleJumpCooldownTimer = Mathf.Max(0f, grappleJumpCooldownTimer - deltaTime);
        if (dashTimer > 0f)
            dashTimer = Mathf.Max(0f, dashTimer - deltaTime);
        if (slideLockoutTimer > 0f)
            slideLockoutTimer -= deltaTime;
        if (slideCooldownTimer > 0f)
            slideCooldownTimer -= deltaTime;
        if (slideJumpChainTimer > 0f)
            slideJumpChainTimer = Mathf.Max(0f, slideJumpChainTimer - deltaTime);
        else
            slideJumpChain = 0;
        if (slamJumpTimer > 0f)
            slamJumpTimer = Mathf.Max(0f, slamJumpTimer - deltaTime);
        if (slamJumpChainTimer > 0f)
            slamJumpChainTimer = Mathf.Max(0f, slamJumpChainTimer - deltaTime);
        else
            slamJumpChainCount = 0;
        if (isSliding)
            slideGroundGraceTimer = isGrounded ? slideGroundGrace : Mathf.Max(0f, slideGroundGraceTimer - deltaTime);
        if (isWallRunning && wallRunTimer > 0f)
            wallRunTimer = Mathf.Max(0f, wallRunTimer - deltaTime);
        if (wallDetachTimer > 0f)
            wallDetachTimer = Mathf.Max(0f, wallDetachTimer - deltaTime);
        if (wallReleaseBlendTimer > 0f)
            wallReleaseBlendTimer = Mathf.Max(0f, wallReleaseBlendTimer - deltaTime);
    }

    private void UpdateSlideAndSlamState(MovementInputState input, float fallSpeed)
    {
        if (!input.slideHeld)
            slideRequiresRelease = false;

        if (input.slidePressed && !isGrounded && !isSlamming && !slideRequiresRelease && !activeGrappleTarget.isValid)
        {
            ExitSlide(false);
            isSlamming = true;
            velocity.y = -slamSpeed;
            momentum = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(momentum, Vector3.up), Mathf.Max(CurrentMoveSpeed, dashExitSpeed));
        }

        if (input.slideHeld && isGrounded && !isSliding && !slideRequiresRelease && slideLockoutTimer <= 0f && slideCooldownTimer <= 0f && !activeGrappleTarget.isValid)
        {
            isSliding = true;
            controller.height = slideHeight;
            controller.center = defaultControllerCenter + Vector3.down * Mathf.Max(0f, (defaultHeight - slideHeight) * 0.5f);
            slideGroundGraceTimer = slideGroundGrace;
            slideTimer = slideMinDuration;

            float fallBoost = 0f;
            if (fallSpeed < -10f && !isSlamming)
                fallBoost = Mathf.Abs(fallSpeed) * fallSpeedToSlideBoost;

            float currentSpeed = momentum.magnitude;
            float slideLimit = slideJumpChain > 0 ? GetActiveSpeedLimit() : Mathf.Max(maxSlideStartSpeed, slideBaseSpeed);
            float newSpeed = Mathf.Clamp(Mathf.Max(slideBaseSpeed, currentSpeed + fallBoost), slideBaseSpeed, slideLimit);
            Vector3 slideDir = input.moveDirection.magnitude > 0.1f ? input.moveDirection.normalized : transform.forward;
            momentum = slideDir * newSpeed;
            velocity.y = Mathf.Max(velocity.y, 0f);
        }
        else if ((!input.slideHeld || (!isGrounded && slideGroundGraceTimer <= 0f)) && isSliding)
        {
            if (!input.slideHeld)
            {
                ExitSlide(true);
            }
            else if (!isGrounded)
            {
                ExitSlide(false);
                slideRequiresRelease = true;
            }
        }
    }

    private void StopGrapple()
    {
        bool hadActiveGrapple = activeGrappleTarget.isValid || activeGrappleHook != null;
        activeGrappleTarget = default;
        activeGrappleRopeLength = 0f;
        if (hadActiveGrapple)
            grappleCooldownTimer = Mathf.Max(grappleCooldownTimer, grappleCooldown);
        grappleVisualPulse = 0f;
        grappleActiveTimer = 0f;
        grappleJumpCooldownTimer = 0f;
        grappleTargetGraceTimer = 0f;
        launchedGrappleTarget = default;
        DestroyActiveGrappleHook();
        grappleState = grappleCooldownTimer > 0f ? GrappleState.Cooldown : GrappleState.Idle;

        if (grappleLine != null)
            grappleLine.enabled = false;
        if (grappleAnchorVisual != null)
            grappleAnchorVisual.gameObject.SetActive(false);
    }

    private void ReleaseGrapplePreservingMomentum()
    {
        BeginGrappleReleaseRetract(activeGrappleTarget, true);
    }

    private void BeginGrappleReleaseRetract(GrappleTarget target, bool applyCooldown)
    {
        if (!target.isValid)
        {
            StopGrapple();
            return;
        }

        Vector3 anchorPoint = GetGrappleAnchorPoint(target);
        Vector3 bounceDirection = Vector3.Reflect(-transform.forward, target.normal);
        if (bounceDirection.sqrMagnitude <= 0.0001f)
            bounceDirection = target.normal.sqrMagnitude > 0.0001f ? target.normal : -transform.forward;

        if (target.pullsPlayer)
        {
            Vector3 releaseDir = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
                : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (releaseDir.sqrMagnitude <= 0.0001f)
                releaseDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            momentum += releaseDir * grappleReleaseForwardBoost;
            velocity.y = Mathf.Max(velocity.y, grappleReleaseUpBoost);
        }

        activeGrappleTarget = default;
        activeGrappleRopeLength = 0f;
        grappleActiveTimer = 0f;
        grappleJumpCooldownTimer = 0f;
        grappleTargetGraceTimer = 0f;
        launchedGrappleTarget = default;
        if (applyCooldown)
            grappleCooldownTimer = Mathf.Max(grappleCooldownTimer, grappleCooldown);
        grappleState = GrappleState.Retracting;
        grappleVisualPulse = 0.8f;

        EnsureRetractHookVisual(anchorPoint, bounceDirection);
        if (grappleAnchorVisual != null)
            grappleAnchorVisual.gameObject.SetActive(false);
    }

    private void EnsureRetractHookVisual(Vector3 anchorPoint, Vector3 bounceDirection)
    {
        if (pooledGrappleHook == null)
        {
            GameObject hookObject = new GameObject("GrappleHookProjectile");
            pooledGrappleHook = hookObject.AddComponent<GrappleHookProjectile>();
        }

        if (activeGrappleHook == null)
            activeGrappleHook = pooledGrappleHook;

        activeGrappleHook.transform.SetParent(null, true);
        activeGrappleHook.gameObject.SetActive(true);
        activeGrappleHook.Initialize(this, anchorPoint, -bounceDirection, grappleHookSpeed, grappleHookRadius, grappleRange, grappleSurfaceMask, grappleViewBodyColor, grappleViewAccentColor);
        activeGrappleHook.BeginRetract(anchorPoint, bounceDirection);
        if (grappleLine != null)
            grappleLine.enabled = true;
    }

    private bool IsGrappleTargetStillValid(GrappleTarget target)
    {
        if (!target.isValid)
            return false;

        Vector3 origin = transform.position + Vector3.up * 0.9f;
        Vector3 anchorPoint = GetGrappleAnchorPoint(target);
        Vector3 toTarget = anchorPoint - origin;
        float distance = toTarget.magnitude;
        if (distance > grappleRange + 1.5f)
            return false;

        if (target.collider == null)
            return false;

        if (distance <= Mathf.Max(grappleAutoReleaseDistance + 1.2f, grappleMinRopeLength + 0.9f))
            return true;

        if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, distance + 0.2f, grappleSurfaceMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == target.collider)
                return true;

            if (target.anchorTransform != null && hit.collider.transform.IsChildOf(target.anchorTransform))
                return true;

            if (Vector3.Distance(hit.point, anchorPoint) <= 0.55f)
                return true;

            return false;
        }

        return true;
    }

    private void UpdateGrappleMotion(Vector3 inputDir, float deltaTime)
    {
        if (!activeGrappleTarget.isValid)
            return;

        if (!activeGrappleTarget.pullsPlayer)
        {
            UpdateLightTargetGrappleMotion(deltaTime);
            return;
        }

        Vector3 anchorPoint = GetGrappleAnchorPoint(activeGrappleTarget);
        Vector3 attachmentPoint = transform.position + Vector3.up * 0.9f;
        Vector3 toAnchor = anchorPoint - attachmentPoint;
        float distance = toAnchor.magnitude;
        if (distance <= 0.001f)
        {
            ReleaseGrapplePreservingMomentum();
            return;
        }

        grappleActiveTimer += deltaTime;
        if (distance <= Mathf.Max(grappleMinRopeLength, grappleAutoReleaseDistance))
        {
            ReleaseGrapplePreservingMomentum();
            return;
        }

        activeGrappleRopeLength = Mathf.MoveTowards(
            activeGrappleRopeLength,
            grappleMinRopeLength,
            grappleReelSpeed * deltaTime);
        Vector3 pullDir = toAnchor / Mathf.Max(0.001f, distance);
        Vector3 currentVelocity = momentum + Vector3.up * velocity.y;
        float preservedSpeed = currentVelocity.magnitude;
        if (jumpBufferTimer > 0f && grappleJumpCooldownTimer <= 0f)
        {
            Vector3 jumpDirection = inputDir.sqrMagnitude > 0.0001f ? inputDir.normalized : transform.forward;
            Vector3 tangentDirection = Vector3.ProjectOnPlane(jumpDirection, pullDir).normalized;
            currentVelocity += tangentDirection * grappleJumpForwardBoost;
            currentVelocity.y = Mathf.Max(currentVelocity.y, grappleJumpBoost);
            grappleJumpCooldownTimer = grappleJumpCooldown;
            jumpBufferTimer = 0f;
        }

        currentVelocity = ApplyGrappleAirSteer(currentVelocity, inputDir, deltaTime);

        float tautLength = Mathf.Max(grappleMinRopeLength, activeGrappleRopeLength - grappleRopeSlack);
        bool ropeTaut = distance >= tautLength;
        if (ropeTaut)
        {
            float radialTowardAnchor = Vector3.Dot(currentVelocity, pullDir);
            if (radialTowardAnchor < 0f)
            {
                float cancelledOutwardSpeed = -radialTowardAnchor * grappleConstraintElasticity;
                currentVelocity += pullDir * cancelledOutwardSpeed;
                radialTowardAnchor += cancelledOutwardSpeed;
            }

            if (grapplePullSpeed > 0f)
            {
                float pullRamp01 = grapplePullRampDuration <= 0.001f
                    ? 1f
                    : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(grappleActiveTimer / grapplePullRampDuration));
                float targetPullSpeed = Mathf.Lerp(grappleInitialPullSpeed, grapplePullSpeed, pullRamp01);
                if (radialTowardAnchor < targetPullSpeed)
                {
                    float solvedSpeed = Mathf.MoveTowards(
                        radialTowardAnchor,
                        targetPullSpeed,
                        grapplePullAcceleration * deltaTime);
                    currentVelocity += pullDir * (solvedSpeed - radialTowardAnchor);
                }
            }
        }

        float carryFloor = preservedSpeed > grapplePullSpeed
            ? Mathf.Lerp(grapplePullSpeed, preservedSpeed, grappleCarryPreservation)
            : 0f;
        if (carryFloor > 0f && currentVelocity.magnitude < carryFloor)
            currentVelocity = currentVelocity.sqrMagnitude > 0.0001f
                ? currentVelocity.normalized * carryFloor
                : pullDir * carryFloor;

        momentum = Vector3.ProjectOnPlane(currentVelocity, Vector3.up);
        velocity.y = currentVelocity.y;
    }

    private void UpdateLightTargetGrappleMotion(float deltaTime)
    {
        Vector3 anchorPoint = GetGrappleAnchorPoint(activeGrappleTarget);
        Vector3 attachmentPoint = transform.position + Vector3.up * 0.9f;
        Vector3 toPlayer = attachmentPoint - anchorPoint;
        float distance = toPlayer.magnitude;
        if (distance <= 0.001f)
        {
            ReleaseGrapplePreservingMomentum();
            return;
        }

        grappleActiveTimer += deltaTime;
        activeGrappleRopeLength = distance;
        if (distance <= Mathf.Max(1.8f, grappleMinRopeLength + 0.8f))
        {
            ReleaseGrapplePreservingMomentum();
            return;
        }

        if (activeGrappleTarget.massTarget == null)
        {
            ReleaseGrapplePreservingMomentum();
            return;
        }

        float pullRamp01 = grapplePullRampDuration <= 0.001f
            ? 1f
            : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(grappleActiveTimer / grapplePullRampDuration));
        float targetPullSpeed = Mathf.Lerp(grappleInitialPullSpeed, grapplePullSpeed, pullRamp01) * 0.9f;
        bool applied = activeGrappleTarget.massTarget.ApplyGrapplePull(
            attachmentPoint,
            toPlayer / Mathf.Max(0.001f, distance),
            targetPullSpeed,
            deltaTime);
        if (!applied)
            ReleaseGrapplePreservingMomentum();
    }

    private Vector3 ApplyGrappleAirSteer(Vector3 currentVelocity, Vector3 inputDir, float deltaTime)
    {
        if (inputDir.sqrMagnitude <= 0.0001f || !activeGrappleTarget.isValid)
            return currentVelocity;

        Vector3 attachmentPoint = transform.position + Vector3.up * 0.9f;
        Vector3 toAnchor = GetGrappleAnchorPoint(activeGrappleTarget) - attachmentPoint;
        if (toAnchor.sqrMagnitude <= 0.0001f)
            return currentVelocity;

        float preservedSpeed = currentVelocity.magnitude;
        Vector3 wishDir = Vector3.ProjectOnPlane(inputDir.normalized, toAnchor.normalized);
        if (wishDir.sqrMagnitude <= 0.0001f)
            return currentVelocity;

        wishDir.Normalize();
        float speedAlongWish = Vector3.Dot(currentVelocity, wishDir);
        float addSpeed = Mathf.Max(0f, CurrentMoveSpeed - speedAlongWish);
        currentVelocity += wishDir * Mathf.Min(grappleAirSteer * deltaTime, addSpeed);
        return Vector3.ClampMagnitude(currentVelocity, Mathf.Max(preservedSpeed, CurrentMoveSpeed));
    }

    private void SolveHorizontalVelocity(MovementInputState input, float deltaTime)
    {
        if (activeGrappleTarget.isValid)
        {
            UpdateGrappleMotion(input.moveDirection, deltaTime);
            return;
        }

        if (dashTimer > 0f)
        {
            momentum = dashVelocity;
            return;
        }

        UpdateWallMovementState(input.moveDirection, deltaTime);

        if (isGrounded)
        {
            if (isSliding)
                ApplySlideMovement(input.moveDirection, input.slideHeld, deltaTime);
            else
                ApplyGroundMovement(input.moveDirection, deltaTime);
            return;
        }

        if (isWallRunning)
        {
            ApplyWallRunMovement(input.moveDirection, deltaTime);
            return;
        }

        ApplyAirMovement(input.moveDirection, deltaTime);
    }

    private void ApplySlideMovement(Vector3 inputDir, bool slideHeld, float deltaTime)
    {
        if (slideTimer > 0f)
            slideTimer = Mathf.Max(0f, slideTimer - deltaTime);

        float frictionMultiplier = slideTimer > 0f ? 0.18f : 1f;
        momentum = Vector3.MoveTowards(momentum, Vector3.zero, slideFriction * CurrentMoveSpeed * frictionMultiplier * deltaTime);
        if (!slideHeld)
            return;

        Vector3 slideDir = momentum.sqrMagnitude > 0.01f ? momentum.normalized : transform.forward;
        bool opposingSlideInput = false;
        if (inputDir.sqrMagnitude > 0.01f)
        {
            float alignment = Vector3.Dot(slideDir, inputDir.normalized);
            opposingSlideInput = alignment < -0.25f;
            if (!opposingSlideInput)
                slideDir = Vector3.RotateTowards(slideDir, inputDir.normalized, slideSteerStrength * Mathf.Deg2Rad * deltaTime, 0f).normalized;
        }

        if (opposingSlideInput)
        {
            momentum = Vector3.MoveTowards(momentum, Vector3.zero, groundDeceleration * deltaTime);
            return;
        }

        float sustainedSpeed = inputDir.sqrMagnitude > 0.01f
            ? slideHoldSpeed
            : Mathf.Max(slideMinHoldSpeed, slideHoldSpeed * 0.82f);
        float slideSpeedLimit = slideJumpChain > 0 ? GetActiveSpeedLimit() : maxSlideStartSpeed;
        float speed = Mathf.Clamp(Mathf.Max(momentum.magnitude, sustainedSpeed), sustainedSpeed, slideSpeedLimit);
        momentum = slideDir * speed;
    }

    private void ProcessBufferedJump(MovementInputState input)
    {
        if (jumpBufferTimer > 0f && !activeGrappleTarget.isValid && (isWallRunning || isWallSliding))
        {
            ExecuteWallJump(input.moveDirection);
            return;
        }

        bool canGroundJump = isGrounded || coyoteTimer > 0f;
        bool canAirJump = !canGroundJump && jumpsRemaining > 0;
        if (activeGrappleTarget.isValid || jumpBufferTimer <= 0f || (!canGroundJump && !canAirJump))
            return;

        bool slamBoostJump = canGroundJump && slamJumpTimer > 0f;
        velocity.y = Mathf.Sqrt(CurrentJumpHeight * -2f * gravity);
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        if (slamBoostJump)
            slamJumpTimer = 0f;
        if (canGroundJump)
            jumpsRemaining = Mathf.Max(0, maxJumps - 1);
        else
            jumpsRemaining--;
        isGrounded = false;

        if (slamBoostJump)
        {
            float slamChainMultiplier = 1f + Mathf.Min(slamJumpChainCount, maxSlamJumpChain) * slamJumpChainHeightBonus;
            velocity.y *= slamJumpVerticalBoost * slamChainMultiplier;
            slamJumpChainCount = Mathf.Min(maxSlamJumpChain, slamJumpChainCount + 1);
            slamJumpChainTimer = slamJumpChainWindow;
        }

        if (!isSliding)
            return;

        Vector3 planar = Vector3.ProjectOnPlane(momentum, Vector3.up);
        Vector3 jumpDir = planar.sqrMagnitude > 0.01f ? planar.normalized : transform.forward;
        float superSpeed = Mathf.Clamp(
            Mathf.Max(planar.magnitude, slideHoldSpeed) * slideJumpSpeedMultiplier * (1f + slideJumpChain * slideJumpChainBonus),
            slideBaseSpeed * 1.08f,
            maxSlideJumpCarrySpeed * (1f + slideJumpChain * slideJumpChainBonus));
        ExitSlide(false);
        momentum = jumpDir * superSpeed;
        velocity.y *= slideJumpVerticalMultiplier;
        slideCooldownTimer = slideCooldown;
        slideJumpChain++;
        slideJumpChainTimer = slideJumpChainWindow;
    }

    private void ApplyVerticalVelocity(float deltaTime)
    {
        float gravityScale = activeGrappleTarget.isValid ? grappleGravityScale : 1f;
        if (isWallRunning)
            gravityScale *= wallRunGravityScale;
        else if (isWallSliding && velocity.y <= 0f)
            gravityScale *= wallSlideGravityScale;

        velocity.y += gravity * gravityScale * deltaTime;
        if (isWallSliding && velocity.y < -wallSlideMaxFallSpeed)
            velocity.y = -wallSlideMaxFallSpeed;
        lastFrameVelocityY = velocity.y;
    }

    private void UpdateWallMovementState(Vector3 inputDir, float deltaTime)
    {
        bool wasWallRunning = isWallRunning;
        bool hasWallState = false;
        if (isGrounded || isSlamming || isSliding || activeGrappleTarget.isValid || wallDetachTimer > 0f)
        {
            if (wasWallRunning)
                StripWallRunCarry();
            ClearWallState();
            UpdateWallMovementBlend(false, deltaTime);
            return;
        }

        if (Time.time - lastSideHitTime > 0.12f || !IsWallLikeNormal(lastSideHitNormal))
        {
            if (wasWallRunning)
                StripWallRunCarry();
            ClearWallState();
            UpdateWallMovementBlend(false, deltaTime);
            return;
        }

        Vector3 wallNormal = Vector3.ProjectOnPlane(lastSideHitNormal, Vector3.up);
        if (wallNormal.sqrMagnitude <= 0.0001f)
        {
            ClearWallState();
            UpdateWallMovementBlend(false, deltaTime);
            return;
        }

        wallNormal.Normalize();
        activeWallNormal = wallNormal;

        Vector3 planarMomentum = Vector3.ProjectOnPlane(momentum, Vector3.up);
        Vector3 movementReference = inputDir.sqrMagnitude > 0.0001f ? inputDir.normalized : (planarMomentum.sqrMagnitude > 0.0001f ? planarMomentum.normalized : transform.forward);
        Vector3 wallDirection = ResolveWallDirection(movementReference, planarMomentum, wallNormal);
        if (wallDirection.sqrMagnitude <= 0.0001f)
        {
            if (wasWallRunning)
                StripWallRunCarry();
            ClearWallState();
            UpdateWallMovementBlend(false, deltaTime);
            return;
        }

        float inputDot = inputDir.sqrMagnitude > 0.0001f ? Vector3.Dot(inputDir.normalized, wallDirection) : 0f;
        float wallPlanarSpeed = Vector3.ProjectOnPlane(planarMomentum, wallNormal).magnitude;
        bool canRun = inputDot >= wallRunMinInputDot && wallPlanarSpeed >= wallRunMinSpeed && wallRunTimer > 0f;

        isWallRunning = canRun;
        isWallSliding = !canRun && velocity.y <= 0f;
        hasWallState = isWallRunning || isWallSliding;
        if (isWallRunning)
        {
            activeWallDirection = wallDirection;
            if (!wasWallRunning)
                jumpsRemaining = Mathf.Max(jumpsRemaining, Mathf.Max(0, maxJumps - 1));
        }

        if (!isWallRunning && !isWallSliding)
        {
            if (wasWallRunning)
                StripWallRunCarry();
            ClearWallState();
        }
        else if (wasWallRunning && !isWallRunning)
        {
            StripWallRunCarry();
        }

        UpdateWallMovementBlend(hasWallState, deltaTime);
    }

    private void ApplyWallRunMovement(Vector3 inputDir, float deltaTime)
    {
        Vector3 wallDirection = inputDir.sqrMagnitude > 0.0001f
            ? ResolveWallDirection(inputDir.normalized, momentum, activeWallNormal)
            : ResolveWallDirection(momentum, momentum, activeWallNormal);
        if (wallDirection.sqrMagnitude <= 0.0001f)
            wallDirection = activeWallDirection.sqrMagnitude > 0.0001f ? activeWallDirection : Vector3.ProjectOnPlane(transform.forward, activeWallNormal).normalized;
        if (wallDirection.sqrMagnitude <= 0.0001f)
            return;

        activeWallDirection = wallDirection;
        Vector3 alongWall = Vector3.ProjectOnPlane(momentum, activeWallNormal);
        float currentSpeed = alongWall.magnitude;
        float preservedCarrySpeed = Mathf.Max(currentSpeed * wallRunCarryPreservation, wallRunTargetSpeed);
        float solvedSpeed = Mathf.MoveTowards(currentSpeed, preservedCarrySpeed, wallRunAcceleration * deltaTime);
        Vector3 carriedAlongWall = wallDirection * solvedSpeed;
        Vector3 wallBias = -activeWallNormal * wallRunSidePull;
        Vector3 targetMomentum = carriedAlongWall + wallBias;
        if (alongWall.sqrMagnitude > 0.0001f && currentSpeed > wallRunTargetSpeed)
        {
            Vector3 preservedExtra = alongWall.normalized * Mathf.Max(0f, currentSpeed - solvedSpeed);
            targetMomentum += preservedExtra * wallRunCarryPreservation;
        }
        float blend = Mathf.Clamp01(wallMovementBlend);
        momentum = Vector3.Lerp(momentum, targetMomentum, blend);
        velocity.y = Mathf.Lerp(velocity.y, Mathf.Max(velocity.y, -1.25f), blend);
    }

    private Vector3 ResolveWallDirection(Vector3 desiredReference, Vector3 velocityReference, Vector3 wallNormal)
    {
        Vector3 candidate = Vector3.ProjectOnPlane(desiredReference, wallNormal);
        if (candidate.sqrMagnitude <= 0.0001f)
            candidate = Vector3.ProjectOnPlane(velocityReference, wallNormal);
        if (candidate.sqrMagnitude <= 0.0001f)
            candidate = activeWallDirection;
        if (candidate.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        candidate.Normalize();
        if (activeWallDirection.sqrMagnitude > 0.0001f && Vector3.Dot(candidate, activeWallDirection) < 0f)
            candidate = -candidate;
        return candidate;
    }

    private void StripWallRunCarry()
    {
        if (activeWallNormal.sqrMagnitude <= 0.0001f)
            return;

        wallReleaseNormal = activeWallNormal;
        wallReleaseBlendTimer = Mathf.Max(wallReleaseBlendTimer, wallTransitionDuration);
        Vector3 projected = Vector3.ProjectOnPlane(momentum, activeWallNormal);
        momentum = Vector3.Lerp(projected, momentum, wallReleaseCarryPreservation);
    }

    private void UpdateWallMovementBlend(bool targetActive, float deltaTime)
    {
        float target = targetActive ? 1f : 0f;
        float duration = Mathf.Max(0.01f, wallTransitionDuration);
        wallMovementBlend = Mathf.MoveTowards(wallMovementBlend, target, deltaTime / duration);
    }

    private void ExecuteWallJump(Vector3 inputDir)
    {
        Vector3 jumpNormal = activeWallNormal.sqrMagnitude > 0.0001f ? activeWallNormal : -transform.forward;
        Vector3 alongWall = Vector3.ProjectOnPlane(momentum, jumpNormal);
        Vector3 jumpDirection = alongWall.sqrMagnitude > 0.0001f
            ? alongWall.normalized
            : (inputDir.sqrMagnitude > 0.0001f ? inputDir.normalized : transform.forward);

        momentum = jumpDirection * Mathf.Max(CurrentMoveSpeed * 1.08f, alongWall.magnitude) + jumpNormal * wallJumpAwayForce;
        velocity.y = Mathf.Max(velocity.y, wallJumpUpForce);
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        isGrounded = false;
        wallDetachTimer = wallDetachCooldown;
        ClearWallState();
    }

    private void ClearWallState()
    {
        isWallRunning = false;
        isWallSliding = false;
        activeWallNormal = Vector3.zero;
        activeWallDirection = Vector3.zero;
    }

    private void ResolveMovementCollision(Vector3 inputDir)
    {
        float preMovePlanarLimit = GetCurrentPlanarSpeedLimit();
        momentum = Vector3.ClampMagnitude(momentum, preMovePlanarLimit);

        Vector3 finalMove = momentum + (Vector3.up * velocity.y);
        CollisionFlags collisionFlags = controller.Move(finalMove * Time.deltaTime);
        ResolveGrappleConstraintPostMove();
        if ((collisionFlags & CollisionFlags.Above) != 0 && velocity.y > 0f)
        {
            velocity.y = 0f;
            ResetSlideJumpChain();
            if (isSliding)
                ExitSlide(false);
        }

        if ((collisionFlags & CollisionFlags.Sides) != 0)
        {
            ResetSlideJumpChain();
            ClipHorizontalMomentumAgainstWall();
            if (dashTimer > 0f)
                FinishDash(inputDir);
            ClipHorizontalMomentumAgainstWall();
        }

        momentum = Vector3.ClampMagnitude(momentum, GetCurrentPlanarSpeedLimit());
    }

    private void ResolveGrappleConstraintPostMove()
    {
        if (!activeGrappleTarget.isValid || controller == null || !controller.enabled)
            return;

        Vector3 anchorPoint = GetGrappleAnchorPoint(activeGrappleTarget);
        Vector3 attachmentPoint = transform.position + Vector3.up * 0.9f;
        Vector3 fromAnchor = attachmentPoint - anchorPoint;
        float distance = fromAnchor.magnitude;
        if (distance <= 0.001f)
            return;

        float tautLength = Mathf.Max(grappleMinRopeLength, activeGrappleRopeLength - grappleRopeSlack);
        if (distance <= tautLength)
            return;

        Vector3 fromAnchorDir = fromAnchor / distance;
        float excessDistance = distance - tautLength;
        Vector3 correction = -fromAnchorDir * Mathf.Min(
            excessDistance * grappleConstraintElasticity,
            grappleConstraintMaxCorrection);
        if (correction.sqrMagnitude > 0.000001f)
            controller.Move(correction);

        Vector3 currentVelocity = momentum + Vector3.up * velocity.y;
        float outwardSpeed = Vector3.Dot(currentVelocity, fromAnchorDir);
        if (outwardSpeed > 0f)
            currentVelocity -= fromAnchorDir * outwardSpeed * grappleConstraintElasticity;

        momentum = Vector3.ProjectOnPlane(currentVelocity, Vector3.up);
        velocity.y = currentVelocity.y;
    }

    private float GetCurrentPlanarSpeedLimit()
    {
        if (activeGrappleTarget.isValid)
            return Mathf.Max(Mathf.Max(GetAirCarryLimit(), grapplePullSpeed), momentum.magnitude + 0.5f);
        if (dashTimer > 0f)
            return Mathf.Max(GetActiveSpeedLimit(), dashVelocity.magnitude);
        if (isSliding)
            return slideJumpChain > 0 ? GetActiveSpeedLimit() : Mathf.Max(maxSlideStartSpeed, slideHoldSpeed);
        if (isGrounded)
            return slideJumpChain > 0 ? GetActiveSpeedLimit() : Mathf.Max(CurrentMoveSpeed * 1.05f, dashExitSpeed);
        return GetAirCarryLimit();
    }

    private void EnsureGrappleLine()
    {
        if (grappleLine != null || cameraTransform == null)
            return;

        GameObject lineObject = new GameObject("GrappleLine");
        lineObject.transform.SetParent(transform, false);
        grappleLine = lineObject.AddComponent<LineRenderer>();
        grappleLine.positionCount = GrappleLineSegments;
        grappleLine.enabled = false;
        grappleLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        grappleLine.receiveShadows = false;
        grappleLine.textureMode = LineTextureMode.Tile;
        grappleLine.alignment = LineAlignment.View;
        grappleLine.startWidth = grappleLineWidthIdle;
        grappleLine.endWidth = grappleLineWidthIdle * 0.8f;
        grappleLine.numCapVertices = 2;
        grappleLine.material = GetGrappleLineMaterial();
        grappleLine.startColor = grappleLineColor;
        Color endColor = grappleLineColor;
        endColor.a *= 0.7f;
        grappleLine.endColor = endColor;
    }

    private Material GetGrappleLineMaterial()
    {
        if (cachedGrappleLineMaterial != null)
            return cachedGrappleLineMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        cachedGrappleLineMaterial = new Material(shader);
        cachedGrappleLineMaterial.name = "RuntimeGrappleLine";
        Texture2D weaveTexture = new Texture2D(8, 16, TextureFormat.RGBA32, false);
        weaveTexture.name = "RuntimeGrappleWeave";
        weaveTexture.wrapMode = TextureWrapMode.Repeat;
        weaveTexture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < weaveTexture.height; y++)
        {
            for (int x = 0; x < weaveTexture.width; x++)
            {
                bool raisedThread = ((x + y / 2) & 3) == 0;
                float shade = raisedThread ? 0.19f : ((x + y) & 1) == 0 ? 0.105f : 0.075f;
                weaveTexture.SetPixel(x, y, new Color(shade, shade * 1.04f, shade * 1.1f, 1f));
            }
        }
        weaveTexture.Apply(false, true);
        if (cachedGrappleLineMaterial.HasProperty("_BaseMap"))
            cachedGrappleLineMaterial.SetTexture("_BaseMap", weaveTexture);
        if (cachedGrappleLineMaterial.HasProperty("_MainTex"))
            cachedGrappleLineMaterial.SetTexture("_MainTex", weaveTexture);
        cachedGrappleLineMaterial.mainTextureScale = new Vector2(5f, 1f);
        ApplyWorldFxColor(cachedGrappleLineMaterial, grappleLineColor);
        return cachedGrappleLineMaterial;
    }

    private void EnsureGrappleViewModel()
    {
        if (cameraTransform == null || grappleViewRoot != null)
            return;

        grappleViewRoot = new GameObject("GrappleViewModel").transform;
        grappleViewRoot.SetParent(cameraTransform, false);
        grappleViewRoot.localPosition = new Vector3(-0.15f, -0.16f, 0.39f);
        grappleViewRoot.localRotation = Quaternion.Euler(7f, -10f, 3f);
        grappleViewRoot.localScale = Vector3.one * 0.72f;

        grappleHandPivot = new GameObject("GrappleHandPivot").transform;
        grappleHandPivot.SetParent(grappleViewRoot, false);
        grappleHandPivot.localPosition = Vector3.zero;

        Transform forearm = CreateViewPrimitive("Forearm", PrimitiveType.Cube, grappleHandPivot, new Vector3(0.048f, -0.024f, -0.008f), new Vector3(0.068f, 0.058f, 0.26f), GetGrappleViewBodyMaterial());
        forearm.localRotation = Quaternion.Euler(0f, -4f, 18f);
        CreateViewPrimitive("ForearmRail", PrimitiveType.Cube, forearm, new Vector3(-0.16f, 0.22f, 0f), new Vector3(0.12f, 0.12f, 0.95f), GetGrappleViewAccentMaterial());

        Transform palm = CreateViewPrimitive("Palm", PrimitiveType.Cube, grappleHandPivot, new Vector3(0.092f, 0.002f, 0.1f), new Vector3(0.086f, 0.048f, 0.11f), GetGrappleViewBodyMaterial());
        palm.localRotation = Quaternion.Euler(8f, -8f, 24f);

        for (int i = 0; i < 3; i++)
        {
            float yOffset = 0.02f - i * 0.018f;
            Transform finger = CreateViewPrimitive("Finger_" + i, PrimitiveType.Cube, palm, new Vector3(0.055f, yOffset, 0.055f), new Vector3(0.018f, 0.014f, 0.07f), GetGrappleViewBodyMaterial());
            finger.localRotation = Quaternion.Euler(-8f, 8f, 0f);
        }

        Transform thumb = CreateViewPrimitive("Thumb", PrimitiveType.Cube, palm, new Vector3(0.015f, -0.028f, 0.025f), new Vector3(0.02f, 0.016f, 0.055f), GetGrappleViewBodyMaterial());
        thumb.localRotation = Quaternion.Euler(20f, -26f, -38f);

        Transform launcherRoot = new GameObject("LauncherRoot").transform;
        launcherRoot.SetParent(grappleHandPivot, false);
        launcherRoot.localPosition = new Vector3(0.122f, 0.016f, 0.108f);
        launcherRoot.localRotation = Quaternion.Euler(-2f, 10f, 12f);

        grappleLauncherBody = CreateViewPrimitive("LauncherBody", PrimitiveType.Cube, launcherRoot, new Vector3(0f, 0f, 0.01f), new Vector3(0.06f, 0.042f, 0.18f), GetGrappleViewBodyMaterial());
        grappleLauncherRail = CreateViewPrimitive("LauncherRail", PrimitiveType.Cube, grappleLauncherBody, new Vector3(-0.014f, 0.022f, -0.01f), new Vector3(0.018f, 0.014f, 0.145f), GetGrappleViewAccentMaterial());
        CreateViewPrimitive("LauncherSideStrip", PrimitiveType.Cube, grappleLauncherBody, new Vector3(0.026f, 0.006f, 0.006f), new Vector3(0.01f, 0.014f, 0.16f), GetGrappleViewAccentMaterial());
        CreateViewPrimitive("LauncherRear", PrimitiveType.Cube, grappleLauncherBody, new Vector3(0f, -0.006f, -0.09f), new Vector3(0.04f, 0.028f, 0.04f), GetGrappleViewBodyMaterial());
        CreateViewPrimitive("LauncherCore", PrimitiveType.Cylinder, grappleLauncherBody, new Vector3(0f, -0.001f, 0.102f), new Vector3(0.016f, 0.052f, 0.016f), GetGrappleViewAccentMaterial()).localRotation = Quaternion.Euler(90f, 0f, 0f);
        Transform clawLeft = CreateViewPrimitive("LauncherClawLeft", PrimitiveType.Cube, grappleLauncherBody, new Vector3(-0.02f, -0.002f, 0.132f), new Vector3(0.01f, 0.014f, 0.05f), GetGrappleViewAccentMaterial());
        Transform clawRight = CreateViewPrimitive("LauncherClawRight", PrimitiveType.Cube, grappleLauncherBody, new Vector3(0.02f, -0.002f, 0.132f), new Vector3(0.01f, 0.014f, 0.05f), GetGrappleViewAccentMaterial());
        clawLeft.localRotation = Quaternion.Euler(-6f, 7f, 0f);
        clawRight.localRotation = Quaternion.Euler(6f, -7f, 0f);

        grappleLauncherMuzzle = new GameObject("GrappleMuzzle").transform;
        grappleLauncherMuzzle.SetParent(launcherRoot, false);
        grappleLauncherMuzzle.localPosition = new Vector3(0f, -0.001f, 0.185f);
        grappleLauncherMuzzle.localRotation = Quaternion.identity;

        grappleViewRoot.gameObject.SetActive(false);
    }

    private Transform CreateViewPrimitive(string name, PrimitiveType primitiveType, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject primitive = GameObject.CreatePrimitive(primitiveType);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localScale = localScale;
        Collider primitiveCollider = primitive.GetComponent<Collider>();
        if (primitiveCollider != null)
            Destroy(primitiveCollider);
        Renderer renderer = primitive.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
        return primitive.transform;
    }

    private Material GetGrappleViewBodyMaterial()
    {
        if (cachedGrappleViewBodyMaterial != null)
            return cachedGrappleViewBodyMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        cachedGrappleViewBodyMaterial = new Material(shader);
        cachedGrappleViewBodyMaterial.name = "RuntimeGrappleViewBody";
        ApplyWorldFxColor(cachedGrappleViewBodyMaterial, grappleViewBodyColor);
        return cachedGrappleViewBodyMaterial;
    }

    private Material GetGrappleViewAccentMaterial()
    {
        if (cachedGrappleViewAccentMaterial != null)
            return cachedGrappleViewAccentMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        cachedGrappleViewAccentMaterial = new Material(shader);
        cachedGrappleViewAccentMaterial.name = "RuntimeGrappleViewAccent";
        ApplyWorldFxColor(cachedGrappleViewAccentMaterial, grappleViewAccentColor);
        return cachedGrappleViewAccentMaterial;
    }

    private void UpdateGrappleViewModel()
    {
        EnsureGrappleViewModel();
        if (grappleViewRoot == null || grappleHandPivot == null)
            return;

        if (grappleLaunchVisualTimer > 0f)
            grappleLaunchVisualTimer = Mathf.Max(0f, grappleLaunchVisualTimer - Time.deltaTime);

        bool hookActive = activeGrappleHook != null;
        bool visible = activeGrappleTarget.isValid || hookActive || grappleLaunchVisualTimer > 0f;
        grappleViewRoot.gameObject.SetActive(visible && !isUIActive && !isDead);
        if (!grappleViewRoot.gameObject.activeSelf)
            return;

        float launch01 = grappleLaunchVisualDuration <= 0.001f
            ? 0f
            : Mathf.Clamp01(grappleLaunchVisualTimer / grappleLaunchVisualDuration);
        float extend01 = (activeGrappleTarget.isValid || hookActive) ? 1f : 1f - launch01;
        grappleVisualPulse = Mathf.MoveTowards(grappleVisualPulse, (activeGrappleTarget.isValid || hookActive) ? 1f : 0f, Time.deltaTime * 5f);
        Vector3 grapplePoint = activeGrappleTarget.isValid
            ? GetGrappleAnchorPoint(activeGrappleTarget)
            : (activeGrappleHook != null ? activeGrappleHook.CurrentPoint : cameraTransform.position + cameraTransform.forward * 3f);
        Vector3 ropeDir = (grapplePoint - (grappleLauncherMuzzle != null ? grappleLauncherMuzzle.position : cameraTransform.position)).normalized;
        Vector3 localRopeDir = cameraTransform.InverseTransformDirection(ropeDir);
        float hookFlightWeight = activeGrappleHook != null && !activeGrappleHook.IsLatched ? 1f : 0f;
        float tensionWeight = activeGrappleTarget.isValid ? 1f : hookFlightWeight * 0.65f;
        float idlePulse = 0.55f + Mathf.Sin(Time.unscaledTime * 8f) * 0.08f;
        float activePulse = 0.92f + Mathf.Sin(Time.unscaledTime * 24f) * 0.28f;
        float pulse = Mathf.Lerp(idlePulse, activePulse, grappleVisualPulse);

        float latchKick = Mathf.Clamp01(grappleVisualPulse - 1f);
        Vector3 targetPos = new Vector3(-0.15f, -0.16f, 0.39f)
            + new Vector3(0.042f, 0.006f, 0.048f) * tensionWeight
            + new Vector3(0.024f, -0.008f, -0.036f) * latchKick;
        Quaternion targetRot = Quaternion.Euler(
            8f - tensionWeight * 10f - localRopeDir.y * 5f + latchKick * 12f,
            -12f + tensionWeight * 14f + localRopeDir.x * 8f - latchKick * 5f,
            4f - tensionWeight * 4f + latchKick * 8f);
        grappleViewRoot.localPosition = Vector3.Lerp(grappleViewRoot.localPosition, targetPos, Time.deltaTime * (grappleHandRecoverSpeed * 0.85f));
        grappleViewRoot.localRotation = Quaternion.Slerp(grappleViewRoot.localRotation, targetRot, Time.deltaTime * (grappleHandRecoverSpeed * 0.8f));

        Vector3 handPos = new Vector3(0f, -0.004f, 0f) + new Vector3(0.01f, -0.012f, 0.016f) * tensionWeight;
        Quaternion handRot = Quaternion.Euler(-14f * tensionWeight, 5f * tensionWeight, -8f * tensionWeight);
        grappleHandPivot.localPosition = Vector3.Lerp(grappleHandPivot.localPosition, handPos, Time.deltaTime * 10f);
        grappleHandPivot.localRotation = Quaternion.Slerp(grappleHandPivot.localRotation, handRot, Time.deltaTime * 10f);

        UpdateGrappleViewEmission(pulse);
    }

    private float ScoreGrappleTarget(Vector3 rayDirection, GrappleTarget target)
    {
        Vector3 origin = cameraTransform != null ? cameraTransform.position : transform.position + Vector3.up * 1.2f;
        Vector3 anchorPoint = GetGrappleAnchorPoint(target);
        Vector3 offset = anchorPoint - origin;
        Vector3 toTarget = offset.sqrMagnitude > 0.0001f ? offset.normalized : rayDirection.normalized;
        float alignment = Vector3.Dot(rayDirection.normalized, toTarget);
        float distance = offset.magnitude;
        float distancePenalty = distance / Mathf.Max(1f, grappleRange);
        float assistBonus = target.isAssisted ? 0.08f : 0f;
        float ledgeBonus = target.isLedgeSnap ? grappleLedgePreference : 0f;
        float verticalBonus = Mathf.Clamp01(Mathf.Max(0f, anchorPoint.y - origin.y) / 12f) * grappleVerticalPreference;
        float wallBonus = Mathf.InverseLerp(0.3f, -0.15f, target.normal.y) * 0.1f;
        return alignment + assistBonus + ledgeBonus + verticalBonus + wallBonus - distancePenalty * 0.15f;
    }

    private bool TryBuildGrappleTarget(RaycastHit hit, out GrappleTarget target)
    {
        target = default;
        if (!IsValidGrappleSurface(hit))
            return false;

        IGrappleMassTarget massTarget = hit.collider.GetComponentInParent<IGrappleMassTarget>();
        target.isValid = true;
        target.normal = hit.normal;
        target.point = hit.point + hit.normal * 0.08f;
        target.collider = hit.collider;
        target.anchorTransform = hit.rigidbody != null ? hit.rigidbody.transform : hit.collider.transform;
        target.localPoint = target.anchorTransform != null
            ? target.anchorTransform.InverseTransformPoint(target.point)
            : target.point;
        target.massTarget = massTarget;
        target.pullsPlayer = massTarget == null || massTarget.GrappleMassClass == GrappleMassClass.Heavy;

        if (massTarget == null && TryFindNearbyLedgePoint(hit, out RaycastHit topHit, out Vector3 ledgePoint))
        {
            target.point = ledgePoint;
            target.isAssisted = true;
            target.isLedgeSnap = true;
            target.collider = topHit.collider;
            target.anchorTransform = topHit.rigidbody != null ? topHit.rigidbody.transform : topHit.collider.transform;
            target.localPoint = target.anchorTransform != null
                ? target.anchorTransform.InverseTransformPoint(target.point)
                : target.point;
        }

        return true;
    }

    private bool TryFindNearbyLedgePoint(RaycastHit hit, out RaycastHit topHit, out Vector3 ledgePoint)
    {
        topHit = default;
        ledgePoint = default;
        if (cameraTransform == null)
            return false;

        if (hit.normal.y > 0.78f)
            return false;

        Vector3 wallPush = Vector3.ProjectOnPlane(-hit.normal, Vector3.up).normalized;
        Vector3 viewBias = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 probeOffset = wallPush.sqrMagnitude > 0.0001f ? wallPush : viewBias;
        if (probeOffset.sqrMagnitude <= 0.0001f)
            probeOffset = Vector3.forward;
        Vector3 probeOrigin = hit.point + Vector3.up * grappleLedgeProbeHeight + probeOffset * 0.5f;
        if (!Physics.Raycast(probeOrigin, Vector3.down, out topHit, grappleLedgeProbeHeight * 2.2f, grappleSurfaceMask, QueryTriggerInteraction.Ignore))
            return false;
        if (!IsValidStandingSurface(topHit))
            return false;

        ledgePoint = topHit.point + Vector3.up * 0.18f - probeOffset * 0.08f;
        return Vector3.Distance(cameraTransform.position, ledgePoint) <= grappleRange;
    }

    private bool IsValidGrappleSurface(RaycastHit hit)
    {
        Collider hitCollider = hit.collider;
        if (hitCollider == null || hitCollider.isTrigger)
            return false;
        if (hit.normal.y < -0.2f)
            return false;
        if (hit.normal.y > 0.72f)
            return false;
        if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
            return false;
        if (hitCollider.GetComponentInParent<PlayerController>() != null)
            return false;
        if (hitCollider.GetComponentInParent<Projectile>() != null)
            return false;
        if (hitCollider.GetComponentInParent<Interactable>() != null)
            return false;
        return true;
    }

    private void UpdateGrappleVisuals()
    {
        EnsureGrappleLine();
        EnsureGrappleAnchorVisual();
        if (grappleLine == null)
            return;

        if ((!activeGrappleTarget.isValid && activeGrappleHook == null) || cameraTransform == null)
        {
            grappleLine.enabled = false;
            if (grappleAnchorVisual != null)
                grappleAnchorVisual.gameObject.SetActive(false);
            return;
        }

        grappleLine.enabled = true;
        float linePulse = activeGrappleTarget.isValid
            ? Mathf.Lerp(grappleLineWidthIdle, grappleLineWidthActive, Mathf.Clamp01(grappleVisualPulse))
            : grappleLineWidthIdle;
        grappleLine.startWidth = linePulse;
        grappleLine.endWidth = linePulse * 0.78f;
        Vector3 start = grappleLauncherMuzzle != null
            ? grappleLauncherMuzzle.position
            : cameraTransform.position + cameraTransform.forward * 0.22f + cameraTransform.right * 0.08f + Vector3.down * 0.06f;
        Vector3 end = activeGrappleHook != null ? activeGrappleHook.CurrentPoint : GetGrappleAnchorPoint(activeGrappleTarget);
        Vector3 ropeDir = (end - start).normalized;
        Vector3 side = Vector3.Cross(ropeDir, cameraTransform.up);
        if (side.sqrMagnitude <= 0.0001f)
            side = Vector3.Cross(ropeDir, cameraTransform.right);
        side = side.sqrMagnitude <= 0.0001f ? Vector3.right : side.normalized;
        Vector3 upWave = cameraTransform.up;
        float length = Vector3.Distance(start, end);
        float wave = 0f;
        Vector3 midA = Vector3.Lerp(start, end, 0.35f) + side * wave;
        Vector3 midB = Vector3.Lerp(start, end, 0.72f) - side * wave * 0.5f;
        float ropeSlackAmount = 0f;
        if (activeGrappleHook != null && !activeGrappleHook.IsLatched)
        {
            float deploy01 = activeGrappleHook.Travel01;
            float coil = 1f - Mathf.Clamp01(deploy01 * 1.2f);
            float quiver = Mathf.Sin(Time.unscaledTime * 34f + deploy01 * 6f) * (0.06f + coil * 0.09f);
            float loopDepth = Mathf.Max(0.1f, length * (0.16f + coil * 0.1f));
            Vector3 coilOrigin = start + ropeDir * Mathf.Min(length * 0.18f, 0.22f + deploy01 * 0.14f);
            if (activeGrappleHook.IsRetracting)
            {
                float retract01 = Mathf.Clamp01(Vector3.Distance(end, GetGrappleReturnPoint()) / Mathf.Max(0.15f, length + 0.001f));
                float loose01 = Mathf.Clamp01(retract01 * 2.1f);
                float tighten01 = 1f - Mathf.Clamp01(retract01 * 1.35f);
                float retractJitter = Mathf.Sin(Time.unscaledTime * 42f) * Mathf.Lerp(0.11f, 0.025f, tighten01);
                float sag = Mathf.Lerp(0.16f, 0.02f, tighten01) * loose01;
                midA = Vector3.Lerp(start, end, 0.24f) + side * retractJitter + upWave * 0.02f - Vector3.down * sag;
                midB = Vector3.Lerp(start, end, 0.58f) - side * retractJitter * 0.72f - upWave * 0.01f - Vector3.down * (sag * 1.35f);
            }
            else
            {
                midA = coilOrigin + side * (0.16f * coil + quiver) + upWave * (0.05f * coil);
                midB = coilOrigin + ropeDir * loopDepth - side * (0.14f * coil + quiver * 0.7f) - upWave * (0.04f * coil);
            }
        }
        else if (activeGrappleTarget.isValid)
        {
            float currentDistance = Vector3.Distance(transform.position + Vector3.up * 0.9f, end);
            float tautLength = Mathf.Max(grappleMinRopeLength, activeGrappleRopeLength - grappleRopeSlack);
            ropeSlackAmount = Mathf.Max(0f, tautLength - currentDistance);
            float sag = Mathf.Min(0.34f, ropeSlackAmount * 0.32f);
            midA = Vector3.Lerp(start, end, 0.28f) + Vector3.down * (sag * 0.65f);
            midB = Vector3.Lerp(start, end, 0.74f) + Vector3.down * sag;
        }
        else if (grappleLaunchVisualTimer > 0f && grappleLaunchVisualDuration > 0.001f)
        {
            float travel01 = 1f - Mathf.Clamp01(grappleLaunchVisualTimer / grappleLaunchVisualDuration);
            end = Vector3.Lerp(start, end, travel01);
            midA = Vector3.Lerp(start, midA, travel01);
            midB = Vector3.Lerp(start, midB, travel01);
        }
        float tension01 = activeGrappleTarget.isValid
            ? 1f - Mathf.Clamp01(ropeSlackAmount / Mathf.Max(0.05f, grappleRopeSlack + 0.08f))
            : 0f;
        grappleLine.startWidth = Mathf.Lerp(0.022f, 0.018f, tension01);
        grappleLine.endWidth = Mathf.Lerp(0.018f, 0.015f, tension01);
        Color ropeColor = Color.Lerp(grappleLineColor, new Color(0.18f, 0.19f, 0.21f, 0.98f), tension01 * 0.28f);
        grappleLine.startColor = ropeColor;
        Color endColor = Color.Lerp(ropeColor, grappleAnchorPulseColor, activeGrappleTarget.isValid ? 0.14f : 0.05f);
        endColor.a = 0.92f;
        grappleLine.endColor = endColor;
        for (int i = 0; i < GrappleLineSegments; i++)
        {
            float t = GrappleLineSegments <= 1 ? 1f : i / (float)(GrappleLineSegments - 1);
            Vector3 point = EvaluateGrappleRopePoint(start, midA, midB, end, t);
            grappleLine.SetPosition(i, point);
        }
        if (activeGrappleTarget.isValid)
            UpdateGrappleAnchorVisual(end, ropeDir);
        else if (grappleAnchorVisual != null)
            grappleAnchorVisual.gameObject.SetActive(false);

        if (grappleReticleRect != null)
        {
            float targetFade = aimedGrappleTarget.isValid ? 1f : 0f;
            grappleReticleFade = Mathf.MoveTowards(grappleReticleFade, targetFade, Time.unscaledDeltaTime * Mathf.Max(0.01f, grappleReticleFadeSpeed));
            grappleReticleRect.localScale = Vector3.one * Mathf.Lerp(1f, 1f + grappleReticleScaleBoost, grappleReticleFade);
            if (grappleReticleImage != null)
            {
                Color reticleColor = grappleReticleColor;
                reticleColor.a *= grappleReticleFade;
                if (aimedGrappleTarget.isValid && aimedGrappleTarget.isLedgeSnap)
                    reticleColor = Color.Lerp(reticleColor, grappleAnchorPulseColor, 0.35f);
                grappleReticleImage.color = reticleColor;
            }
        }
    }

    private static Vector3 EvaluateGrappleRopePoint(Vector3 start, Vector3 midA, Vector3 midB, Vector3 end, float t)
    {
        float omt = 1f - t;
        return omt * omt * omt * start
             + 3f * omt * omt * t * midA
             + 3f * omt * t * t * midB
             + t * t * t * end;
    }

    private void SpawnGrappleHook(Vector3 startPosition, Vector3 direction)
    {
        DestroyActiveGrappleHook();
        if (pooledGrappleHook == null)
        {
            GameObject hookObject = new GameObject("GrappleHookProjectile");
            pooledGrappleHook = hookObject.AddComponent<GrappleHookProjectile>();
        }

        activeGrappleHook = pooledGrappleHook;
        activeGrappleHook.transform.SetParent(null, true);
        activeGrappleHook.gameObject.SetActive(true);
        activeGrappleHook.Initialize(this, startPosition, direction, grappleHookSpeed, grappleHookRadius, grappleRange, grappleSurfaceMask, grappleViewBodyColor, grappleViewAccentColor);
    }

    private void DestroyActiveGrappleHook()
    {
        if (activeGrappleHook == null)
            return;
        GrappleHookProjectile hook = activeGrappleHook;
        activeGrappleHook = null;
        if (hook != null)
        {
            hook.gameObject.SetActive(false);
            hook.transform.SetParent(transform, false);
        }
    }

    private void CancelGrappleHook()
    {
        StopGrapple();
    }

    public Vector3 GetGrappleReturnPoint()
    {
        if (grappleLauncherMuzzle != null)
            return grappleLauncherMuzzle.position;
        if (cameraTransform != null)
            return cameraTransform.position + cameraTransform.forward * 0.22f + cameraTransform.right * 0.08f + Vector3.down * 0.05f;
        return transform.position + Vector3.up * 1.2f;
    }

    public void NotifyGrappleHookInvalidHit(GrappleHookProjectile hook, RaycastHit hit, Vector3 bounceDirection)
    {
        if (hook == null || hook != activeGrappleHook)
            return;

        SpawnWorldBurst(hit.point + hit.normal * 0.03f, new Color(1f, 0.74f, 0.32f, 0.9f), 0.085f, 0.06f, 0.28f);
        SpawnWorldBurst(hit.point + hit.normal * 0.05f, new Color(0.7f, 0.92f, 1f, 0.7f), 0.07f, 0.03f, 0.16f);
        SpawnGrappleFailureSparks(hit.point + hit.normal * 0.04f, hit.normal, bounceDirection);
        grappleState = GrappleState.Retracting;
        hook.BeginRetract(hit.point + hit.normal * 0.03f, bounceDirection);
    }

    private void SpawnGrappleFailureSparks(Vector3 origin, Vector3 surfaceNormal, Vector3 bounceDirection)
    {
        if (!gameObject.activeInHierarchy)
            return;

        GameObject go = new GameObject("GrappleFailureSparks");
        go.transform.position = origin;
        go.transform.rotation = Quaternion.LookRotation((surfaceNormal + bounceDirection).sqrMagnitude > 0.0001f ? (surfaceNormal + bounceDirection).normalized : surfaceNormal, Vector3.up);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.duration = 0.28f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(7.5f, 15.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.026f, 0.065f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.92f, 0.7f, 0.95f),
            new Color(1f, 0.56f, 0.18f, 0.85f));
        main.maxParticles = 64;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 28, 40)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 32f;
        shape.radius = 0.08f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        Vector3 sparkBias = (bounceDirection + surfaceNormal * 0.45f).normalized * 3.6f;
        velocity.x = new ParticleSystem.MinMaxCurve(sparkBias.x - 2.1f, sparkBias.x + 2.1f);
        velocity.y = new ParticleSystem.MinMaxCurve(Mathf.Max(1.1f, sparkBias.y), sparkBias.y + 3.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(sparkBias.z - 2.1f, sparkBias.z + 2.1f);

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.34f;
        renderer.lengthScale = 7.4f;
        renderer.material = CreateSpeedLineMaterial();

        ps.Play();
        Destroy(go, 1.2f);
    }

    public bool NotifyGrappleHookHit(GrappleHookProjectile hook, RaycastHit hit)
    {
        if (hook == null || hook != activeGrappleHook)
            return false;

        if (!TryBuildGrappleTarget(hit, out GrappleTarget target))
        {
            if (launchedGrappleTarget.isValid && IsFallbackGrappleHitCompatible(hit, launchedGrappleTarget))
                target = launchedGrappleTarget;
            else
                return false;
        }

        activeGrappleTarget = target;
        launchedGrappleTarget = target;
        activeGrappleRopeLength = Mathf.Clamp(
            Vector3.Distance(transform.position + Vector3.up * 0.9f, GetGrappleAnchorPoint(target)),
            grappleMinRopeLength,
            grappleRange);
        hook.LatchTo(target.anchorTransform, GetGrappleAnchorPoint(target), target.normal);
        grappleActiveTimer = 0f;
        grappleJumpCooldownTimer = 0f;
        grappleTargetGraceTimer = grappleTargetGraceDuration;
        grappleVisualPulse = 1.35f;
        grappleLaunchVisualTimer = Mathf.Max(grappleLaunchVisualTimer, grappleLaunchVisualDuration * 0.9f);
        grappleState = GrappleState.Latched;
        if (isSliding)
            ExitSlide(false);
        isSlamming = false;
        ApplyGrappleLatchImpact(target);
        return true;
    }

    private void ApplyGrappleLatchImpact(GrappleTarget target)
    {
        Vector3 anchorPoint = GetGrappleAnchorPoint(target);
        Vector3 attachPoint = transform.position + Vector3.up * 0.9f;
        if (target.pullsPlayer)
        {
            Vector3 pullDir = (anchorPoint - attachPoint).normalized;
            if (pullDir.sqrMagnitude > 0.0001f)
            {
                Vector3 currentVelocity = momentum + Vector3.up * velocity.y;
                float along = Vector3.Dot(currentVelocity, pullDir);
                float targetAlong = Mathf.Max(grappleInitialPullSpeed + grappleLatchBoost, along + grappleLatchBoost);
                currentVelocity += pullDir * Mathf.Max(0f, targetAlong - along);
                currentVelocity.y = Mathf.Max(currentVelocity.y, grappleLatchUpBoost);
                momentum = Vector3.ProjectOnPlane(currentVelocity, Vector3.up);
                velocity.y = currentVelocity.y;
            }
        }
        else
        {
            Vector3 recoil = (attachPoint - anchorPoint).normalized;
            if (recoil.sqrMagnitude > 0.0001f)
                momentum += recoil * 1.2f;
        }

        weaponImpactShakeAmount = Mathf.Max(weaponImpactShakeAmount, Mathf.Clamp(grappleLatchShake * 1.25f, 0.05f, 0.32f));
        weaponImpactShakeTimer = Mathf.Max(weaponImpactShakeTimer, 0.24f);
        crosshairFireTimer = Mathf.Max(crosshairFireTimer, 0.24f);
        SpawnWorldBurst(anchorPoint + target.normal * 0.03f, new Color(0.84f, 0.97f, 1f, 0.95f), 0.11f, 0.055f, 0.34f);
        SpawnWorldBurst(attachPoint, new Color(0.62f, 0.92f, 1f, 0.72f), 0.085f, 0.075f, 0.26f);
    }

    private bool IsFallbackGrappleHitCompatible(RaycastHit hit, GrappleTarget target)
    {
        if (!target.isValid || hit.collider == null)
            return false;

        Transform hitTransform = hit.collider.transform;
        if (target.anchorTransform != null && (hitTransform == target.anchorTransform || hitTransform.IsChildOf(target.anchorTransform) || target.anchorTransform.IsChildOf(hitTransform)))
            return true;

        Vector3 anchorPoint = GetGrappleAnchorPoint(target);
        return Vector3.Distance(hit.point, anchorPoint) <= Mathf.Max(0.9f, grappleHookRadius * 10f);
    }

    public void NotifyGrappleHookExpired(GrappleHookProjectile hook)
    {
        if (hook == null || hook != activeGrappleHook)
            return;
        CancelGrappleHook();
    }

    private Vector3 GetGrappleAnchorPoint(GrappleTarget target)
    {
        if (!target.isValid)
            return target.point;
        if (target.anchorTransform != null)
            return target.anchorTransform.TransformPoint(target.localPoint);
        return target.point;
    }

    private void EnsureGrappleAnchorVisual()
    {
        if (grappleAnchorVisual != null)
            return;

        grappleAnchorVisual = new GameObject("GrappleAnchorVisual").transform;
        grappleAnchorVisual.gameObject.SetActive(false);

        GameObject core = new GameObject("AnchorCore");
        core.transform.SetParent(grappleAnchorVisual, false);
        grappleAnchorRenderer = core.AddComponent<SpriteRenderer>();
        grappleAnchorRenderer.sprite = GetGrappleAnchorSprite();
        grappleAnchorRenderer.sharedMaterial = GetGrappleAnchorMaterial();
        grappleAnchorRenderer.sortingOrder = 500;
        grappleAnchorRenderer.color = grappleAnchorColor;

        GameObject pulse = new GameObject("AnchorPulse");
        pulse.transform.SetParent(grappleAnchorVisual, false);
        grappleAnchorPulseRenderer = pulse.AddComponent<SpriteRenderer>();
        grappleAnchorPulseRenderer.sprite = GetGrappleAnchorSprite();
        grappleAnchorPulseRenderer.sharedMaterial = GetGrappleAnchorMaterial();
        grappleAnchorPulseRenderer.sortingOrder = 499;
        grappleAnchorPulseRenderer.color = grappleAnchorPulseColor;
    }

    private void UpdateGrappleAnchorVisual(Vector3 anchorPoint, Vector3 ropeDir)
    {
        if (grappleAnchorVisual == null)
            return;

        grappleAnchorVisual.gameObject.SetActive(true);
        grappleAnchorVisual.position = anchorPoint + ropeDir * 0.01f;
        if (cameraTransform != null)
            grappleAnchorVisual.rotation = Quaternion.LookRotation(cameraTransform.forward, cameraTransform.up);

        float pulse = 0.85f + Mathf.Sin(Time.unscaledTime * 18f) * 0.18f;
        float outerPulse = 1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.12f;
        grappleAnchorVisual.localScale = Vector3.one * 0.24f;
        if (grappleAnchorRenderer != null)
        {
            grappleAnchorRenderer.transform.localScale = Vector3.one * pulse;
            Color color = grappleAnchorColor;
            color.a *= 0.95f;
            grappleAnchorRenderer.color = color;
        }
        if (grappleAnchorPulseRenderer != null)
        {
            grappleAnchorPulseRenderer.transform.localScale = Vector3.one * (1.6f * outerPulse);
            Color pulseColor = grappleAnchorPulseColor;
            pulseColor.a *= 0.38f + Mathf.Sin(Time.unscaledTime * 14f) * 0.1f;
            grappleAnchorPulseRenderer.color = pulseColor;
        }
    }

    private void UpdateGrappleViewEmission(float pulse)
    {
        if (cachedGrappleViewAccentMaterial != null)
            ApplyWorldFxColor(cachedGrappleViewAccentMaterial, Color.Lerp(grappleViewAccentColor * 0.8f, grappleAnchorPulseColor, 0.18f + pulse * 0.16f));

        if (cachedGrappleViewBodyMaterial != null)
            ApplyWorldFxColor(cachedGrappleViewBodyMaterial, grappleViewBodyColor);

        if (grappleLauncherRail != null)
        {
            grappleLauncherRail.localScale = new Vector3(grappleLauncherRail.localScale.x, grappleLauncherRail.localScale.y, 0.12f + pulse * 0.06f);
        }
        if (grappleLauncherBody != null)
        {
            float recoilNudge = activeGrappleTarget.isValid ? Mathf.Sin(Time.unscaledTime * 22f) * 0.0025f : 0f;
            grappleLauncherBody.localPosition = new Vector3(0f, 0f, 0.01f + recoilNudge);
        }
    }

    private Material GetGrappleAnchorMaterial()
    {
        if (cachedGrappleAnchorMaterial != null)
            return cachedGrappleAnchorMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        cachedGrappleAnchorMaterial = new Material(shader);
        cachedGrappleAnchorMaterial.name = "RuntimeGrappleAnchor";
        ApplyWorldFxColor(cachedGrappleAnchorMaterial, grappleAnchorColor);
        return cachedGrappleAnchorMaterial;
    }

    private Sprite GetGrappleAnchorSprite()
    {
        if (cachedGrappleAnchorSprite != null)
            return cachedGrappleAnchorSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "RuntimeGrappleAnchor";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float ringOuter = size * 0.18f;
        float ringInner = size * 0.12f;
        float crossThickness = size * 0.045f;
        float diagonalThickness = size * 0.038f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                float radius = delta.magnitude;
                bool ring = radius <= ringOuter && radius >= ringInner;
                bool cross = Mathf.Abs(delta.x) <= crossThickness || Mathf.Abs(delta.y) <= crossThickness;
                bool diag = Mathf.Abs(delta.x - delta.y) <= diagonalThickness || Mathf.Abs(delta.x + delta.y) <= diagonalThickness;
                bool cutCenter = radius < size * 0.055f;
                texture.SetPixel(x, y, ((ring || cross || diag) && !cutCenter) ? Color.white : Color.clear);
            }
        }

        texture.Apply(false, true);
        cachedGrappleAnchorSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        cachedGrappleAnchorSprite.name = "RuntimeGrappleAnchorSprite";
        return cachedGrappleAnchorSprite;
    }

    private void TrackSafePosition()
    {
        if (!isGrounded || controller == null) return;
        safePositionTimer += Time.deltaTime;
        if (safePositionTimer < 0.15f) return;
        safePositionTimer = 0f;

        if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (IsValidStandingSurface(hit))
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
        StopGrapple();
        abyssRecoveredThisAirborneState = true;
        Vector3 targetPosition = lastSafePosition + Vector3.up * 1.2f;

        controller.enabled = false;
        transform.position = ResolveStandingPosition(targetPosition);
        controller.enabled = true;
        dashTimer = 0f;
        dashVelocity = Vector3.zero;
        isSliding = false;
        isSlamming = false;
        ClearWallState();
        wallRunTimer = wallRunDuration;
        wallDetachTimer = 0f;
        wallMovementBlend = 0f;
        wallReleaseBlendTimer = 0f;
        wallReleaseNormal = Vector3.zero;
        slideRequiresRelease = false;
        slideGroundGraceTimer = 0f;
        slideTimer = 0f;
        slamJumpTimer = 0f;
        slamJumpChainTimer = 0f;
        slamJumpChainCount = 0;
        if (controller != null)
        {
            controller.height = defaultHeight;
            controller.center = defaultControllerCenter;
        }
        velocity = new Vector3(0f, Mathf.Sqrt((CurrentJumpHeight + 1.4f) * -2f * gravity), 0f);
        momentum = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(momentum, Vector3.up), CurrentMoveSpeed * 0.45f);
        lastSafePosition = transform.position;
    }

    private void ClearMovementCarry(bool clearCooldowns)
    {
        StopGrapple();
        CacheControllerDefaults();
        velocity = Vector3.zero;
        momentum = Vector3.zero;
        dashVelocity = Vector3.zero;
        dashTimer = 0f;
        isSliding = false;
        isSlamming = false;
        ClearWallState();
        wallRunTimer = wallRunDuration;
        wallDetachTimer = 0f;
        wallMovementBlend = 0f;
        wallReleaseBlendTimer = 0f;
        wallReleaseNormal = Vector3.zero;
        slideRequiresRelease = false;
        slideGroundGraceTimer = 0f;
        slideTimer = 0f;
        slamJumpTimer = 0f;
        slamJumpChainTimer = 0f;
        slamJumpChainCount = 0;
        slideJumpChain = 0;
        slideJumpChainTimer = 0f;
        groundedHoldTimer = 0f;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        disableGroundCheckTimer = 0f;
        abyssRecoveredThisAirborneState = false;

        if (clearCooldowns)
        {
            dashCooldownTimer = 0f;
            dashCharges = MaxDashCharges;
            slideCooldownTimer = 0f;
            slideLockoutTimer = 0f;
        }

        if (controller != null)
        {
            controller.height = defaultHeight;
            controller.center = defaultControllerCenter;
        }
    }

    private void CacheControllerDefaults()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();
        if (controller != null && defaultHeight <= 0.01f)
        {
            defaultHeight = controller.height;
            defaultControllerCenter = controller.center;
        }
    }

    private void ApplyMovementTuningDefaults()
    {
        moveSpeed = Mathf.Clamp(moveSpeed, 9.5f, 10.8f);
        dashForce = Mathf.Clamp(dashForce, 22f, 28f);
        dashCooldown = Mathf.Clamp(dashCooldown, 0.55f, 0.95f);
        dashDuration = Mathf.Clamp(dashDuration, 0.18f, 0.24f);
        dashTargetSpeed = Mathf.Clamp(dashTargetSpeed, CurrentMoveSpeed * 2.1f, CurrentMoveSpeed * 2.55f);
        dashExitSpeed = Mathf.Clamp(dashExitSpeed, CurrentMoveSpeed * 0.62f, CurrentMoveSpeed * 0.88f);
        dashNoInputExitMultiplier = Mathf.Clamp(dashNoInputExitMultiplier, 0.2f, 0.72f);
        maxDashCharges = Mathf.Clamp(maxDashCharges, 1, 5);
        slamJumpWindow = Mathf.Clamp(slamJumpWindow, 0.08f, 0.5f);
        slamJumpVerticalBoost = Mathf.Clamp(slamJumpVerticalBoost, 1f, 1.6f);
        slamJumpChainWindow = Mathf.Clamp(slamJumpChainWindow, 0.4f, 2f);
        slamJumpChainHeightBonus = Mathf.Clamp(slamJumpChainHeightBonus, 0f, 0.12f);
        maxSlamJumpChain = Mathf.Clamp(maxSlamJumpChain, 0, 8);
        grappleMinRopeLength = Mathf.Clamp(grappleMinRopeLength, 0.65f, 1.25f);
        grappleRopeSlack = Mathf.Clamp(grappleRopeSlack, 0f, 0.35f);
        grappleConstraintElasticity = Mathf.Clamp(grappleConstraintElasticity, 0.5f, 1f);
        grappleConstraintMaxCorrection = Mathf.Clamp(grappleConstraintMaxCorrection, 0.25f, 1.5f);
        grapplePullAcceleration = Mathf.Clamp(grapplePullAcceleration, 45f, 160f);
        grappleInitialPullSpeed = Mathf.Clamp(grappleInitialPullSpeed, CurrentMoveSpeed * 0.65f, CurrentMoveSpeed * 1.4f);
        grapplePullSpeed = Mathf.Clamp(grapplePullSpeed, CurrentMoveSpeed * 2.2f, CurrentMoveSpeed * 3.5f);
        grapplePullRampDuration = Mathf.Clamp(grapplePullRampDuration, 0.08f, 0.5f);
        grappleReelSpeed = Mathf.Clamp(grappleReelSpeed, CurrentMoveSpeed, CurrentMoveSpeed * 2.2f);
        grappleAutoReleaseDistance = Mathf.Clamp(grappleAutoReleaseDistance, grappleMinRopeLength + 0.1f, 1.8f);
        grappleGravityScale = Mathf.Clamp(grappleGravityScale, 0.6f, 0.9f);
        wallSlideMaxFallSpeed = Mathf.Clamp(wallSlideMaxFallSpeed, 5.5f, 14f);
        wallSlideGravityScale = Mathf.Clamp(wallSlideGravityScale, 0.12f, 0.6f);
        wallRunGravityScale = Mathf.Clamp(wallRunGravityScale, 0.05f, 0.4f);
        wallRunMinSpeed = Mathf.Clamp(wallRunMinSpeed, CurrentMoveSpeed * 0.65f, CurrentMoveSpeed * 1.35f);
        wallRunTargetSpeed = Mathf.Clamp(wallRunTargetSpeed, CurrentMoveSpeed * 1.15f, CurrentMoveSpeed * 1.8f);
        wallRunAcceleration = Mathf.Clamp(wallRunAcceleration, 18f, 48f);
        wallRunSidePull = Mathf.Clamp(wallRunSidePull, 1.5f, 8f);
        wallRunMinInputDot = Mathf.Clamp(wallRunMinInputDot, -0.05f, 0.45f);
        wallRunDuration = Mathf.Clamp(wallRunDuration, 0.45f, 1.5f);
        wallJumpAwayForce = Mathf.Clamp(wallJumpAwayForce, CurrentMoveSpeed * 0.65f, CurrentMoveSpeed * 1.4f);
        wallJumpUpForce = Mathf.Clamp(wallJumpUpForce, 6f, 13f);
        wallDetachCooldown = Mathf.Clamp(wallDetachCooldown, 0.08f, 0.35f);
        wallRunCameraTilt = Mathf.Clamp(wallRunCameraTilt, 0f, 12f);
        wallTransitionDuration = Mathf.Clamp(wallTransitionDuration, 0.05f, 0.24f);
        wallReleaseCarryPreservation = Mathf.Clamp01(wallReleaseCarryPreservation);
        airAcceleration = Mathf.Max(airAcceleration, 15f);
        airTurnDamping = Mathf.Clamp(airTurnDamping, 0.8f, 2.4f);
        airNoInputBrake = Mathf.Clamp(airNoInputBrake, 0f, 0.35f);
        airControlImpulseScale = Mathf.Clamp(airControlImpulseScale, 0.05f, 0.45f);
        groundReleaseBrakeMultiplier = Mathf.Clamp(groundReleaseBrakeMultiplier, 0.45f, 1.4f);
        groundAcceleration = Mathf.Max(groundAcceleration, 24f);
        groundDeceleration = Mathf.Max(groundDeceleration, 38f);
        launchCarrySpeedLimit = Mathf.Clamp(launchCarrySpeedLimit, CurrentMoveSpeed * 1.85f, CurrentMoveSpeed * 2.35f);
        slideFriction = Mathf.Min(slideFriction, 0.34f);
        slideBaseSpeed = Mathf.Clamp(slideBaseSpeed, CurrentMoveSpeed * 1.45f, CurrentMoveSpeed * 1.5f);
        maxSlideStartSpeed = Mathf.Clamp(maxSlideStartSpeed, slideBaseSpeed * 1.08f, CurrentMoveSpeed * 1.8f);
        maxSlideJumpCarrySpeed = Mathf.Clamp(maxSlideJumpCarrySpeed, slideBaseSpeed * 1.22f, CurrentMoveSpeed * 2.25f);
        slideJumpSpeedMultiplier = Mathf.Clamp(slideJumpSpeedMultiplier, 1.12f, 1.38f);
        slideJumpVerticalMultiplier = Mathf.Clamp(slideJumpVerticalMultiplier, 1f, 1.18f);
        slideHoldSpeed = Mathf.Clamp(slideHoldSpeed, slideBaseSpeed, maxSlideStartSpeed);
        slideMinHoldSpeed = Mathf.Clamp(slideMinHoldSpeed, CurrentMoveSpeed * 1.12f, slideHoldSpeed);
        slideMinDuration = Mathf.Clamp(slideMinDuration, 0.55f, 0.95f);
        slideSteerStrength = Mathf.Max(slideSteerStrength, 18f);
        slideJumpChainWindow = Mathf.Clamp(slideJumpChainWindow, 0.8f, 3f);
        slideJumpChainBonus = Mathf.Clamp(slideJumpChainBonus, 0f, 0.2f);
        heldJumpLandingDelay = Mathf.Clamp(heldJumpLandingDelay, 0f, 0.35f);
        maxSpeedLimit = Mathf.Clamp(maxSpeedLimit, CurrentMoveSpeed * 2.05f, CurrentMoveSpeed * 2.35f);
        groundedStopSpeed = Mathf.Min(groundedStopSpeed, 0.2f);
    }

    private void ExitSlide(bool applyCooldown)
    {
        CacheControllerDefaults();
        if (!isSliding)
        {
            TryRestoreStandingController();
            return;
        }

        if (!TryRestoreStandingController())
        {
            isSliding = true;
            slideGroundGraceTimer = slideGroundGrace;
            slideTimer = Mathf.Max(slideTimer, 0.08f);
            momentum = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(momentum, Vector3.up), Mathf.Max(CurrentMoveSpeed * 0.45f, dashExitSpeed * 0.75f));
            return;
        }

        isSliding = false;
        slideGroundGraceTimer = 0f;
        slideTimer = 0f;
        momentum = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(momentum, Vector3.up), Mathf.Max(CurrentMoveSpeed * 0.58f, dashExitSpeed));
        if (applyCooldown)
            slideCooldownTimer = slideCooldown;
    }

    private bool TryRestoreStandingController()
    {
        if (controller == null)
            return true;

        float radius = Mathf.Max(0.05f, controller.radius * 0.92f);
        Vector3 center = transform.position + defaultControllerCenter;
        float halfHeight = Mathf.Max(radius, defaultHeight * 0.5f - radius);
        Vector3 bottom = center + Vector3.down * halfHeight;
        Vector3 top = center + Vector3.up * halfHeight;
        Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null) continue;
            if (overlap.transform == transform || overlap.transform.IsChildOf(transform)) continue;
            if (overlap.GetComponentInParent<PlayerController>() == this) continue;
            return false;
        }

        controller.height = defaultHeight;
        controller.center = defaultControllerCenter;
        return true;
    }

    private void FinishDash(Vector3 inputDir)
    {
        if (dashVelocity.sqrMagnitude <= 0.0001f)
        {
            dashTimer = 0f;
            return;
        }

        Vector3 carriedMomentum = Vector3.ProjectOnPlane(momentum.sqrMagnitude > 0.0001f ? momentum : dashVelocity, Vector3.up);
        if (carriedMomentum.sqrMagnitude <= 0.0001f)
        {
            dashVelocity = Vector3.zero;
            dashTimer = 0f;
            return;
        }

        float retainedSpeed = carriedMomentum.magnitude;
        if (inputDir.sqrMagnitude <= 0.0001f)
            retainedSpeed *= dashNoInputExitMultiplier;

        float maxExitSpeed = isGrounded
            ? Mathf.Max(dashExitSpeed, CurrentMoveSpeed * 1.08f)
            : Mathf.Max(dashExitSpeed, CurrentMoveSpeed * 1.2f);
        momentum = carriedMomentum.normalized * Mathf.Min(retainedSpeed, maxExitSpeed);
        dashVelocity = Vector3.zero;
        dashTimer = 0f;
    }

    private void RechargeDashCharges(float deltaTime)
    {
        if (dashCharges >= MaxDashCharges)
        {
            dashCharges = MaxDashCharges;
            dashCooldownTimer = 0f;
            return;
        }

        dashCooldownTimer -= Mathf.Max(0f, deltaTime);
        if (dashCooldownTimer > 0f) return;

        dashCharges = Mathf.Min(MaxDashCharges, dashCharges + 1);
        dashCooldownTimer = dashCharges < MaxDashCharges ? dashCooldown : 0f;
    }

    private void ApplyGroundMovement(Vector3 inputDir, float deltaTime)
    {
        deltaTime = Mathf.Max(0f, deltaTime);
        Vector3 horizontalMomentum = Vector3.ProjectOnPlane(momentum, Vector3.up);

        if (inputDir.sqrMagnitude <= 0.0001f)
        {
            float stopRate = groundDeceleration * 2.4f * groundReleaseBrakeMultiplier * deltaTime;
            horizontalMomentum = Vector3.MoveTowards(horizontalMomentum, Vector3.zero, stopRate);
            if (horizontalMomentum.magnitude <= groundedStopSpeed)
                horizontalMomentum = Vector3.zero;

            momentum = horizontalMomentum;
            return;
        }

        Vector3 desiredDir = inputDir.normalized;
        float parallelSpeed = Vector3.Dot(horizontalMomentum, desiredDir);
        Vector3 lateralVelocity = horizontalMomentum - desiredDir * parallelSpeed;

        float forwardAccel = parallelSpeed >= 0f
            ? groundAcceleration * 5.2f
            : groundAcceleration * 4f + groundDeceleration * 1.8f;
        parallelSpeed = Mathf.MoveTowards(parallelSpeed, CurrentMoveSpeed, forwardAccel * deltaTime);

        float lateralBrake = groundDeceleration * (parallelSpeed < 0f ? 1.8f : 1.25f);
        lateralVelocity = Vector3.MoveTowards(lateralVelocity, Vector3.zero, lateralBrake * deltaTime);

        horizontalMomentum = desiredDir * parallelSpeed + lateralVelocity;

        momentum = horizontalMomentum;
    }

    private float GetAirCarryLimit()
    {
        float baseCarry = Mathf.Max(launchCarrySpeedLimit + moveSpeedBonus, CurrentMoveSpeed * 1.05f);
        return slideJumpChain > 0 ? GetActiveSpeedLimit() : Mathf.Min(baseCarry, maxSpeedLimit);
    }

    private float GetActiveSpeedLimit()
    {
        return maxSpeedLimit * (1f + slideJumpChain * slideJumpChainBonus);
    }

    private float GetOverdriveAmount()
    {
        return Mathf.Clamp01((PlanarSpeed - overdriveSpeedThreshold) / Mathf.Max(8f, overdriveSpeedThreshold * 0.65f));
    }

    private void ApplyAirMovement(Vector3 inputDir, float deltaTime)
    {
        deltaTime = Mathf.Max(0f, deltaTime);
        Vector3 horizontalMomentum = Vector3.ProjectOnPlane(momentum, Vector3.up);
        if (wallReleaseBlendTimer > 0f && wallReleaseNormal.sqrMagnitude > 0.0001f)
        {
            float release01 = 1f - (wallReleaseBlendTimer / Mathf.Max(0.01f, wallTransitionDuration));
            Vector3 projected = Vector3.ProjectOnPlane(horizontalMomentum, wallReleaseNormal);
            horizontalMomentum = Vector3.Lerp(projected, horizontalMomentum, Mathf.Lerp(wallReleaseCarryPreservation, 1f, release01));
        }
        float airCarryLimit = GetAirCarryLimit();
        float currentSpeed = horizontalMomentum.magnitude;
        float speedSteer01 = Mathf.Clamp01((currentSpeed - CurrentMoveSpeed * 0.45f) / Mathf.Max(1f, CurrentMoveSpeed * 1.65f));
        float steerBoost = Mathf.Lerp(1.18f, 1.95f, speedSteer01);

        if (inputDir.sqrMagnitude <= 0.0001f)
        {
            float noInputBrake = airNoInputBrake * CurrentMoveSpeed * Mathf.Lerp(0.72f, 0.46f, speedSteer01) * deltaTime;
            if (noInputBrake > 0f)
                horizontalMomentum = Vector3.MoveTowards(horizontalMomentum, Vector3.zero, noInputBrake);

            momentum = Vector3.ClampMagnitude(horizontalMomentum, airCarryLimit);
            return;
        }

        Vector3 wishDir = inputDir.normalized;
        float speedAlongWish = Vector3.Dot(horizontalMomentum, wishDir);
        Vector3 alignedVelocity = wishDir * speedAlongWish;
        Vector3 lateralVelocity = horizontalMomentum - alignedVelocity;
        Vector3 oppositeVelocity = Vector3.zero;
        float startingSpeed = currentSpeed;

        if (speedAlongWish < 0f)
        {
            oppositeVelocity = alignedVelocity;
            alignedVelocity = Vector3.zero;
            speedAlongWish = 0f;
        }

        float counterTurnBrake = airTurnDamping * CurrentMoveSpeed * steerBoost * deltaTime;
        if (oppositeVelocity.sqrMagnitude > 0.0001f && counterTurnBrake > 0f)
            oppositeVelocity = Vector3.MoveTowards(oppositeVelocity, Vector3.zero, counterTurnBrake);

        float lateralSpeed = lateralVelocity.magnitude;
        if (lateralSpeed > 0.0001f && counterTurnBrake > 0f)
        {
            float lateralBrake = counterTurnBrake * Mathf.Lerp(0.16f, 0.28f, speedSteer01);
            lateralVelocity = Vector3.MoveTowards(lateralVelocity, Vector3.zero, lateralBrake);
        }

        float baseAirTargetSpeed = CurrentMoveSpeed * Mathf.Lerp(1.02f, 1.1f, speedSteer01);
        float carryTargetSpeed = startingSpeed < CurrentMoveSpeed
            ? Mathf.Max(baseAirTargetSpeed, startingSpeed + CurrentMoveSpeed * airControlImpulseScale * 0.16f)
            : startingSpeed + CurrentMoveSpeed * airControlImpulseScale * 0.05f;
        float targetWishSpeed = Mathf.Min(
            airCarryLimit,
            Mathf.Max(baseAirTargetSpeed, carryTargetSpeed));
        float addSpeed = Mathf.Max(0f, targetWishSpeed - speedAlongWish);
        if (addSpeed > 0f)
        {
            float accelScale = startingSpeed < CurrentMoveSpeed ? 1f : 0.42f;
            float accel = airAcceleration * CurrentMoveSpeed * airControlImpulseScale * steerBoost * accelScale * deltaTime;
            alignedVelocity += wishDir * Mathf.Min(accel, addSpeed);
        }

        horizontalMomentum = alignedVelocity + lateralVelocity + oppositeVelocity;
        float finalSpeedCap = startingSpeed < CurrentMoveSpeed
            ? Mathf.Min(airCarryLimit, Mathf.Max(baseAirTargetSpeed, startingSpeed + CurrentMoveSpeed * airControlImpulseScale * 0.22f))
            : Mathf.Min(airCarryLimit, startingSpeed + CurrentMoveSpeed * airControlImpulseScale * 0.08f);
        momentum = Vector3.ClampMagnitude(horizontalMomentum, finalSpeedCap);
    }

    private void ClipHorizontalMomentumAgainstWall()
    {
        if (Time.time - lastSideHitTime > 0.08f) return;
        if (!IsWallLikeNormal(lastSideHitNormal)) return;

        Vector3 normal = Vector3.ProjectOnPlane(lastSideHitNormal, Vector3.up);
        if (normal.sqrMagnitude <= 0.0001f) return;
        normal.Normalize();

        Vector3 horizontalMomentum = Vector3.ProjectOnPlane(momentum, Vector3.up);
        if (Vector3.Dot(horizontalMomentum, normal) < 0f)
            horizontalMomentum = Vector3.ProjectOnPlane(horizontalMomentum, normal);

        if (horizontalMomentum.magnitude < groundedStopSpeed)
            horizontalMomentum = Vector3.zero;

        momentum = horizontalMomentum;

        Vector3 horizontalDash = Vector3.ProjectOnPlane(dashVelocity, Vector3.up);
        if (Vector3.Dot(horizontalDash, normal) < 0f)
        {
            horizontalDash = Vector3.ProjectOnPlane(horizontalDash, normal);
            dashVelocity = horizontalDash + Vector3.up * dashVelocity.y;
        }
    }

    private void ResetSlideJumpChain()
    {
        slideJumpChain = 0;
        slideJumpChainTimer = 0f;
    }

    private static bool IsWallLikeNormal(Vector3 normal)
    {
        return Mathf.Abs(normal.y) <= 0.45f;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!IsWallLikeNormal(hit.normal)) return;
        lastSideHitNormal = hit.normal;
        lastSideHitTime = Time.time;
    }

#if UNITY_EDITOR
    public bool DebugHasActiveGrappleHook => activeGrappleHook != null;

    public void DebugSetGrappleHeldForTest(bool held)
    {
        debugGrappleHeldForTest = held;
        if (!held && (activeGrappleTarget.isValid || activeGrappleHook != null))
            StopGrapple();
    }

    public bool DebugLatchGrappleForTest(Collider targetCollider, Vector3 anchorPoint, Vector3 anchorNormal)
    {
        if (targetCollider == null)
            return false;

        Transform anchorTransform = targetCollider.attachedRigidbody != null
            ? targetCollider.attachedRigidbody.transform
            : targetCollider.transform;
        IGrappleMassTarget massTarget = targetCollider.GetComponentInParent<IGrappleMassTarget>();
        activeGrappleTarget = new GrappleTarget
        {
            isValid = true,
            point = anchorPoint,
            normal = anchorNormal,
            collider = targetCollider,
            anchorTransform = anchorTransform,
            localPoint = anchorTransform != null ? anchorTransform.InverseTransformPoint(anchorPoint) : anchorPoint,
            massTarget = massTarget,
            pullsPlayer = massTarget == null || massTarget.GrappleMassClass == GrappleMassClass.Heavy
        };
        activeGrappleRopeLength = Mathf.Clamp(
            Vector3.Distance(transform.position + Vector3.up * 0.9f, anchorPoint),
            grappleMinRopeLength,
            grappleRange);
        grappleActiveTimer = 0f;
        grappleTargetGraceTimer = grappleTargetGraceDuration;
        grappleState = GrappleState.Latched;
        debugGrappleHeldForTest = true;
        return true;
    }

    public void DebugStepGrappleMotionForTest(Vector3 inputDirection, float deltaTime)
    {
        UpdateGrappleMotion(inputDirection, Mathf.Max(0.0001f, deltaTime));
    }

    public void DebugSetActiveGrappleHookForTest(GrappleHookProjectile hook)
    {
        activeGrappleHook = hook;
        grappleState = hook != null ? GrappleState.Firing : GrappleState.Idle;
    }

    public void DebugStopGrappleForTest()
    {
        StopGrapple();
    }

    public void DebugPrepareMovementForTest()
    {
        CacheControllerDefaults();
        ApplyMovementTuningDefaults();
        ClearMovementCarry(true);
    }

    public void DebugSetMomentumForTest(Vector3 value)
    {
        momentum = value;
    }

    public void DebugSetDashForTest(Vector3 value, float timer)
    {
        dashVelocity = value;
        momentum = value;
        dashTimer = Mathf.Max(0f, timer);
    }

    public void DebugSetDashChargesForTest(int charges, float rechargeTimer)
    {
        dashCharges = Mathf.Clamp(charges, 0, MaxDashCharges);
        dashCooldownTimer = Mathf.Max(0f, rechargeTimer);
    }

    public void DebugRechargeDashForTest(float deltaTime)
    {
        RechargeDashCharges(deltaTime);
    }

    public void DebugApplyGroundMovementForTest(Vector3 inputDir, float deltaTime)
    {
        ApplyGroundMovement(inputDir, deltaTime);
    }

    public void DebugApplyAirMovementForTest(Vector3 inputDir, float deltaTime)
    {
        ApplyAirMovement(inputDir, deltaTime);
    }

    public void DebugClipMomentumAgainstWallForTest(Vector3 wallNormal)
    {
        lastSideHitNormal = wallNormal;
        lastSideHitTime = Time.time;
        ClipHorizontalMomentumAgainstWall();
    }

    public void DebugFinishDashForTest(Vector3 inputDir)
    {
        FinishDash(inputDir);
    }
#endif

    private void HandleDefeatAndRespawn()
    {
        HandleDeath();
    }

    private void HandleDeath()
    {
        if (isDead) return;
        StopGrapple();

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
            controller.center = defaultControllerCenter;
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
    }

    public void NotifyWeaponFired(bool heavy)
    {
        crosshairFireTimer = Mathf.Max(crosshairFireTimer, heavy ? 0.14f : 0.1f);
    }

    public void NotifyHeavyWeaponImpact(float amount)
    {
        weaponImpactShakeAmount = Mathf.Max(weaponImpactShakeAmount, Mathf.Clamp(amount, 0.02f, 0.24f));
        weaponImpactShakeTimer = Mathf.Max(weaponImpactShakeTimer, 0.18f);
        crosshairFireTimer = Mathf.Max(crosshairFireTimer, 0.2f);
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
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null)
            return;

        Transform root = canvas.transform.Find("VitalsHUD");
        if (root != null)
        {
            if (Application.isPlaying)
                Destroy(root.gameObject);
            else
                DestroyImmediate(root.gameObject);
        }

        if (healthText != null) healthText.gameObject.SetActive(false);
        if (currencyText != null) currencyText.gameObject.SetActive(false);
        if (healthBarFill != null && healthBarFill.transform.parent != null) healthBarFill.transform.parent.gameObject.SetActive(false);
        if (healthBarBack != null) healthBarBack.gameObject.SetActive(false);
        if (currencyPanel != null) currencyPanel.gameObject.SetActive(false);

        healthText = null;
        currencyText = null;
        healthBarFill = null;
        healthBarBack = null;
        currencyPanel = null;
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
        ProjectStructureUIRoot.ApplyDefaultFont(text);
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Left;
        text.color = color;
        text.text = string.Empty;
        return text;
    }

    private void TriggerDamageFeedback()
    {
        damageFlashTimer = damageFlashDuration;
        damageKickTimer = weaponKickDuration;
        EnsureDamageOverlay();
    }

    private void UpdateDamageFeedback()
    {
        if (weaponKickTimer > 0f)
            weaponKickTimer -= Time.deltaTime;
        if (damageKickTimer > 0f)
            damageKickTimer -= Time.deltaTime;
        if (damageFlashTimer > 0f)
            damageFlashTimer -= Time.deltaTime;

        if (damageFlashOverlay == null)
            EnsureDamageOverlay();
        if (damageFlashOverlay == null)
            return;

        float flash01 = Mathf.Clamp01(damageFlashTimer / Mathf.Max(0.01f, damageFlashDuration));
        float lowHealth01 = Mathf.InverseLerp(0.62f, 0.18f, Health01);
        float alpha = Mathf.Clamp01(flash01 * damageFlashColor.a + lowHealth01 * lowHealthVignetteAlpha);
        damageFlashOverlay.enabled = alpha > 0.002f;
        damageFlashOverlay.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, alpha);
    }

    private void EnsureDamageOverlay()
    {
        if (damageFlashOverlay != null)
            return;

        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find("DamageVignette");
        GameObject overlay = existing != null ? existing.gameObject : new GameObject("DamageVignette");
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsFirstSibling();

        RectTransform rect = overlay.GetComponent<RectTransform>();
        if (rect == null)
            rect = overlay.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        damageFlashOverlay = overlay.GetComponent<Image>();
        if (damageFlashOverlay == null)
            damageFlashOverlay = overlay.AddComponent<Image>();
        damageFlashOverlay.sprite = GetDamageVignetteSprite();
        damageFlashOverlay.raycastTarget = false;
        damageFlashOverlay.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, 0f);
        damageFlashOverlay.enabled = false;
    }

    private Sprite GetDamageVignetteSprite()
    {
        if (damageVignetteSprite != null)
            return damageVignetteSprite;

        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RuntimeDamageVignette"
        };
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDistance = center.magnitude;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance01 = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 1f, distance01));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        damageVignetteSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        damageVignetteSprite.name = "RuntimeDamageVignetteSprite";
        return damageVignetteSprite;
    }

    public void ApplySettings(float sensitivity, float desiredBaseFov, float masterVolume, bool persist = true)
    {
        mouseSensitivity = Mathf.Clamp(sensitivity, 0f, 200f);
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

        GameObject linesObject = new GameObject("OverdriveSpeedLines");
        linesObject.transform.SetParent(cameraTransform, false);
        linesObject.transform.localPosition = new Vector3(0f, 0f, 7f);
        linesObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        speedLines = linesObject.AddComponent<ParticleSystem>();

        var main = speedLines.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(18f, 30f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.028f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.68f, 0.9f, 1f, 0.08f), new Color(0.9f, 0.98f, 1f, 0.48f));
        main.maxParticles = 220;

        var emission = speedLines.emission;
        emission.rateOverTime = 0f;
        var shape = speedLines.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(12f, 7f, 0.1f);

        ParticleSystemRenderer renderer = speedLines.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.12f;
        renderer.lengthScale = 7f;
        renderer.material = CreateSpeedLineMaterial();
    }

    private Material CreateSpeedLineMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
        if (shader == null) return null;
        Material material = new Material(shader);
        material.name = "Runtime Speed Lines";
        material.color = new Color(0.72f, 0.92f, 1f, 0.72f);
        return material;
    }

    private void UpdateSpeedLines()
    {
        EnsureSpeedLines();
        if (speedLines == null) return;

        float amount = GetOverdriveAmount();
        var emission = speedLines.emission;
        emission.rateOverTime = Mathf.Lerp(0f, 150f, amount);
        if (amount > 0.01f)
        {
            if (!speedLines.isPlaying) speedLines.Play();
        }
        else if (speedLines.isPlaying)
        {
            speedLines.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void EnsureSlideParticles()
    {
        if (cameraTransform == null)
            return;

        if (runtimeSlideGroundFx == null)
            runtimeSlideGroundFx = CreateSlideParticleSystem("SlideGroundFX", transform, new Vector3(0f, 0.06f, -0.38f), false);
        if (runtimeSlideAirFx == null)
            runtimeSlideAirFx = CreateSlideParticleSystem("SlideAirFX", cameraTransform, new Vector3(0f, 0f, 0.95f), true);

        if (slideDust != null && slideDust != runtimeSlideGroundFx)
            slideDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private ParticleSystem CreateSlideParticleSystem(string name, Transform parent, Vector3 localPosition, bool airFx)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = airFx ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = airFx ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.startLifetime = airFx ? new ParticleSystem.MinMaxCurve(0.14f, 0.22f) : new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
        main.startSpeed = airFx ? new ParticleSystem.MinMaxCurve(8f, 14f) : new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
        main.startSize = airFx ? new ParticleSystem.MinMaxCurve(0.015f, 0.03f) : new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startColor = airFx
            ? new ParticleSystem.MinMaxGradient(new Color(0.74f, 0.92f, 1f, 0.08f), new Color(0.9f, 0.98f, 1f, 0.22f))
            : new ParticleSystem.MinMaxGradient(new Color(0.72f, 0.74f, 0.76f, 0.22f), new Color(0.92f, 0.94f, 0.96f, 0.44f));
        main.maxParticles = airFx ? 96 : 72;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = airFx ? ParticleSystemShapeType.Box : ParticleSystemShapeType.Cone;
        shape.scale = airFx ? new Vector3(0.58f, 0.4f, 0.02f) : new Vector3(0.18f, 0.06f, 0.18f);
        if (!airFx)
            shape.angle = 18f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = airFx;
        if (airFx)
        {
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(7f, 12f);
        }

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = airFx ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        renderer.velocityScale = airFx ? 0.1f : 0f;
        renderer.lengthScale = airFx ? 4.8f : 1f;
        renderer.material = CreateSpeedLineMaterial();

        return ps;
    }

    private void UpdateSlideParticles()
    {
        EnsureSlideParticles();
        Vector3 actualGroundVel = controller != null ? new Vector3(controller.velocity.x, 0f, controller.velocity.z) : Vector3.zero;
        bool groundSlide = isSliding && isGrounded && actualGroundVel.magnitude > 2f;
        bool airSlide = isSliding && !isGrounded && momentum.magnitude > 10f;

        if (groundSlide)
        {
            UpdateGroundSlideColor();
            if (runtimeSlideGroundFx != null && !runtimeSlideGroundFx.isPlaying)
                runtimeSlideGroundFx.Play();
        }
        else if (runtimeSlideGroundFx != null && runtimeSlideGroundFx.isPlaying)
        {
            runtimeSlideGroundFx.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        if (airSlide)
        {
            if (runtimeSlideAirFx != null)
            {
                var emission = runtimeSlideAirFx.emission;
                emission.rateOverTime = Mathf.Lerp(16f, 52f, Mathf.InverseLerp(10f, Mathf.Max(18f, slideHoldSpeed), momentum.magnitude));
                if (!runtimeSlideAirFx.isPlaying)
                    runtimeSlideAirFx.Play();
            }
        }
        else if (runtimeSlideAirFx != null && runtimeSlideAirFx.isPlaying)
        {
            runtimeSlideAirFx.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void UpdateGroundSlideColor()
    {
        if (runtimeSlideGroundFx == null)
            return;

        Color dustColor = new Color(0.84f, 0.86f, 0.88f, 0.42f);
        if (Physics.Raycast(transform.position + Vector3.up * 0.12f, Vector3.down, out RaycastHit hit, 2.5f))
        {
            Renderer floorRenderer = hit.collider != null ? hit.collider.GetComponent<Renderer>() : null;
            if (floorRenderer != null && floorRenderer.material != null)
            {
                dustColor = Color.Lerp(floorRenderer.material.color, new Color(0.88f, 0.9f, 0.92f), 0.52f);
                dustColor.a = 0.42f;
            }
        }

        var main = runtimeSlideGroundFx.main;
        main.startColor = dustColor;
        var emission = runtimeSlideGroundFx.emission;
        emission.rateOverTime = Mathf.Lerp(10f, 42f, Mathf.InverseLerp(2f, Mathf.Max(12f, slideHoldSpeed), PlanarSpeed));
    }

    private void StopSlideParticles(bool clear)
    {
        ParticleSystemStopBehavior stopBehavior = clear
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;
        if (runtimeSlideGroundFx != null)
            runtimeSlideGroundFx.Stop(true, stopBehavior);
        if (runtimeSlideAirFx != null)
            runtimeSlideAirFx.Stop(true, stopBehavior);
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

        if (grappleReticleRect == null || grappleReticleImage == null)
            needsSegments = true;

        if (!needsSegments) return;

        Image[] images = crosshair.GetComponentsInChildren<Image>(true);
        if (images.Length >= 4)
        {
            int segmentIndex = 0;
            for (int i = 0; i < images.Length && segmentIndex < 4; i++)
            {
                if (images[i] == null) continue;
                if (images[i].gameObject.name == "GrappleReticle")
                {
                    grappleReticleImage = images[i];
                    grappleReticleRect = images[i].rectTransform;
                    continue;
                }

                crosshairSegmentImages[segmentIndex] = images[i];
                crosshairSegmentRects[segmentIndex] = images[i].rectTransform;
                segmentIndex++;
            }
            if (segmentIndex >= 4 && grappleReticleImage != null && grappleReticleRect != null)
                return;
        }

        BuildCrosshairSegments(crosshair.transform);
    }

    private void BuildCrosshairSegments(Transform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child == null) continue;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

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

        GameObject ringObject = new GameObject("GrappleReticle");
        ringObject.transform.SetParent(root, false);
        grappleReticleRect = ringObject.AddComponent<RectTransform>();
        grappleReticleRect.anchorMin = new Vector2(0.5f, 0.5f);
        grappleReticleRect.anchorMax = new Vector2(0.5f, 0.5f);
        grappleReticleRect.pivot = new Vector2(0.5f, 0.5f);
        grappleReticleRect.sizeDelta = new Vector2(32f, 32f);
        grappleReticleImage = ringObject.AddComponent<Image>();
        grappleReticleImage.sprite = GetGrappleReticleSprite();
        Color hiddenColor = grappleReticleColor;
        hiddenColor.a = 0f;
        grappleReticleImage.color = hiddenColor;
        grappleReticleImage.raycastTarget = false;
        grappleReticleImage.enabled = true;
    }

    private void UpdateCrosshairVisual()
    {
        EnsureCrosshair();
        if (crosshairRect == null || crosshair == null || !crosshair.activeSelf) return;

        if (crosshairFireTimer > 0f)
            crosshairFireTimer -= Time.deltaTime;

        float fire01 = Mathf.Clamp01(crosshairFireTimer / 0.14f);
        bool focused = currentInteractable != null && !isUIActive && !isDead;
        bool hostile = IsAimingAtHostile();
        bool grappleReady = aimedGrappleTarget.isValid || grappleState == GrappleState.Firing || grappleState == GrappleState.Latched || grappleState == GrappleState.Retracting;

        Color targetColor = focused
            ? crosshairFocusColor
            : hostile ? crosshairHostileColor : crosshairBaseColor;

        float gap = 12f + fire01 * 1.6f;
        float length = 11f + fire01 * 0.8f;
        float thickness = 3f;
        float scale = 1f;
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

        UpdateGrappleReticleVisual(grappleReady, fire01);
    }

    private void UpdateGrappleReticleVisual(bool grappleReady, float fire01)
    {
        if (grappleReticleRect == null || grappleReticleImage == null)
            return;

        bool grappling = grappleState == GrappleState.Latched;
        bool hookTravelling = grappleState == GrappleState.Firing;
        bool retracting = grappleState == GrappleState.Retracting;
        float targetFade = grappleReady ? 1f : 0f;
        grappleReticleFade = Mathf.MoveTowards(grappleReticleFade, targetFade, Time.unscaledDeltaTime * (grappleReady ? 9f : 6f));
        grappleReticleImage.enabled = grappleReticleFade > 0.001f;
        if (!grappleReticleImage.enabled)
            return;

        float size = grappling ? 44f : hookTravelling ? 42f : aimedGrappleTarget.isAssisted ? 43f : 41f;
        float alpha = grappling ? 1f : retracting ? 0.68f : aimedGrappleTarget.isAssisted ? 0.9f : 0.76f;
        float pulse = grappling ? 0.98f : 1f + Mathf.Sin(Time.unscaledTime * 9f) * 0.028f;
        float fadeOutExpand = (1f - grappleReticleFade) * 10f;
        grappleReticleRect.sizeDelta = Vector2.one * ((size + fadeOutExpand) + fire01 * 1.6f) * pulse;
        float rotation = hookTravelling ? 45f + Time.unscaledTime * 120f : retracting ? 22f : 45f;
        grappleReticleRect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        Color ringColor = grappleReticleColor;
        if (aimedGrappleTarget.isAssisted && !grappling)
            ringColor = Color.Lerp(grappleReticleColor, grappleAnchorPulseColor, 0.24f);
        if (grappling)
            ringColor = Color.Lerp(grappleReticleColor, grappleAnchorColor, 0.3f);
        else if (retracting)
            ringColor = new Color(1f, 0.62f, 0.28f, ringColor.a);
        ringColor.a *= alpha * grappleReticleFade;
        grappleReticleImage.color = ringColor;
    }

    private Sprite GetGrappleReticleSprite()
    {
        if (cachedGrappleReticleSprite != null)
            return cachedGrappleReticleSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "RuntimeGrappleReticle";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float markerCenter = size * 0.34f;
        float markerLength = size * 0.11f;
        float stroke = size * 0.022f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                bool keepPixel =
                    InwardV(delta, new Vector2(0f, markerCenter), new Vector2(0f, -1f), markerLength, stroke) ||
                    InwardV(delta, new Vector2(markerCenter, 0f), new Vector2(-1f, 0f), markerLength, stroke) ||
                    InwardV(delta, new Vector2(0f, -markerCenter), new Vector2(0f, 1f), markerLength, stroke) ||
                    InwardV(delta, new Vector2(-markerCenter, 0f), new Vector2(1f, 0f), markerLength, stroke);
                texture.SetPixel(x, y, keepPixel ? Color.white : Color.clear);
            }
        }

        texture.Apply(false, true);
        cachedGrappleReticleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        cachedGrappleReticleSprite.name = "RuntimeGrappleReticleSprite";
        return cachedGrappleReticleSprite;
    }

    private static bool InwardV(Vector2 point, Vector2 center, Vector2 inwardDir, float length, float stroke)
    {
        inwardDir = inwardDir.normalized;
        Vector2 perpendicular = new Vector2(-inwardDir.y, inwardDir.x);
        Vector2 armA = (inwardDir + perpendicular * 0.8f).normalized;
        Vector2 armB = (inwardDir - perpendicular * 0.8f).normalized;
        return DistanceToSegment(point, center, center + armA * length) <= stroke ||
               DistanceToSegment(point, center, center + armB * length) <= stroke;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float denom = ab.sqrMagnitude;
        if (denom <= 0.0001f)
            return Vector2.Distance(point, a);
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / denom);
        Vector2 projected = a + ab * t;
        return Vector2.Distance(point, projected);
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
        currency = 0;
        PrepareForRunReset();
    }

    void HandleLook()
    {
        EnsureCameraReferences();
        if (cameraTransform == null || UnityEngine.InputSystem.Mouse.current == null)
            return;

        Vector2 mouseDelta = UnityEngine.InputSystem.Mouse.current.delta.ReadValue();

        float multiplier = mouseSensitivity / 100f;
        float mouseX = mouseDelta.x * multiplier;
        float mouseY = mouseDelta.y * multiplier;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        float sideTilt = -moveInputRaw.x * (isGrounded ? 0.9f : 0.45f);
        if (wallMovementBlend > 0f && activeWallNormal.sqrMagnitude > 0.0001f)
        {
            float wallSide = Mathf.Sign(Vector3.Dot(transform.right, activeWallNormal));
            sideTilt -= wallSide * wallRunCameraTilt * wallMovementBlend;
        }
        currentCameraTilt = Mathf.Lerp(currentCameraTilt, sideTilt, GetExponentialBlend(cameraTiltSmoothSpeed, Time.unscaledDeltaTime));
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, currentCameraTilt);

        transform.Rotate(Vector3.up * mouseX);
    }

    private void UpdateCameraPresentation(float deltaTime)
    {
        if (playerCamera != null)
        {
            float overdrive = GetOverdriveAmount();
            float targetFov = baseFOV + overdrive * overdriveFovBonus;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, GetExponentialBlend(cameraFovSmoothSpeed, deltaTime));
        }

        if (cameraTransform == null)
            return;

        Vector3 cameraTarget = isSliding
            ? baseCameraLocalPos + Vector3.down * slideCameraDrop
            : baseCameraLocalPos;

        Vector3 shakeTarget = Vector3.zero;
        float overdriveShake = GetOverdriveAmount() * overdriveShakeAmount;
        if (overdriveShake > 0.001f)
        {
            float time = Time.unscaledTime;
            shakeTarget += new Vector3(
                Mathf.PerlinNoise(time * 28f, 0.41f) - 0.5f,
                Mathf.PerlinNoise(0.73f, time * 31f) - 0.5f,
                0f) * overdriveShake * 2f;
        }

        if (weaponImpactShakeTimer > 0f)
        {
            weaponImpactShakeTimer = Mathf.Max(0f, weaponImpactShakeTimer - Time.unscaledDeltaTime);
            float impact = weaponImpactShakeAmount * Mathf.Clamp01(weaponImpactShakeTimer / 0.18f);
            float time = Time.unscaledTime * Mathf.Max(1f, impactShakeFrequency);
            shakeTarget += new Vector3(
                Mathf.PerlinNoise(11.17f, time) - 0.5f,
                Mathf.PerlinNoise(time * 1.13f, 23.91f) - 0.5f,
                0f) * impact * 2f;
        }

        currentCameraShakeOffset = Vector3.Lerp(
            currentCameraShakeOffset,
            shakeTarget,
            GetExponentialBlend(cameraShakeSmoothSpeed, Time.unscaledDeltaTime));
        cameraTarget += currentCameraShakeOffset;

        cameraTransform.localPosition = Vector3.SmoothDamp(
            cameraTransform.localPosition,
            cameraTarget,
            ref cameraLocalPositionVelocity,
            cameraPositionSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
    }

    private static float GetExponentialBlend(float speed, float deltaTime)
    {
        if (speed <= 0f)
            return 1f;

        return 1f - Mathf.Exp(-speed * deltaTime);
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

        if (TryFindStandingSurface(rayOrigin, rayDistance, out RaycastHit hit))
        {
            resolved.y = hit.point.y + StandingOffset;
        }
        else
        {
            resolved.y = referencePosition.y + StandingOffset;
        }

        return resolved;
    }

    private bool TryFindStandingSurface(Vector3 origin, float distance, out RaycastHit bestHit)
    {
        bestHit = default;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidStandingSurface(hit)) continue;

            bestHit = hit;
            return true;
        }

        return false;
    }

    private bool IsValidStandingSurface(RaycastHit hit)
    {
        Collider hitCollider = hit.collider;
        if (hitCollider == null || hitCollider.isTrigger) return false;
        if (hit.normal.y < 0.62f) return false;
        if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform)) return false;
        if (hitCollider.GetComponentInParent<PlayerController>() != null) return false;
        if (hitCollider.GetComponentInParent<BasicEnemyAI>() != null) return false;
        if (hitCollider.GetComponentInParent<Projectile>() != null) return false;
        if (hitCollider.GetComponentInParent<Interactable>() != null) return false;

        return true;
    }
}
