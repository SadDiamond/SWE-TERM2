using UnityEngine;

public class Target : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public float maxHealth = 50f;
    public float currentHealth { get; private set; }

    [Header("Effects")]
    public Color damageColor = Color.red;
    private Color originalColor;
    private Renderer targetRenderer;
    private float flashTimer;
    private float flashDuration = 0.1f;

    void Start()
    {
        currentHealth = maxHealth;
        targetRenderer = GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            originalColor = targetRenderer.material.color;
        }
    }

    void Update()
    {
        if (flashTimer > 0)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0 && targetRenderer != null)
            {
                targetRenderer.material.color = originalColor; // Revert color
            }
        }
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        
        // Damage Flash
        if (targetRenderer != null)
        {
            targetRenderer.material.color = damageColor;
            flashTimer = flashDuration;
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        // We can add particle effects or drop currency here later!
        Debug.Log($"{gameObject.name} was destroyed!");
        Destroy(gameObject);
    }
}
