using UnityEngine;
using UnityEngine.UI;
using TMPro; // Standard for text in Unity now

public class KeypadUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text displayScreen;

    private string currentInput = "";
    private KeypadTerminal activeTerminal;
    private PlayerController interactingPlayer;

    // Called by the KeypadTerminal to turn on the screen
    public void OpenKeypad(KeypadTerminal terminal, PlayerController player)
    {
        activeTerminal = terminal;
        interactingPlayer = player;
        
        currentInput = "";
        UpdateScreen();

        gameObject.SetActive(true); // Turn on this Canvas
        player.ToggleUIMode(true);  // Freeze player and show mouse
    }

    // Give this to a "Close" button, or call it when solved
    public void CloseKeypad()
    {
        currentInput = "";
        UpdateScreen();

        gameObject.SetActive(false); // Turn off this Canvas
        
        if (interactingPlayer != null)
        {
            interactingPlayer.ToggleUIMode(false); // Unfreeze player
        }
    }

    // Call this from 0-9 buttons
    public void AddNumber(string num)
    {
        // Limit standard passcodes to 4 digits
        if (currentInput.Length < 4)
        {
            currentInput += num;
            UpdateScreen();
        }
    }

    // Call this from a "Clear" button
    public void ClearInput()
    {
        currentInput = "";
        UpdateScreen();
    }

    // Call this from a "Submit" or "Enter" button
    public void SubmitCode()
    {
        if (activeTerminal != null)
        {
            bool success = activeTerminal.SubmitPasscode(currentInput, interactingPlayer);
            
            if (success)
            {
                displayScreen.color = Color.green;
                displayScreen.text = "ACCEPTED";
                // Close automatically after 1 second
                Invoke(nameof(CloseKeypad), 1f); 
            }
            else
            {
                displayScreen.color = Color.red;
                displayScreen.text = "DENIED";
                currentInput = "";
                // Reset color back to white after 1 sec
                Invoke(nameof(UpdateScreen), 1f); 
            }
        }
    }

    private void UpdateScreen()
    {
        displayScreen.color = Color.white;
        displayScreen.text = currentInput == "" ? "ENTER CODE" : currentInput;
    }
}
