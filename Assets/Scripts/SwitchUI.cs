using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    public void OpenSwitchPanel(SwitchTerminal terminal, PlayerController player)
    {
        activeTerminal = terminal;
        interactingPlayer = player;

        gameObject.SetActive(true);
        player.ToggleUIMode(true);

        UpdateVisuals();
    }

    public void CloseSwitchPanel()
    {
        gameObject.SetActive(false);
        if (interactingPlayer != null) interactingPlayer.ToggleUIMode(false);
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
            statusText.text = "REROUTE POWER";
        }
    }
}
