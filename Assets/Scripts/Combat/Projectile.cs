using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float damage = 10f;
    public float lifetime = 3f;
    public float impactScalePulse = 1.35f;
    public float impactLifetime = 0.08f;
    [HideInInspector] public GameObject owner; // Tells the bullet who shot it so they don't shoot themselves
    
    [Header("Impact FX")]
    public GameObject impactEffectPrefab; // Drag a particle system prefab here

    [Header("Impact Audio")]
    public AudioClip impactSound;
    public float impactVolume = 0.9f;

    public void Initialize(GameObject projectileOwner, float projectileDamage)
    {
        owner = projectileOwner;
        damage = projectileDamage;
        IgnoreOwnerColliders();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.useGravity = false;
    }

    void Start()
    {
        IgnoreOwnerColliders();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            // Mark as kinematic if spawned too close to avoid stuck geometry
            if (Vector3.Distance(transform.position, owner != null ? owner.transform.position : Vector3.zero) < 1.5f)
                rb.isKinematic = true;
        }

        // Automatically destroy bullet after a few seconds so it doesn't clutter the game
        Destroy(gameObject, lifetime);
    }

    void OnCollisionEnter(Collision collision)
    {
        // Don't hit the person who shot this!
        if (IsOwnerCollision(collision.collider)) return;

        // Check if what we hit can take damage
        IDamageable damageable = collision.collider.GetComponent<IDamageable>();
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

    private void IgnoreOwnerColliders()
    {
        if (owner == null) return;

        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider == null) return;

        Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            if (ownerColliders[i] != null)
                Physics.IgnoreCollision(ownCollider, ownerColliders[i], true);
        }
    }

    private bool IsOwnerCollision(Collider other)
    {
        if (owner == null || other == null) return false;
        return other.gameObject == owner || other.transform.IsChildOf(owner.transform);
    }
}
