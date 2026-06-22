using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public class BasicEnemyAI : MonoBehaviour, IDamageable, IGrappleMassTarget
{
    public enum EnemyType { Shooter, Grunt, Tank, Flying }
    public enum BossArchetype { None, Warden, Striker, Sentinel }
    public enum CombatRole { Suppressor, Diver, Bulwark, Harrier, Boss }

    [Header("Identity")]
    public EnemyType enemyType = EnemyType.Shooter;
    public bool isBoss;
    public BossArchetype bossArchetype;
    public string displayName = "Enemy";

    [Header("Stats")]
    public float maxHealth = 30f;
    private float currentHealth;
    public float CurrentHealth => currentHealth;
    public float Health01 => maxHealth <= 0.01f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
    public int BossPhase => GetBossPhase();
    public bool IsCombatResolved => isDying || currentHealth <= 0f || !isActiveAndEnabled || !gameObject.activeInHierarchy;

    [Header("Movement")]
    public float stoppingDistance = 10f; // How close they get before they stop to shoot
    public float moveSpeed = 4.25f;
    public float turnSpeed = 8f;
    public float floorStandoffDistance = 4.5f;
    public float obstacleAvoidanceDistance = 1.4f;
    [Min(0.5f)] public float allySeparationRadius = 3.2f;
    [Min(0f)] public float allySeparationStrength = 1.8f;
    public LayerMask movementObstacleMask = ~0;
    [Min(0.1f)] public float pathRefreshInterval = 0.45f;
    [Min(0.15f)] public float pathNodeReachDistance = 0.65f;
    [Min(0.2f)] public float floorSnapTolerance = 1.1f;
    [Header("Perception")]
    [Min(10f)] public float detectionRadius = 140f;
    [Min(0f)] public float closeAwarenessRadius = 18f;
    public bool requireInitialLineOfSight;
    private NavMeshAgent agent;
    private Transform player;
    private PlayerController playerController;
    private CybergrindArenaGenerator arenaGenerator;
    private readonly List<Vector3> groundPath = new List<Vector3>();
    private int groundPathIndex;
    private float repathTimer;
    private Vector3 lastRequestedPathTarget;
    private Vector3 trackedPlayerVelocity;
    private Vector3 lastTrackedPlayerPosition;
    private bool hasTrackedPlayerPosition;
    private bool hasAggro;
    private bool cachedAggroVisibility;
    private bool cachedThreatLineOfSight;
    private float aggroVisibilityRefreshTimer;
    private float threatLineOfSightRefreshTimer;
    private readonly RaycastHit[] sightHitBuffer = new RaycastHit[24];
    private readonly Collider[] nearbyEnemyBuffer = new Collider[16];
    private float laneBiasSign = 1f;
    private float laneBiasSeed;

    [Header("Grapple")]
    public GrappleMassClass grappleMassClass = GrappleMassClass.Light;
    [Min(0f)] public float grapplePullResponsiveness = 20f;
    [Min(0f)] public float grapplePullStopDistance = 2.1f;
    public GrappleMassClass GrappleMassClass => grappleMassClass;
    public CombatRole CurrentCombatRole => ResolveCombatRole();
    public string PriorityLabel => GetPriorityLabel();
    public float CurrentPressureScore => GetCurrentPressureScore();
    public bool IsActivelyTelegraphing => IsCurrentlyTelegraphing();
    public string PressureDebugSummary => BuildPressureDebugSummary();
    public string CommitGateDebugSummary => BuildCommitGateDebugSummary();
    public static string SharedPressureDebugSummary => BuildSharedPressureDebugSummary();
    public static float GetPressureLimitForCommitment(float playerCommitment, bool bossAllowance = false)
    {
        float pressureLimit = Mathf.Lerp(1.7f, 3.15f, Mathf.Clamp01(playerCommitment));
        if (bossAllowance)
            pressureLimit += 0.45f;
        return pressureLimit;
    }

    [Header("Grunt Tuning")]
    [Range(0.1f, 1f)] public float gruntMoveSpeedMultiplier = 0.72f;
    private float gruntPounceTimeRemaining;
    private Vector3 gruntPounceVelocity;

    [Header("Flying Settings")]
    public float flySpeed = 6f;
    public float hoverHeight = 2.4f;
    public float orbitRadius = 3f;
    public float bobAmplitude = 0.35f;
    public float bobFrequency = 1.6f;
    public float dronePreferredDistance = 12f;
    public float droneDashSpeed = 12f;
    public float droneDashDuration = 0.18f;
    public float droneDashIntervalMin = 0.85f;
    public float droneDashIntervalMax = 1.45f;
    public float dronePostDashShootDelay = 0.35f;
    public float droneRangeCorrectionSpeed = 2.2f;
    private float flyPhase = 0f;
    private float droneDashTimer;
    private float droneDashTimeRemaining;
    private Vector3 droneDashVelocity;

    [Header("Combat")]
    public GameObject projectilePrefab;
    public Transform shootPoint;
    public float fireRate = 1.5f;
    public float meleeDamage = 12f;
    public float meleeRange = 2.1f;
    public float meleeCooldown = 1.1f;
    [Min(0.05f)] public float bossPlayerHitCooldown = 0.42f;
    [Header("Rewards")]
    [Min(0)] public int coinReward = 1;
    [Min(0)] public int bossCoinReward = 6;
    private float fireTimer;
    private float meleeTimer;
    private float bossPatternTimer;
    private float shooterBurstTimer;
    private int shooterBurstShotsRemaining;
    private float gruntPounceCooldown;
    private float tankShockwaveCooldown;
    private float flyingVolleyCooldown;
    private float bossAttackCooldown;
    private float bossSpecialCooldown;
    private float bossPlayerHitTimer;
    private float meleeStunTimer;
    private Coroutine bossRoutine;
    private Coroutine tankShockwaveRoutine;
    private Coroutine tankVolleyRoutine;
    private Coroutine gruntPounceWindupRoutine;
    private Coroutine flyingVolleyRoutine;
    private Coroutine meleeWindupRoutine;
    private static readonly Dictionary<PrimitiveType, Stack<GameObject>> hitFxPrimitivePool = new Dictionary<PrimitiveType, Stack<GameObject>>();
    private static readonly Dictionary<GameObject, PrimitiveType> hitFxPrimitiveTypes = new Dictionary<GameObject, PrimitiveType>();

    [Header("Effects")]
    public Color damageColor = Color.red;
    private Color originalColor;
    private Renderer enemyRenderer;
    private float flashTimer;
    private float hurtPulseTimer;
    private float attackPulseTimer;
    private float hitReactTimer;
    private float hitReactDuration;
    private Vector3 hitReactOffset;
    private Vector3 hitReactAngles;
    private Vector3 baseModelLocalPosition;
    private Quaternion baseModelLocalRotation;
    private Transform modelRoot;
    private float groundY;
    private bool hasGroundAnchor;
    private bool isDying;
    private CapsuleCollider combatCollider;
    private Transform priorityMarker;
    private Transform priorityMarkerRing;
    private Transform priorityMarkerBeam;
    private Renderer priorityMarkerRenderer;
    private Renderer[] priorityMarkerRenderers;
    private Transform priorityOutlineRoot;
    private bool isPriorityTarget;
    public bool IsPriorityTarget => isPriorityTarget && !IsCombatResolved;
    public bool HasAggro => hasAggro;
    private static readonly float[] roleNextCommitTime = new float[5];
    private static readonly float[] roleLastCommitTime = new float[5];
    private static float globalPressureBurstUntil;
    private static float globalPressureScore;
    private static float globalPressureLastUpdateTime = -1f;
    private static int activeCoordinatorCount;
    private static PlayerController sharedPlayerController;
    private static CybergrindArenaGenerator sharedArenaGenerator;
    private bool coordinatorRegistered;

    [Header("Type Visuals")]
    public bool autoBuildTypeModel = true;
    public Color shooterColor = new Color(0.18f, 0.65f, 0.95f);
    public Color gruntColor = new Color(0.92f, 0.24f, 0.24f);
    public Color tankColor = new Color(0.65f, 0.65f, 0.75f);
    public Color coreGlowColor = new Color(0.0f, 0.9f, 1f);

    private Material bodyMaterial;
    private Material darkMaterial;
    private Material glowMaterial;
    private Material transientFxMaterial;
    private MaterialPropertyBlock transientFxBlock;
    private MaterialPropertyBlock visualRendererBlock;
    private Color currentBodyColor;
    private Color currentDarkColor;
    private Color currentGlowColor;
    private static Material sharedBodyMaterial;
    private static Material sharedDarkMaterial;
    private static Material sharedGlowMaterial;
    private static Material sharedTransientFxMaterial;
    private static Material sharedPriorityOutlineMaterial;

    private void OnEnable()
    {
        ClearTransientCombatState();
        hasAggro = false;
        cachedAggroVisibility = false;
        cachedThreatLineOfSight = false;
        aggroVisibilityRefreshTimer = 0f;
        threatLineOfSightRefreshTimer = 0f;
        RegisterCoordinator();
    }

    private void OnDisable()
    {
        ClearTransientCombatState();
        UnregisterCoordinator();
    }

    void Start()
    {
        // Basic initialization and defensive checks
        agent = GetComponent<NavMeshAgent>();
        enemyRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();

        currentHealth = maxHealth;

        if (enemyRenderer != null && enemyRenderer.sharedMaterial != null)
            originalColor = ResolveVisualColorForMaterial(enemyRenderer.sharedMaterial);

        // Find the player automatically (Updated for modern Unity versions)
        PlayerController p = GetSharedPlayerController();
        if (p != null)
        {
            playerController = p;
            player = p.transform;
        }
        arenaGenerator = GetSharedArenaGenerator();

        // Adjust stats per enemy type
        ApplyDefaultDisplayName();
        ApplyDefaultGrappleMassClass();
        switch (enemyType)
        {
            case EnemyType.Tank:
                currentHealth = maxHealth * 2.2f;
                fireRate = Mathf.Max(0.2f, fireRate * 1.6f);
                break;
            case EnemyType.Grunt:
                stoppingDistance = 2f; // get close for melee
                if (agent != null)
                {
                    agent.speed = Mathf.Min(agent.speed, moveSpeed * gruntMoveSpeedMultiplier);
                    agent.acceleration = Mathf.Min(agent.acceleration, 7f);
                }
                break;
            case EnemyType.Flying:
                // Flying enemies don't use the NavMeshAgent — they'll move via transform
                if (agent != null)
                {
                    agent.enabled = false;
                }
                // Give flying units a bit more sight/engagement range
                stoppingDistance = Mathf.Max(4f, stoppingDistance);
                break;
            case EnemyType.Shooter:
            default:
                // use defaults
                break;
        }

        if (isBoss)
        {
            currentHealth = Mathf.Max(currentHealth, maxHealth * 2.6f);
            stoppingDistance = Mathf.Max(stoppingDistance, 12f);
            fireRate = Mathf.Max(0.45f, fireRate * 0.9f);
            moveSpeed *= bossArchetype == BossArchetype.Striker ? 1.05f : 0.92f;
            meleeDamage *= 0.72f;
            bossPlayerHitCooldown = Mathf.Max(0.38f, bossPlayerHitCooldown);
            bossPatternTimer = Random.Range(1.5f, 3f);
        }

        maxHealth = Mathf.Max(maxHealth, currentHealth);

        // Start with a small randomized fire timer so all enemies don't fire at once
        fireTimer = Random.Range(0f, fireRate);
        meleeTimer = Random.Range(0f, meleeCooldown);
        laneBiasSign = Random.value < 0.5f ? -1f : 1f;
        laneBiasSeed = Random.Range(0f, 100f);
        shooterBurstTimer = Random.Range(0.12f, 0.32f);
        shooterBurstShotsRemaining = 0;
        gruntPounceCooldown = Random.Range(0.8f, 1.6f);
        tankShockwaveCooldown = Random.Range(1.8f, 2.8f);
        flyingVolleyCooldown = Random.Range(1.2f, 2.1f);
        droneDashTimer = Random.Range(droneDashIntervalMin, droneDashIntervalMax);
        bossAttackCooldown = Random.Range(0.8f, 1.4f);
        bossSpecialCooldown = Random.Range(2.2f, 3.2f);
        CacheGroundAnchor();

        if (agent == null)
        {
            Debug.LogWarning($"BasicEnemyAI on {gameObject.name} has no NavMeshAgent. Using direct steering fallback.");
        }

        if (autoBuildTypeModel)
        {
            BuildTypeModel();
        }

        EnsureCombatCollider();

        modelRoot = transform.Find("_EnemyTypeModel");
        if (modelRoot != null)
        {
            baseModelLocalPosition = modelRoot.localPosition;
            baseModelLocalRotation = modelRoot.localRotation;
        }
    }

    void Update()
    {
        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        // 1. Damage Flash Revert
        if (flashTimer > 0)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0)
                ApplyEnemyFlashColor(originalColor);
        }

        if (priorityMarker != null && priorityMarker.gameObject.activeSelf)
        {
            priorityMarker.Rotate(Vector3.up, 64f * Time.deltaTime, Space.Self);
            float pulse = 0.9f + Mathf.Sin(Time.time * 7f) * 0.16f;
            if (priorityMarkerRing != null)
                priorityMarkerRing.localScale = new Vector3(1.55f * pulse, 0.035f, 1.55f * pulse);
            if (priorityMarkerBeam != null)
                priorityMarkerBeam.localScale = new Vector3(0.14f * pulse, 5.4f, 0.14f * pulse);
            if (priorityMarkerRenderers != null)
            {
                Color c = new Color(1f, 0.86f, 0.32f, 0.72f + Mathf.Sin(Time.time * 7f) * 0.08f);
                for (int i = 0; i < priorityMarkerRenderers.Length; i++)
                {
                    Renderer markerRenderer = priorityMarkerRenderers[i];
                    ApplyTransientFxRenderer(markerRenderer, c, 1.45f);
                }
            }
        }
        if (priorityOutlineRoot != null && priorityOutlineRoot.gameObject.activeSelf && sharedPriorityOutlineMaterial != null)
        {
            float outlinePulse = 0.026f + (Mathf.Sin(Time.time * 5.5f) * 0.5f + 0.5f) * 0.012f;
            Color outlineColor = Color.Lerp(
                new Color(0.12f, 0.78f, 1f, 0.82f),
                new Color(0.92f, 1f, 1f, 0.96f),
                Mathf.Sin(Time.time * 5.5f) * 0.5f + 0.5f);
            if (sharedPriorityOutlineMaterial.HasProperty("_OutlineThickness"))
                sharedPriorityOutlineMaterial.SetFloat("_OutlineThickness", outlinePulse);
            if (sharedPriorityOutlineMaterial.HasProperty("_OutlineColor"))
                sharedPriorityOutlineMaterial.SetColor("_OutlineColor", outlineColor);
        }

        if (hurtPulseTimer > 0f) hurtPulseTimer -= Time.deltaTime;
        if (attackPulseTimer > 0f) attackPulseTimer -= Time.deltaTime;
        if (hitReactTimer > 0f) hitReactTimer -= Time.deltaTime;
        if (shooterBurstTimer > 0f) shooterBurstTimer -= Time.deltaTime;
        if (gruntPounceCooldown > 0f) gruntPounceCooldown -= Time.deltaTime;
        if (tankShockwaveCooldown > 0f) tankShockwaveCooldown -= Time.deltaTime;
        if (flyingVolleyCooldown > 0f) flyingVolleyCooldown -= Time.deltaTime;
        if (bossAttackCooldown > 0f) bossAttackCooldown -= Time.deltaTime;
        if (bossSpecialCooldown > 0f) bossSpecialCooldown -= Time.deltaTime;
        if (bossPlayerHitTimer > 0f) bossPlayerHitTimer -= Time.deltaTime;
        if (meleeStunTimer > 0f) meleeStunTimer -= Time.deltaTime;
        if (aggroVisibilityRefreshTimer > 0f) aggroVisibilityRefreshTimer -= Time.deltaTime;
        if (threatLineOfSightRefreshTimer > 0f) threatLineOfSightRefreshTimer -= Time.deltaTime;

        if (player == null) return;
        UpdatePlayerTracking();
        if (meleeStunTimer > 0f)
        {
            FacePlayer();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        UpdateModelMotion(distanceToPlayer);
        if (!hasAggro && CanAcquireAggro(distanceToPlayer))
            hasAggro = true;
        if (!hasAggro)
        {
            if (agent != null && agent.enabled)
                agent.isStopped = true;
            return;
        }
        if (UpdateActiveMobilityState(distanceToPlayer))
            return;

        bool canUseAgent = agent != null && agent.enabled && agent.isOnNavMesh;
        if (canUseAgent)
        {
            if (enemyType == EnemyType.Grunt && distanceToPlayer <= meleeRange)
            {
                agent.isStopped = true;
                FacePlayer();
            }
            else if (distanceToPlayer > stoppingDistance)
            {
                agent.isStopped = false;
                Vector3 target = enemyType == EnemyType.Grunt
                    ? GetLandingContestTarget(18f, 0.9f, 1.1f, 1.9f)
                    : enemyType == EnemyType.Tank
                        ? GetLandingContestTarget(20f, 0.45f, 1.4f, 2.8f)
                        : GetLandingContestTarget(20f, 0.65f, 2.4f, 2.1f);
                if (enemyType == EnemyType.Shooter)
                {
                    Vector3 lateral = Vector3.Cross(Vector3.up, (player.position - transform.position).normalized);
                    Vector3 toTarget = target - transform.position;
                    Vector3 toward = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
                    target += lateral * Mathf.Sin(Time.time * 2f) * 3.1f;
                    target += toward * Mathf.Clamp(distanceToPlayer - stoppingDistance * 1.06f, -4.5f, 3.5f);
                    if (GetPlayerPlanarSpeed() > 18f)
                        target = GetRouteCutTarget(target, 2.8f, 2.2f);
                }
                else if (enemyType == EnemyType.Tank)
                {
                    agent.speed = Mathf.Max(agent.speed, 2.5f);
                    agent.acceleration = Mathf.Max(agent.acceleration, 10f);
                    if (GetPlayerPlanarSpeed() > 16f)
                        target = GetRouteCutTarget(target, 1.4f, 2.8f);
                }

                Vector3 formationForward = new Vector3(target.x - transform.position.x, 0f, target.z - transform.position.z);
                target += GetFormationOffset(formationForward);
                if (enemyType == EnemyType.Grunt)
                    agent.speed = Mathf.Max(0.1f, moveSpeed * gruntMoveSpeedMultiplier);

                target += GetAllySeparationOffset();
                agent.SetDestination(target);
            }
            else
            {
                agent.isStopped = true;
                FacePlayer();
            }
        }
        else
        {
            if (enemyType == EnemyType.Flying)
            {
                HandleFlyingMovement();
            }
            else
            {
                Vector3 target = enemyType == EnemyType.Grunt
                    ? GetLandingContestTarget(18f, 0.95f, 1.0f, 1.8f)
                    : enemyType == EnemyType.Tank
                        ? GetLandingContestTarget(20f, 0.5f, 1.3f, 2.6f)
                        : GetLandingContestTarget(20f, 0.7f, 2.2f, 1.9f);
                target.y = hasGroundAnchor ? groundY : transform.position.y;

                Vector3 toPlayer = target - transform.position;
                float planarDistance = toPlayer.magnitude;
                if (planarDistance > 0.001f)
                {
                    Vector3 moveDir = toPlayer / planarDistance;
                    float groundMoveSpeed = enemyType == EnemyType.Grunt
                        ? moveSpeed * gruntMoveSpeedMultiplier
                        : enemyType == EnemyType.Tank
                            ? moveSpeed * 0.55f
                            : moveSpeed;

                    bool usedPath = enemyType != EnemyType.Flying && TryFollowGroundPath(target, groundMoveSpeed);
                    bool canDirectChase =
                                          Mathf.Abs(target.y - transform.position.y) <= floorSnapTolerance &&
                                          HasLineOfSightTo(target + Vector3.up * 0.9f) &&
                                          (arenaGenerator == null || planarDistance <= Mathf.Max(3.6f, stoppingDistance * 0.55f));

                    if (usedPath)
                    {
                        FacePlayer();
                    }
                    else if (!canDirectChase)
                    {
                        ClampToAccessibleSpace();
                        FacePlayer();
                    }
                    else if (enemyType == EnemyType.Grunt)
                    {
                        if (planarDistance > meleeRange)
                        {
                            Vector3 groundedTarget = target + GetFormationOffset(moveDir) * 0.45f + GetAllySeparationOffset();
                            TryMoveTowards(groundedTarget, moveSpeed * gruntMoveSpeedMultiplier * Time.deltaTime);
                        }
                        else
                        {
                            FacePlayer();
                        }
                    }
                    else if (enemyType == EnemyType.Tank)
                    {
                        if (planarDistance > stoppingDistance)
                        {
                            Vector3 groundedTarget = GetPlayerPlanarSpeed() > 16f
                                ? GetRouteCutTarget(target, 1.2f, 2.6f)
                                : target;
                            groundedTarget += GetFormationOffset(moveDir) * 0.55f + GetAllySeparationOffset() * 0.6f;
                            TryMoveTowards(groundedTarget, moveSpeed * 0.55f * Time.deltaTime);
                        }
                        else
                        {
                            FacePlayer();
                        }
                    }
                    else if (planarDistance > stoppingDistance)
                    {
                        Vector3 desired = GetPlayerPlanarSpeed() > 18f
                            ? GetRouteCutTarget(target, 2.6f, 2f)
                            : target;
                        desired += GetFormationOffset(moveDir) + GetAllySeparationOffset();
                        Vector3 lateral = Vector3.Cross(Vector3.up, moveDir).normalized;
                        Vector3 detourBias = lateral * Mathf.Sin(Time.time * 2.2f) * obstacleAvoidanceDistance;
                        Vector3 strafing = desired + detourBias;

                        // Prefer navigating around obstacles instead of walking through them.
                        if (!HasLineOfSightTo(desired + Vector3.up * 0.9f))
                        {
                            Vector3 detour = transform.position + lateral * Mathf.Sign(Mathf.Sin(Time.time * 1.1f)) * obstacleAvoidanceDistance;
                            strafing = Vector3.MoveTowards(transform.position, detour, moveSpeed * 0.8f * Time.deltaTime);
                        }

                        // Raycast forward a bit; if blocked, slide sideways instead of tunneling.
                        if (IsBlockedAhead(strafing))
                        {
                            Vector3 sideways = transform.position + lateral * obstacleAvoidanceDistance;
                            strafing = Vector3.MoveTowards(transform.position, sideways, moveSpeed * 0.6f * Time.deltaTime);
                        }

                        TryMoveTowards(strafing, moveSpeed * Time.deltaTime);
                    }

                    if (!usedPath && hasGroundAnchor && Mathf.Abs(transform.position.y - groundY) <= floorSnapTolerance)
                    {
                        Vector3 grounded = transform.position;
                        grounded.y = groundY;
                        transform.position = grounded;
                    }

                    if (!usedPath)
                        ClampToAccessibleSpace();

                    Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
                }
            }
        }

        // 3. Combat behavior depends on enemy type
        if (isBoss && bossArchetype != BossArchetype.None)
        {
            HandleBossPositioning(distanceToPlayer);
            HandleBossCombat(distanceToPlayer);
        }
        else if (enemyType == EnemyType.Shooter)
        {
            if (distanceToPlayer <= stoppingDistance + 7f && GetThreatLineOfSightCached())
            {
                fireTimer -= Time.deltaTime;
                if (fireTimer <= 0 &&
                    CanCommitRolePressure(CombatRole.Suppressor, 0.42f, 0.08f) &&
                    ShouldSuppressorCommit(distanceToPlayer))
                {
                    StartShooterBurst();
                    MarkRolePressureCommitted(CombatRole.Suppressor, 0.42f, 0.08f);
                    fireTimer = fireRate * 1.2f;
                }

                if (shooterBurstShotsRemaining > 0 && shooterBurstTimer <= 0f)
                {
                    if (projectilePrefab != null)
                        ShootWithSpread(Random.Range(-4f, 4f));
                    else
                        TryDirectDamage(8f);
                    shooterBurstShotsRemaining--;
                    shooterBurstTimer = 0.12f;
                }
            }
        }
        else if (enemyType == EnemyType.Grunt)
        {
            if (distanceToPlayer > meleeRange * 1.5f &&
                distanceToPlayer < 12f &&
                gruntPounceCooldown <= 0f &&
                gruntPounceWindupRoutine == null &&
                CanCommitRolePressure(CombatRole.Diver, 0.68f, 0.06f) &&
                ShouldDiverCommit(distanceToPlayer))
            {
                gruntPounceWindupRoutine = StartCoroutine(GruntPounceTelegraphRoutine());
                MarkRolePressureCommitted(CombatRole.Diver, 0.68f, 0.06f);
                gruntPounceCooldown = Random.Range(1.2f, 2.2f);
            }

            if (distanceToPlayer <= meleeRange)
            {
                meleeTimer -= Time.deltaTime;
                if (meleeTimer <= 0f &&
                    meleeWindupRoutine == null &&
                    CanCommitRolePressure(CombatRole.Diver, 0.34f, 0.04f) &&
                    ShouldDiverCommit(distanceToPlayer))
                {
                    meleeWindupRoutine = StartCoroutine(MeleeTelegraphRoutine(0.11f, meleeDamage, false, meleeCooldown, new Color(1f, 0.2f, 0.14f), meleeRange));
                    MarkRolePressureCommitted(CombatRole.Diver, 0.34f, 0.04f);
                }
            }
        }
        else if (enemyType == EnemyType.Tank)
        {
            if (distanceToPlayer <= 7f &&
                tankShockwaveCooldown <= 0f &&
                tankShockwaveRoutine == null &&
                CanCommitRolePressure(CombatRole.Bulwark, 1.15f, 0.12f) &&
                ShouldBulwarkCommit(distanceToPlayer))
            {
                tankShockwaveRoutine = StartCoroutine(TankShockwaveTelegraphRoutine());
                MarkRolePressureCommitted(CombatRole.Bulwark, 1.15f, 0.12f);
                tankShockwaveCooldown = Random.Range(2.8f, 4.2f);
            }

            if (distanceToPlayer <= stoppingDistance + 8f)
            {
                fireTimer -= Time.deltaTime;
                if (fireTimer <= 0 &&
                    CanCommitRolePressure(CombatRole.Bulwark, 0.78f, 0.1f) &&
                    ShouldBulwarkCommit(distanceToPlayer))
                {
                    if (projectilePrefab != null && (!IsPlayerGrounded() || GetPlayerPlanarSpeed() > 18f))
                    {
                        ShootWithSpread(-6f);
                        ShootWithSpread(6f);
                    }
                    else if (projectilePrefab != null)
                        Shoot();
                    else
                        tankVolleyRoutine = StartCoroutine(TankDirectVolleyTelegraphRoutine(14f));
                    MarkRolePressureCommitted(CombatRole.Bulwark, 0.78f, 0.1f);
                    fireTimer = fireRate * 1.6f;
                }
            }
        }
        else if (enemyType == EnemyType.Flying)
        {
            bool dashing = droneDashTimeRemaining > 0f;
            float droneEngageDistance = Mathf.Max(stoppingDistance + 10f, dronePreferredDistance * 1.8f);
            if (!dashing &&
                distanceToPlayer <= droneEngageDistance &&
                flyingVolleyCooldown <= 0f &&
                flyingVolleyRoutine == null &&
                droneDashTimer <= 0f &&
                CanCommitRolePressure(CombatRole.Harrier, 0.9f, 0.08f) &&
                ShouldHarrierCommit(distanceToPlayer))
            {
                MarkRolePressureCommitted(CombatRole.Harrier, 0.9f, 0.08f);
                Vector3 toPlayer = player != null ? player.position - transform.position : transform.forward;
                toPlayer.y = 0f;
                Vector3 lateral = toPlayer.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, toPlayer.normalized) : transform.right;
                Vector3 dashDirection = lateral * (Random.value < 0.5f ? -1f : 1f);
                float flatDistance = toPlayer.magnitude;
                if (flatDistance < dronePreferredDistance * 0.72f)
                    dashDirection += -toPlayer.normalized * 0.85f;
                else if (flatDistance > dronePreferredDistance * 1.35f)
                    dashDirection += toPlayer.normalized * 0.65f;

                flyingVolleyRoutine = StartCoroutine(FlyingVolleyTelegraphRoutine(dashDirection.normalized));
            }
        }
    }

    private void UpdatePlayerTracking()
    {
        if (player == null)
            return;

        PlayerController cachedController = GetCachedPlayerController();

        Vector3 currentPosition = player.position;
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        Vector3 sampledVelocity = hasTrackedPlayerPosition
            ? (currentPosition - lastTrackedPlayerPosition) / dt
            : Vector3.zero;
        if (cachedController != null)
            sampledVelocity = Vector3.Lerp(sampledVelocity, cachedController.WorldVelocity, 0.72f);

        float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
        trackedPlayerVelocity = Vector3.Lerp(trackedPlayerVelocity, sampledVelocity, blend);
        lastTrackedPlayerPosition = currentPosition;
        hasTrackedPlayerPosition = true;
    }

    private bool UpdateActiveMobilityState(float distanceToPlayer)
    {
        if (gruntPounceWindupRoutine != null || flyingVolleyRoutine != null || meleeWindupRoutine != null || tankVolleyRoutine != null)
        {
            FacePlayer();
            return true;
        }

        if (gruntPounceTimeRemaining <= 0f)
            return false;

        gruntPounceTimeRemaining -= Time.deltaTime;
        transform.position += gruntPounceVelocity * Time.deltaTime;
        ClampToAccessibleSpace();
        FacePlayer();

        if (distanceToPlayer <= meleeRange * 1.15f)
        {
            meleeTimer -= Time.deltaTime;
            if (meleeTimer <= 0f &&
                meleeWindupRoutine == null &&
                CanCommitRolePressure(CombatRole.Diver, 0.24f, 0.03f))
            {
                meleeWindupRoutine = StartCoroutine(MeleeTelegraphRoutine(0.08f, meleeDamage, false, Mathf.Max(0.42f, meleeCooldown * 0.72f), new Color(1f, 0.28f, 0.16f), meleeRange * 0.92f));
                MarkRolePressureCommitted(CombatRole.Diver, 0.24f, 0.03f);
            }
        }

        if (gruntPounceTimeRemaining <= 0f)
            gruntPounceVelocity = Vector3.zero;

        return true;
    }

    private void HandleFlyingMovement()
    {
        if (player == null) return;

        flyPhase += Time.deltaTime * bobFrequency;

        Vector3 predictedPlayer = GetMobilityAwareTarget(20f, 0.78f, false);
        float playerCommitment = GetPlayerCommitment01();
        if (playerCommitment > 0.16f)
        {
            Vector3 planarVelocity = new Vector3(trackedPlayerVelocity.x, 0f, trackedPlayerVelocity.z);
            if (planarVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 forward = planarVelocity.normalized;
                Vector3 lateralBias = Vector3.Cross(Vector3.up, forward).normalized * laneBiasSign;
                predictedPlayer += forward * Mathf.Lerp(0.45f, 1.55f, playerCommitment);
                predictedPlayer += lateralBias * Mathf.Lerp(0.2f, 0.9f, playerCommitment);
            }
        }
        Vector3 toPlayer = predictedPlayer - transform.position;
        Vector3 flatToPlayer = new Vector3(toPlayer.x, 0f, toPlayer.z);
        Vector3 towardPlayer = flatToPlayer.sqrMagnitude > 0.01f ? flatToPlayer.normalized : transform.forward;
        Vector3 lateral = Vector3.Cross(Vector3.up, towardPlayer).normalized;
        float distance = flatToPlayer.magnitude;

        if (droneDashTimeRemaining > 0f)
        {
            droneDashTimeRemaining -= Time.deltaTime;
            transform.position += droneDashVelocity * Time.deltaTime;
            ClampFlyingHeight();
            FacePlayer();
            if (droneDashTimeRemaining <= 0f)
                flyingVolleyCooldown = Mathf.Max(flyingVolleyCooldown, dronePostDashShootDelay);
            return;
        }

        droneDashTimer -= Time.deltaTime;

        Vector3 pos = transform.position;
        float targetY = predictedPlayer.y + hoverHeight + Mathf.Sin(flyPhase * 1.25f) * bobAmplitude;
        UpdateFlyingGroundAnchor();
        if (hasGroundAnchor)
            targetY = Mathf.Max(targetY, groundY + hoverHeight);
        pos.y = Mathf.MoveTowards(pos.y, targetY, flySpeed * 0.28f * Time.deltaTime);
        if (distance < dronePreferredDistance * 0.78f)
        {
            pos += -towardPlayer * droneRangeCorrectionSpeed * Time.deltaTime;
        }
        else if (distance > dronePreferredDistance * 1.28f)
        {
            pos += towardPlayer * droneRangeCorrectionSpeed * 0.72f * Time.deltaTime;
        }
        else
        {
            pos.x = Mathf.Lerp(pos.x, transform.position.x, Time.deltaTime * 10f);
            pos.z = Mathf.Lerp(pos.z, transform.position.z, Time.deltaTime * 10f);
        }
        transform.position = pos;
        ClampFlyingHeight();
        FacePlayer();

        if (distance <= dronePreferredDistance + 4.5f && GetThreatLineOfSightCached())
        {
            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f && flyingVolleyRoutine == null)
            {
                ShootWithSpread(0f);
                fireTimer = Mathf.Max(0.32f, fireRate * 0.68f);
            }
        }
    }

    private void BeginDroneDash(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f)
            direction = transform.right;

        direction.y = 0f;
        droneDashVelocity = direction.normalized * droneDashSpeed;
        droneDashTimeRemaining = droneDashDuration;
        droneDashTimer = Random.Range(droneDashIntervalMin, droneDashIntervalMax);
    }

    private void ClampFlyingHeight()
    {
        Vector3 pos = transform.position;
        UpdateFlyingGroundAnchor();
        float floorY = hasGroundAnchor ? groundY : player != null ? player.position.y : pos.y - hoverHeight;
        float minY = floorY + 1.7f;
        float maxY = floorY + 6.5f;
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        transform.position = pos;
    }

    private void UpdateFlyingGroundAnchor()
    {
        if (TryFindGroundBelow(12f, out RaycastHit hit))
        {
            groundY = hit.point.y + 0.05f;
            hasGroundAnchor = true;
        }
    }

    private void HandleBossCombat(float distanceToPlayer)
    {
        bossPatternTimer -= Time.deltaTime;
        int phase = GetBossPhase();
        bool routineActive = bossRoutine != null;

        switch (bossArchetype)
        {
            case BossArchetype.Warden:
                if (distanceToPlayer <= stoppingDistance + 10f)
                {
                    if (!routineActive &&
                        bossPatternTimer <= 0f &&
                        bossSpecialCooldown <= 0f &&
                        CanCommitRolePressure(CombatRole.Boss, 1.45f, 0.14f) &&
                        ShouldBossSpecialCommit(distanceToPlayer))
                    {
                        if (phase >= 1 && Random.value > 0.45f)
                            StartBossRoutine(BossCageLock(3.8f + phase * 0.45f, meleeDamage * (0.72f + phase * 0.15f)));
                        else
                            StartBossRoutine(BossCrossfireLock(3 + phase, 5.8f + phase, meleeDamage * (0.55f + phase * 0.15f)));
                        MarkRolePressureCommitted(CombatRole.Boss, 1.45f, 0.14f);
                        ResetBossPatternTimer(0.95f, 1.25f, phase);
                        bossSpecialCooldown = Random.Range(4.2f, 5.4f) - phase * 0.35f;
                    }
                    else if (!routineActive &&
                             bossPatternTimer <= 0f &&
                             bossAttackCooldown <= 0f &&
                             CanCommitRolePressure(CombatRole.Boss, 0.82f, 0.08f) &&
                             ShouldBossAttackCommit(distanceToPlayer))
                    {
                        StartBossRoutine(BossPulseSequence(3.2f + phase, meleeDamage * (0.65f + phase * 0.15f), 0.32f, new Color(1f, 0.36f, 0.08f)));
                        MarkRolePressureCommitted(CombatRole.Boss, 0.82f, 0.08f);
                        ResetBossPatternTimer(0.55f, 0.8f, phase);
                        bossAttackCooldown = Random.Range(1.1f, 1.6f) - phase * 0.12f;
                    }
                    else if (!routineActive &&
                             bossPatternTimer <= 0f &&
                             shooterBurstShotsRemaining <= 0 &&
                             shooterBurstTimer <= 0f &&
                             CanCommitRolePressure(CombatRole.Boss, 0.62f, 0.06f) &&
                             ShouldBossAttackCommit(distanceToPlayer))
                    {
                        StartBossRoutine(BossFanBurst(4 + phase, 14f + phase * 4f));
                        MarkRolePressureCommitted(CombatRole.Boss, 0.62f, 0.06f);
                        ResetBossPatternTimer(0.42f, 0.65f, phase);
                        shooterBurstTimer = 0.8f;
                    }
                }
                break;

            case BossArchetype.Striker:
                if (!routineActive &&
                    bossPatternTimer <= 0f &&
                    bossSpecialCooldown <= 0f &&
                    CanCommitRolePressure(CombatRole.Boss, 1.35f, 0.12f) &&
                    ShouldBossSpecialCommit(distanceToPlayer))
                {
                    if (phase >= 1 && Random.value > 0.4f)
                        StartBossRoutine(BossRazorSweep(2 + phase, 8.8f + phase * 1.1f, 2.6f + phase * 0.3f, meleeDamage * (0.6f + phase * 0.14f)));
                    else
                        StartBossRoutine(BossComboAssault(2 + phase, 8.2f + phase, 3.4f + phase * 0.55f, meleeDamage * (0.72f + phase * 0.16f)));
                    MarkRolePressureCommitted(CombatRole.Boss, 1.35f, 0.12f);
                    ResetBossPatternTimer(0.92f, 1.18f, phase);
                    bossSpecialCooldown = Random.Range(3.6f, 4.8f) - phase * 0.3f;
                }
                else if (!routineActive && distanceToPlayer <= meleeRange * (1.5f + phase * 0.15f))
                {
                    meleeTimer -= Time.deltaTime;
                    if (meleeTimer <= 0f &&
                        meleeWindupRoutine == null &&
                        CanCommitRolePressure(CombatRole.Boss, 0.42f, 0.04f) &&
                        ShouldBossAttackCommit(distanceToPlayer))
                    {
                        meleeWindupRoutine = StartCoroutine(MeleeTelegraphRoutine(0.12f, meleeDamage, true, Mathf.Max(0.48f, meleeCooldown * 0.62f), new Color(1f, 0.34f, 0.12f), meleeRange * 1.08f));
                        MarkRolePressureCommitted(CombatRole.Boss, 0.42f, 0.04f);
                    }
                }
                else if (!routineActive &&
                         bossPatternTimer <= 0f &&
                         bossAttackCooldown <= 0f &&
                         CanCommitRolePressure(CombatRole.Boss, 0.9f, 0.08f) &&
                         ShouldBossAttackCommit(distanceToPlayer))
                {
                    Vector3 rushTarget = GetBossGroundTarget(1.15f);
                    transform.position = Vector3.MoveTowards(transform.position, rushTarget, moveSpeed * (3f + phase * 0.6f) * Time.deltaTime);
                    if (distanceToPlayer <= 4.8f)
                    {
                        StartBossRoutine(BossGroundStrike(GetBossGroundTarget(1.05f), 3.2f + phase * 0.4f, meleeDamage * 0.7f, new Color(1f, 0.18f, 0.12f)));
                        MarkRolePressureCommitted(CombatRole.Boss, 0.9f, 0.08f);
                        ResetBossPatternTimer(0.48f, 0.72f, phase);
                        bossAttackCooldown = Random.Range(1.0f, 1.5f);
                    }
                }
                break;

            case BossArchetype.Sentinel:
                if (!routineActive &&
                    bossPatternTimer <= 0f &&
                    bossSpecialCooldown <= 0f &&
                    CanCommitRolePressure(CombatRole.Boss, 1.3f, 0.1f) &&
                    ShouldBossSpecialCommit(distanceToPlayer))
                {
                    if (phase >= 1 && Random.value > 0.4f)
                        StartBossRoutine(BossSkyLanceBarrage(3 + phase, 2.4f + phase * 0.3f, meleeDamage * (0.56f + phase * 0.12f)));
                    else
                        StartBossRoutine(BossSentinelDiveRun(2 + phase, 2.8f + phase * 0.4f, meleeDamage * 0.72f));
                    MarkRolePressureCommitted(CombatRole.Boss, 1.3f, 0.1f);
                    ResetBossPatternTimer(0.88f, 1.12f, phase);
                    bossSpecialCooldown = Random.Range(3.2f, 4.1f) - phase * 0.25f;
                }
                else if (!routineActive &&
                         bossPatternTimer <= 0f &&
                         distanceToPlayer <= stoppingDistance + 12f &&
                         bossAttackCooldown <= 0f &&
                         CanCommitRolePressure(CombatRole.Boss, 0.78f, 0.08f) &&
                         ShouldBossAttackCommit(distanceToPlayer))
                {
                    StartBossRoutine(BossSentinelStrafeVolley(4 + phase, meleeDamage * 0.45f));
                    MarkRolePressureCommitted(CombatRole.Boss, 0.78f, 0.08f);
                    ResetBossPatternTimer(0.5f, 0.76f, phase);
                    bossAttackCooldown = Random.Range(0.95f, 1.35f) - phase * 0.08f;
                }
                break;
        }
    }

    private void ResetBossPatternTimer(float minDelay, float maxDelay, int phase)
    {
        float delay = Random.Range(minDelay, Mathf.Max(minDelay, maxDelay));
        bossPatternTimer = Mathf.Max(0.14f, delay - phase * 0.08f);
    }

    private void HandleBossPositioning(float distanceToPlayer)
    {
        if (player == null || bossRoutine != null) return;

        Vector3 playerFlat = player.position;
        playerFlat = GetBossGroundTarget(0.9f);

        switch (bossArchetype)
        {
            case BossArchetype.Warden:
            {
                Vector3 orbit = new Vector3(Mathf.Sin(Time.time * 0.9f), 0f, Mathf.Cos(Time.time * 0.9f)) * (6f + GetBossPhase() * 1.2f);
                Vector3 hoverTarget = playerFlat + orbit + Vector3.up * Mathf.Max(hoverHeight + 1.8f, 4.2f);
                transform.position = Vector3.Lerp(transform.position, hoverTarget, Time.deltaTime * 1.1f);
                FacePlayer();
                break;
            }
            case BossArchetype.Striker:
                if (distanceToPlayer > meleeRange * 1.2f)
                {
                    Vector3 rushTarget = Vector3.MoveTowards(transform.position, playerFlat, moveSpeed * (1.15f + GetBossPhase() * 0.2f) * Time.deltaTime);
                    transform.position = rushTarget;
                    ClampToAccessibleSpace();
                }
                FacePlayer();
                break;
            case BossArchetype.Sentinel:
            {
                Vector3 targetAirPoint = GetBossAirTarget(0f, 1.05f);
                Vector3 lateral = Vector3.Cross(Vector3.up, (targetAirPoint - transform.position).normalized);
                if (lateral.sqrMagnitude < 0.01f) lateral = transform.right;
                float sway = Mathf.Sin(Time.time * 1.6f) * (5.2f + GetBossPhase());
                Vector3 glideTarget = targetAirPoint + lateral.normalized * sway + Vector3.up * (hoverHeight + 4.8f);
                transform.position = Vector3.Lerp(transform.position, glideTarget, Time.deltaTime * 0.95f);
                FacePlayer();
                break;
            }
        }
    }

    private int GetBossPhase()
    {
        if (!isBoss || maxHealth <= 0.01f) return 0;
        float health01 = Mathf.Clamp01(currentHealth / Mathf.Max(0.01f, maxHealth));
        if (health01 <= 0.33f) return 2;
        if (health01 <= 0.66f) return 1;
        return 0;
    }

    private void CacheGroundAnchor()
    {
        hasGroundAnchor = false;
        groundY = transform.position.y;

        if (TryFindGroundBelow(8f, out RaycastHit hit))
        {
            if (hit.point.y > transform.position.y + 0.35f)
                return;

            groundY = hit.point.y + 0.05f;
            hasGroundAnchor = true;
        }
    }

    private CombatRole ResolveCombatRole()
    {
        if (isBoss)
            return CombatRole.Boss;

        return enemyType switch
        {
            EnemyType.Grunt => CombatRole.Diver,
            EnemyType.Tank => CombatRole.Bulwark,
            EnemyType.Flying => CombatRole.Harrier,
            _ => CombatRole.Suppressor
        };
    }

    private string GetPriorityLabel()
    {
        return CurrentCombatRole switch
        {
            CombatRole.Diver => "DIVE",
            CombatRole.Bulwark => "TANK",
            CombatRole.Harrier => "AIR",
            CombatRole.Boss => "BOSS",
            _ => "PRESS"
        };
    }

    private bool IsCurrentlyTelegraphing()
    {
        if (IsCombatResolved)
            return false;

        return gruntPounceWindupRoutine != null ||
               flyingVolleyRoutine != null ||
               meleeWindupRoutine != null ||
               tankVolleyRoutine != null ||
               gruntPounceTimeRemaining > 0f ||
               tankShockwaveRoutine != null ||
               shooterBurstShotsRemaining > 0 ||
               droneDashTimeRemaining > 0f ||
               bossRoutine != null ||
               (isBoss && attackPulseTimer > 0.1f);
    }

    private float GetCurrentPressureScore()
    {
        if (IsCombatResolved)
            return 0f;

        float distanceToPlayer = player != null
            ? Vector3.Distance(transform.position, player.position)
            : float.MaxValue;
        bool hasThreatWindow = HasThreatWindow(distanceToPlayer);
        bool hasLineOfThreat = GetThreatLineOfSightCached();
        bool hasMeleeThreat = HasMeleeThreatOpportunity(distanceToPlayer);
        float score = CurrentCombatRole switch
        {
            CombatRole.Boss => 2.5f,
            CombatRole.Harrier => 1.35f,
            CombatRole.Diver => 1.2f,
            CombatRole.Bulwark => 1.05f,
            _ => 0.95f
        };

        if (shooterBurstShotsRemaining > 0)
            score += 1.2f + shooterBurstShotsRemaining * 0.2f;
        if (meleeWindupRoutine != null)
            score += 0.8f;
        if (tankVolleyRoutine != null)
            score += 0.95f;
        if (gruntPounceWindupRoutine != null)
            score += 0.9f;
        if (flyingVolleyRoutine != null)
            score += 1.05f;
        if (gruntPounceTimeRemaining > 0f)
            score += 1.8f;
        if (tankShockwaveRoutine != null)
            score += 1.7f;
        if (droneDashTimeRemaining > 0f)
            score += 1.25f;
        if (bossRoutine != null)
            score += 2.2f;

        if (hasThreatWindow && hasLineOfThreat && fireTimer <= 0.18f)
            score += 0.35f;
        if (hasMeleeThreat && meleeTimer <= 0.18f)
            score += 0.45f;
        if (isBoss && bossAttackCooldown <= 0.22f)
            score += 0.5f;
        if (isBoss && bossSpecialCooldown <= 0.3f)
            score += 0.55f;
        if ((bossRoutine != null || shooterBurstShotsRemaining > 0 || tankShockwaveRoutine != null) && attackPulseTimer > 0.08f)
            score += 0.28f;

        return score;
    }

    private string BuildPressureDebugSummary()
    {
        if (IsCombatResolved)
            return "resolved";

        List<string> reasons = new List<string>(8)
        {
            CurrentCombatRole switch
            {
                CombatRole.Boss => "base:b2.50",
                CombatRole.Harrier => "base:h1.35",
                CombatRole.Diver => "base:d1.20",
                CombatRole.Bulwark => "base:t1.05",
                _ => "base:s0.95"
            }
        };

        float distanceToPlayer = player != null
            ? Vector3.Distance(transform.position, player.position)
            : float.MaxValue;
        bool hasThreatWindow = HasThreatWindow(distanceToPlayer);
        bool hasLineOfThreat = GetThreatLineOfSightCached();
        bool hasMeleeThreat = HasMeleeThreatOpportunity(distanceToPlayer);

        if (shooterBurstShotsRemaining > 0)
            reasons.Add($"burst:{shooterBurstShotsRemaining}");
        if (meleeWindupRoutine != null)
            reasons.Add("meleeTelegraph");
        if (tankVolleyRoutine != null)
            reasons.Add("tankVolleyTelegraph");
        if (isBoss && bossPatternTimer > 0.01f)
            reasons.Add($"bossRhythm:{bossPatternTimer:0.00}");
        if (gruntPounceWindupRoutine != null)
            reasons.Add("pounceTelegraph");
        if (flyingVolleyRoutine != null)
            reasons.Add("volleyTelegraph");
        if (gruntPounceTimeRemaining > 0f)
            reasons.Add("pounce");
        if (tankShockwaveRoutine != null)
            reasons.Add("shockwave");
        if (droneDashTimeRemaining > 0f)
            reasons.Add("dash");
        if (bossRoutine != null)
            reasons.Add("bossRoutine");
        if (hasThreatWindow && hasLineOfThreat && fireTimer <= 0.18f)
            reasons.Add("readyFire");
        if (hasMeleeThreat && meleeTimer <= 0.18f)
            reasons.Add("readyMelee");
        if (isBoss && bossAttackCooldown <= 0.22f)
            reasons.Add("bossAtk");
        if (isBoss && bossSpecialCooldown <= 0.3f)
            reasons.Add("bossSpec");
        if ((bossRoutine != null || shooterBurstShotsRemaining > 0 || tankShockwaveRoutine != null) && attackPulseTimer > 0.08f)
            reasons.Add("pulse");
        if (IsActivelyTelegraphing)
            reasons.Add("telegraph");

        return string.Join(", ", reasons);
    }

    private string BuildCommitGateDebugSummary()
    {
        if (IsCombatResolved)
            return "gate:resolved";

        UpdateGlobalPressureState();

        CombatRole role = CurrentCombatRole;
        int roleIndex = GetRoleIndex(role);
        float now = Time.time;
        float laneReady = Mathf.Max(0f, roleNextCommitTime[roleIndex] - now);
        float playerCommitment = GetPlayerCommitment01();
        float pressureLimit = GetPressureLimitForCommitment(playerCommitment, role == CombatRole.Boss);
        float cost = GetRolePressureCost(role);
        float distanceToPlayer = player != null ? Vector3.Distance(transform.position, player.position) : float.MaxValue;
        bool roleIntent = role switch
        {
            CombatRole.Diver => ShouldDiverCommit(distanceToPlayer),
            CombatRole.Bulwark => ShouldBulwarkCommit(distanceToPlayer),
            CombatRole.Harrier => ShouldHarrierCommit(distanceToPlayer),
            CombatRole.Suppressor => ShouldSuppressorCommit(distanceToPlayer),
            CombatRole.Boss => ShouldBossAttackCommit(distanceToPlayer) || ShouldBossSpecialCommit(distanceToPlayer),
            _ => true
        };
        bool budgetOpen = globalPressureScore + cost <= pressureLimit;
        bool cadenceOpen = laneReady <= 0.001f;

        return $"gate[{role}:{(roleIntent ? "intent" : "hold")}, lane={laneReady:0.00}, cost={cost:0.00}, shared={globalPressureScore:0.00}, limit={pressureLimit:0.00}, ok={(cadenceOpen && budgetOpen ? "yes" : "no")}]";
    }

    private static int GetRoleIndex(CombatRole role)
    {
        return role switch
        {
            CombatRole.Suppressor => 0,
            CombatRole.Diver => 1,
            CombatRole.Bulwark => 2,
            CombatRole.Harrier => 3,
            _ => 4
        };
    }

    private bool CanCommitRolePressure(CombatRole role, float cadence, float randomOffset = 0f)
    {
        int index = GetRoleIndex(role);
        if (Time.time < roleNextCommitTime[index])
            return false;

        UpdateGlobalPressureState();

        float playerCommitment = GetPlayerCommitment01();
        float pressureLimit = GetPressureLimitForCommitment(playerCommitment, role == CombatRole.Boss);

        float commitCost = GetRolePressureCost(role);
        return globalPressureScore + commitCost <= pressureLimit;
    }

    private void MarkRolePressureCommitted(CombatRole role, float cadence)
    {
        int index = GetRoleIndex(role);
        roleLastCommitTime[index] = Time.time;
        roleNextCommitTime[index] = Time.time + Mathf.Max(0.05f, cadence);
        RegisterGlobalPressureCommit(role);
    }

    private void MarkRolePressureCommitted(CombatRole role, float cadence, float randomOffset)
    {
        int index = GetRoleIndex(role);
        roleLastCommitTime[index] = Time.time;
        roleNextCommitTime[index] = Time.time + Mathf.Max(0.05f, cadence) + Random.Range(0f, Mathf.Max(0f, randomOffset));
        RegisterGlobalPressureCommit(role);
    }

    private float GetRolePressureCost(CombatRole role)
    {
        return role switch
        {
            CombatRole.Boss => 1.55f,
            CombatRole.Bulwark => 0.98f,
            CombatRole.Harrier => 0.86f,
            CombatRole.Diver => 0.78f,
            _ => 0.68f
        };
    }

    private void RegisterGlobalPressureCommit(CombatRole role)
    {
        globalPressureScore += GetRolePressureCost(role);
        float burstWindow = role switch
        {
            CombatRole.Boss => 0.48f,
            CombatRole.Bulwark => 0.34f,
            CombatRole.Harrier => 0.28f,
            CombatRole.Diver => 0.24f,
            _ => 0.22f
        };
        globalPressureBurstUntil = Mathf.Max(globalPressureBurstUntil, Time.time + burstWindow);
    }

    private static void UpdateGlobalPressureState()
    {
        float now = Time.time;
        if (globalPressureLastUpdateTime < 0f)
        {
            globalPressureLastUpdateTime = now;
            return;
        }

        float elapsed = now - globalPressureLastUpdateTime;
        globalPressureLastUpdateTime = now;
        if (elapsed <= 0f)
            return;

        // Treat long idle gaps as a fresh encounter window instead of carrying stale pressure.
        if (elapsed > 2.5f)
        {
            globalPressureScore = 0f;
            globalPressureBurstUntil = 0f;
            return;
        }

        const float decayRate = 1.85f;
        globalPressureScore = Mathf.MoveTowards(globalPressureScore, 0f, elapsed * decayRate);
        if (now <= globalPressureBurstUntil)
            globalPressureScore = Mathf.Max(globalPressureScore, 0.85f);
    }

    private bool WasRecentRolePressure(CombatRole role, float window)
    {
        int index = GetRoleIndex(role);
        return Time.time - roleLastCommitTime[index] <= Mathf.Max(0.01f, window);
    }

    private bool ShouldDiverCommit(float distanceToPlayer)
    {
        if (IsPlayerMobilityCommitted())
            return true;

        if (distanceToPlayer <= meleeRange * 2.2f)
            return true;

        if (GetPlayerPlanarSpeed() > 18f)
            return true;

        return WasRecentRolePressure(CombatRole.Suppressor, 1.05f) ||
               WasRecentRolePressure(CombatRole.Harrier, 1.15f) ||
               WasRecentRolePressure(CombatRole.Bulwark, 0.9f);
    }

    private bool ShouldSuppressorCommit(float distanceToPlayer)
    {
        if (distanceToPlayer <= stoppingDistance + 2.5f)
            return true;

        if (IsPlayerMobilityCommitted())
            return true;

        return WasRecentRolePressure(CombatRole.Diver, 0.7f) ||
               WasRecentRolePressure(CombatRole.Bulwark, 0.95f);
    }

    private bool ShouldBulwarkCommit(float distanceToPlayer)
    {
        if (distanceToPlayer <= 7.5f)
            return true;

        if (playerController != null && playerController.isGrounded && playerController.PlanarSpeed < 15f)
            return true;

        return WasRecentRolePressure(CombatRole.Suppressor, 0.9f) ||
               WasRecentRolePressure(CombatRole.Diver, 0.75f);
    }

    private bool ShouldHarrierCommit(float distanceToPlayer)
    {
        if (IsPlayerMobilityCommitted())
            return true;

        if (!IsPlayerGrounded())
            return true;

        return distanceToPlayer >= dronePreferredDistance * 0.82f ||
               WasRecentRolePressure(CombatRole.Bulwark, 0.95f) ||
               WasRecentRolePressure(CombatRole.Suppressor, 0.82f);
    }

    private bool ShouldBossSpecialCommit(float distanceToPlayer)
    {
        if (IsPlayerMobilityCommitted())
            return true;

        if (distanceToPlayer <= stoppingDistance + 7.5f)
            return true;

        return WasRecentRolePressure(CombatRole.Harrier, 1.05f) ||
               WasRecentRolePressure(CombatRole.Diver, 0.92f) ||
               WasRecentRolePressure(CombatRole.Suppressor, 0.88f);
    }

    private bool ShouldBossAttackCommit(float distanceToPlayer)
    {
        if (distanceToPlayer <= meleeRange * 2.4f)
            return true;

        if (GetPlayerCommitment01() > 0.18f)
            return true;

        return WasRecentRolePressure(CombatRole.Bulwark, 0.82f) ||
               WasRecentRolePressure(CombatRole.Suppressor, 0.72f) ||
               WasRecentRolePressure(CombatRole.Harrier, 0.78f);
    }

    private bool HasThreatWindow(float distanceToPlayer)
    {
        return player != null && distanceToPlayer <= stoppingDistance + 10f;
    }

    private bool CanAcquireAggro(float distanceToPlayer)
    {
        if (player == null)
            return false;

        float acquireDistance = Mathf.Max(10f, detectionRadius);
        if (arenaGenerator != null)
        {
            float arenaDiagonal = new Vector2(
                arenaGenerator.width * arenaGenerator.tileSize,
                arenaGenerator.length * arenaGenerator.tileSize).magnitude;
            acquireDistance = Mathf.Max(acquireDistance, arenaDiagonal * 1.05f);
        }

        if (distanceToPlayer > acquireDistance)
            return false;

        if (!requireInitialLineOfSight || distanceToPlayer <= closeAwarenessRadius)
            return true;

        if (aggroVisibilityRefreshTimer > 0f)
            return cachedAggroVisibility;

        aggroVisibilityRefreshTimer = 0.12f;
        Vector3 origin = shootPoint != null ? shootPoint.position : transform.position + Vector3.up * 1f;
        cachedAggroVisibility = Has360DegreeSightToPlayer(origin);
        return cachedAggroVisibility;
    }

    private bool Has360DegreeSightToPlayer(Vector3 origin)
    {
        if (player == null)
            return false;

        float centerHeight = enemyType == EnemyType.Flying ? 1.15f : 0.95f;
        return HasUnobstructedSightToPlayer(origin, player.position + Vector3.up * centerHeight) ||
               HasUnobstructedSightToPlayer(origin, player.position + Vector3.up * 0.35f) ||
               HasUnobstructedSightToPlayer(origin, player.position + Vector3.up * 1.55f);
    }

    private bool HasUnobstructedSightToPlayer(Vector3 origin, Vector3 target)
    {
        return HasFilteredLineOfSight(origin, target);
    }

    private bool HasThreatLineOfSight()
    {
        if (player == null)
            return false;

        Vector3 origin = shootPoint != null ? shootPoint.position : transform.position + Vector3.up * 1f;

        return enemyType switch
        {
            _ when isBoss => HasLineOfSightTo(origin, GetMobilityAwareTarget(22f, 0.74f, false)),
            EnemyType.Tank => HasLineOfSightTo(origin, GetMobilityAwareTarget(20f, 0.42f, false)),
            EnemyType.Flying => HasLineOfSightTo(origin, GetMobilityAwareTarget(20f, 0.82f, false)),
            EnemyType.Grunt => HasLineOfSightTo(origin, GetLandingContestTarget(18f, 1.0f, 1.1f, 2.1f) + Vector3.up * 0.65f),
            _ => HasLineOfSightTo(origin, GetMobilityAwareTarget(20f, 0.62f, false))
        };
    }

    private bool GetThreatLineOfSightCached()
    {
        if (threatLineOfSightRefreshTimer > 0f)
            return cachedThreatLineOfSight;

        threatLineOfSightRefreshTimer = 0.08f;
        cachedThreatLineOfSight = HasThreatLineOfSight();
        return cachedThreatLineOfSight;
    }

    private bool HasMeleeThreatOpportunity(float distanceToPlayer)
    {
        if (player == null || distanceToPlayer > meleeRange * 1.4f)
            return false;

        Vector3 flatPlayer = player.position;
        flatPlayer.y = transform.position.y;
        Vector3 flatSelf = transform.position;
        flatSelf.y = transform.position.y;
        return IsPlayerWithinMeleeArc(flatPlayer - flatSelf, isBoss ? 0.18f : 0.32f);
    }

    private bool IsPlayerMobilityCommitted()
    {
        if (playerController == null)
            return !IsPlayerGrounded() || GetPlayerPlanarSpeed() > 18f;

        return playerController.IsGrappling ||
               playerController.IsGrappleHookInFlight ||
               !playerController.isGrounded ||
               playerController.DebugIsSliding ||
               playerController.DebugIsSlamming ||
               playerController.PlanarSpeed > 18f;
    }

    private float GetPlayerCommitment01()
    {
        if (playerController == null)
            return Mathf.Clamp01((GetPlayerPlanarSpeed() - 10f) / 18f);

        float score = 0f;
        if (!playerController.isGrounded) score += 0.28f;
        if (playerController.IsGrappling || playerController.IsGrappleHookInFlight) score += 0.42f;
        if (playerController.DebugIsSliding || playerController.DebugIsSlamming) score += 0.22f;
        score += Mathf.InverseLerp(12f, 28f, playerController.PlanarSpeed) * 0.35f;
        return Mathf.Clamp01(score);
    }

    private Vector3 GetMobilityAwareTarget(float projectileSpeed, float baseLeadScale, bool clampToGround)
    {
        Vector3 target = GetPredictedPlayerPosition(projectileSpeed, baseLeadScale, clampToGround);
        float commitment = GetPlayerCommitment01();
        if (commitment <= 0.001f)
            return target;

        Vector3 planarVelocity = new Vector3(trackedPlayerVelocity.x, 0f, trackedPlayerVelocity.z);
        if (planarVelocity.sqrMagnitude > 0.01f)
            target += planarVelocity.normalized * Mathf.Lerp(0.35f, 2.2f, commitment);

        if (!clampToGround && playerController != null && (playerController.IsGrappling || !playerController.isGrounded))
            target.y += Mathf.Lerp(0.2f, 0.9f, commitment);

        return target;
    }

    private Vector3 GetLandingContestTarget(float projectileSpeed, float baseLeadScale, float lateralWeight, float forwardWeight)
    {
        Vector3 target = GetMobilityAwareTarget(projectileSpeed, baseLeadScale, true);
        float commitment = GetPlayerCommitment01();
        if (commitment <= 0.12f)
            return target;

        Vector3 planarVelocity = new Vector3(trackedPlayerVelocity.x, 0f, trackedPlayerVelocity.z);
        if (planarVelocity.sqrMagnitude > 0.01f)
        {
            Vector3 forward = planarVelocity.normalized;
            Vector3 lateral = Vector3.Cross(Vector3.up, forward).normalized;
            float side = Mathf.Sign(Vector3.Dot(lateral, transform.position - player.position));
            if (Mathf.Approximately(side, 0f))
                side = laneBiasSign;

            target += forward * Mathf.Lerp(0.55f, forwardWeight, commitment);
            target += lateral * side * Mathf.Lerp(0.3f, lateralWeight, commitment);
        }

        target.y = hasGroundAnchor ? groundY : transform.position.y;
        return target;
    }

    private Vector3 GetDiverPounceTarget()
    {
        float commitment = GetPlayerCommitment01();
        Vector3 target = commitment > 0.16f
            ? GetLandingContestTarget(18f, 1.08f, 1.45f, 2.9f)
            : GetMobilityAwareTarget(18f, 1.05f, true);

        Vector3 planarVelocity = new Vector3(trackedPlayerVelocity.x, 0f, trackedPlayerVelocity.z);
        if (commitment > 0.28f && planarVelocity.sqrMagnitude > 0.01f)
            target += planarVelocity.normalized * Mathf.Lerp(0.45f, 1.4f, commitment);

        target.y = hasGroundAnchor ? groundY : transform.position.y;
        return target;
    }

    private Vector3 GetPredictedPlayerPosition(float projectileSpeed, float leadScale, bool clampToGround)
    {
        if (player == null)
            return transform.position;

        Vector3 target = player.position + Vector3.up * ((!IsPlayerGrounded() || enemyType == EnemyType.Flying) ? 1.15f : 1f);
        float originDistance = shootPoint != null
            ? Vector3.Distance(shootPoint.position, target)
            : Vector3.Distance(transform.position, target);
        float leadTime = projectileSpeed > 0.01f
            ? Mathf.Clamp(originDistance / projectileSpeed, 0f, 0.42f)
            : 0f;
        target += trackedPlayerVelocity * (leadTime * Mathf.Max(0f, leadScale));
        if (clampToGround && hasGroundAnchor)
            target.y = groundY;
        return target;
    }

    private bool IsPlayerGrounded()
    {
        return playerController == null || playerController.isGrounded;
    }

    private float GetPlayerPlanarSpeed()
    {
        if (playerController != null)
            return playerController.PlanarSpeed;

        Vector3 planarVelocity = new Vector3(trackedPlayerVelocity.x, 0f, trackedPlayerVelocity.z);
        return planarVelocity.magnitude;
    }

    private Vector3 GetRouteCutTarget(Vector3 baseTarget, float lateralWeight, float forwardWeight)
    {
        Vector3 planarVelocity = new Vector3(trackedPlayerVelocity.x, 0f, trackedPlayerVelocity.z);
        if (planarVelocity.sqrMagnitude <= 0.01f)
            return baseTarget;

        Vector3 forward = planarVelocity.normalized;
        Vector3 lateral = Vector3.Cross(Vector3.up, forward).normalized;
        float side = Mathf.Sign(Vector3.Dot(lateral, transform.position - player.position));
        if (Mathf.Approximately(side, 0f))
            side = laneBiasSign;

        return baseTarget + forward * forwardWeight + lateral * side * lateralWeight;
    }

    private Vector3 GetFormationOffset(Vector3 referenceForward)
    {
        referenceForward.y = 0f;
        if (referenceForward.sqrMagnitude <= 0.0001f)
            referenceForward = transform.forward;
        referenceForward.Normalize();

        Vector3 lateral = Vector3.Cross(Vector3.up, referenceForward).normalized;
        float oscillation = Mathf.Sin(Time.time * 0.65f + laneBiasSeed) * 0.35f;
        float side = laneBiasSign;

        return CurrentCombatRole switch
        {
            CombatRole.Suppressor => lateral * side * (2.1f + oscillation) + referenceForward * (-0.4f + oscillation * 0.3f),
            CombatRole.Bulwark => lateral * side * (1.2f + oscillation * 0.55f) + referenceForward * (0.85f + oscillation * 0.15f),
            CombatRole.Harrier => lateral * side * (2.8f + oscillation * 0.8f) + referenceForward * (0.3f + oscillation * 0.2f),
            CombatRole.Diver => lateral * side * (0.9f + oscillation * 0.45f) + referenceForward * (0.55f + oscillation * 0.18f),
            _ => lateral * side * (1.6f + oscillation * 0.5f)
        };
    }

    private Vector3 GetAllySeparationOffset()
    {
        if (allySeparationRadius <= 0.01f || allySeparationStrength <= 0.01f)
            return Vector3.zero;

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            allySeparationRadius,
            nearbyEnemyBuffer,
            ~0,
            QueryTriggerInteraction.Ignore);
        if (hitCount <= 0)
            return Vector3.zero;

        Vector3 push = Vector3.zero;
        int contributors = 0;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = nearbyEnemyBuffer[i];
            if (hit == null)
                continue;

            BasicEnemyAI ally = hit.GetComponentInParent<BasicEnemyAI>();
            if (ally == null || ally == this || ally.IsCombatResolved)
                continue;

            Vector3 away = transform.position - ally.transform.position;
            away.y = 0f;
            float distance = away.magnitude;
            if (distance <= 0.001f || distance >= allySeparationRadius)
                continue;

            float weight = 1f - (distance / allySeparationRadius);
            push += (away / distance) * weight;
            contributors++;
        }

        if (contributors <= 0 || push.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        return push.normalized * Mathf.Min(allySeparationStrength, push.magnitude * allySeparationStrength);
    }

    private Vector3 GetBossGroundTarget(float leadScale = 1f)
    {
        float commitment = GetPlayerCommitment01();
        Vector3 target = commitment > 0.14f
            ? GetLandingContestTarget(22f, leadScale, 1.1f, 2.35f)
            : GetMobilityAwareTarget(22f, leadScale, false);
        target.y = hasGroundAnchor ? groundY : transform.position.y;
        return target;
    }

    private Vector3 GetBossAirTarget(float verticalOffset = 0f, float leadScale = 1f)
    {
        Vector3 target = GetMobilityAwareTarget(22f, leadScale, false);
        target.y += verticalOffset;
        return target;
    }

    private bool TryFollowGroundPath(Vector3 target, float speed)
    {
        if (arenaGenerator == null || enemyType == EnemyType.Flying)
            return false;

        repathTimer -= Time.deltaTime;
        bool needsPath = groundPath.Count == 0 ||
                         groundPathIndex >= groundPath.Count ||
                         repathTimer <= 0f ||
                         Vector3.Distance(lastRequestedPathTarget, target) > 2.5f ||
                         !HasLineOfSightTo(target + Vector3.up * 0.9f);

        if (needsPath)
        {
            if (!arenaGenerator.TryBuildGroundPath(transform.position, target, out List<Vector3> path) || path == null || path.Count == 0)
            {
                if (groundPath.Count == 0 || groundPathIndex >= groundPath.Count)
                    return false;
            }
            else
            {
                groundPath.Clear();
                groundPath.AddRange(path);
                groundPathIndex = Mathf.Min(1, Mathf.Max(0, groundPath.Count - 1));
                repathTimer = pathRefreshInterval;
                lastRequestedPathTarget = target;
            }
        }

        if (groundPath.Count == 0 || groundPathIndex >= groundPath.Count)
            return false;

        Vector3 next = groundPath[groundPathIndex];
        Vector3 current = transform.position;
        bool verticalConnector = Mathf.Abs(next.y - current.y) > 0.18f || Mathf.Abs(next.y - groundY) > floorSnapTolerance;

        if (IsGroundPathSegmentObstructed(current, next, verticalConnector))
        {
            groundPath.Clear();
            groundPathIndex = 0;
            repathTimer = 0f;
            return false;
        }

        Vector3 moveTarget = verticalConnector
            ? next
            : new Vector3(next.x, Mathf.MoveTowards(current.y, next.y, speed * 4f * Time.deltaTime), next.z);
        Vector3 move = Vector3.MoveTowards(current, moveTarget, speed * Time.deltaTime);
        transform.position = move;

        if (verticalConnector)
        {
            groundY = Mathf.MoveTowards(groundY, next.y, speed * 2.5f * Time.deltaTime);
            hasGroundAnchor = true;
        }
        else
        {
            groundY = Mathf.MoveTowards(groundY, next.y, speed * 4f * Time.deltaTime);
            hasGroundAnchor = true;
        }

        bool reachedNode = verticalConnector
            ? Vector3.Distance(transform.position, next) <= Mathf.Max(pathNodeReachDistance, 0.55f)
            : Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(next.x, next.z)) <= pathNodeReachDistance &&
              Mathf.Abs(transform.position.y - next.y) <= 0.45f;
        if (reachedNode)
            groundPathIndex++;

        if (groundPathIndex >= groundPath.Count)
            groundPath.Clear();

        return true;
    }

    private bool IsGroundPathSegmentObstructed(Vector3 current, Vector3 next, bool verticalConnector)
    {
        Vector3 segment = next - current;
        float distance = segment.magnitude;
        if (distance <= 0.05f)
            return false;

        Vector3 direction = segment / distance;
        float radius = combatCollider != null ? Mathf.Max(0.12f, combatCollider.radius * 0.82f) : 0.32f;
        float height = combatCollider != null ? Mathf.Max(radius * 2.1f, combatCollider.height) : 1.8f;
        float half = Mathf.Max(radius, height * 0.5f - radius);
        Vector3 center = transform.TransformPoint(combatCollider != null ? combatCollider.center : new Vector3(0f, height * 0.5f, 0f));
        Vector3 bottom = center + Vector3.up * (radius - half);
        Vector3 top = center + Vector3.up * (half - radius);

        float castDistance = Mathf.Max(0f, distance - 0.04f);
        if (castDistance <= 0.001f)
            return false;

        if (Physics.CapsuleCast(bottom, top, radius, direction, out RaycastHit hit, castDistance, movementObstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && (hit.collider.transform.IsChildOf(transform) || hit.collider.gameObject == gameObject))
                return false;

            if (!verticalConnector)
                return true;

            Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
            if (horizontal.sqrMagnitude <= 0.01f)
                return true;

            horizontal.Normalize();
            return Physics.CapsuleCast(bottom, top, radius, horizontal, out hit, Mathf.Max(0.08f, castDistance * 0.35f), movementObstacleMask, QueryTriggerInteraction.Ignore) &&
                   hit.collider != null &&
                   !hit.collider.transform.IsChildOf(transform) &&
                   hit.collider.gameObject != gameObject;
        }

        return false;
    }

    private void ClampToAccessibleSpace()
    {
        if (enemyType == EnemyType.Flying) return;

        if (TryFindGroundBelow(9f, out RaycastHit hit))
        {
            float targetY = hit.point.y + 0.05f;
            Vector3 p = transform.position;
            float signedDelta = targetY - p.y;
            float delta = Mathf.Abs(signedDelta);

            if (signedDelta > 0.35f)
                return;

            float settleSpeed = delta <= floorSnapTolerance ? 14f : 5.5f;
            p.y = Mathf.MoveTowards(p.y, targetY, Time.deltaTime * settleSpeed);
            transform.position = p;
            groundY = targetY;
            hasGroundAnchor = true;
        }
        else
        {
            Vector3 p = transform.position;
            p.y -= 9.5f * Time.deltaTime;
            transform.position = p;
            hasGroundAnchor = false;
        }
    }

    private bool TryFindGroundBelow(float distance, out RaycastHit bestHit)
    {
        bestHit = default;
        Vector3 origin = transform.position + Vector3.up * 0.7f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, movementObstacleMask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null) continue;
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;
            if (!IsWalkableGroundHit(hit)) continue;
            if (hit.point.y > transform.position.y + 0.35f) continue;
            if (hit.distance >= bestDistance) continue;

            bestHit = hit;
            bestDistance = hit.distance;
        }

        return bestDistance < float.PositiveInfinity;
    }

    private bool IsWalkableGroundHit(RaycastHit hit)
    {
        Collider collider = hit.collider;
        if (collider == null)
            return false;
        if (collider.isTrigger)
            return false;
        if (collider.GetComponentInParent<PlayerController>() != null)
            return false;
        if (collider.GetComponentInParent<BasicEnemyAI>() != null)
            return false;
        if (collider.GetComponentInParent<Projectile>() != null)
            return false;
        if (collider.GetComponentInParent<Interactable>() != null)
            return false;
        if (hit.normal.y < 0.45f)
            return false;
        if (collider.bounds.size.y > 8f && collider.bounds.size.x < 1.2f && collider.bounds.size.z < 1.2f)
            return false;
        return true;
    }

    private bool HasLineOfSightTo(Vector3 targetPos)
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        return HasLineOfSightTo(origin, targetPos);
    }

    private bool HasLineOfSightTo(Vector3 origin, Vector3 targetPos)
    {
        return HasFilteredLineOfSight(origin, targetPos);
    }

    private bool HasFilteredLineOfSight(Vector3 origin, Vector3 targetPos)
    {
        Vector3 offset = targetPos - origin;
        float distance = offset.magnitude;
        if (distance <= 0.001f)
            return true;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            offset / distance,
            sightHitBuffer,
            distance,
            movementObstacleMask,
            QueryTriggerInteraction.Ignore);

        float nearestBlockingDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = sightHitBuffer[i].collider;
            if (collider == null)
                continue;
            if (collider.transform.IsChildOf(transform) || collider.gameObject == gameObject)
                continue;
            if (collider.GetComponentInParent<BasicEnemyAI>() != null)
                continue;
            if (collider.GetComponentInParent<PlayerController>() == playerController)
                return true;

            nearestBlockingDistance = Mathf.Min(nearestBlockingDistance, sightHitBuffer[i].distance);
        }

        return float.IsPositiveInfinity(nearestBlockingDistance);
    }

    private bool IsBlockedAhead(Vector3 desiredPos)
    {
        Vector3 origin = transform.position + Vector3.up * 0.9f;
        Vector3 dir = desiredPos - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return false;
        dir /= dist;
        return Physics.Raycast(origin, dir, dist, movementObstacleMask, QueryTriggerInteraction.Ignore);
    }

    private bool TryMoveTowards(Vector3 desiredWorldPos, float maxDistanceDelta)
    {
        Vector3 next = Vector3.MoveTowards(transform.position, desiredWorldPos, maxDistanceDelta);
        if (IsMovementBlocked(transform.position, next))
            return false;

        transform.position = next;
        return true;
    }

    private bool IsMovementBlocked(Vector3 current, Vector3 next)
    {
        Vector3 segment = next - current;
        float distance = segment.magnitude;
        if (distance <= 0.001f)
            return false;

        Vector3 direction = segment / distance;
        float radius = combatCollider != null ? Mathf.Max(0.12f, combatCollider.radius * 0.82f) : 0.32f;
        float height = combatCollider != null ? Mathf.Max(radius * 2.1f, combatCollider.height) : 1.8f;
        float half = Mathf.Max(radius, height * 0.5f - radius);
        Vector3 center = transform.TransformPoint(combatCollider != null ? combatCollider.center : new Vector3(0f, height * 0.5f, 0f));
        Vector3 bottom = center + Vector3.up * (radius - half);
        Vector3 top = center + Vector3.up * (half - radius);

        if (!Physics.CapsuleCast(bottom, top, radius, direction, out RaycastHit hit, Mathf.Max(0f, distance - 0.02f), movementObstacleMask, QueryTriggerInteraction.Ignore))
            return false;

        return hit.collider != null &&
               !hit.collider.transform.IsChildOf(transform) &&
               hit.collider.gameObject != gameObject;
    }

    private void FacePlayer()
    {
        if (player == null) return;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f) return;

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction.normalized), 7f * Time.deltaTime);
    }

    private IEnumerator MeleeTelegraphRoutine(float windup, float damage, bool useBossGate, float cooldownReset, Color telegraphColor, float telegraphRadius)
    {
        if (player == null)
        {
            meleeWindupRoutine = null;
            yield break;
        }

        Vector3 center = transform.position + transform.forward * Mathf.Max(0.55f, meleeRange * 0.42f);
        center.y = hasGroundAnchor ? groundY + 0.04f : transform.position.y + 0.04f;
        attackPulseTimer = Mathf.Max(attackPulseTimer, windup + 0.04f);
        SpawnTelegraphDisc(center, Mathf.Max(0.9f, telegraphRadius), telegraphColor, Mathf.Max(0.12f, windup + 0.04f));
        yield return new WaitForSeconds(windup);

        if (!IsCombatResolved && player != null)
            TryMeleeAttack(damage, useBossGate);

        meleeTimer = cooldownReset;
        meleeWindupRoutine = null;
    }

    private void TryMeleeAttack(float damage, bool useBossGate)
    {
        if (player == null) return;

        PlayerController cachedController = GetCachedPlayerController();
        if (cachedController == null) return;

        attackPulseTimer = 0.16f;
        Vector3 flatPlayer = player.position;
        flatPlayer.y = transform.position.y;
        Vector3 flatSelf = transform.position;
        flatSelf.y = transform.position.y;
        if (Vector3.Distance(flatSelf, flatPlayer) > meleeRange * 1.18f)
            return;
        if (!IsPlayerWithinMeleeArc(flatPlayer - flatSelf, useBossGate ? 0.18f : 0.32f))
            return;

        TryDamagePlayer(cachedController, damage, useBossGate);
    }

    private bool IsPlayerWithinMeleeArc(Vector3 toPlayerFlat, float minDot)
    {
        toPlayerFlat.y = 0f;
        if (toPlayerFlat.sqrMagnitude <= 0.0001f)
            return true;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            return true;

        return Vector3.Dot(forward.normalized, toPlayerFlat.normalized) >= minDot;
    }

    private void StartShooterBurst()
    {
        int burstCount = isBoss ? 3 : 2;
        if (!IsPlayerGrounded() || GetPlayerPlanarSpeed() > 20f)
            burstCount++;
        shooterBurstShotsRemaining = burstCount;
        shooterBurstTimer = 0f;
    }

    private IEnumerator GruntPounceTelegraphRoutine()
    {
        if (player == null)
        {
            gruntPounceWindupRoutine = null;
            yield break;
        }

        Vector3 target = GetDiverPounceTarget();
        target.y = hasGroundAnchor ? groundY : transform.position.y;
        attackPulseTimer = 0.18f;
        SpawnTelegraphLine(transform.position + Vector3.up * 0.15f, target + Vector3.up * 0.15f, new Color(1f, 0.22f, 0.14f), 0.16f);
        SpawnTelegraphDisc(target + Vector3.up * 0.02f, Mathf.Max(1.15f, meleeRange * 0.95f), new Color(1f, 0.24f, 0.16f), 0.18f);
        yield return new WaitForSeconds(0.13f);

        if (!IsCombatResolved && player != null)
            PerformGruntPounce();

        gruntPounceWindupRoutine = null;
    }

    private void PerformGruntPounce()
    {
        if (player == null) return;
        float commitment = GetPlayerCommitment01();
        Vector3 target = GetDiverPounceTarget();
        target.y = hasGroundAnchor ? groundY : transform.position.y;
        Vector3 dash = target - transform.position;
        dash.y = 0f;
        if (dash.sqrMagnitude <= 0.001f)
            dash = transform.forward;
        float pounceSpeed = Mathf.Max(8.5f, moveSpeed * Mathf.Lerp(3.5f, 4.6f, commitment));
        float pounceDuration = Mathf.Lerp(0.2f, 0.3f, commitment);
        gruntPounceVelocity = dash.normalized * pounceSpeed;
        gruntPounceTimeRemaining = pounceDuration;
        attackPulseTimer = Mathf.Max(0.2f, pounceDuration);
    }

    private IEnumerator FlyingVolleyTelegraphRoutine(Vector3 dashDirection)
    {
        if (player == null)
        {
            flyingVolleyRoutine = null;
            yield break;
        }

        attackPulseTimer = 0.16f;
        yield return null;

        if (!IsCombatResolved && player != null)
        {
            ShootWithSpread(-9f);
            ShootWithSpread(0f);
            ShootWithSpread(9f);
            BeginDroneDash(dashDirection);
        }

        flyingVolleyRoutine = null;
    }

    private IEnumerator TankDirectVolleyTelegraphRoutine(float damage)
    {
        if (player == null)
        {
            tankVolleyRoutine = null;
            yield break;
        }

        attackPulseTimer = Mathf.Max(attackPulseTimer, 0.16f);
        yield return null;

        if (!IsCombatResolved && player != null)
            TryDirectDamage(damage);

        tankVolleyRoutine = null;
    }

    private void SpawnRangedTelegraph(Color color, float duration, float projectileSpeed, float leadScale)
    {
        if (shootPoint == null || player == null)
            return;

        Vector3 telegraphTarget = GetMobilityAwareTarget(projectileSpeed, leadScale, false);
        SpawnTelegraphLine(shootPoint.position, telegraphTarget, color, duration);
    }

    private void EmitTankShockwave()
    {
        attackPulseTimer = 0.24f;
        Collider[] hits = Physics.OverlapSphere(transform.position, 4.4f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;
            PlayerController playerController = hit.GetComponentInParent<PlayerController>();
            if (playerController == null) continue;
            TryDamagePlayer(playerController, meleeDamage * 0.85f, false);
        }
    }

    private IEnumerator TankShockwaveTelegraphRoutine()
    {
        Vector3 center = hasGroundAnchor
            ? new Vector3(transform.position.x, groundY, transform.position.z)
            : transform.position;
        SpawnTelegraphDisc(center, 4.4f, new Color(1f, 0.62f, 0.16f), 0.34f);
        SpawnTelegraphDisc(center, 2.8f, new Color(1f, 0.34f, 0.12f), 0.24f);
        attackPulseTimer = 0.18f;
        yield return new WaitForSeconds(0.26f);
        EmitTankShockwave();
        SpawnTelegraphDisc(center, 4.0f, Color.white, 0.12f);
        tankShockwaveRoutine = null;
    }

    private IEnumerator BossGroundStrike(Vector3 targetPosition, float radius, float damage, Color telegraphColor)
    {
        SpawnTelegraphDisc(targetPosition, radius, telegraphColor, 0.55f);
        yield return new WaitForSeconds(0.55f);
        DamagePlayerIfInside(targetPosition, radius, damage);
        SpawnTelegraphDisc(targetPosition, radius * 0.55f, Color.white, 0.16f);
        attackPulseTimer = 0.22f;
    }

    private IEnumerator BossPulseSequence(float radius, float damage, float delay, Color telegraphColor)
    {
        for (int i = 0; i < 3; i++)
        {
            SpawnTelegraphDisc(transform.position, radius + i * 1.3f, telegraphColor, delay);
            yield return new WaitForSeconds(delay);
            DamagePlayerIfInside(transform.position, radius + i * 1.3f, damage);
        }
        attackPulseTimer = 0.24f;
    }

    private IEnumerator BossFanBurst(int shotCount, float spanDegrees)
    {
        if (shootPoint == null || player == null)
            yield break;

        float leftYaw = -spanDegrees * 0.5f;
        float rightYaw = spanDegrees * 0.5f;
        Vector3 targetPos = GetMobilityAwareTarget(22f, 0.72f, false);
        Vector3 baseDirection = (targetPos - shootPoint.position).normalized;
        Vector3 leftDirection = Quaternion.Euler(0f, leftYaw, 0f) * baseDirection;
        Vector3 rightDirection = Quaternion.Euler(0f, rightYaw, 0f) * baseDirection;
        Vector3 origin = shootPoint.position;

        SpawnTelegraphLine(origin, origin + leftDirection * 14f, new Color(1f, 0.46f, 0.1f), 0.16f);
        SpawnTelegraphLine(origin, origin + baseDirection * 15f, new Color(1f, 0.7f, 0.18f), 0.16f);
        SpawnTelegraphLine(origin, origin + rightDirection * 14f, new Color(1f, 0.46f, 0.1f), 0.16f);
        attackPulseTimer = 0.18f;
        yield return new WaitForSeconds(0.12f);

        if (!IsCombatResolved && player != null)
            FireSpreadFan(shotCount, spanDegrees);

        attackPulseTimer = 0.18f;
    }

    private IEnumerator BossDashSlam(float dashSpeed, float radius, float damage)
    {
        if (player == null) yield break;

        Vector3 start = transform.position;
        Vector3 end = GetBossGroundTarget(1.1f);
        SpawnTelegraphLine(start, end, new Color(1f, 0.22f, 0.12f), 0.3f);
        yield return new WaitForSeconds(0.3f);

        float elapsed = 0f;
        float duration = Mathf.Clamp(Vector3.Distance(start, end) / Mathf.Max(0.1f, dashSpeed), 0.08f, 0.32f);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        DamagePlayerIfInside(end, radius, damage);
        SpawnTelegraphDisc(end, radius, new Color(1f, 0.44f, 0.12f), 0.18f);
        attackPulseTimer = 0.28f;
    }

    private void StartBossRoutine(IEnumerator routine)
    {
        if (routine == null || bossRoutine != null) return;
        bossRoutine = StartCoroutine(RunBossRoutine(routine));
    }

    private IEnumerator RunBossRoutine(IEnumerator routine)
    {
        yield return StartCoroutine(routine);
        bossRoutine = null;
    }

    private IEnumerator BossCrossfireLock(int waveCount, float halfSpan, float damage)
    {
        for (int i = 0; i < waveCount; i++)
        {
            bool horizontal = i % 2 == 0;
            Vector3 lineStart = transform.position + (horizontal ? Vector3.left : Vector3.forward) * halfSpan;
            Vector3 lineEnd = transform.position + (horizontal ? Vector3.right : Vector3.back) * halfSpan;
            lineStart.y = transform.position.y + 0.05f;
            lineEnd.y = transform.position.y + 0.05f;

            SpawnTelegraphLine(lineStart, lineEnd, new Color(1f, 0.44f, 0.08f), 0.55f);
            yield return new WaitForSeconds(0.52f);
            DamagePlayerNearLine(lineStart, lineEnd, 1.1f, damage);
            SpawnTelegraphLine(lineStart, lineEnd, Color.white, 0.12f);
            yield return new WaitForSeconds(0.14f);
        }

        attackPulseTimer = 0.26f;
    }

    private IEnumerator BossCageLock(float halfExtent, float damage)
    {
        if (player == null) yield break;

        Vector3 center = GetBossGroundTarget(1.15f);

        Vector3 frontLeft = center + new Vector3(-halfExtent, 0.05f, halfExtent);
        Vector3 frontRight = center + new Vector3(halfExtent, 0.05f, halfExtent);
        Vector3 backLeft = center + new Vector3(-halfExtent, 0.05f, -halfExtent);
        Vector3 backRight = center + new Vector3(halfExtent, 0.05f, -halfExtent);

        SpawnTelegraphLine(frontLeft, frontRight, new Color(1f, 0.54f, 0.12f), 0.62f);
        SpawnTelegraphLine(frontRight, backRight, new Color(1f, 0.54f, 0.12f), 0.62f);
        SpawnTelegraphLine(backRight, backLeft, new Color(1f, 0.54f, 0.12f), 0.62f);
        SpawnTelegraphLine(backLeft, frontLeft, new Color(1f, 0.54f, 0.12f), 0.62f);
        SpawnTelegraphDisc(center, halfExtent * 0.82f, new Color(1f, 0.26f, 0.08f), 0.62f);
        yield return new WaitForSeconds(0.62f);

        DamagePlayerNearLine(frontLeft, frontRight, 0.9f, damage);
        DamagePlayerNearLine(frontRight, backRight, 0.9f, damage);
        DamagePlayerNearLine(backRight, backLeft, 0.9f, damage);
        DamagePlayerNearLine(backLeft, frontLeft, 0.9f, damage);
        DamagePlayerIfInside(center, halfExtent * 0.58f, damage * 0.7f);
        SpawnTelegraphDisc(center, halfExtent * 0.52f, Color.white, 0.14f);
        attackPulseTimer = 0.28f;
    }

    private IEnumerator BossComboAssault(int dashCount, float dashSpeed, float radius, float damage)
    {
        for (int i = 0; i < dashCount; i++)
        {
            yield return BossDashSlam(dashSpeed + i * 1.2f, radius, damage);
            yield return new WaitForSeconds(0.12f);
        }

        if (player != null)
            yield return BossGroundStrike(GetBossGroundTarget(1.18f), radius + 0.8f, damage * 1.1f, new Color(1f, 0.32f, 0.12f));
    }

    private IEnumerator BossRazorSweep(int sweepCount, float dashSpeed, float radius, float damage)
    {
        if (player == null) yield break;

        for (int i = 0; i < sweepCount; i++)
        {
            Vector3 toPlayer = (player.position - transform.position);
            toPlayer.y = 0f;
            Vector3 lateral = toPlayer.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, toPlayer.normalized) : transform.right;
            float side = (i % 2 == 0) ? 1f : -1f;
            Vector3 targetCenter = GetBossGroundTarget(1.08f);
            Vector3 start = targetCenter + lateral * (4.8f * side);
            Vector3 end = targetCenter - lateral * (4.8f * side);
            start.y = hasGroundAnchor ? groundY : transform.position.y;
            end.y = start.y;

            transform.position = start;
            SpawnTelegraphLine(start, end, new Color(1f, 0.22f, 0.12f), 0.3f);
            yield return new WaitForSeconds(0.22f);

            float elapsed = 0f;
            float duration = Mathf.Clamp(Vector3.Distance(start, end) / Mathf.Max(0.1f, dashSpeed), 0.12f, 0.34f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            DamagePlayerNearLine(start, end, radius, damage);
            SpawnTelegraphLine(start, end, Color.white, 0.1f);
            yield return new WaitForSeconds(0.08f);
        }

        yield return BossGroundStrike(GetBossGroundTarget(1.12f), radius + 0.9f, damage * 1.1f, new Color(1f, 0.30f, 0.14f));
        attackPulseTimer = 0.3f;
    }

    private IEnumerator BossSentinelDiveRun(int strikeCount, float radius, float damage)
    {
        if (player == null) yield break;

        Vector3 riseTarget = GetBossAirTarget(hoverHeight + 6f, 0.9f);
        float riseElapsed = 0f;
        Vector3 riseStart = transform.position;
        while (riseElapsed < 0.56f)
        {
            riseElapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(riseStart, riseTarget, Mathf.Clamp01(riseElapsed / 0.45f));
            yield return null;
        }

        for (int i = 0; i < strikeCount; i++)
        {
            Vector3 target = GetBossGroundTarget(1.12f) + new Vector3(Mathf.Sin(Time.time + i) * 1.8f, 0f, Mathf.Cos(Time.time * 1.2f + i) * 1.8f);
            yield return BossGroundStrike(target, radius, damage, new Color(0.42f, 0.88f, 1f));
            yield return new WaitForSeconds(0.08f);
        }

        Vector3 diveTarget = GetBossGroundTarget(1.18f);
        SpawnTelegraphLine(transform.position, diveTarget, new Color(0.62f, 0.95f, 1f), 0.24f);
        yield return new WaitForSeconds(0.3f);
        transform.position = diveTarget;
        DamagePlayerIfInside(diveTarget, radius * 0.8f, damage * 1.15f);
        SpawnTelegraphDisc(diveTarget, radius * 0.72f, Color.white, 0.12f);
        attackPulseTimer = 0.26f;
    }

    private IEnumerator BossSkyLanceBarrage(int strikeCount, float radius, float damage)
    {
        if (player == null) yield break;

        Vector3 highAnchor = GetBossAirTarget(hoverHeight + 8f, 0.92f);
        Vector3 start = transform.position;
        float riseElapsed = 0f;
        while (riseElapsed < 0.42f)
        {
            riseElapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, highAnchor, Mathf.Clamp01(riseElapsed / 0.32f));
            yield return null;
        }

        for (int i = 0; i < strikeCount; i++)
        {
            float angle = (Mathf.PI * 2f / Mathf.Max(1, strikeCount)) * i;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (1.8f + i * 0.55f);
            Vector3 target = GetBossGroundTarget(1.14f) + offset;
            SpawnTelegraphDisc(target, radius, new Color(0.38f, 0.88f, 1f), 0.42f);
            yield return new WaitForSeconds(0.1f);
            DamagePlayerIfInside(target, radius, damage);
            SpawnTelegraphDisc(target, radius * 0.52f, Color.white, 0.1f);
        }

        attackPulseTimer = 0.22f;
    }

    private IEnumerator BossSentinelStrafeVolley(int volleyCount, float damage)
    {
        if (player == null) yield break;

        Vector3 origin = transform.position;
        Vector3 volleyTarget = GetBossAirTarget(0f, 1f);
        Vector3 lateral = Vector3.Cross(Vector3.up, (volleyTarget - transform.position).normalized);
        if (lateral.sqrMagnitude < 0.01f)
            lateral = transform.right;

        Vector3 left = origin - lateral * 5.2f + Vector3.up * 1.5f;
        Vector3 right = origin + lateral * 5.2f + Vector3.up * 1.5f;
        SpawnTelegraphLine(left, right, new Color(0.4f, 0.88f, 1f), 0.32f);
        yield return new WaitForSeconds(0.18f);

        for (int i = 0; i < volleyCount; i++)
        {
            float t = volleyCount <= 1 ? 0.5f : (float)i / (volleyCount - 1);
            transform.position = Vector3.Lerp(left, right, t);
            ShootWithSpread(Mathf.Lerp(-10f, 10f, t));
            ShootWithSpread(0f);
            yield return new WaitForSeconds(0.1f);
        }

        transform.position = origin;
        if (player != null)
            yield return BossGroundStrike(GetBossGroundTarget(1.08f), 2.2f, damage, new Color(0.42f, 0.88f, 1f));
        attackPulseTimer = 0.18f;
    }

    private void FireSpreadFan(int shotCount, float spanDegrees)
    {
        float startYaw = -spanDegrees * 0.5f;
        float step = shotCount <= 1 ? 0f : spanDegrees / (shotCount - 1);
        for (int i = 0; i < shotCount; i++)
            ShootWithSpread(startYaw + step * i);
    }

    private void DamagePlayerIfInside(Vector3 center, float radius, float damage)
    {
        if (player == null) return;
        PlayerController cachedController = GetCachedPlayerController();
        if (cachedController == null) return;
        if (Mathf.Abs(player.position.y - center.y) > GetAttackVerticalAllowance(radius))
            return;

        Vector3 flatPlayer = player.position;
        flatPlayer.y = center.y;
        if (Vector3.Distance(flatPlayer, center) <= radius)
            TryDamagePlayer(cachedController, damage, isBoss);
    }

    private void DamagePlayerNearLine(Vector3 start, Vector3 end, float width, float damage)
    {
        if (player == null) return;
        PlayerController cachedController = GetCachedPlayerController();
        if (cachedController == null) return;
        float centerY = (start.y + end.y) * 0.5f;
        if (Mathf.Abs(player.position.y - centerY) > GetAttackVerticalAllowance(width * 1.6f))
            return;

        Vector3 playerPos = player.position;
        playerPos.y = start.y;
        Vector3 line = end - start;
        float lengthSq = Mathf.Max(0.001f, line.sqrMagnitude);
        float t = Mathf.Clamp01(Vector3.Dot(playerPos - start, line) / lengthSq);
        Vector3 closest = start + line * t;
        if (Vector3.Distance(playerPos, closest) <= width)
            TryDamagePlayer(cachedController, damage, isBoss);
    }

    private float GetAttackVerticalAllowance(float radiusLike)
    {
        return Mathf.Clamp(radiusLike * 0.55f, 1.1f, 2.6f);
    }

    private bool TryDamagePlayer(PlayerController playerController, float damage, bool useBossGate)
    {
        if (playerController == null || damage <= 0f)
            return false;
        if (useBossGate)
        {
            if (bossPlayerHitTimer > 0f)
                return false;
            bossPlayerHitTimer = bossPlayerHitCooldown;
        }

        playerController.TakeDamage(damage);
        return true;
    }

    private void SpawnTelegraphDisc(Vector3 center, float radius, Color color, float lifetime)
    {
        GameObject root = new GameObject("BossTelegraphDisc");
        root.transform.position = center + Vector3.up * 0.06f;

        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.transform.SetParent(root.transform, false);
        disc.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
        Collider collider = disc.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer discRenderer = disc.GetComponent<Renderer>();
        if (discRenderer != null)
            ApplyTransientFxRenderer(discRenderer, new Color(color.r, color.g, color.b, 0.22f), 1.25f);

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.transform.SetParent(root.transform, false);
        ring.transform.localScale = new Vector3(radius * 2.25f, 0.012f, radius * 2.25f);
        ring.transform.localPosition = new Vector3(0f, 0.012f, 0f);
        Collider ringCollider = ring.GetComponent<Collider>();
        if (ringCollider != null) Destroy(ringCollider);
        Renderer ringRenderer = ring.GetComponent<Renderer>();
        if (ringRenderer != null)
            ApplyTransientFxRenderer(ringRenderer, new Color(color.r, color.g, color.b, 0.42f), 1.65f);

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        core.transform.SetParent(root.transform, false);
        core.transform.localScale = new Vector3(radius * 0.2f, 1.2f, radius * 0.2f);
        core.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        Collider coreCollider = core.GetComponent<Collider>();
        if (coreCollider != null) Destroy(coreCollider);
        Renderer coreRenderer = core.GetComponent<Renderer>();
        if (coreRenderer != null)
            ApplyTransientFxRenderer(coreRenderer, new Color(color.r, color.g, color.b, 0.18f), 1.1f);

        StartCoroutine(AnimateTelegraphDisc(root.transform, disc.transform, ring.transform, core.transform, discRenderer, ringRenderer, coreRenderer, color, lifetime));
    }

    private void SpawnTelegraphLine(Vector3 start, Vector3 end, Color color, float lifetime)
    {
        Vector3 direction = end - start;
        float length = Mathf.Max(0.1f, direction.magnitude);
        GameObject root = new GameObject("BossTelegraphLine");
        root.transform.position = start + direction * 0.5f + Vector3.up * 0.08f;
        root.transform.rotation = Quaternion.LookRotation(direction.normalized);

        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.transform.SetParent(root.transform, false);
        line.transform.localScale = new Vector3(0.22f, 0.06f, length);
        Collider collider = line.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer lineRenderer = line.GetComponent<Renderer>();
        if (lineRenderer != null)
            ApplyTransientFxRenderer(lineRenderer, new Color(color.r, color.g, color.b, 0.32f), 1.45f);

        GameObject railA = GameObject.CreatePrimitive(PrimitiveType.Cube);
        railA.transform.SetParent(root.transform, false);
        railA.transform.localPosition = new Vector3(0.24f, 0f, 0f);
        railA.transform.localScale = new Vector3(0.06f, 0.08f, length);
        Collider railACollider = railA.GetComponent<Collider>();
        if (railACollider != null) Destroy(railACollider);
        Renderer railARenderer = railA.GetComponent<Renderer>();
        if (railARenderer != null)
            ApplyTransientFxRenderer(railARenderer, new Color(color.r, color.g, color.b, 0.52f), 1.95f);

        GameObject railB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        railB.transform.SetParent(root.transform, false);
        railB.transform.localPosition = new Vector3(-0.24f, 0f, 0f);
        railB.transform.localScale = new Vector3(0.06f, 0.08f, length);
        Collider railBCollider = railB.GetComponent<Collider>();
        if (railBCollider != null) Destroy(railBCollider);
        Renderer railBRenderer = railB.GetComponent<Renderer>();
        if (railBRenderer != null)
            ApplyTransientFxRenderer(railBRenderer, new Color(color.r, color.g, color.b, 0.52f), 1.95f);

        StartCoroutine(AnimateTelegraphLine(root.transform, line.transform, railA.transform, railB.transform, lineRenderer, railARenderer, railBRenderer, color, lifetime));
    }

    private IEnumerator AnimateTelegraphDisc(Transform root, Transform disc, Transform ring, Transform core, Renderer discRenderer, Renderer ringRenderer, Renderer coreRenderer, Color color, float lifetime)
    {
        if (root == null) yield break;

        float elapsed = 0f;
        Vector3 discTargetScale = disc != null ? disc.localScale : Vector3.one;
        Vector3 ringTargetScale = ring != null ? ring.localScale : Vector3.one;
        Vector3 coreTargetScale = core != null ? core.localScale : Vector3.one;

        if (disc != null) disc.localScale = new Vector3(discTargetScale.x * 0.35f, discTargetScale.y, discTargetScale.z * 0.35f);
        if (ring != null) ring.localScale = new Vector3(ringTargetScale.x * 0.45f, ringTargetScale.y, ringTargetScale.z * 0.45f);
        if (core != null) core.localScale = new Vector3(coreTargetScale.x, 0.2f, coreTargetScale.z);

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
            float pulse = Mathf.Sin(t * Mathf.PI);

            if (disc != null)
                disc.localScale = Vector3.Lerp(new Vector3(discTargetScale.x * 0.35f, discTargetScale.y, discTargetScale.z * 0.35f), discTargetScale, Mathf.SmoothStep(0f, 1f, t));
            if (ring != null)
                ring.localScale = Vector3.Lerp(new Vector3(ringTargetScale.x * 0.45f, ringTargetScale.y, ringTargetScale.z * 0.45f), ringTargetScale * (1f + pulse * 0.16f), Mathf.SmoothStep(0f, 1f, t));
            if (core != null)
            {
                core.localScale = Vector3.Lerp(new Vector3(coreTargetScale.x, 0.2f, coreTargetScale.z), coreTargetScale, Mathf.SmoothStep(0f, 1f, t));
                core.localPosition = new Vector3(0f, Mathf.Lerp(0.25f, 0.6f, t), 0f);
            }

            if (discRenderer != null) ApplyTransientFxRenderer(discRenderer, new Color(color.r, color.g, color.b, Mathf.Lerp(0.16f, 0.32f, pulse)), 1.25f + pulse * 0.35f);
            if (ringRenderer != null) ApplyTransientFxRenderer(ringRenderer, new Color(color.r, color.g, color.b, Mathf.Lerp(0.28f, 0.62f, pulse)), 1.75f + pulse * 0.45f);
            if (coreRenderer != null) ApplyTransientFxRenderer(coreRenderer, new Color(color.r, color.g, color.b, Mathf.Lerp(0.08f, 0.26f, pulse)), 1.05f + pulse * 0.25f);
            yield return null;
        }

        if (root != null)
            Destroy(root.gameObject);
    }

    private IEnumerator AnimateTelegraphLine(Transform root, Transform line, Transform railA, Transform railB, Renderer lineRenderer, Renderer railARenderer, Renderer railBRenderer, Color color, float lifetime)
    {
        if (root == null) yield break;

        float elapsed = 0f;
        Vector3 lineTarget = line != null ? line.localScale : Vector3.one;
        Vector3 railTargetA = railA != null ? railA.localScale : Vector3.one;
        Vector3 railTargetB = railB != null ? railB.localScale : Vector3.one;
        if (line != null) line.localScale = new Vector3(lineTarget.x, lineTarget.y, lineTarget.z * 0.08f);
        if (railA != null) railA.localScale = new Vector3(railTargetA.x, railTargetA.y, railTargetA.z * 0.08f);
        if (railB != null) railB.localScale = new Vector3(railTargetB.x, railTargetB.y, railTargetB.z * 0.08f);

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
            float pulse = Mathf.Sin(t * Mathf.PI);
            if (line != null)
                line.localScale = Vector3.Lerp(new Vector3(lineTarget.x, lineTarget.y, lineTarget.z * 0.08f), lineTarget, Mathf.SmoothStep(0f, 1f, t));
            if (railA != null)
                railA.localScale = Vector3.Lerp(new Vector3(railTargetA.x, railTargetA.y, railTargetA.z * 0.08f), new Vector3(railTargetA.x, railTargetA.y, railTargetA.z * (1f + pulse * 0.08f)), Mathf.SmoothStep(0f, 1f, t));
            if (railB != null)
                railB.localScale = Vector3.Lerp(new Vector3(railTargetB.x, railTargetB.y, railTargetB.z * 0.08f), new Vector3(railTargetB.x, railTargetB.y, railTargetB.z * (1f + pulse * 0.08f)), Mathf.SmoothStep(0f, 1f, t));
            if (lineRenderer != null) ApplyTransientFxRenderer(lineRenderer, new Color(color.r, color.g, color.b, Mathf.Lerp(0.18f, 0.36f, pulse)), 1.45f + pulse * 0.28f);
            if (railARenderer != null) ApplyTransientFxRenderer(railARenderer, new Color(color.r, color.g, color.b, Mathf.Lerp(0.32f, 0.68f, pulse)), 1.95f + pulse * 0.42f);
            if (railBRenderer != null) ApplyTransientFxRenderer(railBRenderer, new Color(color.r, color.g, color.b, Mathf.Lerp(0.32f, 0.68f, pulse)), 1.95f + pulse * 0.42f);
            yield return null;
        }

        if (root != null)
            Destroy(root.gameObject);
    }

    private void TryDirectDamage(float amount)
    {
        if (player == null) return;

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController == null) return;
        Vector3 origin = shootPoint != null ? shootPoint.position : transform.position + Vector3.up * 1f;
        Vector3 targetPos = GetMobilityAwareTarget(32f, 0.58f, false);
        float maxRange = enemyType == EnemyType.Tank ? stoppingDistance + 8f : stoppingDistance + 7f;
        if (Vector3.Distance(transform.position, player.position) > maxRange)
            return;
        if (!HasLineOfSightTo(origin, targetPos))
            return;
        if (!IsPlayerInsideDamageRay(origin, targetPos, enemyType == EnemyType.Tank ? 1.05f : 0.72f))
            return;

        attackPulseTimer = 0.12f;
        TryDamagePlayer(playerController, amount, false);
    }

    private bool IsPlayerInsideDamageRay(Vector3 origin, Vector3 targetPos, float width)
    {
        if (player == null)
            return false;

        if (Mathf.Abs(player.position.y - origin.y) > GetAttackVerticalAllowance(width * 1.8f))
            return false;

        Vector3 playerPos = player.position;
        playerPos.y = origin.y;
        Vector3 line = targetPos - origin;
        float lengthSq = Mathf.Max(0.001f, line.sqrMagnitude);
        float t = Mathf.Clamp01(Vector3.Dot(playerPos - origin, line) / lengthSq);
        Vector3 closest = origin + line * t;
        return Vector3.Distance(playerPos, closest) <= width;
    }

    private void UpdateModelMotion(float distanceToPlayer)
    {
        if (modelRoot == null) return;

        float speedFactor = enemyType == EnemyType.Tank ? 0.35f : enemyType == EnemyType.Grunt ? 0.6f : 1f;
        if (enemyType == EnemyType.Flying)
            speedFactor = 1f;

        float bob = Mathf.Sin(Time.time * (bobFrequency * 2f) + flyPhase) * bobAmplitude * 0.55f;
        float walk = Mathf.Clamp01(distanceToPlayer / Mathf.Max(0.1f, stoppingDistance));
        Vector3 localOffset = baseModelLocalPosition + new Vector3(0f, bob, 0f);
        Quaternion sway = Quaternion.Euler(Mathf.Sin(Time.time * 8f) * 1.5f * speedFactor, Mathf.Sin(Time.time * 6f) * 2.0f * speedFactor, Mathf.Sin(Time.time * 5f) * 1.25f * speedFactor);

        if (enemyType == EnemyType.Grunt)
            localOffset += new Vector3(0f, -0.05f + Mathf.Sin(Time.time * 10f) * 0.03f, 0f);
        else if (enemyType == EnemyType.Tank)
            localOffset += new Vector3(0f, -0.08f, 0f);
        else if (enemyType == EnemyType.Flying)
            localOffset += new Vector3(0f, Mathf.Sin(Time.time * bobFrequency * 3f) * bobAmplitude, 0f);

        Vector3 pulseScale = Vector3.one;
        if (hurtPulseTimer > 0f)
            pulseScale += Vector3.one * (Mathf.Sin((0.18f - hurtPulseTimer) * 32f) * 0.08f + 0.08f);
        if (attackPulseTimer > 0f)
            pulseScale += new Vector3(0.04f, -0.03f, 0.04f);

        float hitReact01 = hitReactDuration > 0.001f ? Mathf.Clamp01(hitReactTimer / hitReactDuration) : 0f;
        float hitReactEnvelope = 1f - Mathf.Abs(hitReact01 * 2f - 1f);
        localOffset += hitReactOffset * hitReactEnvelope;
        sway *= Quaternion.Euler(hitReactAngles * hitReactEnvelope);

        modelRoot.localPosition = Vector3.Lerp(modelRoot.localPosition, localOffset, Time.deltaTime * 8f);
        modelRoot.localRotation = Quaternion.Slerp(modelRoot.localRotation, baseModelLocalRotation * sway, Time.deltaTime * (walk * 6f + 4f));
        modelRoot.localScale = Vector3.Lerp(modelRoot.localScale, pulseScale, Time.deltaTime * 14f);
    }

    void Shoot()
    {
        if (projectilePrefab == null || shootPoint == null || player == null) return;

        attackPulseTimer = 0.12f;

        Vector3 targetPos = GetMobilityAwareTarget(20f, enemyType == EnemyType.Tank ? 0.4f : 0.72f, false);

        Projectile p = Projectile.Spawn(projectilePrefab, shootPoint.position, Quaternion.LookRotation(targetPos - shootPoint.position));
        if (p == null) return;

        // Give ownership so the enemy doesn't instantly hit itself in the face
        p.Initialize(gameObject, meleeDamage * 0.7f);

        Rigidbody rb = p.GetComponent<Rigidbody>();
        
        if (rb != null)
        {
            rb.linearVelocity = p.transform.forward * 20f; // Enemy bullets should be a bit slower so you can dodge them
        }
    }

    private void ShootWithSpread(float yawOffset)
    {
        if (projectilePrefab == null || shootPoint == null || player == null) return;

        attackPulseTimer = 0.12f;
        Vector3 targetPos = GetMobilityAwareTarget(20f, enemyType == EnemyType.Flying ? 0.82f : 0.68f, false);
        Vector3 dir = (targetPos - shootPoint.position).normalized;
        dir = Quaternion.Euler(0f, yawOffset, 0f) * dir;

        Projectile p = Projectile.Spawn(projectilePrefab, shootPoint.position, Quaternion.LookRotation(dir));
        if (p == null) return;
        p.Initialize(gameObject, meleeDamage * 0.62f);
        Rigidbody rb = p.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = dir * 20f;
        }
    }

    public void TakeDamage(float amount)
    {
        if (IsCombatResolved || amount <= 0f)
            return;

        if (enemyType == EnemyType.Tank && !isBoss)
            amount *= 0.82f;
        else if (enemyType == EnemyType.Flying)
            amount *= 0.9f;

        currentHealth -= amount;
        
        if (enemyRenderer != null)
        {
            ApplyEnemyFlashColor(damageColor);
            flashTimer = isBoss ? 0.14f : 0.1f;
            hurtPulseTimer = isBoss ? 0.24f : 0.18f;
        }

        ApplyHitReaction(amount);
        SpawnHitGlint(amount);
        SpawnHitShock(amount);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public bool ApplyGrapplePull(Vector3 pullTargetPoint, Vector3 pullDirection, float pullSpeed, float deltaTime)
    {
        if (grappleMassClass != GrappleMassClass.Light)
            return false;

        Vector3 currentPosition = transform.position;
        Vector3 planarTarget = new Vector3(pullTargetPoint.x, currentPosition.y, pullTargetPoint.z);
        Vector3 toTarget = planarTarget - currentPosition;
        float distance = toTarget.magnitude;
        if (distance <= grapplePullStopDistance)
            return true;

        Vector3 desiredDirection = toTarget / Mathf.Max(0.001f, distance);
        float moveSpeed = Mathf.Max(0f, pullSpeed) * Mathf.Max(0.2f, grapplePullResponsiveness / 20f);
        Vector3 nextPosition = currentPosition + desiredDirection * Mathf.Min(distance - grapplePullStopDistance, moveSpeed * deltaTime);
        transform.position = nextPosition;
        if (agent != null && agent.enabled)
            agent.Warp(nextPosition);
        CacheGroundAnchor();
        return true;
    }

    public void SetPriorityTarget(bool highlighted)
    {
        isPriorityTarget = highlighted;
        EnsurePriorityMarker();
        bool active = highlighted && !IsCombatResolved;
        if (active && priorityOutlineRoot == null && modelRoot != null)
            BuildPriorityOutline(modelRoot);
        if (priorityMarker != null)
            priorityMarker.gameObject.SetActive(false);
        if (priorityOutlineRoot != null)
            priorityOutlineRoot.gameObject.SetActive(active);
    }

    public void ApplyMeleeStun(float duration)
    {
        meleeStunTimer = Mathf.Max(meleeStunTimer, duration);
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = true;
    }

    private void Die()
    {
        if (isDying) return;
        isDying = true;
        ClearTransientCombatState();
        if (priorityMarker != null)
            priorityMarker.gameObject.SetActive(false);
        if (priorityOutlineRoot != null)
            priorityOutlineRoot.gameObject.SetActive(false);
        CybergrindRunState.GetOrCreate().RegisterEnemyDefeated();
        RewardPlayerOnDeath();
        SpawnDeathBurst();
        Destroy(gameObject);
    }

    private void ClearTransientCombatState()
    {
        gruntPounceWindupRoutine = null;
        flyingVolleyRoutine = null;
        tankVolleyRoutine = null;
        tankShockwaveRoutine = null;
        bossRoutine = null;
        gruntPounceTimeRemaining = 0f;
        gruntPounceVelocity = Vector3.zero;
        meleeWindupRoutine = null;
        shooterBurstShotsRemaining = 0;
        shooterBurstTimer = 0f;
        droneDashTimeRemaining = 0f;
        droneDashVelocity = Vector3.zero;
        attackPulseTimer = 0f;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = false;
    }

    private void RewardPlayerOnDeath()
    {
        int reward = isBoss ? bossCoinReward : coinReward;
        if (reward <= 0) return;

        PlayerController cachedController = GetCachedPlayerController();
        if (cachedController == null) return;

        cachedController.AddCurrency(reward);
    }

    private PlayerController GetCachedPlayerController()
    {
        if (playerController != null)
            return playerController;

        if (player != null)
            playerController = player.GetComponent<PlayerController>();
        else
            playerController = GetSharedPlayerController();

        if (playerController != null && player == null)
            player = playerController.transform;

        return playerController;
    }

    private void ApplyDefaultDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(displayName) && displayName != "Enemy")
            return;

        displayName = enemyType switch
        {
            EnemyType.Grunt => "Ripper",
            EnemyType.Tank => "Bulwark",
            EnemyType.Flying => "Skimmer",
            _ => "Lancer"
        };
    }

    private void ApplyDefaultGrappleMassClass()
    {
        if (isBoss)
        {
            grappleMassClass = GrappleMassClass.Heavy;
            return;
        }

        grappleMassClass = enemyType == EnemyType.Tank
            ? GrappleMassClass.Heavy
            : GrappleMassClass.Light;
    }

    private void SpawnDeathBurst()
    {
        Color burstColor = enemyType == EnemyType.Grunt ? gruntColor :
            enemyType == EnemyType.Tank ? new Color(1f, 0.6f, 0.16f) :
            enemyType == EnemyType.Flying ? new Color(0.65f, 0.2f, 1f) :
            shooterColor;

        int shardCount = isBoss ? 14 : 5;
        float life = isBoss ? 0.7f : 0.45f;
        float minSpeed = isBoss ? 3.8f : 2f;
        float maxSpeed = isBoss ? 7.2f : 4.8f;
        SpawnDeathRing(burstColor, isBoss ? 2.4f : 1.15f, isBoss ? 0.58f : 0.34f);

        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "EnemyBurstShard";
            shard.transform.position = transform.position + Vector3.up * 1f;
            shard.transform.localScale = Vector3.one * Random.Range(isBoss ? 0.16f : 0.12f, isBoss ? 0.34f : 0.24f);
            Renderer renderer = shard.GetComponent<Renderer>();
            ApplyTransientFxRenderer(renderer, burstColor, 1.6f);

            Collider collider = shard.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Rigidbody rb = shard.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearVelocity = (Random.onUnitSphere + Vector3.up * (isBoss ? 1.2f : 0.8f)) * Random.Range(minSpeed, maxSpeed);
            Destroy(shard, life);
        }

        if (!isBoss) return;

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "BossDeathRing";
        ring.transform.position = transform.position + Vector3.up * 0.08f;
        ring.transform.localScale = new Vector3(1.4f, 0.04f, 1.4f);
        Collider ringCollider = ring.GetComponent<Collider>();
        if (ringCollider != null)
            Destroy(ringCollider);
        Renderer ringRenderer = ring.GetComponent<Renderer>();
        ApplyTransientFxRenderer(ringRenderer, new Color(burstColor.r, burstColor.g, burstColor.b, 0.55f), 1.8f);
        Destroy(ring, 0.4f);
    }

    private void SpawnHitGlint(float amount)
    {
        if (!Application.isPlaying) return;

        Color color = isBoss ? new Color(1f, 0.55f, 0.22f) :
            enemyType == EnemyType.Grunt ? gruntColor :
            enemyType == EnemyType.Tank ? new Color(1f, 0.62f, 0.18f) :
            enemyType == EnemyType.Flying ? new Color(0.65f, 0.28f, 1f) :
            shooterColor;

        float size = Mathf.Clamp(0.42f + amount * 0.012f, 0.48f, isBoss ? 1.45f : 0.95f);
        Vector3 center = transform.position + Vector3.up * (isBoss ? 1.85f : 1.15f);
        GameObject vertical = SpawnPooledHitFxPrimitive("EnemyHitGlintVertical", PrimitiveType.Cube, center, transform.rotation, new Vector3(0.045f, size, 0.045f));
        Renderer verticalRenderer = vertical != null ? vertical.GetComponent<Renderer>() : null;
        Color fxColor = new Color(color.r, color.g, color.b, 0.72f);
        ApplyTransientFxRenderer(verticalRenderer, fxColor, 2.2f);
        StartCoroutine(ScaleAndFadeHitFx(vertical.transform, vertical.transform.localScale, 0.11f, fxColor, 2.2f));

        GameObject horizontal = SpawnPooledHitFxPrimitive("EnemyHitGlintHorizontal", PrimitiveType.Cube, center, transform.rotation * Quaternion.Euler(0f, 0f, 90f), new Vector3(0.04f, size * 0.72f, 0.04f));
        Renderer horizontalRenderer = horizontal != null ? horizontal.GetComponent<Renderer>() : null;
        ApplyTransientFxRenderer(horizontalRenderer, fxColor, 2.2f);
        StartCoroutine(ScaleAndFadeHitFx(horizontal.transform, horizontal.transform.localScale, 0.09f, fxColor, 2.2f));
    }

    private void ApplyHitReaction(float amount)
    {
        Vector3 awayFromSource = player != null
            ? (transform.position - player.position).normalized
            : (-transform.forward + Vector3.up * 0.08f).normalized;
        Vector3 localAway = transform.InverseTransformDirection(awayFromSource);
        float intensity = Mathf.Clamp01(amount / (isBoss ? 55f : 28f));

        hitReactDuration = isBoss ? 0.16f : 0.13f;
        hitReactTimer = hitReactDuration;
        hitReactOffset = new Vector3(
            localAway.x * (isBoss ? 0.07f : 0.11f),
            0.015f + intensity * (isBoss ? 0.02f : 0.035f),
            localAway.z * (isBoss ? 0.05f : 0.09f)) * (0.55f + intensity * 0.75f);
        hitReactAngles = new Vector3(
            -localAway.z * (isBoss ? 5f : 8f),
            localAway.x * (isBoss ? 4f : 6f),
            -localAway.x * (isBoss ? 6f : 10f)) * (0.45f + intensity * 0.85f);
    }

    private void SpawnHitShock(float amount)
    {
        if (!Application.isPlaying) return;

        Color color = isBoss ? new Color(1f, 0.58f, 0.24f) :
            enemyType == EnemyType.Grunt ? gruntColor :
            enemyType == EnemyType.Tank ? new Color(1f, 0.66f, 0.22f) :
            enemyType == EnemyType.Flying ? new Color(0.7f, 0.34f, 1f) :
            shooterColor;

        float radius = Mathf.Clamp(0.2f + amount * 0.012f, 0.24f, isBoss ? 1f : 0.62f);
        float life = isBoss ? 0.16f : 0.12f;
        Vector3 center = transform.position + Vector3.up * (isBoss ? 1.55f : 1.05f);
        Vector3 toPlayer = player != null ? (player.position - center).normalized : transform.forward;
        Vector3 faceDir = Vector3.ProjectOnPlane(-toPlayer, Vector3.up);
        if (faceDir.sqrMagnitude < 0.001f)
            faceDir = transform.forward;
        faceDir.Normalize();

        GameObject ring = SpawnPooledHitFxPrimitive("EnemyHitShockRing", PrimitiveType.Cylinder, center, Quaternion.identity, new Vector3(radius * 0.22f, 0.02f, radius * 0.22f));
        Renderer ringRenderer = ring != null ? ring.GetComponent<Renderer>() : null;
        Color ringColor = new Color(color.r, color.g, color.b, 0.45f);
        ApplyTransientFxRenderer(ringRenderer, ringColor, 1.9f);
        StartCoroutine(ScaleAndFadeHitFx(ring.transform, new Vector3(radius, 0.02f, radius), life, ringColor, 1.9f));

        GameObject lance = SpawnPooledHitFxPrimitive("EnemyHitShockLance", PrimitiveType.Cube, center + faceDir * (radius * 0.18f), Quaternion.LookRotation(faceDir, Vector3.up), new Vector3(radius * 0.22f, radius * 0.14f, radius * 1.35f));
        Renderer lanceRenderer = lance != null ? lance.GetComponent<Renderer>() : null;
        Color lanceColor = new Color(color.r, color.g, color.b, 0.72f);
        ApplyTransientFxRenderer(lanceRenderer, lanceColor, 2.6f);
        StartCoroutine(ScaleAndFadeHitFx(lance.transform, new Vector3(radius * 0.08f, radius * 0.08f, 0f), life * 0.9f, lanceColor, 2.6f));

        for (int i = 0; i < 3; i++)
        {
            float angle = -28f + (28f * i);
            Vector3 sparkDir = Quaternion.AngleAxis(angle, Vector3.up) * faceDir;
            GameObject spark = SpawnPooledHitFxPrimitive("EnemyHitShockSpark", PrimitiveType.Cube, center + sparkDir * (radius * 0.12f), Quaternion.LookRotation(sparkDir, Vector3.up), new Vector3(radius * 0.08f, radius * 0.08f, radius * 0.66f));
            Renderer sparkRenderer = spark != null ? spark.GetComponent<Renderer>() : null;
            ApplyTransientFxRenderer(sparkRenderer, lanceColor, 2.2f);
            StartCoroutine(ScaleAndFadeHitFx(spark.transform, new Vector3(radius * 0.04f, radius * 0.04f, 0f), life * 0.78f, lanceColor, 2.2f));
        }
    }

    private void SpawnDeathRing(Color burstColor, float radius, float life)
    {
        if (!Application.isPlaying) return;

        GameObject ring = SpawnPooledHitFxPrimitive("EnemyDeathRing", PrimitiveType.Cylinder, transform.position + Vector3.up * 0.06f, Quaternion.identity, new Vector3(radius * 0.35f, 0.025f, radius * 0.35f));
        Renderer renderer = ring != null ? ring.GetComponent<Renderer>() : null;
        Color ringColor = new Color(burstColor.r, burstColor.g, burstColor.b, 0.5f);
        ApplyTransientFxRenderer(renderer, ringColor, 1.8f);
        StartCoroutine(ScaleAndFadeHitFx(ring.transform, new Vector3(radius, 0.025f, radius), life, ringColor, 1.8f));
    }

    private IEnumerator ScaleAndFadeHitFx(Transform fx, Vector3 endScale, float lifetime, Color color, float emissionStrength)
    {
        if (fx == null) yield break;

        Vector3 startScale = fx.localScale;
        Renderer renderer = fx.GetComponent<Renderer>();
        float elapsed = 0f;
        while (elapsed < lifetime && fx != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
            fx.localScale = Vector3.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, t));
            if (renderer != null)
            {
                Color c = color;
                c.a *= 1f - t;
                ApplyTransientFxRenderer(renderer, c, emissionStrength);
            }
            yield return null;
        }

        if (fx != null)
            ReleasePooledHitFxPrimitive(fx.gameObject);
    }

    private GameObject SpawnPooledHitFxPrimitive(string name, PrimitiveType primitiveType, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        GameObject fx = null;
        if (hitFxPrimitivePool.TryGetValue(primitiveType, out Stack<GameObject> pool))
        {
            while (pool.Count > 0 && fx == null)
                fx = pool.Pop();
        }

        if (fx == null)
        {
            fx = GameObject.CreatePrimitive(primitiveType);
            hitFxPrimitiveTypes[fx] = primitiveType;
            Collider col = fx.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
        }

        fx.name = name;
        fx.transform.SetPositionAndRotation(position, rotation);
        fx.transform.localScale = scale;
        fx.SetActive(true);
        return fx;
    }

    private void ReleasePooledHitFxPrimitive(GameObject fx)
    {
        if (fx == null)
            return;

        if (!hitFxPrimitiveTypes.TryGetValue(fx, out PrimitiveType primitiveType))
        {
            Destroy(fx);
            return;
        }

        fx.SetActive(false);
        if (!hitFxPrimitivePool.TryGetValue(primitiveType, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            hitFxPrimitivePool[primitiveType] = pool;
        }

        pool.Push(fx);
    }

    private void BuildTypeModel()
    {
        Transform existing = transform.Find("_EnemyTypeModel");
        if (existing != null)
            Destroy(existing.gameObject);

        HidePrefabRenderer();
        EnsureVisualMaterials();

        GameObject modelRoot = new GameObject("_EnemyTypeModel");
        modelRoot.transform.SetParent(transform, false);

        switch (enemyType)
        {
            case EnemyType.Shooter:
                BuildShooterModel(modelRoot.transform);
                transform.localScale = Vector3.one;
                break;

            case EnemyType.Grunt:
                BuildGruntModel(modelRoot.transform);
                transform.localScale = new Vector3(0.95f, 0.95f, 0.95f);
                break;

            case EnemyType.Tank:
                BuildTankModel(modelRoot.transform);
                transform.localScale = isBoss ? new Vector3(1.85f, 1.85f, 1.85f) : new Vector3(1.2f, 1.2f, 1.2f);
                break;
            case EnemyType.Flying:
                BuildFlyingModel(modelRoot.transform);
                transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                break;
        }

        if (isBoss)
            BuildBossCrown(modelRoot.transform);

        enemyRenderer = modelRoot.transform.Find("Core")?.GetComponent<Renderer>() ?? modelRoot.GetComponentInChildren<Renderer>();
        if (enemyRenderer != null && enemyRenderer.sharedMaterial != null)
            originalColor = ResolveVisualColorForMaterial(enemyRenderer.sharedMaterial);

        if (shootPoint == null)
        {
            GameObject sp = new GameObject("ShootPoint");
            sp.transform.SetParent(transform, false);
            shootPoint = sp.transform;
        }

        shootPoint.localPosition = GetShootPointLocalPosition();
        shootPoint.localRotation = Quaternion.identity;
        EnsureCombatCollider();
    }

    private void RegisterCoordinator()
    {
        if (coordinatorRegistered)
            return;

        coordinatorRegistered = true;
        activeCoordinatorCount++;
        if (activeCoordinatorCount == 1)
            ResetSharedPressureCoordination();
    }

    private void UnregisterCoordinator()
    {
        if (!coordinatorRegistered)
            return;

        coordinatorRegistered = false;
        activeCoordinatorCount = Mathf.Max(0, activeCoordinatorCount - 1);
        if (activeCoordinatorCount == 0)
            ResetSharedPressureCoordination();
    }

    private static void ResetSharedPressureCoordination()
    {
        globalPressureBurstUntil = 0f;
        globalPressureScore = 0f;
        globalPressureLastUpdateTime = -1f;
        for (int i = 0; i < roleNextCommitTime.Length; i++)
        {
            roleNextCommitTime[i] = 0f;
            roleLastCommitTime[i] = -999f;
        }
    }

    private static string BuildSharedPressureDebugSummary()
    {
        UpdateGlobalPressureState();
        float now = Time.time;
        float burstRemaining = Mathf.Max(0f, globalPressureBurstUntil - now);
        float suppressorReady = Mathf.Max(0f, roleNextCommitTime[GetRoleIndex(CombatRole.Suppressor)] - now);
        float diverReady = Mathf.Max(0f, roleNextCommitTime[GetRoleIndex(CombatRole.Diver)] - now);
        float bulwarkReady = Mathf.Max(0f, roleNextCommitTime[GetRoleIndex(CombatRole.Bulwark)] - now);
        float harrierReady = Mathf.Max(0f, roleNextCommitTime[GetRoleIndex(CombatRole.Harrier)] - now);
        float bossReady = Mathf.Max(0f, roleNextCommitTime[GetRoleIndex(CombatRole.Boss)] - now);
        return $"coord[active={activeCoordinatorCount}, pressure={globalPressureScore:0.00}, burst={burstRemaining:0.00}, next:S{suppressorReady:0.00}/D{diverReady:0.00}/B{bulwarkReady:0.00}/H{harrierReady:0.00}/Boss{bossReady:0.00}]";
    }

    private void BuildShooterModel(Transform root)
    {
        currentBodyColor = shooterColor * 0.82f;
        currentDarkColor = new Color(0.04f, 0.045f, 0.055f);
        currentGlowColor = coreGlowColor;

        CreateModelPart(root, "Core", PrimitiveType.Cube, new Vector3(0f, 1.18f, 0f), new Vector3(0.72f, 1.08f, 0.52f), Quaternion.Euler(0f, 45f, 0f), bodyMaterial);
        CreateModelPart(root, "ChestGlow", PrimitiveType.Cube, new Vector3(0f, 1.25f, 0.31f), new Vector3(0.34f, 0.52f, 0.06f), Quaternion.identity, glowMaterial);
        CreateModelPart(root, "Head", PrimitiveType.Cube, new Vector3(0f, 2.02f, 0.03f), new Vector3(0.48f, 0.34f, 0.42f), Quaternion.identity, bodyMaterial);
        CreateModelPart(root, "Visor", PrimitiveType.Cube, new Vector3(0f, 2.05f, 0.27f), new Vector3(0.38f, 0.08f, 0.05f), Quaternion.identity, glowMaterial);
        CreateModelPart(root, "SpineFin", PrimitiveType.Cube, new Vector3(0f, 1.34f, -0.37f), new Vector3(0.16f, 1.05f, 0.18f), Quaternion.Euler(-12f, 0f, 0f), darkMaterial);
        CreateModelPart(root, "LeftWing", PrimitiveType.Cube, new Vector3(-0.55f, 1.34f, -0.04f), new Vector3(0.12f, 0.86f, 0.42f), Quaternion.Euler(0f, 0f, -18f), darkMaterial);
        CreateModelPart(root, "RightWing", PrimitiveType.Cube, new Vector3(0.55f, 1.34f, -0.04f), new Vector3(0.12f, 0.86f, 0.42f), Quaternion.Euler(0f, 0f, 18f), darkMaterial);
        CreateModelPart(root, "RifleBarrel", PrimitiveType.Cylinder, new Vector3(0.34f, 1.35f, 0.62f), new Vector3(0.11f, 0.58f, 0.11f), Quaternion.Euler(90f, 0f, 0f), glowMaterial);
        CreateModelPart(root, "RifleStock", PrimitiveType.Cube, new Vector3(0.28f, 1.25f, 0.28f), new Vector3(0.22f, 0.22f, 0.42f), Quaternion.Euler(0f, 0f, -8f), darkMaterial);
    }

    private void BuildGruntModel(Transform root)
    {
        currentBodyColor = gruntColor * 0.82f;
        currentDarkColor = new Color(0.04f, 0.045f, 0.055f);
        currentGlowColor = new Color(1f, 0.18f, 0.08f);

        CreateModelPart(root, "Core", PrimitiveType.Capsule, new Vector3(0f, 1.0f, 0f), new Vector3(0.58f, 0.86f, 0.58f), Quaternion.identity, bodyMaterial);
        CreateModelPart(root, "Head", PrimitiveType.Cube, new Vector3(0f, 1.74f, 0.09f), new Vector3(0.44f, 0.34f, 0.42f), Quaternion.Euler(-8f, 0f, 0f), bodyMaterial);
        CreateModelPart(root, "EyeSlash", PrimitiveType.Cube, new Vector3(0f, 1.78f, 0.34f), new Vector3(0.36f, 0.07f, 0.05f), Quaternion.Euler(0f, 0f, -8f), glowMaterial);
        CreateModelPart(root, "LeftClaw", PrimitiveType.Cube, new Vector3(-0.58f, 1.05f, 0.28f), new Vector3(0.14f, 0.88f, 0.16f), Quaternion.Euler(20f, 0f, 28f), glowMaterial);
        CreateModelPart(root, "RightClaw", PrimitiveType.Cube, new Vector3(0.58f, 1.05f, 0.28f), new Vector3(0.14f, 0.88f, 0.16f), Quaternion.Euler(20f, 0f, -28f), glowMaterial);
        CreateModelPart(root, "LeftLeg", PrimitiveType.Cube, new Vector3(-0.23f, 0.32f, 0f), new Vector3(0.18f, 0.7f, 0.22f), Quaternion.Euler(0f, 0f, 8f), darkMaterial);
        CreateModelPart(root, "RightLeg", PrimitiveType.Cube, new Vector3(0.23f, 0.32f, 0f), new Vector3(0.18f, 0.7f, 0.22f), Quaternion.Euler(0f, 0f, -8f), darkMaterial);
        CreateModelPart(root, "BackSpikeA", PrimitiveType.Cube, new Vector3(0f, 1.22f, -0.42f), new Vector3(0.16f, 0.72f, 0.14f), Quaternion.Euler(25f, 0f, 0f), darkMaterial);
        CreateModelPart(root, "BackSpikeB", PrimitiveType.Cube, new Vector3(0f, 0.82f, -0.44f), new Vector3(0.14f, 0.58f, 0.12f), Quaternion.Euler(18f, 0f, 0f), darkMaterial);
    }

    private void BuildTankModel(Transform root)
    {
        currentBodyColor = tankColor * 0.9f;
        currentDarkColor = new Color(0.04f, 0.045f, 0.055f);
        currentGlowColor = new Color(1f, 0.62f, 0.08f);

        CreateModelPart(root, "Core", PrimitiveType.Cube, new Vector3(0f, 1.08f, 0f), new Vector3(1.42f, 1.36f, 1.08f), Quaternion.identity, bodyMaterial);
        CreateModelPart(root, "ChestReactor", PrimitiveType.Cylinder, new Vector3(0f, 1.12f, 0.62f), new Vector3(0.34f, 0.08f, 0.34f), Quaternion.Euler(90f, 0f, 0f), glowMaterial);
        CreateModelPart(root, "HeadBlock", PrimitiveType.Cube, new Vector3(0f, 2.02f, 0.08f), new Vector3(0.82f, 0.46f, 0.62f), Quaternion.identity, bodyMaterial);
        CreateModelPart(root, "HeavyVisor", PrimitiveType.Cube, new Vector3(0f, 2.05f, 0.43f), new Vector3(0.58f, 0.09f, 0.06f), Quaternion.identity, glowMaterial);
        CreateModelPart(root, "LeftShoulder", PrimitiveType.Cube, new Vector3(-1.0f, 1.46f, 0f), new Vector3(0.58f, 0.58f, 0.86f), Quaternion.Euler(0f, 0f, 6f), darkMaterial);
        CreateModelPart(root, "RightShoulder", PrimitiveType.Cube, new Vector3(1.0f, 1.46f, 0f), new Vector3(0.58f, 0.58f, 0.86f), Quaternion.Euler(0f, 0f, -6f), darkMaterial);
        CreateModelPart(root, "LeftArmCannon", PrimitiveType.Cylinder, new Vector3(-0.92f, 1.05f, 0.52f), new Vector3(0.18f, 0.62f, 0.18f), Quaternion.Euler(90f, 0f, 0f), glowMaterial);
        CreateModelPart(root, "RightArmShield", PrimitiveType.Cube, new Vector3(0.98f, 1.08f, 0.38f), new Vector3(0.34f, 0.82f, 0.24f), Quaternion.Euler(0f, 0f, -8f), darkMaterial);
        CreateModelPart(root, "LeftTread", PrimitiveType.Cube, new Vector3(-0.45f, 0.28f, 0f), new Vector3(0.46f, 0.46f, 1.1f), Quaternion.identity, darkMaterial);
        CreateModelPart(root, "RightTread", PrimitiveType.Cube, new Vector3(0.45f, 0.28f, 0f), new Vector3(0.46f, 0.46f, 1.1f), Quaternion.identity, darkMaterial);
    }

    private void BuildBossCrown(Transform root)
    {
        Color bossGlowColor = new Color(1f, 0.24f, 0.04f);

        GameObject halo = CreateModelPart(root, "BossHalo", PrimitiveType.Cylinder, new Vector3(0f, 2.52f, 0f), new Vector3(0.86f, 0.06f, 0.86f), Quaternion.identity, glowMaterial);
        GameObject hornL = CreateModelPart(root, "BossHornL", PrimitiveType.Cube, new Vector3(-0.42f, 2.42f, 0f), new Vector3(0.18f, 0.7f, 0.18f), Quaternion.Euler(0f, 0f, 24f), glowMaterial);
        GameObject hornR = CreateModelPart(root, "BossHornR", PrimitiveType.Cube, new Vector3(0.42f, 2.42f, 0f), new Vector3(0.18f, 0.7f, 0.18f), Quaternion.Euler(0f, 0f, -24f), glowMaterial);
        if (halo != null) ApplyTransientFxRenderer(halo.GetComponent<Renderer>(), bossGlowColor, 2.2f);
        if (hornL != null) ApplyTransientFxRenderer(hornL.GetComponent<Renderer>(), bossGlowColor, 2.2f);
        if (hornR != null) ApplyTransientFxRenderer(hornR.GetComponent<Renderer>(), bossGlowColor, 2.2f);
    }

    private GameObject CreateModelPart(Transform root, string partName, PrimitiveType primitiveType, Vector3 localPos, Vector3 localScale, Quaternion localRotation, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.transform.SetParent(root, false);
        part.transform.localPosition = localPos;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;

        Collider c = part.GetComponent<Collider>();
        if (c != null)
            Destroy(c);

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            ApplyVisualRendererTint(renderer, material, ResolveVisualColorForMaterial(material));
        }

        return part;
    }

    private void HidePrefabRenderer()
    {
        Renderer rootRenderer = GetComponent<Renderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = false;
    }

    private void EnsureVisualMaterials()
    {
        if (sharedBodyMaterial == null)
            sharedBodyMaterial = new Material(FindVisualShader(false)) { name = "Enemy Body Shared" };
        bodyMaterial = sharedBodyMaterial;

        if (sharedDarkMaterial == null)
            sharedDarkMaterial = new Material(FindVisualShader(false)) { name = "Enemy Dark Armor Shared" };
        darkMaterial = sharedDarkMaterial;

        if (sharedGlowMaterial == null)
        {
            sharedGlowMaterial = new Material(FindVisualShader(true)) { name = "Enemy Glow Shared" };
            sharedGlowMaterial.EnableKeyword("_EMISSION");
        }
        glowMaterial = sharedGlowMaterial;
    }

    private void ApplyTransientFxRenderer(Renderer renderer, Color color, float emissionStrength)
    {
        if (renderer == null)
            return;

        if (sharedTransientFxMaterial == null)
        {
            sharedTransientFxMaterial = new Material(FindVisualShader(true)) { name = "Enemy Transient FX Shared" };
            if (sharedTransientFxMaterial.HasProperty("_EmissionColor"))
                sharedTransientFxMaterial.EnableKeyword("_EMISSION");
        }
        transientFxMaterial = sharedTransientFxMaterial;

        renderer.sharedMaterial = transientFxMaterial;

        if (transientFxBlock == null)
            transientFxBlock = new MaterialPropertyBlock();

        renderer.GetPropertyBlock(transientFxBlock);
        if (transientFxMaterial.HasProperty("_BaseColor"))
            transientFxBlock.SetColor("_BaseColor", color);
        if (transientFxMaterial.HasProperty("_Color"))
            transientFxBlock.SetColor("_Color", color);
        if (transientFxMaterial.HasProperty("_EmissionColor"))
            transientFxBlock.SetColor("_EmissionColor", color * emissionStrength);
        renderer.SetPropertyBlock(transientFxBlock);
    }

    private void ApplyVisualRendererTint(Renderer renderer, Material material, Color color)
    {
        if (renderer == null || material == null)
            return;

        if (visualRendererBlock == null)
            visualRendererBlock = new MaterialPropertyBlock();

        renderer.GetPropertyBlock(visualRendererBlock);
        if (material.HasProperty("_BaseColor"))
            visualRendererBlock.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            visualRendererBlock.SetColor("_Color", color);
        if (material == glowMaterial && material.HasProperty("_EmissionColor"))
            visualRendererBlock.SetColor("_EmissionColor", color * 2.2f);
        else if (material.HasProperty("_EmissionColor"))
            visualRendererBlock.SetColor("_EmissionColor", Color.black);
        renderer.SetPropertyBlock(visualRendererBlock);
    }

    private Color ResolveVisualColorForMaterial(Material material)
    {
        if (material == bodyMaterial)
            return currentBodyColor;
        if (material == glowMaterial)
            return currentGlowColor;
        return currentDarkColor;
    }

    private void ApplyEnemyFlashColor(Color color)
    {
        if (enemyRenderer == null)
            return;

        ApplyVisualRendererTint(enemyRenderer, enemyRenderer.sharedMaterial, color);
    }

    private static PlayerController GetSharedPlayerController()
    {
        if (sharedPlayerController == null)
            sharedPlayerController = Object.FindAnyObjectByType<PlayerController>();
        return sharedPlayerController;
    }

    private static CybergrindArenaGenerator GetSharedArenaGenerator()
    {
        if (sharedArenaGenerator == null)
            sharedArenaGenerator = Object.FindAnyObjectByType<CybergrindArenaGenerator>();
        return sharedArenaGenerator;
    }

    private Shader FindVisualShader(bool unlit)
    {
        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader;
    }

    private Vector3 GetShootPointLocalPosition()
    {
        switch (enemyType)
        {
            case EnemyType.Tank:
                return isBoss ? new Vector3(-0.92f, 1.05f, 1.42f) : new Vector3(-0.92f, 1.05f, 1.18f);
            case EnemyType.Grunt:
                return new Vector3(0f, 1.2f, 0.85f);
            case EnemyType.Flying:
                return new Vector3(0f, 0.6f, 0.9f);
            case EnemyType.Shooter:
            default:
                return new Vector3(0.34f, 1.35f, 1.1f);
        }
    }

    private void BuildFlyingModel(Transform root)
    {
        currentBodyColor = new Color(0.6f, 0.2f, 0.9f) * 0.9f;
        currentDarkColor = new Color(0.04f, 0.045f, 0.055f);
        currentGlowColor = new Color(0.0f, 0.9f, 1f);

        CreateModelPart(root, "Core", PrimitiveType.Sphere, new Vector3(0f, 1.0f, 0f), new Vector3(0.6f, 0.6f, 0.6f), Quaternion.identity, bodyMaterial);
        CreateModelPart(root, "Cockpit", PrimitiveType.Cube, new Vector3(0f, 1.18f, 0.25f), new Vector3(0.5f, 0.28f, 0.6f), Quaternion.Euler(0f, 10f, 0f), darkMaterial);
        CreateModelPart(root, "LeftRotor", PrimitiveType.Cube, new Vector3(-0.6f, 1.3f, 0f), new Vector3(0.12f, 0.02f, 1.4f), Quaternion.identity, glowMaterial);
        CreateModelPart(root, "RightRotor", PrimitiveType.Cube, new Vector3(0.6f, 1.3f, 0f), new Vector3(0.12f, 0.02f, 1.4f), Quaternion.identity, glowMaterial);
        CreateModelPart(root, "TailFin", PrimitiveType.Cube, new Vector3(0f, 1.08f, -0.55f), new Vector3(0.18f, 0.46f, 0.12f), Quaternion.Euler(12f, 0f, 0f), darkMaterial);

        // small guns
        CreateModelPart(root, "GunL", PrimitiveType.Cylinder, new Vector3(-0.28f, 1.02f, 0.6f), new Vector3(0.06f, 0.34f, 0.06f), Quaternion.Euler(90f, 0f, 0f), glowMaterial);
        CreateModelPart(root, "GunR", PrimitiveType.Cylinder, new Vector3(0.28f, 1.02f, 0.6f), new Vector3(0.06f, 0.34f, 0.06f), Quaternion.Euler(90f, 0f, 0f), glowMaterial);
    }

    private void EnsureCombatCollider()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        if (combatCollider == null)
            combatCollider = GetComponent<CapsuleCollider>();
        if (combatCollider == null)
            combatCollider = gameObject.AddComponent<CapsuleCollider>();

        Vector3 centerLocal = transform.InverseTransformPoint(bounds.center);
        Vector3 lossy = transform.lossyScale;
        float scaleY = Mathf.Max(0.01f, lossy.y);
        float scaleX = Mathf.Max(0.01f, lossy.x);
        float scaleZ = Mathf.Max(0.01f, lossy.z);
        float radius = Mathf.Max(bounds.extents.x / scaleX, bounds.extents.z / scaleZ) * 0.92f;
        float height = Mathf.Max(radius * 2.1f, bounds.size.y / scaleY * 0.96f);

        combatCollider.direction = 1;
        combatCollider.center = new Vector3(centerLocal.x, centerLocal.y, centerLocal.z);
        combatCollider.radius = Mathf.Max(0.18f, radius);
        combatCollider.height = Mathf.Max(combatCollider.radius * 2.1f, height);
        combatCollider.isTrigger = false;
    }

    private void EnsurePriorityMarker()
    {
        if (priorityOutlineRoot != null)
            return;
        priorityMarker = null;
        priorityMarkerRing = null;
        priorityMarkerBeam = null;
        priorityMarkerRenderer = null;
        priorityMarkerRenderers = null;
    }

    private void BuildPriorityOutline(Transform modelRoot)
    {
        if (modelRoot == null)
            return;

        Transform existing = modelRoot.Find("PriorityOutline");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        if (sharedPriorityOutlineMaterial == null)
        {
            Shader outlineShader = Shader.Find("Custom/InvertedHullOutline");
            if (outlineShader == null)
                outlineShader = FindVisualShader(true);
            sharedPriorityOutlineMaterial = new Material(outlineShader) { name = "Enemy Priority Outline Shared" };
            Color outlineColor = new Color(0.12f, 0.86f, 1f, 0.9f);
            if (sharedPriorityOutlineMaterial.HasProperty("_BaseColor"))
                sharedPriorityOutlineMaterial.SetColor("_BaseColor", outlineColor);
            if (sharedPriorityOutlineMaterial.HasProperty("_Color"))
                sharedPriorityOutlineMaterial.SetColor("_Color", outlineColor);
            if (sharedPriorityOutlineMaterial.HasProperty("_EmissionColor"))
            {
                sharedPriorityOutlineMaterial.EnableKeyword("_EMISSION");
                sharedPriorityOutlineMaterial.SetColor("_EmissionColor", outlineColor * 2.2f);
            }
            if (sharedPriorityOutlineMaterial.HasProperty("_OutlineColor"))
                sharedPriorityOutlineMaterial.SetColor("_OutlineColor", outlineColor);
            if (sharedPriorityOutlineMaterial.HasProperty("_OutlineThickness"))
                sharedPriorityOutlineMaterial.SetFloat("_OutlineThickness", 0.03f);
            sharedPriorityOutlineMaterial.renderQueue = 5000;
        }

        GameObject outlineRoot = new GameObject("PriorityOutline");
        outlineRoot.transform.SetParent(modelRoot, false);
        outlineRoot.transform.localPosition = Vector3.zero;
        outlineRoot.transform.localRotation = Quaternion.identity;
        outlineRoot.transform.localScale = Vector3.one;

        Renderer[] sourceRenderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < sourceRenderers.Length; i++)
        {
            Renderer source = sourceRenderers[i];
            if (source == null) continue;

            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
                continue;

            GameObject shell = new GameObject($"{source.gameObject.name}_Outline");
            shell.transform.SetParent(outlineRoot.transform, false);
            shell.transform.localPosition = source.transform.localPosition;
            shell.transform.localRotation = source.transform.localRotation;
            shell.transform.localScale = source.transform.localScale;

            MeshFilter filter = shell.AddComponent<MeshFilter>();
            filter.sharedMesh = sourceFilter.sharedMesh;
            MeshRenderer renderer = shell.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sharedPriorityOutlineMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        priorityOutlineRoot = outlineRoot.transform;
        priorityOutlineRoot.gameObject.SetActive(false);
    }
}
