using UnityEngine;
using UnityEngine.Events;

public class Terminal : Interactable
{
    [Header("Terminal State")]
    public bool isSolved = false;
    public string overridePrompt = "Press E to hack terminal";

    [Header("Connected Systems")]
    public Door connectedDoor; // The door this terminal will unlock
    public bool autoOpenConnectedDoor = true; // If false, only unlocks — player still has to open it

    // UnityEvents allow you to drag-and-drop anything in the Inspector
    // Example: turning off lights, playing sounds, etc.
    public UnityEvent onPuzzleSolved;

    protected override void Start()
    {
        base.Start();
        UpdatePrompt();
    }

    public override void OnInteract(PlayerController player)
    {
        if (isSolved) return;
        TriggerPuzzle(player);
    }

    // Virtual method so child classes (Keypad, Riddle, etc.) can override it with their own puzzle logic
    public virtual void TriggerPuzzle(PlayerController player)
    {
        // Base terminal behaviour: instantly solves if no puzzle is defined.
        SolvePuzzle(player);
    }

    // Called when the UI puzzle is completed
    public void SolvePuzzle(PlayerController player)
    {
        isSolved = true;
        UpdatePrompt();

        if (connectedDoor != null)
        {
            connectedDoor.isLocked = false;
            if (autoOpenConnectedDoor) connectedDoor.isOpen = true;
        }

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
