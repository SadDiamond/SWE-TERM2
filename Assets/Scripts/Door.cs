using UnityEngine;

public class Door : Interactable
{
    public bool isLocked = false;
    public bool isOpen = false;
    
    [Header("Door Access")]
    public int requiredAccessLevel = 0; // 0 means no keycard needed

    public float openAngle = 90f;
    public float openSpeed = 3f;

    private Quaternion closedRotation;
    private Quaternion openRotation;

    protected override void Start()
    {
        base.Start();
        closedRotation = transform.localRotation;
        // Apply the rotation locally
        openRotation = closedRotation * Quaternion.Euler(0, openAngle, 0);
        promptMessage = "Press E to open";
    }

    void Update()
    {
        // Smoothly rotate to open or closed position
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        
        transform.localRotation = Quaternion.RotateTowards(transform.localRotation, targetRotation, Time.deltaTime * openSpeed * 50f);

        // Debug visual to see if the transform is actually rotating, even if the mesh isn't
        Debug.DrawRay(transform.position, transform.forward * 2f, Color.red);
        Debug.DrawRay(transform.position, transform.right * 2f, Color.blue);
    }

    public override void OnInteract(PlayerController player)
    {
        Debug.Log($"[Door] OnInteract called on {gameObject.name}. isLocked: {isLocked}, isOpen (before): {isOpen}");
        if (!isLocked)
        {
            isOpen = !isOpen; // toggle open/closed
            Debug.Log($"[Door] Successfully toggled! isOpen is now: {isOpen}");
        }
        else
        {
            // Check if the player has a keycard with a high enough level
            if (player.HasKeycard(requiredAccessLevel))
            {
                isLocked = false;
                isOpen = true; // Open the door immediately
                Debug.Log($"[Door] Unlocked with correct keycard! isOpen is now: {isOpen}");
            }
            else
            {
                Debug.Log($"[Door] This door is locked. Requires access level {requiredAccessLevel}.");
            }
        }
    }
}