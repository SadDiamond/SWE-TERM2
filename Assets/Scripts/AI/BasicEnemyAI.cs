using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class BasicEnemyAI : MonoBehaviour, IDamageable
{
    public enum EnemyType { Shooter, Grunt, Tank, Flying }
    public enum BossArchetype { None, Warden, Striker, Sentinel }

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
    public LayerMask movementObstacleMask = ~0;
    private NavMeshAgent agent;
    private Transform player;

    [Header("Grunt Tuning")]
    [Range(0.1f, 1f)] public float gruntMoveSpeedMultiplier = 0.72f;
    [Range(0f, 1f)] public float gruntReactionTime = 0.28f;
    private Vector3 gruntReactiveTarget;
    private float gruntReactionTimer;

    [Header("Flying Settings")]
    public float flySpeed = 6f;
    public float hoverHeight = 2.4f;
    public float orbitRadius = 3f;
    public float bobAmplitude = 0.35f;
    public float bobFrequency = 1.6f;
    private float flyPhase = 0f;

    [Header("Combat")]
    public GameObject projectilePrefab;
    public Transform shootPoint;
    public float fireRate = 1.5f;
    public float meleeDamage = 12f;
    public float meleeRange = 2.1f;
    public float meleeCooldown = 1.1f;
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
    private Coroutine bossRoutine;

    [Header("Effects")]
    public Color damageColor = Color.red;
    private Color originalColor;
    private Renderer enemyRenderer;
    private float flashTimer;
    private float hurtPulseTimer;
    private float attackPulseTimer;
    private Vector3 baseModelLocalPosition;
    private Quaternion baseModelLocalRotation;
    private Transform modelRoot;
    private float groundY;
    private bool hasGroundAnchor;
    private bool isDying;

    [Header("Type Visuals")]
    public bool autoBuildTypeModel = true;
    public Color shooterColor = new Color(0.18f, 0.65f, 0.95f);
    public Color gruntColor = new Color(0.92f, 0.24f, 0.24f);
    public Color tankColor = new Color(0.65f, 0.65f, 0.75f);
    public Color coreGlowColor = new Color(0.0f, 0.9f, 1f);

    private Material bodyMaterial;
    private Material darkMaterial;
    private Material glowMaterial;

    void Start()
    {
        // Basic initialization and defensive checks
        agent = GetComponent<NavMeshAgent>();
        enemyRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();

        currentHealth = maxHealth;

        if (enemyRenderer != null && enemyRenderer.material != null)
        {
            originalColor = enemyRenderer.material.color;
        }

        // Find the player automatically (Updated for modern Unity versions)
        PlayerController p = Object.FindAnyObjectByType<PlayerController>();
        if (p != null) player = p.transform;

        // Adjust stats per enemy type
        ApplyDefaultDisplayName();
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
            currentHealth = Mathf.Max(currentHealth, maxHealth * 4f);
            stoppingDistance = Mathf.Max(stoppingDistance, 12f);
            fireRate = Mathf.Max(0.35f, fireRate * 0.72f);
            bossPatternTimer = Random.Range(1.5f, 3f);
        }

        maxHealth = Mathf.Max(maxHealth, currentHealth);

        // Start with a small randomized fire timer so all enemies don't fire at once
        fireTimer = Random.Range(0f, fireRate);
        meleeTimer = Random.Range(0f, meleeCooldown);
        gruntReactiveTarget = player != null ? player.position : transform.position;
        gruntReactionTimer = Random.Range(0f, Mathf.Max(0.01f, gruntReactionTime));
        shooterBurstTimer = Random.Range(0.12f, 0.32f);
        shooterBurstShotsRemaining = 0;
        gruntPounceCooldown = Random.Range(0.8f, 1.6f);
        tankShockwaveCooldown = Random.Range(1.8f, 2.8f);
        flyingVolleyCooldown = Random.Range(1.2f, 2.1f);
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
            if (flashTimer <= 0 && enemyRenderer != null && enemyRenderer.material != null)
                enemyRenderer.material.color = originalColor;
        }

        if (hurtPulseTimer > 0f) hurtPulseTimer -= Time.deltaTime;
        if (attackPulseTimer > 0f) attackPulseTimer -= Time.deltaTime;
        if (shooterBurstTimer > 0f) shooterBurstTimer -= Time.deltaTime;
        if (gruntPounceCooldown > 0f) gruntPounceCooldown -= Time.deltaTime;
        if (tankShockwaveCooldown > 0f) tankShockwaveCooldown -= Time.deltaTime;
        if (flyingVolleyCooldown > 0f) flyingVolleyCooldown -= Time.deltaTime;
        if (bossAttackCooldown > 0f) bossAttackCooldown -= Time.deltaTime;
        if (bossSpecialCooldown > 0f) bossSpecialCooldown -= Time.deltaTime;

        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        UpdateModelMotion(distanceToPlayer);
        Vector3 reactivePlayerPosition = GetReactivePlayerPosition();

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
                Vector3 target = enemyType == EnemyType.Grunt ? reactivePlayerPosition : player.position;
                if (enemyType == EnemyType.Shooter)
                {
                    Vector3 lateral = Vector3.Cross(Vector3.up, (player.position - transform.position).normalized);
                    target += lateral * Mathf.Sin(Time.time * 2f) * 2.4f;
                }
                else if (enemyType == EnemyType.Tank)
                {
                    agent.speed = Mathf.Max(agent.speed, 2.5f);
                    agent.acceleration = Mathf.Max(agent.acceleration, 10f);
                }

                if (enemyType == EnemyType.Grunt)
                    agent.speed = Mathf.Max(0.1f, moveSpeed * gruntMoveSpeedMultiplier);

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
                flyPhase += Time.deltaTime * bobFrequency;
                Vector3 towardPlayer = (player.position - transform.position);
                towardPlayer.y = 0f;
                Vector3 orbitDir = towardPlayer.sqrMagnitude > 0.001f ? Vector3.Cross(Vector3.up, towardPlayer.normalized) : transform.right;
                Vector3 orbitOffset = orbitDir * Mathf.Sin(flyPhase) * orbitRadius;
                Vector3 desired = player.position + Vector3.up * hoverHeight + orbitOffset + Vector3.up * Mathf.Sin(flyPhase * 1.4f) * bobAmplitude;
                transform.position = Vector3.MoveTowards(transform.position, desired, flySpeed * Time.deltaTime);

                // Don’t tunnel through floors/walls when moving to lower levels.
                ClampToAccessibleSpace();
                FacePlayer();
            }
            else
            {
                Vector3 target = enemyType == EnemyType.Grunt ? reactivePlayerPosition : player.position;
                target.y = hasGroundAnchor ? groundY : transform.position.y;

                Vector3 toPlayer = target - transform.position;
                float planarDistance = toPlayer.magnitude;
                if (planarDistance > 0.001f)
                {
                    Vector3 moveDir = toPlayer / planarDistance;

                    if (enemyType == EnemyType.Grunt)
                    {
                        if (planarDistance > meleeRange)
                        {
                            Vector3 groundedTarget = new Vector3(reactivePlayerPosition.x, target.y, reactivePlayerPosition.z);
                            transform.position = Vector3.MoveTowards(transform.position, groundedTarget, moveSpeed * gruntMoveSpeedMultiplier * Time.deltaTime);
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
                            Vector3 groundedTarget = new Vector3(player.position.x, target.y, player.position.z);
                            transform.position = Vector3.MoveTowards(transform.position, groundedTarget, moveSpeed * 0.55f * Time.deltaTime);
                        }
                        else
                        {
                            FacePlayer();
                        }
                    }
                    else if (planarDistance > stoppingDistance)
                    {
                        Vector3 desired = new Vector3(player.position.x, target.y, player.position.z);
                        Vector3 lateral = Vector3.Cross(Vector3.up, moveDir).normalized;
                        Vector3 detourBias = lateral * Mathf.Sin(Time.time * 2.2f) * obstacleAvoidanceDistance;
                        Vector3 strafing = desired + detourBias;

                        // Prefer navigating around obstacles instead of walking through them.
                        if (!HasLineOfSightTo(player.position + Vector3.up * 1.2f))
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

                        transform.position = Vector3.MoveTowards(transform.position, strafing, moveSpeed * Time.deltaTime);
                    }

                    if (hasGroundAnchor)
                    {
                        Vector3 grounded = transform.position;
                        grounded.y = groundY;
                        transform.position = grounded;
                    }

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
            if (distanceToPlayer <= stoppingDistance + 5f) // Add a little buffer so they shoot as they approach
            {
                fireTimer -= Time.deltaTime;
                if (fireTimer <= 0)
                {
                    StartShooterBurst();
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
            if (distanceToPlayer > meleeRange * 1.8f && distanceToPlayer < 9f && gruntPounceCooldown <= 0f)
            {
                PerformGruntPounce();
                gruntPounceCooldown = Random.Range(1.8f, 3.1f);
            }

            if (distanceToPlayer <= meleeRange)
            {
                meleeTimer -= Time.deltaTime;
                if (meleeTimer <= 0f)
                {
                    TryMeleeAttack();
                    meleeTimer = meleeCooldown;
                }
            }
        }
        else if (enemyType == EnemyType.Tank)
        {
            if (distanceToPlayer <= 7f && tankShockwaveCooldown <= 0f)
            {
                EmitTankShockwave();
                tankShockwaveCooldown = Random.Range(2.8f, 4.2f);
            }

            // Tanks could have slower fire and more health. For now, reuse shooter behavior but slower.
            if (distanceToPlayer <= stoppingDistance + 5f)
            {
                fireTimer -= Time.deltaTime;
                if (fireTimer <= 0)
                {
                    if (projectilePrefab != null)
                        Shoot();
                    else
                        TryDirectDamage(14f);
                    fireTimer = fireRate * 1.6f;
                }
            }
        }
        else if (enemyType == EnemyType.Flying)
        {
            if (distanceToPlayer <= stoppingDistance + 8f && flyingVolleyCooldown <= 0f)
            {
                ShootWithSpread(-9f);
                ShootWithSpread(0f);
                ShootWithSpread(9f);
                flyingVolleyCooldown = Random.Range(2.2f, 3.8f);
            }

            // Flying units can fire when roughly within shooting range
            if (distanceToPlayer <= stoppingDistance + 6f)
            {
                fireTimer -= Time.deltaTime;
                if (fireTimer <= 0)
                {
                    if (projectilePrefab != null)
                        Shoot();
                    else
                        TryDirectDamage(10f);
                    fireTimer = fireRate * 0.75f;
                }
            }
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
                    if (!routineActive && bossSpecialCooldown <= 0f)
                    {
                        if (phase >= 1 && Random.value > 0.45f)
                            StartBossRoutine(BossCageLock(3.8f + phase * 0.45f, meleeDamage * (0.72f + phase * 0.15f)));
                        else
                            StartBossRoutine(BossCrossfireLock(3 + phase, 5.8f + phase, meleeDamage * (0.55f + phase * 0.15f)));
                        bossSpecialCooldown = Random.Range(4.2f, 5.4f) - phase * 0.35f;
                    }
                    else if (!routineActive && bossAttackCooldown <= 0f)
                    {
                        StartBossRoutine(BossPulseSequence(3.2f + phase, meleeDamage * (0.65f + phase * 0.15f), 0.32f, new Color(1f, 0.36f, 0.08f)));
                        bossAttackCooldown = Random.Range(1.1f, 1.6f) - phase * 0.12f;
                    }
                    else if (!routineActive && shooterBurstShotsRemaining <= 0 && shooterBurstTimer <= 0f)
                    {
                        FireSpreadFan(4 + phase, 14f + phase * 4f);
                        shooterBurstTimer = 0.8f;
                    }
                }
                break;

            case BossArchetype.Striker:
                if (!routineActive && bossSpecialCooldown <= 0f)
                {
                    if (phase >= 1 && Random.value > 0.4f)
                        StartBossRoutine(BossRazorSweep(2 + phase, 8.8f + phase * 1.1f, 2.6f + phase * 0.3f, meleeDamage * (0.6f + phase * 0.14f)));
                    else
                        StartBossRoutine(BossComboAssault(2 + phase, 8.2f + phase, 3.4f + phase * 0.55f, meleeDamage * (0.72f + phase * 0.16f)));
                    bossSpecialCooldown = Random.Range(3.6f, 4.8f) - phase * 0.3f;
                }
                else if (distanceToPlayer <= meleeRange * (1.5f + phase * 0.15f))
                {
                    meleeTimer -= Time.deltaTime;
                    if (meleeTimer <= 0f)
                    {
                        TryMeleeAttack();
                        meleeTimer = Mathf.Max(0.48f, meleeCooldown * 0.62f);
                    }
                }
                else if (!routineActive && bossAttackCooldown <= 0f)
                {
                    Vector3 rushTarget = player.position;
                    rushTarget.y = transform.position.y;
                    transform.position = Vector3.MoveTowards(transform.position, rushTarget, moveSpeed * (3f + phase * 0.6f) * Time.deltaTime);
                    if (distanceToPlayer <= 4.8f)
                    {
                        StartBossRoutine(BossGroundStrike(player.position, 3.2f + phase * 0.4f, meleeDamage * 0.7f, new Color(1f, 0.18f, 0.12f)));
                        bossAttackCooldown = Random.Range(1.0f, 1.5f);
                    }
                }
                break;

            case BossArchetype.Sentinel:
                if (!routineActive && bossSpecialCooldown <= 0f)
                {
                    if (phase >= 1 && Random.value > 0.4f)
                        StartBossRoutine(BossSkyLanceBarrage(3 + phase, 2.4f + phase * 0.3f, meleeDamage * (0.56f + phase * 0.12f)));
                    else
                        StartBossRoutine(BossSentinelDiveRun(2 + phase, 2.8f + phase * 0.4f, meleeDamage * 0.72f));
                    bossSpecialCooldown = Random.Range(3.2f, 4.1f) - phase * 0.25f;
                }
                else if (!routineActive && distanceToPlayer <= stoppingDistance + 12f && bossAttackCooldown <= 0f)
                {
                    StartBossRoutine(BossSentinelStrafeVolley(4 + phase, meleeDamage * 0.45f));
                    bossAttackCooldown = Random.Range(0.95f, 1.35f) - phase * 0.08f;
                }
                break;
        }
    }

    private void HandleBossPositioning(float distanceToPlayer)
    {
        if (player == null || bossRoutine != null) return;

        Vector3 playerFlat = player.position;
        playerFlat.y = hasGroundAnchor ? groundY : transform.position.y;

        switch (bossArchetype)
        {
            case BossArchetype.Warden:
            {
                Vector3 orbit = new Vector3(Mathf.Sin(Time.time * 0.9f), 0f, Mathf.Cos(Time.time * 0.9f)) * (6f + GetBossPhase() * 1.2f);
                Vector3 hoverTarget = playerFlat + orbit + Vector3.up * Mathf.Max(hoverHeight + 1.8f, 4.2f);
                transform.position = Vector3.Lerp(transform.position, hoverTarget, Time.deltaTime * 1.8f);
                FacePlayer();
                break;
            }
            case BossArchetype.Striker:
                if (distanceToPlayer > meleeRange * 1.2f)
                {
                    Vector3 rushTarget = Vector3.MoveTowards(transform.position, playerFlat, moveSpeed * (1.8f + GetBossPhase() * 0.35f) * Time.deltaTime);
                    transform.position = rushTarget;
                    ClampToAccessibleSpace();
                }
                FacePlayer();
                break;
            case BossArchetype.Sentinel:
            {
                Vector3 lateral = Vector3.Cross(Vector3.up, (player.position - transform.position).normalized);
                if (lateral.sqrMagnitude < 0.01f) lateral = transform.right;
                float sway = Mathf.Sin(Time.time * 1.6f) * (5.2f + GetBossPhase());
                Vector3 glideTarget = player.position + lateral.normalized * sway + Vector3.up * (hoverHeight + 4.8f);
                transform.position = Vector3.Lerp(transform.position, glideTarget, Time.deltaTime * 1.65f);
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

        Vector3 origin = transform.position + Vector3.up * 2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y + 0.05f;
            hasGroundAnchor = true;
        }
    }

    private Vector3 GetReactivePlayerPosition()
    {
        if (player == null) return transform.position;
        if (enemyType != EnemyType.Grunt || gruntReactionTime <= 0f)
            return player.position;

        gruntReactionTimer -= Time.deltaTime;
        if (gruntReactionTimer <= 0f)
        {
            gruntReactiveTarget = player.position;
            gruntReactionTimer = gruntReactionTime;
        }

        return gruntReactiveTarget;
    }

    private void ClampToAccessibleSpace()
    {
        if (enemyType == EnemyType.Flying) return;

        Vector3 origin = transform.position + Vector3.up * 0.6f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 8f, movementObstacleMask, QueryTriggerInteraction.Ignore))
        {
            float minY = hit.point.y + 0.05f;
            if (transform.position.y < minY)
            {
                Vector3 p = transform.position;
                p.y = minY;
                transform.position = p;
            }
        }
    }

    private bool HasLineOfSightTo(Vector3 targetPos)
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Vector3 dir = targetPos - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;
        dir /= dist;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, movementObstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && (hit.collider.transform.IsChildOf(transform) || hit.collider.gameObject == gameObject))
                return true;
            return false;
        }

        return true;
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

    private void FacePlayer()
    {
        if (player == null) return;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f) return;

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction.normalized), 7f * Time.deltaTime);
    }

    private void TryMeleeAttack()
    {
        if (player == null) return;

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController == null) return;

        attackPulseTimer = 0.16f;
        playerController.TakeDamage(meleeDamage);
    }

    private void StartShooterBurst()
    {
        shooterBurstShotsRemaining = isBoss ? 3 : 2;
        shooterBurstTimer = 0f;
    }

    private void PerformGruntPounce()
    {
        if (player == null) return;
        Vector3 target = player.position;
        target.y = hasGroundAnchor ? groundY : transform.position.y;
        transform.position = Vector3.MoveTowards(transform.position, target, Mathf.Max(3.5f, moveSpeed * 2.8f) * Time.deltaTime);
        attackPulseTimer = 0.18f;
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
            playerController.TakeDamage(meleeDamage * 0.85f);
        }
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

    private IEnumerator BossDashSlam(float dashSpeed, float radius, float damage)
    {
        if (player == null) yield break;

        Vector3 start = transform.position;
        Vector3 end = player.position;
        end.y = transform.position.y;
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
            yield return new WaitForSeconds(0.42f);
            DamagePlayerNearLine(lineStart, lineEnd, 1.1f, damage);
            SpawnTelegraphLine(lineStart, lineEnd, Color.white, 0.12f);
            yield return new WaitForSeconds(0.14f);
        }

        attackPulseTimer = 0.26f;
    }

    private IEnumerator BossCageLock(float halfExtent, float damage)
    {
        if (player == null) yield break;

        Vector3 center = player.position;
        center.y = hasGroundAnchor ? groundY : transform.position.y;

        Vector3 frontLeft = center + new Vector3(-halfExtent, 0.05f, halfExtent);
        Vector3 frontRight = center + new Vector3(halfExtent, 0.05f, halfExtent);
        Vector3 backLeft = center + new Vector3(-halfExtent, 0.05f, -halfExtent);
        Vector3 backRight = center + new Vector3(halfExtent, 0.05f, -halfExtent);

        SpawnTelegraphLine(frontLeft, frontRight, new Color(1f, 0.54f, 0.12f), 0.62f);
        SpawnTelegraphLine(frontRight, backRight, new Color(1f, 0.54f, 0.12f), 0.62f);
        SpawnTelegraphLine(backRight, backLeft, new Color(1f, 0.54f, 0.12f), 0.62f);
        SpawnTelegraphLine(backLeft, frontLeft, new Color(1f, 0.54f, 0.12f), 0.62f);
        SpawnTelegraphDisc(center, halfExtent * 0.82f, new Color(1f, 0.26f, 0.08f), 0.62f);
        yield return new WaitForSeconds(0.48f);

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
            yield return BossGroundStrike(player.position, radius + 0.8f, damage * 1.1f, new Color(1f, 0.32f, 0.12f));
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
            Vector3 start = player.position + lateral * (4.8f * side);
            Vector3 end = player.position - lateral * (4.8f * side);
            start.y = hasGroundAnchor ? groundY : transform.position.y;
            end.y = start.y;

            transform.position = start;
            SpawnTelegraphLine(start, end, new Color(1f, 0.22f, 0.12f), 0.3f);
            yield return new WaitForSeconds(0.16f);

            float elapsed = 0f;
            float duration = Mathf.Clamp(Vector3.Distance(start, end) / Mathf.Max(0.1f, dashSpeed), 0.08f, 0.26f);
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

        yield return BossGroundStrike(player.position, radius + 0.9f, damage * 1.1f, new Color(1f, 0.30f, 0.14f));
        attackPulseTimer = 0.3f;
    }

    private IEnumerator BossSentinelDiveRun(int strikeCount, float radius, float damage)
    {
        if (player == null) yield break;

        Vector3 riseTarget = player.position + Vector3.up * (hoverHeight + 6f);
        float riseElapsed = 0f;
        Vector3 riseStart = transform.position;
        while (riseElapsed < 0.45f)
        {
            riseElapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(riseStart, riseTarget, Mathf.Clamp01(riseElapsed / 0.45f));
            yield return null;
        }

        for (int i = 0; i < strikeCount; i++)
        {
            Vector3 target = player.position + new Vector3(Mathf.Sin(Time.time + i) * 1.8f, 0f, Mathf.Cos(Time.time * 1.2f + i) * 1.8f);
            yield return BossGroundStrike(target, radius, damage, new Color(0.42f, 0.88f, 1f));
            yield return new WaitForSeconds(0.08f);
        }

        Vector3 diveTarget = player.position;
        diveTarget.y = hasGroundAnchor ? groundY : transform.position.y;
        SpawnTelegraphLine(transform.position, diveTarget, new Color(0.62f, 0.95f, 1f), 0.24f);
        yield return new WaitForSeconds(0.2f);
        transform.position = diveTarget;
        DamagePlayerIfInside(diveTarget, radius * 0.8f, damage * 1.15f);
        SpawnTelegraphDisc(diveTarget, radius * 0.72f, Color.white, 0.12f);
        attackPulseTimer = 0.26f;
    }

    private IEnumerator BossSkyLanceBarrage(int strikeCount, float radius, float damage)
    {
        if (player == null) yield break;

        Vector3 highAnchor = player.position + Vector3.up * (hoverHeight + 8f);
        Vector3 start = transform.position;
        float riseElapsed = 0f;
        while (riseElapsed < 0.32f)
        {
            riseElapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, highAnchor, Mathf.Clamp01(riseElapsed / 0.32f));
            yield return null;
        }

        for (int i = 0; i < strikeCount; i++)
        {
            float angle = (Mathf.PI * 2f / Mathf.Max(1, strikeCount)) * i;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (1.8f + i * 0.55f);
            Vector3 target = player.position + offset;
            target.y = hasGroundAnchor ? groundY : transform.position.y;
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
        Vector3 lateral = Vector3.Cross(Vector3.up, (player.position - transform.position).normalized);
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
            yield return BossGroundStrike(player.position, 2.2f, damage, new Color(0.42f, 0.88f, 1f));
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
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController == null) return;

        Vector3 flatPlayer = player.position;
        flatPlayer.y = center.y;
        if (Vector3.Distance(flatPlayer, center) <= radius)
            playerController.TakeDamage(damage);
    }

    private void DamagePlayerNearLine(Vector3 start, Vector3 end, float width, float damage)
    {
        if (player == null) return;
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController == null) return;

        Vector3 playerPos = player.position;
        playerPos.y = start.y;
        Vector3 line = end - start;
        float lengthSq = Mathf.Max(0.001f, line.sqrMagnitude);
        float t = Mathf.Clamp01(Vector3.Dot(playerPos - start, line) / lengthSq);
        Vector3 closest = start + line * t;
        if (Vector3.Distance(playerPos, closest) <= width)
            playerController.TakeDamage(damage);
    }

    private void SpawnTelegraphDisc(Vector3 center, float radius, Color color, float lifetime)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "BossTelegraphDisc";
        disc.transform.position = center + Vector3.up * 0.06f;
        disc.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
        Collider collider = disc.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer renderer = disc.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            mat.color = new Color(color.r, color.g, color.b, 0.45f);
            renderer.material = mat;
        }
        Destroy(disc, lifetime);
    }

    private void SpawnTelegraphLine(Vector3 start, Vector3 end, Color color, float lifetime)
    {
        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = "BossTelegraphLine";
        Vector3 direction = end - start;
        float length = Mathf.Max(0.1f, direction.magnitude);
        line.transform.position = start + direction * 0.5f + Vector3.up * 0.08f;
        line.transform.rotation = Quaternion.LookRotation(direction.normalized);
        line.transform.localScale = new Vector3(0.3f, 0.06f, length);
        Collider collider = line.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer renderer = line.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            mat.color = new Color(color.r, color.g, color.b, 0.42f);
            renderer.material = mat;
        }
        Destroy(line, lifetime);
    }

    private void TryDirectDamage(float amount)
    {
        if (player == null) return;

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController == null) return;

        attackPulseTimer = 0.12f;
        playerController.TakeDamage(amount);
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

        modelRoot.localPosition = Vector3.Lerp(modelRoot.localPosition, localOffset, Time.deltaTime * 8f);
        modelRoot.localRotation = Quaternion.Slerp(modelRoot.localRotation, baseModelLocalRotation * sway, Time.deltaTime * (walk * 6f + 4f));
        modelRoot.localScale = Vector3.Lerp(modelRoot.localScale, pulseScale, Time.deltaTime * 14f);
    }

    void Shoot()
    {
        if (projectilePrefab == null || shootPoint == null || player == null) return;

        attackPulseTimer = 0.12f;

        // Aim slightly ahead/at the player's center
        Vector3 targetPos = player.position + Vector3.up * 1f; 
        
        GameObject bullet = Instantiate(projectilePrefab, shootPoint.position, Quaternion.LookRotation(targetPos - shootPoint.position));
        
        // Give ownership so the enemy doesn't instantly hit itself in the face
        Projectile p = bullet.GetComponent<Projectile>();
        if (p != null) p.owner = gameObject;

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        
        if (rb != null)
        {
#pragma warning disable 0618
            rb.velocity = bullet.transform.forward * 20f; // Enemy bullets should be a bit slower so you can dodge them
#pragma warning restore 0618
        }
    }

    private void ShootWithSpread(float yawOffset)
    {
        if (projectilePrefab == null || shootPoint == null || player == null) return;

        attackPulseTimer = 0.12f;
        Vector3 targetPos = player.position + Vector3.up * 1f;
        Vector3 dir = (targetPos - shootPoint.position).normalized;
        dir = Quaternion.Euler(0f, yawOffset, 0f) * dir;

        GameObject bullet = Instantiate(projectilePrefab, shootPoint.position, Quaternion.LookRotation(dir));
        Projectile p = bullet.GetComponent<Projectile>();
        if (p != null) p.owner = gameObject;
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
#pragma warning disable 0618
            rb.velocity = dir * 20f;
#pragma warning restore 0618
        }
    }

    public void TakeDamage(float amount)
    {
        if (enemyType == EnemyType.Tank && !isBoss)
            amount *= 0.82f;
        else if (enemyType == EnemyType.Flying)
            amount *= 0.9f;

        currentHealth -= amount;
        
        if (enemyRenderer != null && enemyRenderer.material != null)
        {
            enemyRenderer.material.color = damageColor;
            flashTimer = 0.1f;
            hurtPulseTimer = 0.18f;
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDying) return;
        isDying = true;
        CybergrindRunState.GetOrCreate().RegisterEnemyDefeated();
        SpawnDeathBurst();
        Destroy(gameObject);
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

        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "EnemyBurstShard";
            shard.transform.position = transform.position + Vector3.up * 1f;
            shard.transform.localScale = Vector3.one * Random.Range(isBoss ? 0.16f : 0.12f, isBoss ? 0.34f : 0.24f);
            Renderer renderer = shard.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                renderer.material.color = burstColor;
            }

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
        if (ringRenderer != null)
        {
            Material ringMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            ringMat.color = new Color(burstColor.r, burstColor.g, burstColor.b, 0.55f);
            ringRenderer.material = ringMat;
        }
        Destroy(ring, 0.4f);
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
        if (enemyRenderer != null && enemyRenderer.material != null)
            originalColor = enemyRenderer.material.color;

        if (shootPoint == null)
        {
            GameObject sp = new GameObject("ShootPoint");
            sp.transform.SetParent(transform, false);
            shootPoint = sp.transform;
        }

        shootPoint.localPosition = GetShootPointLocalPosition();
        shootPoint.localRotation = Quaternion.identity;
    }

    private void BuildShooterModel(Transform root)
    {
        bodyMaterial.color = shooterColor * 0.82f;
        glowMaterial.color = coreGlowColor;

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
        bodyMaterial.color = gruntColor * 0.82f;
        glowMaterial.color = new Color(1f, 0.18f, 0.08f);

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
        bodyMaterial.color = tankColor * 0.9f;
        glowMaterial.color = new Color(1f, 0.62f, 0.08f);

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
        Material bossGlow = new Material(glowMaterial) { name = "Boss Glow" };
        bossGlow.color = new Color(1f, 0.24f, 0.04f);

        CreateModelPart(root, "BossHalo", PrimitiveType.Cylinder, new Vector3(0f, 2.52f, 0f), new Vector3(0.86f, 0.06f, 0.86f), Quaternion.identity, bossGlow);
        CreateModelPart(root, "BossHornL", PrimitiveType.Cube, new Vector3(-0.42f, 2.42f, 0f), new Vector3(0.18f, 0.7f, 0.18f), Quaternion.Euler(0f, 0f, 24f), bossGlow);
        CreateModelPart(root, "BossHornR", PrimitiveType.Cube, new Vector3(0.42f, 2.42f, 0f), new Vector3(0.18f, 0.7f, 0.18f), Quaternion.Euler(0f, 0f, -24f), bossGlow);
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
            renderer.sharedMaterial = material;

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
        if (bodyMaterial == null)
            bodyMaterial = new Material(FindVisualShader(false)) { name = "Enemy Body" };

        if (darkMaterial == null)
        {
            darkMaterial = new Material(FindVisualShader(false)) { name = "Enemy Dark Armor" };
            darkMaterial.color = new Color(0.04f, 0.045f, 0.055f);
        }

        if (glowMaterial == null)
        {
            glowMaterial = new Material(FindVisualShader(true)) { name = "Enemy Glow" };
            glowMaterial.EnableKeyword("_EMISSION");
        }
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
        bodyMaterial.color = new Color(0.6f, 0.2f, 0.9f) * 0.9f;
        glowMaterial.color = new Color(0.0f, 0.9f, 1f);

        CreateModelPart(root, "Core", PrimitiveType.Sphere, new Vector3(0f, 1.0f, 0f), new Vector3(0.6f, 0.6f, 0.6f), Quaternion.identity, bodyMaterial);
        CreateModelPart(root, "Cockpit", PrimitiveType.Cube, new Vector3(0f, 1.18f, 0.25f), new Vector3(0.5f, 0.28f, 0.6f), Quaternion.Euler(0f, 10f, 0f), darkMaterial);
        CreateModelPart(root, "LeftRotor", PrimitiveType.Cube, new Vector3(-0.6f, 1.3f, 0f), new Vector3(0.12f, 0.02f, 1.4f), Quaternion.identity, glowMaterial);
        CreateModelPart(root, "RightRotor", PrimitiveType.Cube, new Vector3(0.6f, 1.3f, 0f), new Vector3(0.12f, 0.02f, 1.4f), Quaternion.identity, glowMaterial);
        CreateModelPart(root, "TailFin", PrimitiveType.Cube, new Vector3(0f, 1.08f, -0.55f), new Vector3(0.18f, 0.46f, 0.12f), Quaternion.Euler(12f, 0f, 0f), darkMaterial);

        // small guns
        CreateModelPart(root, "GunL", PrimitiveType.Cylinder, new Vector3(-0.28f, 1.02f, 0.6f), new Vector3(0.06f, 0.34f, 0.06f), Quaternion.Euler(90f, 0f, 0f), glowMaterial);
        CreateModelPart(root, "GunR", PrimitiveType.Cylinder, new Vector3(0.28f, 1.02f, 0.6f), new Vector3(0.06f, 0.34f, 0.06f), Quaternion.Euler(90f, 0f, 0f), glowMaterial);
    }
}
