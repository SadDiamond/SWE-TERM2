using UnityEngine;

public class CybergrindPickup : MonoBehaviour
{
    public enum PickupType
    {
        Health,
        Coin
    }

    public PickupType pickupType = PickupType.Health;
    public float bobHeight = 0.22f;
    public float bobSpeed = 2.7f;
    public float spinSpeed = 90f;
    public int value = 1;
    public float healthRestore = 20f;

    private Vector3 basePosition;

    private void Start()
    {
        basePosition = transform.position;
    }

    private void Update()
    {
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = basePosition + Vector3.up * bob;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        if (pickupType == PickupType.Health)
        {
            player.Heal(healthRestore);
            player.ShowTransientStatus($"Hull patch +{Mathf.RoundToInt(healthRestore)}", 1.1f);
        }
        else
        {
            player.AddCurrency(Mathf.Max(1, value));
            player.ShowTransientStatus($"Coin cache +{Mathf.Max(1, value)}", 1.0f);
        }

        ProjectStructureAudioDirector audioDirector = FindAnyObjectByType<ProjectStructureAudioDirector>();
        if (audioDirector != null)
            audioDirector.PlayPickupCue(pickupType);

        SpawnPickupBurst();

        Destroy(gameObject);
    }

    private void SpawnPickupBurst()
    {
        Color burstColor = pickupType == PickupType.Health
            ? new Color(0.28f, 1f, 0.62f, 1f)
            : new Color(1f, 0.82f, 0.24f, 1f);

        for (int i = 0; i < 4; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "PickupBurstShard";
            shard.transform.position = transform.position + Vector3.up * 0.3f;
            shard.transform.localScale = Vector3.one * Random.Range(0.08f, 0.18f);

            if (shard.TryGetComponent(out Renderer renderer))
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                renderer.material = new Material(shader);
                renderer.material.color = burstColor;
            }

            Collider collider = shard.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Rigidbody rb = shard.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearVelocity = (Random.onUnitSphere + Vector3.up * 0.8f) * Random.Range(1.6f, 3.4f);
            Destroy(shard, 0.45f);
        }
    }
}
