using UnityEngine;

// Inherits from our base Terminal class - this is perfect for your OOP assessment!
public class KeypadTerminal : Terminal
{
    [Header("Keypad Settings")]
    public string passcode = "1234";
    
    // We need to tell the terminal which UI controller to talk to
    public KeypadUI keypadUIManager;

    public override void TriggerPuzzle(PlayerController player)
    {
        if (keypadUIManager == null)
        {
            Debug.LogError("[KeypadTerminal] Keypad UI Manager is not assigned in the Inspector!");
            return;
        }

        keypadUIManager.OpenKeypad(this, player);
    }

    // Called by the KeypadUI when the player hits "SUBMIT"
    public bool SubmitPasscode(string inputCode, PlayerController player)
    {
        if (inputCode != passcode) return false;

        SolvePuzzle(player);
        return true;
    }
}
