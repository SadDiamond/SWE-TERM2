using UnityEngine;
using TMPro;

public class PlayerController : MonoBehaviour
{
    [Header("UI")]
    public GameObject crosshair;
    public TMP_Text interactionPromptText;

    [Header("Movement (Core)")]
    public float moveSpeed = 12f;
    public float groundAcceleration = 10f;
    public float groundDeceleration = 10f;
    public float airAcceleration = 2f; // Low value = Retains momentum in air
    public float gravity = -25f;
    public float jumpHeight = 2.5f;
    public int maxJumps = 2; 

    [Header("Movement (Dash)")]
    public float dashForce = 25f; // Pure momentum burst
    public float dashCooldown = 1f;

    [Header("Movement (Slide & Slam)")]
    public float slideBaseSpeed = 16f; // Just a bit faster than running (12)
    public float slideFriction = 2.5f; // How fast you slow down while sliding
    public float fallSpeedToSlideBoost = 0.5f; // Hitting the ground hard speeds up your slide
    public float slideHeight = 1f;
    public float slamSpeed = 40f; // How fast you plummet downwards
    private float defaultHeight;

    [Header("Movement (Limits)")]
    public float maxSpeedLimit = 36f; // Exactly 3x running speed

    [Header("FX & Polish")]
    public Camera playerCamera;
    public ParticleSystem slideDust; // Drag a particle system here!
    public float maxSpeedFOV = 100f; // Zoom out FOV when going fast!
    public float slideCameraDrop = 0.5f; // Dip the camera down when sliding
    private float baseFOV;
    private Vector3 baseCameraLocalPos;

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

    private Interactable currentInteractable;
    public float interactionDistance = 3f;
    public LayerMask interactableLayer;

    [Header("State")]
    public bool isUIActive = false; // True when a puzzle or terminal screen is open

    void Start()
    {
        controller = GetComponent<CharacterController>();
        defaultHeight = controller.height;
        Cursor.lockState = CursorLockMode.Locked;

        // Auto-assign camera if we forgot, and log its starting positions for FX
        if (playerCamera == null && cameraTransform != null) playerCamera = cameraTransform.GetComponent<Camera>();
        if (playerCamera != null) baseFOV = playerCamera.fieldOfView;
        if (cameraTransform != null) baseCameraLocalPos = cameraTransform.localPosition;

        if (interactionPromptText != null)
        {
            interactionPromptText.gameObject.SetActive(false); // Hide text at start
        }
    }

    void Update()
    {
        if (isUIActive) return; // Don't move or interact if a puzzle is open

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
            if (crosshair != null) crosshair.SetActive(true);
        }
    }

    // --- External Physics Methods ---
    public void LaunchPlayer(Vector3 launchVelocity)
    {
        // Cancel downward gravity immediately
        if (velocity.y < 0) velocity.y = 0;
        
        // Add vertical height
        velocity.y += launchVelocity.y;
        
        // Add forward/horizontal momentum
        momentum += new Vector3(launchVelocity.x, 0, launchVelocity.z);
        
        // Put player in falling state so they aren't stuck sliding etc
        isGrounded = false;
        isSliding = false;
        controller.height = defaultHeight;
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

    void HandleInteraction()
    {
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        Interactable interactable = null;

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactableLayer))
        {
            interactable = hit.collider.GetComponent<Interactable>();
        }

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

    void ClearFocus()
    {
        if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);
        if (currentInteractable == null) return;
        currentInteractable.OnLoseFocus();
        currentInteractable = null;
    }

    void ShowPrompt(string message)
    {
        if (interactionPromptText == null) return;
        interactionPromptText.text = message;
        interactionPromptText.gameObject.SetActive(true);
    }

    void HandleMovement()
    {
        isGrounded = controller.isGrounded;
        float fallSpeed = lastFrameVelocityY; // Capture before we reset it

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            jumpsRemaining = maxJumps;
            isSlamming = false; // FIX: Always reset slam state when touching the ground!
        }

        if (dashCooldownTimer > 0) dashCooldownTimer -= Time.deltaTime;

        // Wasd Input
        Vector2 input = new Vector2(
            UnityEngine.InputSystem.Keyboard.current.dKey.isPressed ? 1 :
            UnityEngine.InputSystem.Keyboard.current.aKey.isPressed ? -1 : 0,
            UnityEngine.InputSystem.Keyboard.current.wKey.isPressed ? 1 :
            UnityEngine.InputSystem.Keyboard.current.sKey.isPressed ? -1 : 0
        ).normalized;

        Vector3 inputDir = transform.right * input.x + transform.forward * input.y;

        // --- DASH LOGIC ---
        // Dash now ADDS a massive burst of momentum rather than overriding control
        if (UnityEngine.InputSystem.Keyboard.current.leftShiftKey.wasPressedThisFrame && dashCooldownTimer <= 0)
        {
            dashCooldownTimer = dashCooldown;
            Vector3 dashDir = inputDir.magnitude > 0.1f ? inputDir : transform.forward;
            
            // Add momentum (retaining previous velocity)
            momentum += dashDir * dashForce; 
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

        if (wantsToSlide && isGrounded && !isSliding)
        {
            isSliding = true;
            controller.height = slideHeight;

            // If we fell from a great height (or slammed), turn that fall speed into forward slide speed!
            float fallBoost = 0f;
            if (fallSpeed < -10f || isSlamming) 
            {
                // Multiplier makes massive slams give crazy slide speed
                fallBoost = Mathf.Abs(fallSpeed) * fallSpeedToSlideBoost;
            }
            isSlamming = false; // Reset slam state when hitting the ground

            float currentSpeed = momentum.magnitude;
            float newSpeed = Mathf.Max(slideBaseSpeed, currentSpeed + fallBoost); 
            
            // If we have no input direction, slide wherever we are looking
            Vector3 slideDir = inputDir.magnitude > 0.1f ? inputDir.normalized : transform.forward;
            momentum = slideDir * newSpeed;
        }
        else if ((!wantsToSlide || !isGrounded) && isSliding)
        {
            // Stop sliding 
            if (!wantsToSlide)
            {
                isSliding = false;
                controller.height = defaultHeight;
            }
        }

        // --- MOMENTUM & FRICTION LOGIC ---
        if (isGrounded)
        {
            if (isSliding)
            {
                // Sliding decays momentum very slowly
                momentum = Vector3.Lerp(momentum, Vector3.zero, slideFriction * Time.deltaTime);
            }
            else
            {
                // Normal running (snaps quickly to target speed)
                Vector3 targetVel = inputDir * moveSpeed;
                float accel = (inputDir.magnitude > 0) ? groundAcceleration : groundDeceleration;
                momentum = Vector3.Lerp(momentum, targetVel, accel * Time.deltaTime);
            }
        }
        else
        {
            // Air control (Decays smoothly, retains high speeds from dashes/slides)
            Vector3 targetVel = inputDir * moveSpeed;
            momentum = Vector3.Lerp(momentum, targetVel, airAcceleration * Time.deltaTime);
        }

        // --- JUMP LOGIC ---
        if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame && jumpsRemaining > 0)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpsRemaining--;

            if (isSliding)
            {
                // Slide jump! Stand up, but momentum is automatically preserved by the low airAcceleration!
                isSliding = false; 
                controller.height = defaultHeight;
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
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition, 
                new Vector3(baseCameraLocalPos.x, targetCamY, baseCameraLocalPos.z), 
                15f * Time.deltaTime
            );
        }

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