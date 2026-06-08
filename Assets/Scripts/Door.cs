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

    protected override void Update()
    {
        base.Update();
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;

        // Skip work when already at the target rotation
        if (transform.localRotation == targetRotation) return;

        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            targetRotation,
            Time.deltaTime * openSpeed * 50f);
    }

    public override void OnInteract(PlayerController player)
    {
        if (!isLocked)
        {
            isOpen = !isOpen;
            return;
        }

        if (player.HasKeycard(requiredAccessLevel))
        {
            isLocked = false;
            isOpen = true;
        }
        else
        {
            Debug.Log($"[Door] Locked. Requires access level {requiredAccessLevel}.");
        }
    }
}
