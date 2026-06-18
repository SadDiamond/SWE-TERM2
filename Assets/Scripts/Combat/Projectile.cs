using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float damage = 10f;
    public float lifetime = 3f;
    public float impactScalePulse = 1.35f;
    public float impactLifetime = 0.08f;
    public float ownerIgnoreGraceTime = 0.28f;
    public float ownerClearanceRadius = 3.1f;
    [HideInInspector] public GameObject owner; // Tells the bullet who shot it so they don't shoot themselves
    
    [Header("Impact FX")]
    public GameObject impactEffectPrefab; // Drag a particle system prefab here

    [Header("Impact Audio")]
    public AudioClip impactSound;
    public float impactVolume = 0.9f;

    private float spawnedAtTime;
    private Vector3 spawnPosition;

    public void Initialize(GameObject projectileOwner, float projectileDamage)
    {
        owner = projectileOwner;
        damage = projectileDamage;
        spawnedAtTime = Time.time;
        spawnPosition = transform.position;
        IgnoreOwnerColliders();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.useGravity = false;
    }

    void Start()
    {
        if (spawnedAtTime <= 0f)
        {
            spawnedAtTime = Time.time;
            spawnPosition = transform.position;
        }

        IgnoreOwnerColliders();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.detectCollisions = true;
        }

        // Automatically destroy bullet after a few seconds so it doesn't clutter the game
        Destroy(gameObject, lifetime);
    }

    void FixedUpdate()
    {
        if (owner == null) return;
        if (!ShouldKeepIgnoringOwner()) return;
        IgnoreOwnerColliders();
    }

    void OnCollisionEnter(Collision collision)
    {
        // Don't hit the person who shot this!
        if (ShouldIgnoreCollision(collision.collider)) return;

        if (ShouldDiscardEarlyClearanceCollision(collision.collider))
            return;

        // Check if what we hit can take damage
        IDamageable damageable = collision.collider.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);

            // Small punchier hit reaction for enemies that expose a transform.
            if (collision.collider.attachedRigidbody != null)
            {
                Vector3 hitDir = collision.collider.transform.position - transform.position;
                hitDir.y *= 0.2f;
                collision.collider.attachedRigidbody.AddForce(hitDir.normalized * 2.8f, ForceMode.Impulse);
            }
        }

        // Spawn Impact Sparks/FX at the exact point of collision pointing outwards
        if (impactEffectPrefab != null)
        {
            ContactPoint contact = collision.contacts[0];
            // Rotate the sparks so they shoot OUT from the wall
            GameObject impact = Instantiate(impactEffectPrefab, contact.point, Quaternion.LookRotation(contact.normal));
            
            // Note: If you want bullet holes/decals later, you can spawn them here too, 
            // set slightly offset from contact.point and parented to the wall.

            // Clean up the impact particles after 2 seconds
            Destroy(impact, 2f);
        }

        if (impactSound != null)
            AudioSource.PlayClipAtPoint(impactSound, transform.position, impactVolume);

        // Tiny scale pulse for nearby impact feel before destruction.
        transform.localScale *= impactScalePulse;

        // Destroy the bullet immediately after hitting anything
        Destroy(gameObject, impactLifetime);
    }

    public void Reflect(GameObject newOwner, Vector3 direction, float speed, float damageMultiplier)
    {
        owner = newOwner;
        damage *= Mathf.Max(1f, damageMultiplier);
        spawnedAtTime = Time.time;
        spawnPosition = transform.position;
        transform.rotation = Quaternion.LookRotation(direction.normalized);
        IgnoreOwnerColliders();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
#pragma warning disable 0618
            rb.velocity = direction.normalized * speed;
#pragma warning restore 0618
        }
    }

    private void IgnoreOwnerColliders()
    {
        if (owner == null) return;

        Collider[] ownColliders = GetComponentsInChildren<Collider>(true);
        if (ownColliders == null || ownColliders.Length == 0) return;

        Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider ownCollider = ownColliders[i];
            if (ownCollider == null) continue;

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                if (ownerColliders[j] != null)
                    Physics.IgnoreCollision(ownCollider, ownerColliders[j], true);
            }
        }
    }

    private bool IsOwnerCollision(Collider other)
    {
        if (owner == null || other == null) return false;
        if (other.gameObject == owner ||
            other.transform.IsChildOf(owner.transform) ||
            transform.IsChildOf(owner.transform))
            return true;

        PlayerController hitPlayer = other.GetComponentInParent<PlayerController>();
        PlayerController ownerPlayer = owner.GetComponentInParent<PlayerController>();
        return hitPlayer != null && ownerPlayer != null && hitPlayer == ownerPlayer;
    }

    private bool ShouldIgnoreCollision(Collider other)
    {
        if (IsOwnerCollision(other)) return true;

        if (!ShouldKeepIgnoringOwner())
            return false;

        if (owner != null)
        {
            Vector3 ownerPoint = owner.transform.position + Vector3.up * 0.9f;
            if (Vector3.Distance(transform.position, ownerPoint) <= ownerClearanceRadius)
                return true;
        }

        return Vector3.Distance(transform.position, spawnPosition) <= ownerClearanceRadius * 0.55f;
    }

    private bool ShouldKeepIgnoringOwner()
    {
        if (Time.time - spawnedAtTime <= ownerIgnoreGraceTime)
            return true;

        if (owner != null)
        {
            Vector3 ownerPoint = owner.transform.position + Vector3.up * 0.9f;
            if (Vector3.Distance(transform.position, ownerPoint) <= ownerClearanceRadius)
                return true;
        }

        return Vector3.Distance(transform.position, spawnPosition) <= ownerClearanceRadius * 0.55f;
    }

    private bool ShouldDiscardEarlyClearanceCollision(Collider other)
    {
        if (other == null)
            return true;

        if (!ShouldKeepIgnoringOwner())
            return false;

        if (other.GetComponentInParent<IDamageable>() != null)
            return false;

        return true;
    }

#if UNITY_EDITOR
    public bool DebugShouldDiscardEarlyClearanceCollision(Collider other)
    {
        return ShouldDiscardEarlyClearanceCollision(other);
    }
#endif
}
