using UnityEngine;

// A hacking minigame where the player has to flip switches to match a target configuration
public class SwitchTerminal : Terminal
{
    [Header("Switch Puzzle Settings")]
    public bool[] targetSwitchStates = new bool[3] { true, false, true };
    private bool[] currentSwitchStates = new bool[3] { false, false, false };

    public SwitchUI switchUIManager;

    public int SwitchCount => currentSwitchStates.Length;
    public bool IsSwitchOn(int index) => currentSwitchStates[index];

    public override void TriggerPuzzle(PlayerController player)
    {
        if (switchUIManager == null)
        {
            Debug.LogError("[SwitchTerminal] Switch UI Manager is not assigned in the Inspector!");
            return;
        }

        switchUIManager.OpenSwitchPanel(this, player);
    }

    // Called by UI switch buttons
    public void ToggleSwitch(int switchIndex, PlayerController player)
    {
        if (switchIndex < 0 || switchIndex >= currentSwitchStates.Length) return;

        currentSwitchStates[switchIndex] = !currentSwitchStates[switchIndex];
        CheckWinCondition(player);
    }

    private void CheckWinCondition(PlayerController player)
    {
        for (int i = 0; i < targetSwitchStates.Length; i++)
        {
            if (currentSwitchStates[i] != targetSwitchStates[i]) return;
        }

        SolvePuzzle(player);
    }
}
