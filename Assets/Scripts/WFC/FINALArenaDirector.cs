using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CybergrindArenaDirector : MonoBehaviour
{
    public CybergrindArenaGenerator generator;
    public int floor = 1;
    [Min(1)] public int combatFloorsBeforeShop = 2;
    [Min(1)] public int combatFloorsAfterShop = 2;
    public float exitActivationRange = 5f;
    public Transform player;
    public CybergrindRunState runState;
    public CybergrindTransitionController transitionController;
    [Min(1)] public int bossFloorsToReachCore = 2;
    [Header("Floor Timer")]
    [Min(15f)] public float combatBaseDuration = 80f;
    [Min(15f)] public float bossBaseDuration = 110f;
    [Min(0f)] public float areaDurationScale = 0.03f;
    [Min(0f)] public float terminalDurationBonus = 18f;
    [Min(0f)] public float enemyDurationBonus = 5.5f;
    [Min(0f)] public float traversalDurationBonus = 8f;
    [Min(15f)] public float minimumFloorDuration = 55f;
    [Min(15f)] public float maximumCombatDuration = 180f;
    [Min(15f)] public float maximumBossDuration = 240f;
    [Min(1f)] public float urgentThreshold = 30f;
    [Min(1f)] public float criticalThreshold = 10f;
    private bool exitHighlighted;
    private CybergrindWeaponReward currentReward;
    private bool shopInteractionThisFloor;
    private bool bossRewardRevealActive;
    private bool bossRewardRevealQueued;
    private bool coreAccessActive;
    private ArenaCoreBeacon currentCoreBeacon;
    private float encounterStartTime;
    private const float PriorityHighlightDelay = 30f;
    private const float PriorityRevealDuration = 2f;
    private int lastPriorityAliveCount = -1;
    private float lastEnemyProgressTime;
    private float revealAllEnemiesUntil;
    private Coroutine floorTimerArmRoutine;
    private float floorTimerDuration;
    private float floorTimerRemaining;
    private bool floorTimerActive;
    private bool floorTimerExpired;
    private BossEncounterHUD cachedBossHud;
    private Gun cachedGun;
    private MaterialPropertyBlock rewardPulseBlock;
    private static Material sharedRewardPulseMaterial;
    public bool RunComplete { get; private set; }
    public bool IsBossRewardRevealActive => bossRewardRevealActive;
    public bool IsCoreAccessActive => coreAccessActive;
    public bool IsFloorTimerVisible => floorTimerActive && IsTimedFloorActive();
    public float FloorTimerDuration => floorTimerDuration;
    public float FloorTimerRemaining => floorTimerRemaining;
    public float FloorTimerNormalized => CybergrindRules.GetTimerNormalized(floorTimerRemaining, floorTimerDuration);
    public bool IsFloorTimerUrgent => floorTimerRemaining > criticalThreshold && floorTimerRemaining <= urgentThreshold;
    public bool IsFloorTimerCritical => floorTimerRemaining <= criticalThreshold;
    public int combatFloorsPerTheme => combatFloorsBeforeShop + combatFloorsAfterShop;
    public int CycleLength => combatFloorsBeforeShop + combatFloorsAfterShop + 2;
    public int CyclePosition => (floor - 1) % CycleLength;

    public int CurrentThemeIndex => Mathf.Max(0, (floor - 1) / CycleLength);
    public string CurrentThemeLabel => generator != null
        ? generator.GetThemeLabel()
        : CybergrindArenaGenerator.GetThemeLabel(CurrentThemeIndex);
    public string CurrentDirectiveTitle => generator != null
        ? generator.GetThemeDirectiveTitle()
        : CybergrindArenaGenerator.GetThemeDirectiveTitle(CurrentThemeIndex);
    public string CurrentDirectiveDetail => generator != null
        ? generator.GetThemeDirectiveDetail()
        : CybergrindArenaGenerator.GetThemeDirectiveDetail(CurrentThemeIndex);

    private void Start()
    {
        if (generator == null) generator = GetComponent<CybergrindArenaGenerator>();
        if (runState == null) runState = CybergrindRunState.GetOrCreate();
        if (transitionController == null)
            transitionController = GetComponent<CybergrindTransitionController>() ?? gameObject.AddComponent<CybergrindTransitionController>();
        if (FindAnyObjectByType<EnemyPriorityHUD>() == null)
        {
            GameObject go = new GameObject("EnemyPriorityHUD");
            go.AddComponent<EnemyPriorityHUD>();
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        ApplyFloorMode();
        PrepareFloorSeed();
        QueueFloorTimerArm();
    }

    private BossEncounterHUD GetBossHud()
    {
        if (cachedBossHud == null)
            cachedBossHud = FindAnyObjectByType<BossEncounterHUD>();
        return cachedBossHud;
    }

    private Gun GetCachedGun()
    {
        if (cachedGun == null)
            cachedGun = FindAnyObjectByType<Gun>();
        return cachedGun;
    }

    private void Update()
    {
        if (generator == null) return;
        if (RunComplete) return;

        TickFloorTimer();
        if (transitionController != null && transitionController.IsTransitioning) return;

        bool terminalsSolved = AreAllTerminalsSolved();
        bool enemiesCleared = AreAllEnemiesCleared();
        UpdateEnemyPriorityHighlights();
        if (terminalsSolved && enemiesCleared && !exitHighlighted)
        {
            if (generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Boss)
            {
                TryBeginBossRewardReveal();
            }
            else
            {
                if (transitionController != null)
                    transitionController.HighlightExitInGenerator(generator);
                if (generator.arenaMode != CybergrindArenaGenerator.ArenaMode.Shop)
                    SpawnExitReward();
                exitHighlighted = true;
            }
        }

        if (!terminalsSolved) return;
        if (!enemiesCleared) return;
        if (generator.arenaMode != CybergrindArenaGenerator.ArenaMode.Shop && currentReward != null && !currentReward.IsClaimed) return;
        if (ShouldOpenCoreAccess())
        {
            ActivateCoreAccess();
            return;
        }
        if (coreAccessActive) return;
        if (!IsPlayerAtExit()) return;

        StopFloorTimer();

        if (transitionController != null)
        {
            StartCoroutine(transitionController.StartExitSequence(GetPlayerController(), generator, AdvanceFloor));
        }
        else
            AdvanceFloor();
    }

    private bool AreAllTerminalsSolved()
    {
        if (generator != null && generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Shop)
            return shopInteractionThisFloor;

        if (generator != null && generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Boss)
            return true;

        Terminal[] terminals = GetCurrentArenaTerminals();
        bool foundAny = false;
        for (int i = 0; i < terminals.Length; i++)
        {
            if (terminals[i] == null || !terminals[i].name.StartsWith("PuzzleTerminal")) continue;
            foundAny = true;
            if (!terminals[i].isSolved) return false;
        }

        return foundAny || (generator != null && generator.arenaMode != CybergrindArenaGenerator.ArenaMode.Combat);
    }

    private bool AreAllEnemiesCleared()
    {
        BasicEnemyAI[] enemies = GetCurrentArenaEnemies();
        if (enemies == null || enemies.Length == 0)
            return true;

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null) continue;
            if (enemies[i].IsCombatResolved) continue;
            return false;
        }

        return true;
    }

    private void UpdateEnemyPriorityHighlights()
    {
        BasicEnemyAI[] enemies = GetCurrentArenaEnemies();
        if (enemies == null || enemies.Length == 0)
            return;

        List<BasicEnemyAI> aliveEnemies = new List<BasicEnemyAI>();
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null || enemies[i].IsCombatResolved) continue;
            aliveEnemies.Add(enemies[i]);
        }

        for (int i = 0; i < enemies.Length; i++)
        {
            BasicEnemyAI enemy = enemies[i];
            if (enemy == null) continue;
            enemy.SetPriorityTarget(false);
        }

        float now = Time.time;
        if (lastPriorityAliveCount < 0 || aliveEnemies.Count != lastPriorityAliveCount)
        {
            lastPriorityAliveCount = aliveEnemies.Count;
            lastEnemyProgressTime = now;
            revealAllEnemiesUntil = 0f;
        }

        if (aliveEnemies.Count <= 2)
        {
            for (int i = 0; i < aliveEnemies.Count; i++)
                aliveEnemies[i].SetPriorityTarget(true);
            return;
        }

        float inactivityStart = Mathf.Max(encounterStartTime, lastEnemyProgressTime);
        if (now >= revealAllEnemiesUntil && now - inactivityStart >= PriorityHighlightDelay)
        {
            revealAllEnemiesUntil = now + PriorityRevealDuration;
            lastEnemyProgressTime = now;
        }

        if (now < revealAllEnemiesUntil)
        {
            for (int i = 0; i < aliveEnemies.Count; i++)
                aliveEnemies[i].SetPriorityTarget(true);
            return;
        }

        BasicEnemyAI priorityEnemy = SelectPriorityEnemy(aliveEnemies);
        if (priorityEnemy != null)
            priorityEnemy.SetPriorityTarget(true);
    }

    private void ResetEnemyPriorityTracking()
    {
        encounterStartTime = Time.time;
        lastPriorityAliveCount = -1;
        lastEnemyProgressTime = Time.time;
        revealAllEnemiesUntil = 0f;
    }

    private BasicEnemyAI SelectPriorityEnemy(List<BasicEnemyAI> aliveEnemies)
    {
        if (aliveEnemies == null || aliveEnemies.Count == 0)
            return null;

        if (aliveEnemies.Count <= 2)
            return null;

        Vector3 playerPosition = player != null ? player.position : Vector3.zero;
        PlayerController playerController = GetPlayerController();
        bool mobilityCommitted = playerController != null &&
                                 (playerController.IsGrappling ||
                                  playerController.IsGrappleHookInFlight ||
                                  !playerController.isGrounded ||
                                  playerController.DebugIsSliding ||
                                  playerController.DebugIsSlamming ||
                                  playerController.PlanarSpeed > 18f);
        BasicEnemyAI bestEnemy = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            BasicEnemyAI enemy = aliveEnemies[i];
            if (enemy == null)
                continue;

            float total = ComputePriorityScore(enemy, playerController, playerPosition, mobilityCommitted);
            if (total > bestScore)
            {
                bestScore = total;
                bestEnemy = enemy;
            }
        }

        return bestEnemy;
    }

    private float ComputePriorityScore(BasicEnemyAI enemy, PlayerController playerController, Vector3 playerPosition, bool mobilityCommitted)
    {
        if (enemy == null)
            return float.MinValue;

        float distance = player != null ? Vector3.Distance(enemy.transform.position, playerPosition) : 12f;
        float roleScore = enemy.CurrentCombatRole switch
        {
            BasicEnemyAI.CombatRole.Boss => 52f,
            BasicEnemyAI.CombatRole.Harrier => 44f,
            BasicEnemyAI.CombatRole.Diver => 38f,
            BasicEnemyAI.CombatRole.Bulwark => 34f,
            _ => 30f
        };
        float distanceScore = Mathf.Clamp(24f - distance, -8f, 16f);
        float verticalDelta = player != null ? Mathf.Abs(enemy.transform.position.y - playerPosition.y) : 0f;
        float verticalityScore = GetPriorityVerticalityScore(enemy, distance, verticalDelta, mobilityCommitted);
        float stateScore = GetPriorityStateScore(enemy, playerController, distance, verticalDelta, mobilityCommitted);
        float livePressureScore = enemy.CurrentPressureScore * 10.5f;
        float telegraphScore = enemy.IsActivelyTelegraphing
            ? enemy.CurrentCombatRole == BasicEnemyAI.CombatRole.Boss ? 6f : 10f
            : 0f;
        return roleScore + distanceScore + verticalityScore + stateScore + livePressureScore + telegraphScore;
    }

    private float GetPriorityVerticalityScore(BasicEnemyAI enemy, float distance, float verticalDelta, bool mobilityCommitted)
    {
        if (enemy == null)
            return 0f;

        return enemy.CurrentCombatRole switch
        {
            BasicEnemyAI.CombatRole.Harrier => Mathf.Clamp(verticalDelta * 1.35f, 0f, 12f),
            BasicEnemyAI.CombatRole.Boss => Mathf.Clamp(verticalDelta * 0.85f, 0f, 7f),
            BasicEnemyAI.CombatRole.Suppressor => verticalDelta <= 4.5f
                ? Mathf.Clamp(verticalDelta * 0.45f, 0f, 2.2f)
                : -Mathf.Lerp(0.6f, 3f, Mathf.InverseLerp(4.5f, 8f, Mathf.Clamp(verticalDelta, 4.5f, 8f))),
            BasicEnemyAI.CombatRole.Diver => mobilityCommitted
                ? Mathf.Clamp(2.6f - verticalDelta * 0.18f, -1.5f, 2.6f)
                : Mathf.Clamp(1.6f - verticalDelta * 0.3f, -2.8f, 1.6f),
            BasicEnemyAI.CombatRole.Bulwark => distance <= 9f
                ? Mathf.Clamp(2f - verticalDelta * 0.28f, -2.4f, 2f)
                : Mathf.Clamp(1f - verticalDelta * 0.38f, -3.4f, 1f),
            _ => 0f
        };
    }

    private float GetPriorityStateScore(BasicEnemyAI enemy, PlayerController playerController, float distance, float verticalDelta, bool mobilityCommitted)
    {
        if (enemy == null)
            return 0f;

        float score = 0f;
        switch (enemy.CurrentCombatRole)
        {
            case BasicEnemyAI.CombatRole.Harrier:
                if (mobilityCommitted) score += 12f;
                if (verticalDelta >= 4f) score += 8f;
                if (distance >= 10f && distance <= 24f) score += 4f;
                break;
            case BasicEnemyAI.CombatRole.Diver:
                if (mobilityCommitted) score += 9f;
                if (distance <= 10f) score += 8f;
                if (playerController != null && playerController.PlanarSpeed > 16f) score += 4f;
                break;
            case BasicEnemyAI.CombatRole.Bulwark:
                if (playerController != null && playerController.isGrounded && playerController.PlanarSpeed < 14f) score += 8f;
                if (distance <= 11f) score += 6f;
                if (verticalDelta <= 2f) score += 3f;
                break;
            case BasicEnemyAI.CombatRole.Suppressor:
                if (mobilityCommitted) score += 7f;
                if (distance >= 8f && distance <= 20f) score += 5f;
                if (verticalDelta <= 3f) score += 2f;
                break;
            case BasicEnemyAI.CombatRole.Boss:
                if (mobilityCommitted) score += 5f;
                if (distance <= 14f) score += 4f;
                if (verticalDelta <= 5f) score += 3f;
                break;
        }

        return score;
    }

    private bool IsPlayerAtExit()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (player == null) return false;

        Transform arenaRoot = generator.CurrentArenaRoot;
        GameObject exit = null;
        if (arenaRoot != null)
        {
            Transform exitTransform = arenaRoot.Find("Exit_" + (generator.width / 2) + "_" + (generator.length - 3));
            if (exitTransform != null) exit = exitTransform.gameObject;
        }

        if (exit == null)
            exit = GameObject.Find("Exit_" + (generator.width / 2) + "_" + (generator.length - 3));

        if (exit == null) return false;

        return Vector3.Distance(player.position, exit.transform.position) <= exitActivationRange;
    }

    private void AdvanceFloor()
    {
        PlayerController pc = GetPlayerController();
        if (pc != null)
            pc.ToggleUIMode(false);

        if (generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Boss)
        {
            if (runState == null) runState = CybergrindRunState.GetOrCreate();
            runState.RegisterBossDefeated(CurrentThemeIndex);
            Debug.Log("[ArenaDirector] Boss floor cleared.");

        }

        if (runState == null) runState = CybergrindRunState.GetOrCreate();
        runState.RegisterFloorCleared();
        floor++;
        exitHighlighted = false;
        currentReward = null;
        shopInteractionThisFloor = false;
        bossRewardRevealActive = false;
        bossRewardRevealQueued = false;
        coreAccessActive = false;
        currentCoreBeacon = null;
        ApplyFloorMode();
        PrepareFloorSeed();
        ResetEnemyPriorityTracking();
        ResetFloorTimerState();
        bool previousClearSetting = generator.clearBeforeGenerate;
        generator.clearBeforeGenerate = transitionController == null;
        if (transitionController != null)
            generator.BeginGenerateArenaAsync();
        else
            generator.GenerateArena();
        generator.clearBeforeGenerate = previousClearSetting;
        QueueFloorTimerArm();
    }

    [ContextMenu("Force Advance Floor")]
    public void ForceAdvanceFloor()
    {
        AdvanceFloor();
    }

    public void ResetRun()
    {
        if (runState == null) runState = CybergrindRunState.GetOrCreate();

        floor = 1;
        exitHighlighted = false;
        currentReward = null;
        shopInteractionThisFloor = false;
        bossRewardRevealActive = false;
        bossRewardRevealQueued = false;
        coreAccessActive = false;
        currentCoreBeacon = null;
        RunComplete = false;
        runState.ResetRunStats();
        runState.SetRunSeed(unchecked(System.Environment.TickCount ^ (int)System.DateTime.UtcNow.Ticks));
        ResetPlayerRunModifiers();

        ApplyFloorMode();
        PrepareFloorSeed();
        ResetEnemyPriorityTracking();
        ResetFloorTimerState();

        if (generator == null) return;
        generator.ClearArena();
        if (PersistentLoadingScreen.IsActive)
            generator.BeginGenerateArenaAsync();
        else
            generator.GenerateArena();
        QueueFloorTimerArm();
    }

    private void OnDisable()
    {
        CancelFloorTimerArm();
        ResetFloorTimerState();
    }

    private void ApplyFloorMode()
    {
        if (generator == null) return;

        generator.arenaMode = GetArenaModeForFloor(floor);
        generator.themeIndex = CurrentThemeIndex;
        generator.enemyHealthMultiplier = CybergrindRules.GetEnemyHealthMultiplier(floor);
        generator.enemyCountBonus = CybergrindRules.GetEnemyCountBonus(floor);
        ResetEnemyPriorityTracking();
    }

    private void TickFloorTimer()
    {
        if (!floorTimerActive || floorTimerExpired)
            return;

        if (ShouldPauseFloorTimer())
            return;

        floorTimerRemaining = CybergrindRules.TickTimer(floorTimerRemaining, Time.unscaledDeltaTime);
        if (floorTimerRemaining > 0f)
            return;

        floorTimerRemaining = 0f;
        floorTimerExpired = true;
        floorTimerActive = false;
        TriggerFloorTimeoutDeath();
    }

    private bool ShouldPauseFloorTimer()
    {
        if (!IsTimedFloorActive())
            return true;

        if (PersistentLoadingScreen.IsActive)
            return true;

        if (generator == null || generator.IsGenerating || generator.CurrentArenaRoot == null)
            return true;

        if (transitionController != null && transitionController.IsTransitioning)
            return true;

        if (Time.timeScale <= 0.0001f)
            return true;

        PlayerController playerController = GetPlayerController();
        if (playerController == null || playerController.isDead)
            return true;

        return false;
    }

    private bool IsTimedFloorActive()
    {
        return enabled &&
               generator != null &&
               (generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Combat ||
                generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Boss) &&
               !RunComplete;
    }

    private void QueueFloorTimerArm()
    {
        CancelFloorTimerArm();
        if (!IsTimedFloorActive())
            return;

        floorTimerArmRoutine = StartCoroutine(ArmFloorTimerWhenReady());
    }

    private IEnumerator ArmFloorTimerWhenReady()
    {
        while (enabled && generator != null &&
               (PersistentLoadingScreen.IsActive ||
                generator.IsGenerating ||
                generator.CurrentArenaRoot == null ||
                (transitionController != null && transitionController.IsTransitioning)))
        {
            yield return null;
        }

        floorTimerArmRoutine = null;
        if (!enabled || !IsTimedFloorActive() || generator == null || generator.CurrentArenaRoot == null)
            yield break;

        floorTimerDuration = CalculateFloorTimerDuration();
        floorTimerRemaining = floorTimerDuration;
        floorTimerExpired = false;
        floorTimerActive = floorTimerDuration > 0.01f;
    }

    private void CancelFloorTimerArm()
    {
        if (floorTimerArmRoutine == null)
            return;

        StopCoroutine(floorTimerArmRoutine);
        floorTimerArmRoutine = null;
    }

    private void ResetFloorTimerState()
    {
        CancelFloorTimerArm();
        floorTimerDuration = 0f;
        floorTimerRemaining = 0f;
        floorTimerActive = false;
        floorTimerExpired = false;
    }

    private void StopFloorTimer()
    {
        floorTimerActive = false;
        floorTimerExpired = false;
    }

    private float CalculateFloorTimerDuration()
    {
        if (generator == null)
            return 0f;

        float baseDuration = generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Boss
            ? bossBaseDuration
            : combatBaseDuration;
        float maxDuration = generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Boss
            ? maximumBossDuration
            : maximumCombatDuration;
        float arenaArea = Mathf.Max(1f, generator.width * generator.length) * Mathf.Max(0.5f, generator.tileSize);
        Terminal[] terminals = GetCurrentArenaTerminals();
        BasicEnemyAI[] enemies = GetCurrentArenaEnemies();
        int puzzleTerminalCount = 0;
        for (int i = 0; i < terminals.Length; i++)
        {
            Terminal terminal = terminals[i];
            if (terminal != null && terminal.name.StartsWith("PuzzleTerminal"))
                puzzleTerminalCount++;
        }

        int enemyCount = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null)
                enemyCount++;
        }

        float duration = baseDuration;
        duration += arenaArea * areaDurationScale;
        duration += puzzleTerminalCount * terminalDurationBonus;
        duration += enemyCount * enemyDurationBonus;
        duration += Mathf.Max(0f, generator.verticalTraversalBias) * traversalDurationBonus;
        return Mathf.Clamp(duration, minimumFloorDuration, maxDuration);
    }

    private void TriggerFloorTimeoutDeath()
    {
        PlayerController playerController = GetPlayerController();
        if (playerController == null || playerController.isDead)
            return;

        playerController.TriggerGameOverDeath();
    }

    public CybergrindArenaGenerator.ArenaMode GetArenaModeForFloor(int targetFloor)
    {
        int cycleLength = Mathf.Max(1, CycleLength);
        int position = Mathf.Abs(targetFloor - 1) % cycleLength;

        if (position < combatFloorsBeforeShop)
            return CybergrindArenaGenerator.ArenaMode.Combat;
        if (position == combatFloorsBeforeShop)
            return CybergrindArenaGenerator.ArenaMode.Shop;
        if (position < combatFloorsBeforeShop + 1 + combatFloorsAfterShop)
            return CybergrindArenaGenerator.ArenaMode.Combat;
        return CybergrindArenaGenerator.ArenaMode.Boss;
    }

    private void PrepareFloorSeed()
    {
        if (generator == null) return;
        if (runState == null) runState = CybergrindRunState.GetOrCreate();

        generator.randomizeSeedEachGeneration = false;
        generator.seed = runState.GetFloorSeed(floor, CurrentThemeIndex);
    }

    private Terminal[] GetCurrentArenaTerminals()
    {
        if (generator != null && generator.CurrentArenaRoot != null)
            return generator.CurrentArenaRoot.GetComponentsInChildren<Terminal>(true);

        return Object.FindObjectsByType<Terminal>();
    }

    private BasicEnemyAI[] GetCurrentArenaEnemies()
    {
        if (generator != null && generator.CurrentArenaRoot != null)
            return generator.CurrentArenaRoot.GetComponentsInChildren<BasicEnemyAI>(true);

        return Object.FindObjectsByType<BasicEnemyAI>();
    }

    public string DebugSummarizeEncounterPressure()
    {
        BasicEnemyAI[] enemies = GetCurrentArenaEnemies();
        if (enemies == null || enemies.Length == 0)
            return "Encounter: no active enemies.";

        List<BasicEnemyAI> aliveEnemies = new List<BasicEnemyAI>();
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null || enemies[i].IsCombatResolved)
                continue;
            aliveEnemies.Add(enemies[i]);
        }

        if (aliveEnemies.Count == 0)
            return "Encounter: all enemies cleared.";

        int suppressors = 0;
        int divers = 0;
        int bulwarks = 0;
        int harriers = 0;
        int bosses = 0;
        int telegraphing = 0;
        float totalPressure = 0f;
        BasicEnemyAI peakEnemy = null;
        float peakPressure = float.MinValue;
        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            BasicEnemyAI enemy = aliveEnemies[i];
            switch (enemy.CurrentCombatRole)
            {
                case BasicEnemyAI.CombatRole.Suppressor: suppressors++; break;
                case BasicEnemyAI.CombatRole.Diver: divers++; break;
                case BasicEnemyAI.CombatRole.Bulwark: bulwarks++; break;
                case BasicEnemyAI.CombatRole.Harrier: harriers++; break;
                case BasicEnemyAI.CombatRole.Boss: bosses++; break;
            }

            float pressure = enemy.CurrentPressureScore;
            totalPressure += pressure;
            if (enemy.IsActivelyTelegraphing)
                telegraphing++;
            if (pressure > peakPressure)
            {
                peakPressure = pressure;
                peakEnemy = enemy;
            }
        }

        BasicEnemyAI priorityEnemy = SelectPriorityEnemy(aliveEnemies);
        PlayerController playerController = GetPlayerController();
        float playerCommitment = GetPlayerCommitmentForDebug(playerController);
        float pressureLimit = BasicEnemyAI.GetPressureLimitForCommitment(playerCommitment, false);
        float bossPressureLimit = BasicEnemyAI.GetPressureLimitForCommitment(playerCommitment, true);
        bool mobilityCommitted = playerController != null &&
                                 (playerController.IsGrappling ||
                                  playerController.IsGrappleHookInFlight ||
                                  !playerController.isGrounded ||
                                  playerController.DebugIsSliding ||
                                  playerController.DebugIsSlamming ||
                                  playerController.PlanarSpeed > 18f);
        string peakLabel = peakEnemy != null
            ? $"{peakEnemy.displayName} {peakEnemy.PriorityLabel} {peakPressure:0.00} [{peakEnemy.PressureDebugSummary}]"
            : "none";
        string priorityLabel = priorityEnemy != null
            ? $"{priorityEnemy.displayName} {priorityEnemy.PriorityLabel} [{priorityEnemy.PressureDebugSummary}]"
            : aliveEnemies.Count <= 2 ? "final-target mode" : "none";
        List<string> rankedThreats = new List<string>(3);
        List<BasicEnemyAI> ranked = new List<BasicEnemyAI>(aliveEnemies);
        ranked.Sort((a, b) => ComputePriorityScore(b, playerController, player != null ? player.position : Vector3.zero, mobilityCommitted)
            .CompareTo(ComputePriorityScore(a, playerController, player != null ? player.position : Vector3.zero, mobilityCommitted)));
        int threatCount = Mathf.Min(3, ranked.Count);
        for (int i = 0; i < threatCount; i++)
        {
            BasicEnemyAI enemy = ranked[i];
            float threatScore = ComputePriorityScore(enemy, playerController, player != null ? player.position : Vector3.zero, mobilityCommitted);
            rankedThreats.Add($"{enemy.displayName}:{enemy.PriorityLabel}:{threatScore:0.0}[{enemy.CommitGateDebugSummary}]");
        }

        return $"Encounter: alive={aliveEnemies.Count}, roles[S={suppressors}, D={divers}, B={bulwarks}, H={harriers}, Boss={bosses}], telegraphs={telegraphing}, totalPressure={totalPressure:0.00}, playerCommit={playerCommitment:0.00}, limit={pressureLimit:0.00}, bossLimit={bossPressureLimit:0.00}, peak={peakLabel}, priority={priorityLabel}, top=[{string.Join(" | ", rankedThreats)}], {BasicEnemyAI.SharedPressureDebugSummary}.";
    }

    public string DebugAuditEncounterPressure()
    {
        BasicEnemyAI[] enemies = GetCurrentArenaEnemies();
        if (enemies == null || enemies.Length == 0)
            return "Encounter audit: no active enemies.";

        List<BasicEnemyAI> aliveEnemies = new List<BasicEnemyAI>();
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null || enemies[i].IsCombatResolved)
                continue;
            aliveEnemies.Add(enemies[i]);
        }

        if (aliveEnemies.Count == 0)
            return "Encounter audit: all enemies cleared.";

        PlayerController playerController = GetPlayerController();
        float playerCommitment = GetPlayerCommitmentForDebug(playerController);
        float pressureLimit = BasicEnemyAI.GetPressureLimitForCommitment(playerCommitment, false);
        float bossPressureLimit = BasicEnemyAI.GetPressureLimitForCommitment(playerCommitment, true);
        bool hasBoss = false;
        int telegraphs = 0;
        int suppressors = 0;
        int divers = 0;
        int bulwarks = 0;
        int harriers = 0;
        int closeTelegraphs = 0;
        int diverTelegraphs = 0;
        int bossTelegraphs = 0;
        float totalPressure = 0f;
        float highestPressure = 0f;
        BasicEnemyAI peakEnemy = null;
        List<string> issues = new List<string>();

        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            BasicEnemyAI enemy = aliveEnemies[i];
            float pressure = enemy.CurrentPressureScore;
            totalPressure += pressure;
            if (pressure > highestPressure)
            {
                highestPressure = pressure;
                peakEnemy = enemy;
            }

            if (enemy.IsActivelyTelegraphing)
            {
                telegraphs++;
                if (player != null && Vector3.Distance(enemy.transform.position, player.position) <= 6.5f)
                    closeTelegraphs++;
                if (enemy.CurrentCombatRole == BasicEnemyAI.CombatRole.Diver)
                    diverTelegraphs++;
                if (enemy.CurrentCombatRole == BasicEnemyAI.CombatRole.Boss)
                    bossTelegraphs++;
            }

            switch (enemy.CurrentCombatRole)
            {
                case BasicEnemyAI.CombatRole.Suppressor: suppressors++; break;
                case BasicEnemyAI.CombatRole.Diver: divers++; break;
                case BasicEnemyAI.CombatRole.Bulwark: bulwarks++; break;
                case BasicEnemyAI.CombatRole.Harrier: harriers++; break;
                case BasicEnemyAI.CombatRole.Boss: hasBoss = true; break;
            }
        }

        float activeLimit = hasBoss ? bossPressureLimit : pressureLimit;
        if (totalPressure > activeLimit * 1.28f)
            issues.Add($"pressure over budget ({totalPressure:0.00}>{activeLimit:0.00})");
        if (telegraphs >= Mathf.Max(3, aliveEnemies.Count - 1))
            issues.Add($"too many simultaneous telegraphs ({telegraphs})");
        if (closeTelegraphs >= 2 && playerCommitment < 0.38f)
            issues.Add($"close-range telegraph stack ({closeTelegraphs})");
        if (divers >= 3 && playerCommitment < 0.22f)
            issues.Add($"diver stack while player is not committed ({divers})");
        if (diverTelegraphs >= 2 && playerCommitment < 0.28f)
            issues.Add($"multiple diver telegraphs while player is not committed ({diverTelegraphs})");
        if (bossTelegraphs >= 1 && telegraphs >= 3 && playerCommitment < 0.5f)
            issues.Add($"boss pressure overlaps too many supporting telegraphs ({telegraphs})");
        if (harriers >= 2 && bulwarks >= 2)
            issues.Add($"heavy vertical+ground crossfire overlap (H={harriers}, B={bulwarks})");
        if (suppressors >= 3 && playerCommitment > 0.55f)
            issues.Add($"suppressor stack can overpunish movement commits ({suppressors})");
        if (peakEnemy != null && peakEnemy.CurrentPressureScore > activeLimit * 0.82f && aliveEnemies.Count > 2)
            issues.Add($"single enemy dominates live pressure ({peakEnemy.displayName}:{peakEnemy.CurrentPressureScore:0.00})");

        string summary = DebugSummarizeEncounterPressure();
        if (issues.Count == 0)
            return $"[Encounter Audit] PASS. {summary}";

        return $"[Encounter Audit] WARN. {summary} Issues: {string.Join("; ", issues)}.";
    }

    [ContextMenu("Debug/Log Encounter Pressure")]
    private void DebugLogEncounterPressure()
    {
        Debug.Log(DebugSummarizeEncounterPressure());
    }

    [ContextMenu("Debug/Run Encounter Audit")]
    private void DebugRunEncounterAudit()
    {
        string audit = DebugAuditEncounterPressure();
        if (audit.Contains("WARN"))
            Debug.LogWarning(audit);
        else
            Debug.Log(audit);
    }

    private float GetPlayerCommitmentForDebug(PlayerController playerController)
    {
        if (playerController == null)
            return 0f;

        float score = 0f;
        if (!playerController.isGrounded) score += 0.28f;
        if (playerController.IsGrappling || playerController.IsGrappleHookInFlight) score += 0.42f;
        if (playerController.DebugIsSliding || playerController.DebugIsSlamming) score += 0.22f;
        score += Mathf.InverseLerp(12f, 28f, playerController.PlanarSpeed) * 0.35f;
        return Mathf.Clamp01(score);
    }

    private IEnumerator BossRewardRevealSequence()
    {
        BossEncounterHUD hud = GetBossHud();
        yield return new WaitForSecondsRealtime(0.35f);

        bool finalBoss = IsFinalBossFloor();
        if (!finalBoss && transitionController != null)
            transitionController.HighlightExitInGenerator(generator);

        if (hud != null)
        {
            hud.ShowSystemBanner(
                finalBoss ? "CORE READY" : "WEAPON DROP",
                finalBoss
                    ? "Take the weapon, then enter the core."
                    : "Grab the weapon, then take the exit.",
                new Color(0.18f, 0.08f, 0.03f, 0.95f),
                2.8f);
        }

        yield return new WaitForSecondsRealtime(0.3f);
        exitHighlighted = !finalBoss;
        bossRewardRevealActive = false;
        bossRewardRevealQueued = false;
    }

    private void SpawnExitReward(bool isBossReward = false, Vector3? overridePosition = null)
    {
        if (generator == null || generator.CurrentArenaRoot == null) return;
        if (currentReward != null) return;

        Transform exit = transitionController != null
            ? transitionController.FindExitCellTransform(generator)
            : null;
        if (exit == null && !isBossReward) return;
        if (!overridePosition.HasValue && exit == null) return;

        int presetIndex = GetFloorRewardPresetIndex();
        GameObject reward = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        reward.name = $"WeaponReward_{presetIndex}";
        reward.transform.SetParent(generator.CurrentArenaRoot, true);
        reward.transform.position = overridePosition ?? (exit.position + Vector3.up * 1.45f);
        reward.transform.localScale = isBossReward ? new Vector3(1.05f, 0.34f, 1.05f) : new Vector3(0.9f, 0.28f, 0.9f);

        CybergrindWeaponReward weaponReward = reward.AddComponent<CybergrindWeaponReward>();
        weaponReward.presetIndex = presetIndex;
        weaponReward.exitTransform = exit;
        weaponReward.highlightRenderer = reward.GetComponent<Renderer>();
        weaponReward.isBossReward = isBossReward;
        currentReward = weaponReward;
    }

    private Vector3 ResolveBossRewardAnchor()
    {
        if (generator == null || generator.CurrentArenaRoot == null)
            return Vector3.zero;

        Transform dais = generator.CurrentArenaRoot.Find("BossArenaDais");
        if (dais != null)
            return dais.position + Vector3.up * 1.5f;

        Transform exit = transitionController != null ? transitionController.FindExitCellTransform(generator) : null;
        return exit != null ? exit.position + Vector3.up * 1.45f : generator.CurrentArenaRoot.position + Vector3.up * 1.5f;
    }

    private void StartBossRewardPreludeFx(Vector3 center)
    {
        if (!Application.isPlaying)
            return;
        StartCoroutine(BossRewardPreludeFx(center));
    }

    private IEnumerator BossRewardPreludeFx(Vector3 center)
    {
        for (int i = 0; i < 3; i++)
        {
            SpawnRewardPulse(center, 1.35f + i * 0.8f, new Color(1f, 0.68f, 0.22f, 0.55f), 0.34f + i * 0.06f);
            yield return new WaitForSecondsRealtime(0.12f);
        }
    }

    private void SpawnRewardPulse(Vector3 center, float radius, Color color, float lifetime)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "BossRewardPulse";
        ring.transform.position = center + Vector3.up * 0.04f;
        ring.transform.localScale = new Vector3(radius, 0.025f, radius);
        Collider collider = ring.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }

        Renderer renderer = ring.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (sharedRewardPulseMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                sharedRewardPulseMaterial = new Material(shader) { name = "SharedRewardPulse" };
            }
            renderer.sharedMaterial = sharedRewardPulseMaterial;
            ApplyRewardPulseColor(renderer, color);
        }

        StartCoroutine(AnimateRewardPulse(ring.transform, renderer, color, lifetime));
    }

    private IEnumerator AnimateRewardPulse(Transform ring, Renderer renderer, Color color, float lifetime)
    {
        if (ring == null) yield break;

        Vector3 startScale = ring.localScale * 0.4f;
        Vector3 endScale = ring.localScale * 1.8f;
        ring.localScale = startScale;
        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
            ring.localScale = Vector3.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, t));
            if (renderer != null)
                ApplyRewardPulseColor(renderer, new Color(color.r, color.g, color.b, Mathf.Lerp(color.a, 0f, t)));
            yield return null;
        }

        if (ring != null)
        {
            if (Application.isPlaying) Destroy(ring.gameObject);
            else DestroyImmediate(ring.gameObject);
        }
    }

    private void ApplyRewardPulseColor(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;

        if (rewardPulseBlock == null)
            rewardPulseBlock = new MaterialPropertyBlock();

        renderer.GetPropertyBlock(rewardPulseBlock);
        rewardPulseBlock.SetColor("_BaseColor", color);
        rewardPulseBlock.SetColor("_Color", color);
        renderer.SetPropertyBlock(rewardPulseBlock);
    }

    private int GetFloorRewardPresetIndex()
    {
        if (runState == null) runState = CybergrindRunState.GetOrCreate();

        if (!runState.shotgunUnlockedThisRun)
            return 2;

        int[] rewardCycle = { 1, 2, 3 };
        return rewardCycle[Mathf.Abs(floor + CurrentThemeIndex) % rewardCycle.Length];
    }

    public int PreviewCurrentFloorRewardPresetIndex()
    {
        return GetFloorRewardPresetIndex();
    }

    public bool HasPendingReward()
    {
        return currentReward != null && !currentReward.IsClaimed;
    }

    public bool IsFinalBossFloor()
    {
        return false;
    }

    public bool HasShopInteractionThisFloor()
    {
        return shopInteractionThisFloor;
    }

    public void TryBeginBossRewardReveal()
    {
        if (generator == null || generator.arenaMode != CybergrindArenaGenerator.ArenaMode.Boss)
            return;
        if (bossRewardRevealActive || bossRewardRevealQueued || currentReward != null)
            return;

        bossRewardRevealQueued = true;
        bossRewardRevealActive = true;

        PlayerController playerController = GetPlayerController();
        if (playerController != null)
            playerController.ToggleUIMode(false);

        BossEncounterHUD hud = GetBossHud();
        if (hud != null)
        {
            hud.ShowSystemBanner(
                "BOSS DOWN",
                "Weapon drop incoming.",
                new Color(0.14f, 0.05f, 0.04f, 0.94f),
                2.6f);
        }

        Vector3 rewardAnchor = ResolveBossRewardAnchor();
        StartBossRewardPreludeFx(rewardAnchor);
        SpawnExitReward(true, rewardAnchor);

        StartCoroutine(BossRewardRevealSequence());
    }

    public void NotifyCoreReached()
    {
        // Infinite runs never expose a completion beacon.
    }

    public void NotifyShopInteraction()
    {
        shopInteractionThisFloor = true;
        if (runState == null) runState = CybergrindRunState.GetOrCreate();
        runState.RegisterShopInteraction();
    }

    private PlayerController GetPlayerController()
    {
        if (player != null)
            return player.GetComponent<PlayerController>();

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.GetComponent<PlayerController>() : null;
    }

    private void ResetPlayerRunModifiers()
    {
        PlayerController playerController = GetPlayerController();
        if (playerController != null)
            playerController.ResetRunModifiers();

        Gun gun = GetCachedGun();
        if (gun != null)
            gun.ResetRunModifiers();
    }

    private bool ShouldOpenCoreAccess()
    {
        return false;
    }

    private void ActivateCoreAccess()
    {
        if (generator == null || generator.CurrentArenaRoot == null || coreAccessActive)
            return;

        Vector3 beaconPosition = ResolveBossRewardAnchor() + Vector3.up * 0.2f;
        GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "CoreBeacon";
        beacon.transform.SetParent(generator.CurrentArenaRoot, true);
        beacon.transform.position = beaconPosition;
        beacon.transform.localScale = new Vector3(0.9f, 0.5f, 0.9f);

        ArenaCoreBeacon coreBeacon = beacon.AddComponent<ArenaCoreBeacon>();
        coreBeacon.director = this;
        coreBeacon.highlightRenderer = beacon.GetComponent<Renderer>();
        currentCoreBeacon = coreBeacon;
        coreAccessActive = true;

        BuildCoreBeaconModel(beacon.transform);

        BossEncounterHUD hud = GetBossHud();
        if (hud != null)
        {
            hud.ShowSystemBanner(
                "CORE OPEN",
                "Step in to finish the run.",
                new Color(0.05f, 0.13f, 0.16f, 0.96f),
                3.5f);
        }
    }

    private void BuildCoreBeaconModel(Transform root)
    {
        Renderer baseRenderer = root.GetComponent<Renderer>();
        Material baseMaterial = baseRenderer != null ? baseRenderer.sharedMaterial : null;
        if (baseMaterial == null && generator != null)
            baseMaterial = generator.accentMaterial;
        if (baseRenderer != null && baseMaterial != null)
            baseRenderer.sharedMaterial = baseMaterial;

        CreateCorePart(root, "CoreSpire", PrimitiveType.Cube, new Vector3(0f, 1.4f, 0f), new Vector3(0.24f, 2.4f, 0.24f), generator != null ? generator.darkMaterial : baseMaterial);
        CreateCorePart(root, "CoreRingA", PrimitiveType.Cylinder, new Vector3(0f, 1.1f, 0f), new Vector3(0.95f, 0.035f, 0.95f), baseMaterial);
        CreateCorePart(root, "CoreRingB", PrimitiveType.Cylinder, new Vector3(0f, 2.05f, 0f), new Vector3(0.68f, 0.03f, 0.68f), baseMaterial);
        CreateCorePart(root, "CoreLens", PrimitiveType.Sphere, new Vector3(0f, 2.35f, 0f), new Vector3(0.52f, 0.52f, 0.52f), baseMaterial);

        for (int i = 0; i < 4; i++)
        {
            float yaw = i * 90f;
            Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.72f, 0.88f);
            CreateCorePart(root, "CoreBlade_" + i, PrimitiveType.Cube, offset, new Vector3(0.12f, 1.2f, 0.42f), baseMaterial, new Vector3(0f, yaw, 28f));
        }
    }

    private void CreateCorePart(Transform parent, string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Material material, Vector3? localEuler = null)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        if (localEuler.HasValue)
            part.transform.localRotation = Quaternion.Euler(localEuler.Value);

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }
    }
}
