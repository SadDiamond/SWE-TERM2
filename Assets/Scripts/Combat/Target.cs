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
    private Transform originalTransform;
    private float flashTimer;
    private float flashDuration = 0.1f;
    private Vector3 originalScale;

    void Start()
    {
        currentHealth = maxHealth;
        // Prefer a renderer on the root, but fall back to any child renderer so models with nested meshes flash correctly
        targetRenderer = GetComponent<Renderer>();
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
        {
            originalColor = targetRenderer.material.color;
        }
        originalTransform = transform;
        originalScale = transform.localScale;
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

        // Quick hit-stagger style scale punch.
        StopAllCoroutines();
        StartCoroutine(PunchScale());

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

    private System.Collections.IEnumerator PunchScale()
    {
        float elapsed = 0f;
        float duration = 0.08f;
        Vector3 peak = originalScale * 1.08f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.Sin(t * Mathf.PI);
            transform.localScale = Vector3.Lerp(originalScale, peak, eased);
            yield return null;
        }
        transform.localScale = originalScale;
    }
}
