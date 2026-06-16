using System.Collections;
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
    private bool exitHighlighted;
    private CybergrindWeaponReward currentReward;
    private bool shopInteractionThisFloor;
    private bool bossRewardRevealActive;
    private bool bossRewardRevealQueued;
    private bool coreAccessActive;
    private ArenaCoreBeacon currentCoreBeacon;
    public bool RunComplete { get; private set; }
    public bool IsBossRewardRevealActive => bossRewardRevealActive;
    public bool IsCoreAccessActive => coreAccessActive;
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
        if (ShouldOpenCoreAccess())
        {
            ActivateCoreAccess();
            return;
        }
        if (coreAccessActive) return;
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
            runState.RegisterBossDefeated(CurrentThemeIndex);
            Debug.Log("[ArenaDirector] Boss floor cleared.");

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
        coreAccessActive = false;
        currentCoreBeacon = null;
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
        coreAccessActive = false;
        currentCoreBeacon = null;
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

        generator.arenaMode = GetArenaModeForFloor(floor);
        generator.themeIndex = CurrentThemeIndex;
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
            return runState.heavyUnlockedThisRun
                ? Mathf.Clamp(6 + Mathf.Abs((CurrentThemeIndex + floor) % 3), 6, Mathf.Max(6, runState.maxTrackedWeaponPresets - 1))
                : 6;

        int position = CyclePosition;
        if (position == 0)
        {
            if (!runState.shotgunUnlockedThisRun)
                return 3;
            return 1 + Mathf.Abs(CurrentThemeIndex % 2);
        }

        if (position == 1)
            return runState.shotgunUnlockedThisRun
                ? Mathf.Clamp(3 + Mathf.Abs((CurrentThemeIndex + floor) % 3), 3, 5)
                : 3;

        if (position == combatFloorsBeforeShop + 1)
            return runState.heavyUnlockedThisRun
                ? Mathf.Clamp(6 + Mathf.Abs((CurrentThemeIndex + floor + 1) % 3), 6, 8)
                : 6;

        if (position == combatFloorsBeforeShop + 2)
        {
            if (runState.heavyUnlockedThisRun)
                return Mathf.Clamp(6 + Mathf.Abs((CurrentThemeIndex + floor + 2) % 3), 6, 8);
            if (runState.shotgunUnlockedThisRun)
                return Mathf.Clamp(3 + Mathf.Abs((CurrentThemeIndex + floor + 1) % 3), 3, 5);
        }

        return runState.GetFirstUnlockedPreset();
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
        if (generator == null || generator.arenaMode != CybergrindArenaGenerator.ArenaMode.Boss)
            return false;

        if (runState == null)
            runState = CybergrindRunState.GetOrCreate();

        return runState.bossesClearedThisRun + 1 >= bossFloorsToReachCore;
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
        if (!coreAccessActive || RunComplete)
            return;

        if (runState == null)
            runState = CybergrindRunState.GetOrCreate();

        runState.RegisterFloorCleared();
        Debug.Log("[ArenaDirector] Core reached.");

        BossEncounterHUD hud = FindAnyObjectByType<BossEncounterHUD>();
        if (hud != null)
        {
            hud.ShowSystemBanner(
                "CORE OPEN",
                "Run complete.",
                new Color(0.08f, 0.14f, 0.16f, 0.96f),
                3.4f);
        }

        coreAccessActive = false;
        currentCoreBeacon = null;
        RunComplete = true;
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

    private bool ShouldOpenCoreAccess()
    {
        return IsFinalBossFloor() &&
               currentReward != null &&
               currentReward.IsClaimed &&
               !coreAccessActive &&
               !RunComplete;
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

        BossEncounterHUD hud = FindAnyObjectByType<BossEncounterHUD>();
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
