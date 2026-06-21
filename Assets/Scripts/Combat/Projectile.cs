using UnityEngine;
using System.Collections.Generic;

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
    private float despawnAtTime;
    private float pendingReleaseAtTime = -1f;
    private Vector3 initialLocalScale;
    private Rigidbody cachedRigidbody;
    private GameObject sourcePrefab;
    private GameObject sourcePrefabKey;
    private GameObject ignoredOwner;
    private Collider[] cachedOwnColliders;
    private static readonly Dictionary<GameObject, Stack<Projectile>> ProjectilePools = new Dictionary<GameObject, Stack<Projectile>>();
    private static readonly Dictionary<GameObject, Stack<GameObject>> ImpactPools = new Dictionary<GameObject, Stack<GameObject>>();
    private static Transform audioPoolRoot;
    private static readonly List<AudioSource> AudioSourcePool = new List<AudioSource>();

    public static Projectile Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        GameObject key = prefab;
        if (ProjectilePools.TryGetValue(key, out Stack<Projectile> pool))
        {
            while (pool.Count > 0)
            {
                Projectile pooled = pool.Pop();
                if (pooled == null)
                    continue;

                pooled.transform.SetPositionAndRotation(position, rotation);
                pooled.gameObject.SetActive(true);
                pooled.PrepareForSpawn(prefab, key);
                return pooled;
            }
        }

        GameObject instance = Instantiate(prefab, position, rotation);
        Projectile projectile = instance.GetComponent<Projectile>();
        if (projectile != null)
            projectile.PrepareForSpawn(prefab, key);
        return projectile;
    }

    public void Initialize(GameObject projectileOwner, float projectileDamage)
    {
        ClearIgnoredOwnerColliders();
        owner = projectileOwner;
        damage = projectileDamage;
        spawnedAtTime = Time.time;
        spawnPosition = transform.position;
        despawnAtTime = Time.time + lifetime;
        pendingReleaseAtTime = -1f;
        transform.localScale = initialLocalScale == Vector3.zero ? Vector3.one : initialLocalScale;
        IgnoreOwnerColliders();

        Rigidbody rb = GetCachedRigidbody();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.detectCollisions = true;
            rb.isKinematic = false;
        }
    }

    void Start()
    {
        if (initialLocalScale == Vector3.zero)
            initialLocalScale = transform.localScale;

        if (spawnedAtTime <= 0f)
        {
            spawnedAtTime = Time.time;
            spawnPosition = transform.position;
            despawnAtTime = Time.time + lifetime;
        }

        IgnoreOwnerColliders();

        Rigidbody rb = GetCachedRigidbody();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.detectCollisions = true;
        }
    }

    void Update()
    {
        if (pendingReleaseAtTime > 0f)
        {
            if (Time.time >= pendingReleaseAtTime)
                ReleaseToPool();
            return;
        }

        if (despawnAtTime > 0f && Time.time >= despawnAtTime)
            ReleaseToPool();
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
            SpawnImpactEffect(contact.point, Quaternion.LookRotation(contact.normal));
        }

        PlayImpactSound(transform.position);

        // Tiny scale pulse for nearby impact feel before destruction.
        transform.localScale *= impactScalePulse;

        Rigidbody rb = GetCachedRigidbody();
        if (rb != null)
        {
            rb.detectCollisions = false;
            rb.linearVelocity = Vector3.zero;
        }
        pendingReleaseAtTime = Time.time + impactLifetime;
    }

    public void Reflect(GameObject newOwner, Vector3 direction, float speed, float damageMultiplier)
    {
        ClearIgnoredOwnerColliders();
        owner = newOwner;
        damage *= Mathf.Max(1f, damageMultiplier);
        spawnedAtTime = Time.time;
        spawnPosition = transform.position;
        transform.rotation = Quaternion.LookRotation(direction.normalized);
        IgnoreOwnerColliders();

        Rigidbody rb = GetCachedRigidbody();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = direction.normalized * speed;
        }
    }

    private void IgnoreOwnerColliders()
    {
        if (owner == null) return;

        Collider[] ownColliders = GetOwnColliders();
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

        ignoredOwner = owner;
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

    private void PrepareForSpawn(GameObject prefab, GameObject prefabKey)
    {
        sourcePrefab = prefab;
        sourcePrefabKey = prefabKey;
        ClearIgnoredOwnerColliders();
        owner = null;
        if (initialLocalScale == Vector3.zero)
            initialLocalScale = transform.localScale;
        transform.localScale = initialLocalScale;
        pendingReleaseAtTime = -1f;
        despawnAtTime = Time.time + lifetime;
        Rigidbody rb = GetCachedRigidbody();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.detectCollisions = true;
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private Rigidbody GetCachedRigidbody()
    {
        if (cachedRigidbody == null)
            cachedRigidbody = GetComponent<Rigidbody>();
        return cachedRigidbody;
    }

    private void ReleaseToPool()
    {
        pendingReleaseAtTime = -1f;
        despawnAtTime = 0f;
        owner = null;
        ClearIgnoredOwnerColliders();
        spawnedAtTime = 0f;
        spawnPosition = transform.position;

        Rigidbody rb = GetCachedRigidbody();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.detectCollisions = false;
            rb.isKinematic = true;
        }

        if (sourcePrefab == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!ProjectilePools.TryGetValue(sourcePrefabKey, out Stack<Projectile> pool))
        {
            pool = new Stack<Projectile>();
            ProjectilePools[sourcePrefabKey] = pool;
        }

        gameObject.SetActive(false);
        pool.Push(this);
    }

    private void SpawnImpactEffect(Vector3 position, Quaternion rotation)
    {
        if (impactEffectPrefab == null)
            return;

        GameObject key = impactEffectPrefab;
        GameObject impact = null;
        if (ImpactPools.TryGetValue(key, out Stack<GameObject> pool))
        {
            while (pool.Count > 0 && impact == null)
                impact = pool.Pop();
        }

        if (impact == null)
        {
            impact = Instantiate(impactEffectPrefab);
            PooledImpactEffect pooled = impact.GetComponent<PooledImpactEffect>();
            if (pooled == null)
                pooled = impact.AddComponent<PooledImpactEffect>();
            pooled.Configure(key);
        }

        impact.transform.SetPositionAndRotation(position, rotation);
        impact.SetActive(true);

        ParticleSystem[] particles = impact.GetComponentsInChildren<ParticleSystem>(true);
        float longestLifetime = 0.2f;
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null) continue;
            particle.Clear(true);
            particle.Play(true);
            var main = particle.main;
            longestLifetime = Mathf.Max(longestLifetime, main.duration + main.startLifetime.constantMax);
        }

        PooledImpactEffect controller = impact.GetComponent<PooledImpactEffect>();
        if (controller != null)
            controller.Activate(longestLifetime + 0.1f);
    }

    public static void ReturnImpactToPool(GameObject impact, GameObject prefabKey)
    {
        if (impact == null)
            return;

        if (!ImpactPools.TryGetValue(prefabKey, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            ImpactPools[prefabKey] = pool;
        }

        impact.SetActive(false);
        pool.Push(impact);
    }

    private void PlayImpactSound(Vector3 position)
    {
        if (impactSound == null)
            return;

        AudioSource source = GetAvailableAudioSource();
        if (source == null)
            return;

        source.transform.position = position;
        source.clip = impactSound;
        source.volume = impactVolume;
        source.Play();
    }

    private static AudioSource GetAvailableAudioSource()
    {
        EnsureAudioPool();
        for (int i = 0; i < AudioSourcePool.Count; i++)
        {
            AudioSource source = AudioSourcePool[i];
            if (source != null && !source.isPlaying)
                return source;
        }

        if (audioPoolRoot == null)
            return null;

        GameObject go = new GameObject("ProjectileImpactAudio");
        go.transform.SetParent(audioPoolRoot, false);
        AudioSource newSource = go.AddComponent<AudioSource>();
        newSource.playOnAwake = false;
        newSource.spatialBlend = 1f;
        newSource.rolloffMode = AudioRolloffMode.Linear;
        newSource.maxDistance = 32f;
        AudioSourcePool.Add(newSource);
        return newSource;
    }

    private static void EnsureAudioPool()
    {
        if (audioPoolRoot != null)
            return;

        GameObject root = new GameObject("_ProjectileAudioPool");
        audioPoolRoot = root.transform;
    }

    private Collider[] GetOwnColliders()
    {
        if (cachedOwnColliders == null || cachedOwnColliders.Length == 0)
            cachedOwnColliders = GetComponentsInChildren<Collider>(true);
        return cachedOwnColliders;
    }

    private void ClearIgnoredOwnerColliders()
    {
        if (ignoredOwner == null)
            return;

        Collider[] ownColliders = GetOwnColliders();
        if (ownColliders == null || ownColliders.Length == 0)
        {
            ignoredOwner = null;
            return;
        }

        Collider[] ownerColliders = ignoredOwner.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider ownCollider = ownColliders[i];
            if (ownCollider == null) continue;

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider ownerCollider = ownerColliders[j];
                if (ownerCollider != null)
                    Physics.IgnoreCollision(ownCollider, ownerCollider, false);
            }
        }

        ignoredOwner = null;
    }
}

public class PooledImpactEffect : MonoBehaviour
{
    private GameObject prefabKey;
    private float releaseAtTime;
    private bool active;

    public void Configure(GameObject key)
    {
        prefabKey = key;
    }

    public void Activate(float lifetime)
    {
        releaseAtTime = Time.time + Mathf.Max(0.05f, lifetime);
        active = true;
    }

    private void Update()
    {
        if (!active || Time.time < releaseAtTime)
            return;

        active = false;
        Projectile.ReturnImpactToPool(gameObject, prefabKey);
    }

    private void OnDisable()
    {
        active = false;
    }
}
