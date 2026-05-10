using UnityEngine;

// A hacking minigame where the player has to flip switches to match a target configuration
public class SwitchTerminal : Terminal
{
    [Header("Switch Puzzle Settings")]
    public bool[] targetSwitchStates = new bool[3] { true, false, true };
    private bool[] currentSwitchStates = new bool[3] { false, false, false };

    public override void TriggerPuzzle(PlayerController player)
    {
        Debug.Log("[SwitchTerminal] UI Opened. Player must configure power routing switches.");
    }

    // Called by UI switch buttons
    public void ToggleSwitch(int switchIndex, PlayerController player)
    {
        if (switchIndex >= 0 && switchIndex < currentSwitchStates.Length)
        {
            currentSwitchStates[switchIndex] = !currentSwitchStates[switchIndex];
            Debug.Log($"[SwitchTerminal] Switch {switchIndex} toggled to {currentSwitchStates[switchIndex]}");
            
            CheckWinCondition(player);
        }
    }

    private void CheckWinCondition(PlayerController player)
    {
        for (int i = 0; i < targetSwitchStates.Length; i++)
        {
            if (currentSwitchStates[i] != targetSwitchStates[i])
            {
                return; // At least one switch is wrong, abort check
            }
        }

        // If loop completes, all switches match!
        Debug.Log("[SwitchTerminal] Power rerouted successfully!");
        SolvePuzzle(player); // Uses parent method
    }
}
