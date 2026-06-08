using System.Collections;
using UnityEngine;

public class CybergrindArenaDirector : MonoBehaviour
{
    public CybergrindArenaGenerator generator;
    public int floor = 1;
    [Min(1)] public int combatFloorsPerTheme = 5;
    public float exitActivationRange = 5f;
    public Transform player;
    public CybergrindRunState runState;
    public CybergrindTransitionController transitionController;
    [Min(1)] public int bossFloorsToReachCore = 2;
    private bool exitHighlighted;
    private CybergrindWeaponReward currentReward;
    private bool shopInteractionThisFloor;
    private bool bossRewardRevealActive;
    private bool bossRewardRevealQueued;
    public bool RunComplete { get; private set; }
    public bool IsBossRewardRevealActive => bossRewardRevealActive;

    public int CurrentThemeIndex => Mathf.Max(0, (floor - 1) / (combatFloorsPerTheme + 2));
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
    }

    private void Update()
    {
        if (generator == null) return;
        if (transitionController != null && transitionController.IsTransitioning) return;
        if (RunComplete) return;

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
        if (!IsPlayerAtExit()) return;

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

        int aliveCount = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null || enemies[i].IsCombatResolved) continue;
            aliveCount++;
        }

        bool highlightFinalTargets = aliveCount > 0 && aliveCount <= 2;
        for (int i = 0; i < enemies.Length; i++)
        {
            BasicEnemyAI enemy = enemies[i];
            if (enemy == null) continue;
            enemy.SetPriorityTarget(highlightFinalTargets && !enemy.IsCombatResolved);
        }
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
            int unlockedPreset = runState.RegisterBossDefeated(CurrentThemeIndex);
            Debug.Log($"[ArenaDirector] Boss floor cleared. Unlocked weapon preset {unlockedPreset}.");

            if (runState.bossesClearedThisRun >= bossFloorsToReachCore)
            {
                RunComplete = true;
                return;
            }
        }

        if (runState == null) runState = CybergrindRunState.GetOrCreate();
        runState.RegisterFloorCleared();
        floor++;
        exitHighlighted = false;
        currentReward = null;
        shopInteractionThisFloor = false;
        bossRewardRevealActive = false;
        bossRewardRevealQueued = false;
        ApplyFloorMode();
        PrepareFloorSeed();
        bool previousClearSetting = generator.clearBeforeGenerate;
        generator.clearBeforeGenerate = transitionController == null;
        generator.GenerateArena();
        generator.clearBeforeGenerate = previousClearSetting;
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
        RunComplete = false;
        runState.ResetRunStats();
        runState.SetRunSeed(unchecked(System.Environment.TickCount ^ (int)System.DateTime.UtcNow.Ticks));
        ResetPlayerRunModifiers();

        ApplyFloorMode();
        PrepareFloorSeed();

        if (generator == null) return;
        generator.ClearArena();
        generator.GenerateArena();
    }

    private void ApplyFloorMode()
    {
        if (generator == null) return;

        int cycleLength = combatFloorsPerTheme + 2;
        int position = (floor - 1) % cycleLength;

        if (position < combatFloorsPerTheme)
            generator.arenaMode = CybergrindArenaGenerator.ArenaMode.Combat;
        else if (position == combatFloorsPerTheme)
            generator.arenaMode = CybergrindArenaGenerator.ArenaMode.Shop;
        else
            generator.arenaMode = CybergrindArenaGenerator.ArenaMode.Boss;

        generator.themeIndex = CurrentThemeIndex;
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

        return FindObjectsByType<Terminal>();
    }

    private BasicEnemyAI[] GetCurrentArenaEnemies()
    {
        if (generator != null && generator.CurrentArenaRoot != null)
            return generator.CurrentArenaRoot.GetComponentsInChildren<BasicEnemyAI>(true);

        return FindObjectsByType<BasicEnemyAI>();
    }

    private IEnumerator BossRewardRevealSequence()
    {
        BossEncounterHUD hud = FindAnyObjectByType<BossEncounterHUD>();
        yield return new WaitForSecondsRealtime(0.35f);

        if (transitionController != null)
            transitionController.HighlightExitInGenerator(generator);

        if (hud != null)
        {
            hud.ShowSystemBanner(
                "WEAPON DROP INBOUND",
                "Core weapon aligning. Grab it, then take the exit.",
                new Color(0.18f, 0.08f, 0.03f, 0.95f),
                2.8f);
        }

        yield return new WaitForSecondsRealtime(0.3f);
        exitHighlighted = true;
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
        Material mat = null;
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            mat = new Material(shader);
            mat.color = color;
            renderer.material = mat;
        }

        StartCoroutine(AnimateRewardPulse(ring.transform, mat, color, lifetime));
    }

    private IEnumerator AnimateRewardPulse(Transform ring, Material mat, Color color, float lifetime)
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
            if (mat != null)
                mat.color = new Color(color.r, color.g, color.b, Mathf.Lerp(color.a, 0f, t));
            yield return null;
        }

        if (ring != null)
        {
            if (Application.isPlaying) Destroy(ring.gameObject);
            else DestroyImmediate(ring.gameObject);
        }
    }

    private int GetFloorRewardPresetIndex()
    {
        if (runState == null) runState = CybergrindRunState.GetOrCreate();

        if (generator != null && generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Boss)
            return Mathf.Clamp(CurrentThemeIndex + 1, 1, Mathf.Max(1, runState.maxTrackedWeaponPresets - 1));

        int cycleLength = combatFloorsPerTheme + 2;
        int position = (floor - 1) % cycleLength;
        int familyOffset = position % 2 == 0 ? 0 : 3;
        int variant = Mathf.Abs((floor + CurrentThemeIndex) % 3);
        return Mathf.Clamp(familyOffset + variant, 0, Mathf.Max(0, runState.maxTrackedWeaponPresets - 1));
    }

    public bool HasPendingReward()
    {
        return currentReward != null && !currentReward.IsClaimed;
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

        BossEncounterHUD hud = FindAnyObjectByType<BossEncounterHUD>();
        if (hud != null)
        {
            hud.ShowSystemBanner(
                "CHAMBER OPEN",
                "The boss shell is breaking apart. Hold for the weapon drop.",
                new Color(0.14f, 0.05f, 0.04f, 0.94f),
                2.6f);
        }

        Vector3 rewardAnchor = ResolveBossRewardAnchor();
        StartBossRewardPreludeFx(rewardAnchor);
        SpawnExitReward(true, rewardAnchor);

        StartCoroutine(BossRewardRevealSequence());
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

        Gun gun = FindAnyObjectByType<Gun>();
        if (gun != null)
            gun.ResetRunModifiers();
    }
}
