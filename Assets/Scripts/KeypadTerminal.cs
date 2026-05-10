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
        Debug.Log($"[KeypadTerminal] UI Opened. Waiting for player to enter code.");
        
        if (keypadUIManager != null)
        {
            keypadUIManager.OpenKeypad(this, player);
        }
        else
        {
            Debug.LogError("[KeypadTerminal] Keypad UI Manager is not assigned in the Inspector!");
        }
    }

    // Called by the KeypadUI when the player hits "SUBMIT"
    public bool SubmitPasscode(string inputCode, PlayerController player)
    {
        if (inputCode == passcode)
        {
            Debug.Log("[KeypadTerminal] Passcode Accepted!");
            SolvePuzzle(player); // Use the parent class's solve method
            return true;
        }
        else
        {
            Debug.Log("[KeypadTerminal] Passcode Denied!");
            return false;
        }
    }
}
