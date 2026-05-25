using UnityEngine;
using UnityEngine.AI;

public class BasicEnemyAI : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public float maxHealth = 30f;
    private float currentHealth;

    [Header("Movement")]
    public float stoppingDistance = 10f; // How close they get before they stop to shoot
    private NavMeshAgent agent;
    private Transform player;

    [Header("Combat")]
    public GameObject projectilePrefab;
    public Transform shootPoint;
    public float fireRate = 1.5f;
    private float fireTimer;

    [Header("Effects")]
    public Color damageColor = Color.red;
    private Color originalColor;
    private Renderer enemyRenderer;
    private float flashTimer;

    void Start()
    {
        currentHealth = maxHealth;
        agent = GetComponent<NavMeshAgent>();
        enemyRenderer = GetComponent<Renderer>();

        if (enemyRenderer != null)
        {
            originalColor = enemyRenderer.material.color;
        }

        // Find the player automatically (Updated for modern Unity versions)
        PlayerController p = Object.FindAnyObjectByType<PlayerController>();
        if (p != null) player = p.transform;
    }

    void Update()
    {
        // 1. Damage Flash Revert
        if (flashTimer > 0)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0 && enemyRenderer != null)
                enemyRenderer.material.color = originalColor;
        }

        if (player == null) return;

        // 2. Movement (Chase the player)
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        if (distanceToPlayer > stoppingDistance)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        else
        {
            // Stop and look at player
            agent.isStopped = true;
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0; // Don't lean backwards when looking up
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 5f * Time.deltaTime);
        }

        // 3. Combat (Shoot at player if close enough and they can see them)
        if (distanceToPlayer <= stoppingDistance + 5f) // Add a little buffer so they shoot as they approach
        {
            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0)
            {
                Shoot();
                fireTimer = fireRate;
            }
        }
    }

    void Shoot()
    {
        if (projectilePrefab == null || shootPoint == null) return;

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

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = damageColor;
            flashTimer = 0.1f;
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        // In Ultrakill, enemies drop blood/health. We can add that later!
        Debug.Log("Enemy Defeated!");
        Destroy(gameObject);
    }
}
