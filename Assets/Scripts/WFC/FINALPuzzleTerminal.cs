using System.Collections.Generic;
using UnityEngine;

public class CybergrindPuzzleTerminal : Terminal
{
    public enum ChallengeMode
    {
        Relay,
        Burst,
        Rhythm,
        Delay,
        DoubleTap,
        Hold,
        Alternating,
        Calibration,
        Pulse,
        Lockstep
    }

    [Header("Challenge")]
    public int sequenceIndex;
    public int terminalSeed;
    public ChallengeMode challengeMode = ChallengeMode.Relay;
    [Min(1)] public int requiredPresses = 3;
    [Min(0.05f)] public float timingWindow = 0.35f;
    [Min(0.05f)] public float requiredDelay = 0.75f;
    [Min(0.1f)] public float holdDuration = 1.35f;
    [Min(0.1f)] public float pulseSpeed = 2.5f;
    [Min(0.05f)] public float calibrationDelay = 0.75f;
    [Header("UI")]
    public CybergrindTerminalUI terminalUI;

    private PlayerController activePlayer;
    private bool puzzleOpen;
    private float puzzleTimer;
    private float lastPrimaryPressTimer;
    private float holdTimer;
    private float pulseTimer;
    private int progressStep;
    private int calibrationValue;
    private int calibrationTarget;
    private int lockstepIndex;
    private int alternatingStep;
    private bool lockstepExpectPrimary = true;
    private int[] lockstepSequence = new int[0];

    protected override void Start()
    {
        base.Start();
        ConfigureChallengeState();
        RefreshPromptFromState();
    }

    protected override void Update()
    {
        base.Update();
        if (!puzzleOpen || isSolved) return;

        float dt = Time.deltaTime;
        puzzleTimer += dt;
        lastPrimaryPressTimer += dt;
        pulseTimer += dt;

        if (challengeMode == ChallengeMode.Hold)
        {
            if (IsPrimaryHeld())
            {
                holdTimer += dt;
                if (holdTimer >= holdDuration)
                    CompletePuzzle();
            }
            else
            {
                holdTimer = 0f;
            }
        }

        if (terminalUI != null)
            terminalUI.RefreshFromTerminal(this);
    }

    public override void TriggerPuzzle(PlayerController player)
    {
        if (isSolved) return;
        if (!CanOpenThisTerminal())
        {
            UpdateBlockedPrompt();
            OnFocus();
            return;
        }

        activePlayer = player;
        ConfigureChallengeState();

        if (terminalUI == null)
        {
            terminalUI = CybergrindTerminalUI.GetOrCreate();
        }

        puzzleOpen = true;
        terminalUI.OpenTerminal(this, player);
    }

    public new void SolvePuzzle(PlayerController player)
    {
        base.SolvePuzzle(player);
        puzzleOpen = false;
        if (terminalUI != null)
            terminalUI.NotifySolved(this);
    }

    public void SubmitPrimaryAction()
    {
        if (!puzzleOpen || isSolved) return;

        switch (challengeMode)
        {
            case ChallengeMode.Relay:
                progressStep++;
                if (progressStep >= requiredPresses)
                    CompletePuzzle();
                break;

            case ChallengeMode.Burst:
                if (lastPrimaryPressTimer > timingWindow && progressStep > 0)
            ResetProgress("Too slow.");
                else
                    progressStep++;

                lastPrimaryPressTimer = 0f;
                if (progressStep >= requiredPresses)
                    CompletePuzzle();
                break;

            case ChallengeMode.Rhythm:
                if (IsBeatWindow())
                {
                    progressStep++;
                    if (progressStep >= requiredPresses)
                        CompletePuzzle();
                }
                else
                {
                    ResetProgress("Missed.");
                }
                break;

            case ChallengeMode.Delay:
                if (puzzleTimer >= requiredDelay)
                    CompletePuzzle();
                else
                    ResetProgress("Too early.");
                break;

            case ChallengeMode.DoubleTap:
                if (progressStep == 0)
                {
                    progressStep = 1;
                    lastPrimaryPressTimer = 0f;
                }
                else if (lastPrimaryPressTimer <= timingWindow)
                {
                    CompletePuzzle();
                }
                else
                {
                    ResetProgress("Too slow.");
                }
                break;

            case ChallengeMode.Hold:
                if (IsPrimaryHeld())
                {
                    holdTimer += Time.deltaTime;
                    if (holdTimer >= holdDuration)
                        CompletePuzzle();
                }
                else
                {
                    holdTimer = 0f;
                }
                break;

            case ChallengeMode.Alternating:
                if (alternatingStep % 2 == 0)
                {
                    alternatingStep++;
                    if (alternatingStep >= requiredPresses * 2)
                        CompletePuzzle();
                }
                else
                {
                    ResetProgress("Wrong input.");
                }
                break;

            case ChallengeMode.Calibration:
                calibrationValue = Mathf.Clamp(calibrationValue + 1, 0, 9);
                break;

            case ChallengeMode.Pulse:
                if (IsPulseWindow())
                {
                    progressStep++;
                    if (progressStep >= requiredPresses)
                        CompletePuzzle();
                }
                else
                {
                    ResetProgress("Missed.");
                }
                break;

            case ChallengeMode.Lockstep:
                SubmitLockstepAction(1);
                break;
        }

        RefreshPromptFromState();
        if (terminalUI != null)
            terminalUI.RefreshFromTerminal(this);
    }

    public void SubmitSecondaryAction()
    {
        if (!puzzleOpen || isSolved) return;

        switch (challengeMode)
        {
            case ChallengeMode.Alternating:
                if (alternatingStep % 2 == 1)
                {
                    alternatingStep++;
                    if (alternatingStep >= requiredPresses * 2)
                        CompletePuzzle();
                }
                else
                {
                    ResetProgress("Wrong input.");
                }
                break;

            case ChallengeMode.Calibration:
                calibrationValue = Mathf.Clamp(calibrationValue - 1, 0, 9);
                break;

            case ChallengeMode.Lockstep:
                SubmitLockstepAction(0);
                break;
        }

        RefreshPromptFromState();
        if (terminalUI != null)
            terminalUI.RefreshFromTerminal(this);
    }

    public void SubmitIncrease()
    {
        if (!puzzleOpen || isSolved) return;
        if (challengeMode != ChallengeMode.Calibration) return;
        calibrationValue = Mathf.Clamp(calibrationValue + 1, 0, 9);
        RefreshPromptFromState();
        if (terminalUI != null)
            terminalUI.RefreshFromTerminal(this);
    }

    public void SubmitDecrease()
    {
        if (!puzzleOpen || isSolved) return;
        if (challengeMode != ChallengeMode.Calibration) return;
        calibrationValue = Mathf.Clamp(calibrationValue - 1, 0, 9);
        RefreshPromptFromState();
        if (terminalUI != null)
            terminalUI.RefreshFromTerminal(this);
    }

    public void SubmitConfirm()
    {
        if (!puzzleOpen || isSolved) return;

        if (challengeMode == ChallengeMode.Calibration)
        {
            if (calibrationValue == calibrationTarget)
                CompletePuzzle();
            else
                ResetProgress("Wrong number.");
        }
        else if (challengeMode == ChallengeMode.Delay && puzzleTimer >= requiredDelay)
        {
            CompletePuzzle();
        }

        RefreshPromptFromState();
        if (terminalUI != null)
            terminalUI.RefreshFromTerminal(this);
    }

    public void CancelPuzzle()
    {
        puzzleOpen = false;
        holdTimer = 0f;
        progressStep = 0;
        if (activePlayer != null)
            activePlayer.ToggleUIMode(false);
        if (terminalUI != null)
            terminalUI.CloseTerminal(this);
    }

    public bool IsPuzzleOpen => puzzleOpen;

    public string GetTerminalTitle()
    {
        return $"TERMINAL {sequenceIndex + 1:00}";
    }

    public string GetModeLabel()
    {
        return challengeMode switch
        {
            ChallengeMode.DoubleTap => "DOUBLE TAP",
            ChallengeMode.Hold => "HOLD",
            ChallengeMode.Delay => "WAIT",
            ChallengeMode.Alternating => "SWITCH",
            _ => challengeMode.ToString().ToUpperInvariant()
        };
    }

    public string GetStatusLine()
    {
        if (isSolved) return "DONE";
        if (!puzzleOpen) return "READY";

        switch (challengeMode)
        {
            case ChallengeMode.Relay:
                return $"PRESS {progressStep}/{requiredPresses}";
            case ChallengeMode.Burst:
                return $"BURST {progressStep}/{requiredPresses}";
            case ChallengeMode.Rhythm:
                return $"RHYTHM {progressStep}/{requiredPresses}";
            case ChallengeMode.Delay:
                return $"WAIT {Mathf.Max(0f, requiredDelay - puzzleTimer):0.00}s";
            case ChallengeMode.DoubleTap:
                return progressStep == 0 ? "FIRST TAP" : "SECOND TAP";
            case ChallengeMode.Hold:
                return $"HOLD {holdTimer:0.00}/{holdDuration:0.00}s";
            case ChallengeMode.Alternating:
                return $"SWITCH {alternatingStep}/{requiredPresses * 2}";
            case ChallengeMode.Calibration:
                return $"MATCH {calibrationValue:0} / {calibrationTarget:0}";
            case ChallengeMode.Pulse:
                return $"PULSE {progressStep}/{requiredPresses}";
            case ChallengeMode.Lockstep:
                return $"SEQUENCE {lockstepIndex}/{lockstepSequence.Length}";
            default:
                return "ACTIVE";
        }
    }

    public string GetInstructionLine()
    {
        switch (challengeMode)
        {
            case ChallengeMode.Relay:
                return $"Press primary {requiredPresses} times.";
            case ChallengeMode.Burst:
                return $"Press quickly. Gap under {timingWindow:0.00}s.";
            case ChallengeMode.Rhythm:
                return "Press when the bar is bright.";
            case ChallengeMode.Delay:
                return $"Wait {requiredDelay:0.00}s, then confirm.";
            case ChallengeMode.DoubleTap:
                return "Tap twice quickly.";
            case ChallengeMode.Hold:
                return $"Hold E or SPACE for {holdDuration:0.00}s.";
            case ChallengeMode.Alternating:
                return "Alternate primary and secondary.";
            case ChallengeMode.Calibration:
                return "Match the number, then confirm.";
            case ChallengeMode.Pulse:
                return "Press during the bright pulse.";
            case ChallengeMode.Lockstep:
                return "Copy the sequence.";
            default:
                return overridePrompt;
        }
    }

    public string GetDetailLine()
    {
        if (isSolved) return "Exit progress updated.";

        string detail;
        switch (challengeMode)
        {
            case ChallengeMode.Relay:
                detail = $"{progressStep} of {requiredPresses}.";
                break;
            case ChallengeMode.Burst:
                detail = $"Gap {lastPrimaryPressTimer:0.00}s.";
                break;
            case ChallengeMode.Rhythm:
                detail = IsBeatWindow() ? "Press now." : "Wait.";
                break;
            case ChallengeMode.Delay:
                detail = puzzleTimer >= requiredDelay ? "Confirm now." : "Wait.";
                break;
            case ChallengeMode.DoubleTap:
                detail = progressStep == 0 ? "First tap ready." : $"Tap again within {timingWindow:0.00}s.";
                break;
            case ChallengeMode.Hold:
                detail = $"Held {holdTimer / Mathf.Max(0.01f, holdDuration):P0}.";
                break;
            case ChallengeMode.Alternating:
                detail = alternatingStep % 2 == 0 ? "Use primary." : "Use secondary.";
                break;
            case ChallengeMode.Calibration:
                detail = calibrationValue == calibrationTarget ? "Matched." : "Adjust the number.";
                break;
            case ChallengeMode.Pulse:
                detail = IsPulseWindow() ? "Press now." : "Wait.";
                break;
            case ChallengeMode.Lockstep:
                detail = lockstepSequence.Length == 0 ? "Waiting." : $"Step {lockstepIndex + 1}/{lockstepSequence.Length}.";
                break;
            default:
                detail = string.Empty;
                break;
        }

        return detail;
    }

    public float GetPressure01()
    {
        return 0f;
    }

    public string GetPrimaryActionLabel()
    {
        switch (challengeMode)
        {
            case ChallengeMode.Calibration:
                return "CONFIRM";
            case ChallengeMode.Hold:
                return "HOLD";
            default:
                return "PRIMARY";
        }
    }

    public string GetSecondaryActionLabel()
    {
        switch (challengeMode)
        {
            case ChallengeMode.Calibration:
                return "ALTERNATE";
            case ChallengeMode.Alternating:
                return "ALTERNATE";
            case ChallengeMode.Lockstep:
                return "SECONDARY";
            default:
                return "SECONDARY";
        }
    }

    public string GetSubmitLabel()
    {
        return challengeMode == ChallengeMode.Calibration || challengeMode == ChallengeMode.Delay ? "SUBMIT" : "CONFIRM";
    }

    public string GetHintLabel()
    {
        return $"{GetCurrentSectorLabel().ToUpperInvariant()} // SEED {terminalSeed}";
    }

    private string GetCurrentSectorLabel()
    {
        CybergrindArenaDirector director = FindAnyObjectByType<CybergrindArenaDirector>();
        if (director != null)
            return director.CurrentThemeLabel;

        CybergrindArenaGenerator generator = FindAnyObjectByType<CybergrindArenaGenerator>();
        if (generator != null)
            return generator.GetThemeLabel();

        return "Null Sector";
    }

    public float GetProgress01()
    {
        if (isSolved) return 1f;
        if (!puzzleOpen) return 0f;

        switch (challengeMode)
        {
            case ChallengeMode.Relay:
            case ChallengeMode.Burst:
            case ChallengeMode.Rhythm:
            case ChallengeMode.Pulse:
                return Mathf.Clamp01((float)progressStep / Mathf.Max(1, requiredPresses));
            case ChallengeMode.Delay:
                return Mathf.Clamp01(puzzleTimer / Mathf.Max(0.01f, requiredDelay));
            case ChallengeMode.DoubleTap:
                return Mathf.Clamp01((progressStep + (lastPrimaryPressTimer <= timingWindow ? 0.5f : 0f)) / 2f);
            case ChallengeMode.Hold:
                return Mathf.Clamp01(holdTimer / Mathf.Max(0.01f, holdDuration));
            case ChallengeMode.Alternating:
                return Mathf.Clamp01((float)alternatingStep / Mathf.Max(1, requiredPresses * 2));
            case ChallengeMode.Calibration:
                return Mathf.Clamp01(Mathf.Abs(calibrationTarget - calibrationValue) > 0 ? 1f - (Mathf.Abs(calibrationTarget - calibrationValue) / 9f) : 1f);
            case ChallengeMode.Lockstep:
                return Mathf.Clamp01((float)lockstepIndex / Mathf.Max(1, lockstepSequence.Length));
            default:
                return 0f;
        }
    }

    public bool CanSubmitNow()
    {
        if (isSolved) return true;
        return challengeMode == ChallengeMode.Calibration || challengeMode == ChallengeMode.Delay;
    }

    public bool UsesSecondaryAction()
    {
        return challengeMode == ChallengeMode.Alternating || challengeMode == ChallengeMode.Lockstep;
    }

    public bool UsesAdjustmentButtons()
    {
        return challengeMode == ChallengeMode.Calibration;
    }

    public bool UsesPrimaryAction()
    {
        return challengeMode != ChallengeMode.Calibration && challengeMode != ChallengeMode.Delay;
    }

    public bool IsTimingWindowOpen()
    {
        return challengeMode switch
        {
            ChallengeMode.Rhythm => IsBeatWindow(),
            ChallengeMode.Pulse => IsPulseWindow(),
            ChallengeMode.Delay => puzzleTimer >= requiredDelay,
            _ => false
        };
    }

    private void ConfigureChallengeState()
    {
        var rng = new System.Random(unchecked(terminalSeed ^ (sequenceIndex * 97) ^ (int)challengeMode * 53));
        requiredPresses = Mathf.Clamp(requiredPresses <= 1 ? 3 : requiredPresses, 2, 8);
        timingWindow = Mathf.Clamp(timingWindow, 0.12f, 0.6f);
        requiredDelay = Mathf.Clamp(requiredDelay, 0.2f, 2.5f);
        holdDuration = Mathf.Clamp(holdDuration, 0.35f, 3.5f);
        pulseSpeed = Mathf.Clamp(pulseSpeed, 1.2f, 6f);
        calibrationDelay = Mathf.Clamp(calibrationDelay, 0.2f, 2f);

        puzzleTimer = 0f;
        lastPrimaryPressTimer = 999f;
        holdTimer = 0f;
        pulseTimer = 0f;
        progressStep = 0;
        calibrationValue = rng.Next(0, 10);
        calibrationTarget = rng.Next(0, 10);
        lockstepIndex = 0;
        alternatingStep = 0;
        lockstepExpectPrimary = true;
        lockstepSequence = BuildLockstepSequence(rng);

        if (challengeMode == ChallengeMode.Delay)
            requiredDelay = Mathf.Max(requiredDelay, 0.5f);
    }

    private int[] BuildLockstepSequence(System.Random rng)
    {
        int length = Mathf.Clamp(3 + rng.Next(0, 3), 3, 6);
        int[] sequence = new int[length];
        for (int i = 0; i < sequence.Length; i++)
            sequence[i] = rng.Next(0, 2);
        return sequence;
    }

    private void SubmitLockstepAction(int action)
    {
        if (lockstepSequence == null || lockstepSequence.Length == 0)
        {
            ResetProgress("No sequence.");
            return;
        }

        if (lockstepSequence[lockstepIndex] != action)
        {
            ResetProgress("Wrong input.");
            return;
        }

        lockstepIndex++;
        lockstepExpectPrimary = !lockstepExpectPrimary;
        if (lockstepIndex >= lockstepSequence.Length)
            CompletePuzzle();
    }

    private bool CanOpenThisTerminal()
    {
        CybergrindPuzzleTerminal[] terminals = Object.FindObjectsByType<CybergrindPuzzleTerminal>();
        for (int i = 0; i < terminals.Length; i++)
        {
            CybergrindPuzzleTerminal terminal = terminals[i];
            if (terminal == null || terminal == this) continue;
            if (terminal.sequenceIndex < sequenceIndex && !terminal.isSolved)
                return false;
        }

        return true;
    }

    private void UpdateBlockedPrompt()
    {
        CybergrindPuzzleTerminal[] terminals = Object.FindObjectsByType<CybergrindPuzzleTerminal>();
        for (int i = 0; i < terminals.Length; i++)
        {
            CybergrindPuzzleTerminal terminal = terminals[i];
            if (terminal == null || terminal == this) continue;
            if (terminal.sequenceIndex < sequenceIndex && !terminal.isSolved)
            {
                overridePrompt = $"Finish terminal {terminal.sequenceIndex + 1} first";
                break;
            }
        }
        UpdatePrompt();
    }

    private void RefreshPromptFromState()
    {
        if (isSolved)
        {
            overridePrompt = "Terminal done";
            UpdatePrompt();
            return;
        }

        if (puzzleOpen)
            overridePrompt = $"{GetModeLabel()} - {GetInstructionLine()}";
        else if (!CanOpenThisTerminal())
            overridePrompt = GetBlockedPrompt();
        else
            overridePrompt = $"Use terminal {sequenceIndex + 1}";

        UpdatePrompt();
    }

    private string GetBlockedPrompt()
    {
        CybergrindPuzzleTerminal[] terminals = FindObjectsByType<CybergrindPuzzleTerminal>(FindObjectsInactive.Exclude);
        for (int i = 0; i < terminals.Length; i++)
        {
            CybergrindPuzzleTerminal terminal = terminals[i];
            if (terminal == null || terminal == this) continue;
            if (terminal.sequenceIndex < sequenceIndex && !terminal.isSolved)
                return $"Finish terminal {terminal.sequenceIndex + 1} first";
        }

        return $"Use terminal {sequenceIndex + 1}";
    }

    private bool IsBeatWindow()
    {
        float cycle = Mathf.Repeat(pulseTimer * pulseSpeed, 1f);
        return cycle > 0.30f && cycle < 0.82f;
    }

    private bool IsPulseWindow()
    {
        float phase = Mathf.Repeat(pulseTimer * pulseSpeed, 1f);
        return phase > 0.20f && phase < 0.86f;
    }

    private void ResetProgress(string reason)
    {
        progressStep = 0;
        holdTimer = 0f;
        alternatingStep = 0;
        lockstepIndex = 0;
        lastPrimaryPressTimer = 999f;
        if (challengeMode == ChallengeMode.Calibration)
            calibrationValue = Mathf.Clamp(calibrationValue, 0, 9);

        if (terminalUI != null)
            terminalUI.SetTransientMessage(reason);
    }

    private void CompletePuzzle()
    {
        if (isSolved) return;

        CybergrindRunState.GetOrCreate().RegisterTerminalSolved();
        SolvePuzzle(activePlayer);
        if (terminalUI != null)
            terminalUI.RefreshFromTerminal(this);
    }

    private bool IsPrimaryHeld()
    {
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        return keyboard != null && (keyboard.spaceKey.isPressed || keyboard.eKey.isPressed);
    }
}
