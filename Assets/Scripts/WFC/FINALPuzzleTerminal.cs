using UnityEngine;

public class CybergrindPuzzleTerminal : Terminal
{
    public int sequenceIndex;

    public override void TriggerPuzzle(PlayerController player)
    {
        CybergrindPuzzleTerminal[] terminals = FindObjectsByType<CybergrindPuzzleTerminal>();
        for (int i = 0; i < terminals.Length; i++)
        {
            CybergrindPuzzleTerminal terminal = terminals[i];
            if (terminal == null || terminal == this) continue;
            if (terminal.sequenceIndex < sequenceIndex && !terminal.isSolved)
            {
                overridePrompt = $"Solve node {terminal.sequenceIndex + 1} first";
                OnFocus();
                return;
            }
        }

        SolvePuzzle(player);
    }
}
