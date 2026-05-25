# SWE-TERM2

## Week 3 (B Week) - Focus: Preliminary Planning and Research

4-5-2026 - 6-5-2026
- Did the beginning of the documentation (defining the game and how it would work), also started doing some designs on the overall structure of the game (how the game would be navigated through)
8-5-2026
- Made player controller and interactable scripts and made a playground for testing

9-5-2026
- Debugged and fixed Player raycasting (added layered Raycasts) so the interactable system correctly detects objects.
- Fixed the Door rotation script to smoothly open and close using Quaternion.

10-5-2026
- Implemented core OOP structures: CollectibleItem (parent) and Keycard (child).
- Created Terminal puzzle foundation (Terminal base class, KeypadTerminal child).
- Programmed a 2D Keypad UI system that communicates with the terminal objects.

## Week 4 (A Week) - Focus: Identification of Classes, Objects, System Diagramming

11-5-2026
- Began cleanup pass on existing scripts now that the core loop is working. Stripped Debug.Log statements out of Interactable, Door, and Keycard so the console isn't flooded during playtesting.
- Reviewed the current class hierarchy (Interactable -> Door / CollectibleItem -> Keycard / Terminal -> KeypadTerminal, SwitchTerminal) in preparation for the class diagram.

13-5-2026
- Refactored PlayerController.HandleInteraction: flattened the nested if/else chain into early-return guards, and extracted ClearFocus() and ShowPrompt() helper methods so the Update path is easier to read. Also removed the unused mouseSmoothTime smoothing variables that were left over from earlier experiments.
- Cleaned up KeypadUI.SubmitCode and KeypadTerminal.SubmitPasscode using the same early-return pattern — demonstrates the same encapsulation principle being applied consistently across the codebase.

14-5-2026
- Added autoOpenConnectedDoor flag to Terminal so puzzles can either unlock-and-open the door, or just unlock it and leave opening to the player. Small change but it makes the Terminal → Door relationship more flexible for level design.
- Marked SwitchTerminal.TriggerPuzzle with a TODO — currently has no UI manager wired up, unlike KeypadTerminal/KeypadUI. Noted as a known gap for Week 5 programming.

15-5-2026

## Week 5 (B Week) - Focus: Programming, Asset Creation/Identification and Journaling

18-5-2026
20-5-2026
22-5-2026

## Week 6 (A Week) - Focus: Programming, Asset Creation/Identification and Journaling

25-5-2026
27-5-2026
28-5-2026
29-5-2026

## Week 7 (B Week) - Focus: Programming, Asset Creation/Identification and Journaling

1-6-2026
3-6-2026
5-6-2026

## Week 8 (A Week) - Focus: Programming, Journaling and Testing and Evaluating

8-6-2026
10-6-2026
11-6-2026
12-6-2026

## Week 9 (B Week) - Focus: Final Documentation, Creating Presentations and Testing and Evaluating

15-6-2026
17-6-2026
19-6-2026 - Assessment Submission Due

## Week 10 (A Week)

22-6-2026 - 25-6-2026 - Presentations
