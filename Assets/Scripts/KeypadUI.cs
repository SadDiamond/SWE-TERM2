using UnityEngine;
using UnityEngine.UI;
using TMPro; // Standard for text in Unity now

public class KeypadUI : MonoBehaviour
{
    private static readonly Color SuccessColor = new Color(0.290f, 0.871f, 0.502f); // #4ADE80
    private static readonly Color DangerColor = new Color(0.937f, 0.267f, 0.267f);  // #EF4444
    private static readonly Color DefaultTextColor = new Color(0.910f, 0.910f, 0.910f); // #E8E8E8

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
        if (activeTerminal == null) return;

        bool success = activeTerminal.SubmitPasscode(currentInput, interactingPlayer);

        if (success)
        {
            displayScreen.color = SuccessColor;
            displayScreen.text = "ACCEPTED";
            Invoke(nameof(CloseKeypad), 1f);
        }
        else
        {
            displayScreen.color = DangerColor;
            displayScreen.text = "DENIED";
            currentInput = "";
            Invoke(nameof(UpdateScreen), 1f);
        }
    }

    private void UpdateScreen()
    {
        displayScreen.color = DefaultTextColor;
        displayScreen.text = currentInput == "" ? "ENTER CODE" : currentInput;
    }
}
