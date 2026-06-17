using UnityEngine;

public class WeaponLabProjectileTurret : MonoBehaviour
{
    public PlayerController target;
    public float fireInterval = 1.4f;
    public float projectileSpeed = 13f;
    private float nextFireTime;

    private void Update()
    {
        if (target == null || Time.time < nextFireTime) return;
        nextFireTime = Time.time + fireInterval;
        Vector3 origin = transform.position + Vector3.up * 1.2f;
        Vector3 destination = target.transform.position + Vector3.up * 1.1f;
        Vector3 direction = (destination - origin).normalized;
        GameObject shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shot.name = "WeaponLabParryShot";
        shot.transform.position = origin;
        shot.transform.localScale = Vector3.one * 0.24f;
        Renderer renderer = shot.GetComponent<Renderer>();
        renderer.material.color = new Color(1f, 0.26f, 0.12f);
        Rigidbody body = shot.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = direction * projectileSpeed;
        Projectile projectile = shot.AddComponent<Projectile>();
        projectile.lifetime = 5f;
        projectile.Initialize(gameObject, 8f);
    }
}
