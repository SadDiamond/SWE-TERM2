using UnityEngine;
using TMPro;

public class PlayerController : MonoBehaviour
{
    [Header("UI")]
    public GameObject crosshair;
    public TMP_Text interactionPromptText;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float gravity = -9.81f;

    [Header("Look")]
    public float mouseSensitivity = 100f;
    public Transform cameraTransform;

    [Header("Inventory")]
    public System.Collections.Generic.List<CollectibleItem> inventory = new System.Collections.Generic.List<CollectibleItem>();

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;

    private Interactable currentInteractable;
    public float interactionDistance = 3f;
    public LayerMask interactableLayer;

    [Header("State")]
    public bool isUIActive = false; // True when a puzzle or terminal screen is open

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;

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
            Cursor.lockState = CursorLockMode.None; // Free the mouse
            Cursor.visible = true;
            if (crosshair != null) crosshair.SetActive(false); // Hide crosshair when UI is open
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked; // Lock the mouse
            Cursor.visible = false;
            if (crosshair != null) crosshair.SetActive(true); // Show crosshair when playing
        }
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
        if (currentInteractable == null) return;
        currentInteractable.OnLoseFocus();
        currentInteractable = null;
        if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);
    }

    void ShowPrompt(string message)
    {
        if (interactionPromptText == null) return;
        interactionPromptText.text = message;
        interactionPromptText.gameObject.SetActive(true);
    }

    void HandleMovement()
    {
        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        // New Input System
        Vector2 input = new Vector2(
            UnityEngine.InputSystem.Keyboard.current.dKey.isPressed ? 1 :
            UnityEngine.InputSystem.Keyboard.current.aKey.isPressed ? -1 : 0,
            UnityEngine.InputSystem.Keyboard.current.wKey.isPressed ? 1 :
            UnityEngine.InputSystem.Keyboard.current.sKey.isPressed ? -1 : 0
        );

        Vector3 move = transform.right * input.x + transform.forward * input.y;
        controller.Move(move * moveSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
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