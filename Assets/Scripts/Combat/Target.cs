using UnityEngine;

public class Target : MonoBehaviour, IDamageable, IGrappleMassTarget
{
    [Header("Stats")]
    public float maxHealth = 50f;
    public float currentHealth { get; private set; }
    public GrappleMassClass grappleMassClass = GrappleMassClass.Heavy;
    [Min(0f)] public float grapplePullResponsiveness = 22f;
    [Min(0f)] public float grapplePullStopDistance = 1.7f;

    [Header("Effects")]
    public Color damageColor = Color.red;
    private Color originalColor;
    private Renderer targetRenderer;
    private Transform originalTransform;
    private float flashTimer;
    private float flashDuration = 0.1f;
    private Vector3 originalScale;
    public GrappleMassClass GrappleMassClass => grappleMassClass;

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

    public bool ApplyGrapplePull(Vector3 pullTargetPoint, Vector3 pullDirection, float pullSpeed, float deltaTime)
    {
        if (grappleMassClass != GrappleMassClass.Light)
            return false;

        Vector3 currentPosition = transform.position;
        Vector3 toTarget = pullTargetPoint - currentPosition;
        float distance = toTarget.magnitude;
        if (distance <= grapplePullStopDistance)
            return true;

        Vector3 desiredDirection = toTarget / Mathf.Max(0.001f, distance);
        float moveSpeed = Mathf.Max(0f, pullSpeed) * Mathf.Max(0.2f, grapplePullResponsiveness / 22f);
        transform.position = currentPosition + desiredDirection * Mathf.Min(distance - grapplePullStopDistance, moveSpeed * deltaTime);
        return true;
    }
}
