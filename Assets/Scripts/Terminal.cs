using UnityEngine;
using UnityEngine.Events;

public class Terminal : Interactable
{
    [Header("Terminal State")]
    public bool isSolved = false;
    public string overridePrompt = "Press E to hack terminal";

    [Header("Connected Systems")]
    public Door connectedDoor; // The door this terminal will unlock
    
    // UnityEvents allow you to drag-and-drop anything in the Inspector
    // Example: turning off lights, playing sounds, etc.
    public UnityEvent onPuzzleSolved; 

    protected override void Start()
    {
        base.Start(); // Let the base class set up the outline generator
        UpdatePrompt();
    }

    public override void OnInteract(PlayerController player)
    {
        if (isSolved)
        {
            Debug.Log("[Terminal] System already overriden.");
            return;
        }

        Debug.Log("[Terminal] Accessing mainframe...");
        TriggerPuzzle(player);
    }

    // Virtual method so child classes (Keypad, Riddle, etc.) can override it with their own puzzle logic
    public virtual void TriggerPuzzle(PlayerController player)
    {
        // Base terminal behaviour: instantly solves if no puzzle is defined.
        Debug.Log("[Terminal] No specific puzzle set. Instantly bypassing security.");
        SolvePuzzle(player);
    }

    // Called when the UI puzzle is completed
    public void SolvePuzzle(PlayerController player)
    {
        isSolved = true;
        UpdatePrompt();
        Debug.Log("[Terminal] Hack successful. Override accepted.");

        // 1. Unlock the connected door remotely
        if (connectedDoor != null)
        {
            connectedDoor.isLocked = false;
            if (!connectedDoor.isOpen)
            {
                connectedDoor.isOpen = true; // Auto-open it for dramatic effect
            }
            Debug.Log($"[Terminal] Unlocking remote door: {connectedDoor.gameObject.name}");
        }

        // 2. Fire off any extra custom events we set in the Unity Inspector
        onPuzzleSolved?.Invoke();
    }

    // Update the text that the player sees when looking at it
    public override void OnFocus()
    {
        UpdatePrompt();
        base.OnFocus();
    }

    private void UpdatePrompt()
    {
        promptMessage = isSolved ? "Terminal Offline" : overridePrompt;
    }
}
