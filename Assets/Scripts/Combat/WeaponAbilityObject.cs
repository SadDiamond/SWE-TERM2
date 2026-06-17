using UnityEngine;

public class WeaponAbilityObject : MonoBehaviour
{
    public enum Kind { Coin, Core, Bomb, Anchor, Spike }

    public Kind kind;
    public Gun owner;
    public float lifetime = 10f;
    public bool conductive = true;

    private void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) Destroy(gameObject);
        if (kind == Kind.Coin && Camera.main != null)
        {
            transform.forward = Camera.main.transform.forward;
            float flash = 0.72f + Mathf.Sin(Time.time * 18f) * 0.28f;
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = Color.Lerp(Color.white, new Color(0.2f, 0.85f, 1f), flash);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null) owner.HandleAbilityObjectCollision(this, collision);
    }

    public void Hit(float damage, Vector3 direction)
    {
        if (owner != null) owner.HandleAbilityObjectHit(this, damage, direction);
    }
}
