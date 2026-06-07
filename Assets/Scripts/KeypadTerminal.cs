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
            // Try to find a KeypadUI in the scene automatically
            keypadUIManager = FindAnyObjectByType<KeypadUI>();
            if (keypadUIManager == null)
            {
                GameObject uiObject = new GameObject("RuntimeKeypadUI");
                keypadUIManager = uiObject.AddComponent<KeypadUI>();
            }
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

    public void ConfigurePasscode(string newPasscode)
    {
        passcode = string.IsNullOrEmpty(newPasscode) ? "1234" : newPasscode;
    }
}
