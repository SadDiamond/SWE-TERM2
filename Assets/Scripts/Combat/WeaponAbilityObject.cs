using UnityEngine;

public class WeaponAbilityObject : MonoBehaviour
{
    public enum Kind { Coin, Core, Bomb, Anchor, Spike }

    public Kind kind;
    public Gun owner;
    public float lifetime = 10f;
    public bool conductive = true;
    [Min(0f)] public float coinFirstGlintDelay = 1f / 3f;
    [Min(0f)] public float coinPersistentGlintDelay = 1f;

    private float coinAge;
    private bool coinFirstGlintTriggered;
    private bool coinPersistentGlintTriggered;
    private bool resolved;

    public bool TryResolve()
    {
        if (resolved) return false;
        resolved = true;
        return true;
    }

    private void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) Destroy(gameObject);
        if (kind == Kind.Coin)
        {
            coinAge += Time.unscaledDeltaTime;
            if (!coinFirstGlintTriggered && coinAge >= coinFirstGlintDelay)
            {
                coinFirstGlintTriggered = true;
                if (owner != null) owner.HandleCoinGlint(this, false);
            }
            if (!coinPersistentGlintTriggered && coinAge >= coinPersistentGlintDelay)
            {
                coinPersistentGlintTriggered = true;
                if (owner != null) owner.HandleCoinGlint(this, true);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (resolved) return;
        if (owner != null) owner.HandleAbilityObjectCollision(this, collision);
    }

    public void Hit(float damage, Vector3 direction)
    {
        if (kind == Kind.Coin && !TryResolve()) return;
        if (owner != null) owner.HandleAbilityObjectHit(this, damage, direction);
    }
}
