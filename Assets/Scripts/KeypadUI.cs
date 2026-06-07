using UnityEngine;
using UnityEngine.UI;
using TMPro; // Standard for text in Unity now
using UnityEngine.InputSystem;

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
    private Canvas runtimeCanvas;

    // Called by the KeypadTerminal to turn on the screen
    public void OpenKeypad(KeypadTerminal terminal, PlayerController player)
    {
        activeTerminal = terminal;
        interactingPlayer = player;
        EnsureRuntimeUI();
        
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

    private void Update()
    {
        if (!gameObject.activeInHierarchy || activeTerminal == null) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseKeypad();
            return;
        }

        for (int i = 0; i <= 9; i++)
        {
            Key key = (Key)((int)Key.Digit0 + i);
            if (Keyboard.current[key].wasPressedThisFrame)
                AddNumber(i.ToString());
        }

        if (Keyboard.current.backspaceKey.wasPressedThisFrame)
            currentInput = currentInput.Length > 0 ? currentInput.Substring(0, currentInput.Length - 1) : "";

        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            SubmitCode();

        UpdateScreen();
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
        EnsureRuntimeUI();
        if (displayScreen == null) return;

        displayScreen.color = DefaultTextColor;
        displayScreen.text = currentInput == "" ? "ENTER CODE" : currentInput;
    }

    private void EnsureRuntimeUI()
    {
        if (displayScreen != null && runtimeCanvas != null) return;

        if (runtimeCanvas == null)
        {
            GameObject canvasObject = new GameObject("RuntimeKeypadCanvas");
            canvasObject.transform.SetParent(transform, false);
            runtimeCanvas = canvasObject.AddComponent<Canvas>();
            runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvasObject.transform, false);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.82f);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            GameObject textObject = new GameObject("Display");
            textObject.transform.SetParent(panel.transform, false);
            displayScreen = textObject.AddComponent<TextMeshProUGUI>();
            displayScreen.fontSize = 48f;
            displayScreen.alignment = TextAlignmentOptions.Center;
            displayScreen.color = DefaultTextColor;
            displayScreen.enableAutoSizing = true;
            displayScreen.font = TMP_Settings.defaultFontAsset;
            RectTransform textRect = displayScreen.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.1f, 0.35f);
            textRect.anchorMax = new Vector2(0.9f, 0.75f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }
    }
}
