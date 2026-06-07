using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

// Sister class to KeypadUI. Same pattern: the Terminal owns puzzle rules,
// this UI owns presentation and forwards player input back to the terminal.
public class SwitchUI : MonoBehaviour
{
    private static readonly Color SuccessColor = new Color(0.290f, 0.871f, 0.502f); // #4ADE80
    private static readonly Color DefaultTextColor = new Color(0.910f, 0.910f, 0.910f); // #E8E8E8

    [Header("UI Elements")]
    public TMP_Text statusText;
    public Image[] switchIndicators;

    [Header("Colors")]
    public Color onColor = Color.green;
    public Color offColor = Color.red;

    private SwitchTerminal activeTerminal;
    private PlayerController interactingPlayer;
    private Canvas runtimeCanvas;

    public void OpenSwitchPanel(SwitchTerminal terminal, PlayerController player)
    {
        activeTerminal = terminal;
        interactingPlayer = player;
        EnsureRuntimeUI();

        gameObject.SetActive(true);
        player.ToggleUIMode(true);

        UpdateVisuals();
    }

    public void CloseSwitchPanel()
    {
        gameObject.SetActive(false);
        if (interactingPlayer != null) interactingPlayer.ToggleUIMode(false);
    }

    private void Update()
    {
        if (!gameObject.activeInHierarchy || activeTerminal == null) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseSwitchPanel();
            return;
        }

        for (int i = 0; i < activeTerminal.SwitchCount && i < 9; i++)
        {
            Key key = (Key)((int)Key.Digit1 + i);
            if (Keyboard.current[key].wasPressedThisFrame)
                OnSwitchClicked(i);
        }

        UpdateVisuals();
    }

    // Wire each switch button's OnClick to this with its index as the int argument.
    public void OnSwitchClicked(int index)
    {
        if (activeTerminal == null) return;

        activeTerminal.ToggleSwitch(index, interactingPlayer);
        UpdateVisuals();

        if (activeTerminal.isSolved)
        {
            if (statusText != null)
            {
                statusText.color = SuccessColor;
                statusText.text = "POWER REROUTED";
            }
            Invoke(nameof(CloseSwitchPanel), 1f);
        }
    }

    private void UpdateVisuals()
    {
        if (activeTerminal == null) return;

        if (switchIndicators != null)
        {
            for (int i = 0; i < switchIndicators.Length && i < activeTerminal.SwitchCount; i++)
            {
                switchIndicators[i].color = activeTerminal.IsSwitchOn(i) ? onColor : offColor;
            }
        }

        if (statusText != null && !activeTerminal.isSolved)
        {
            statusText.color = DefaultTextColor;
            string pattern = "";
            for (int i = 0; i < activeTerminal.SwitchCount; i++)
                pattern += activeTerminal.IsSwitchOn(i) ? "[ON] " : "[OFF] ";
            statusText.text = "REROUTE POWER\n" + pattern.TrimEnd();
        }
    }

    private void EnsureRuntimeUI()
    {
        if (runtimeCanvas != null) return;

        GameObject canvasObject = new GameObject("RuntimeSwitchCanvas");
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

        GameObject textObject = new GameObject("Status");
        textObject.transform.SetParent(panel.transform, false);
        statusText = textObject.AddComponent<TextMeshProUGUI>();
        statusText.fontSize = 36f;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.color = DefaultTextColor;
        statusText.enableAutoSizing = true;
        statusText.font = TMP_Settings.defaultFontAsset;
        RectTransform textRect = statusText.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.15f, 0.35f);
        textRect.anchorMax = new Vector2(0.85f, 0.78f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
}
