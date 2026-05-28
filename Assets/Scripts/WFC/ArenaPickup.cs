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
        if (other.GetComponentInParent<PlayerController>() == null) return;
        Destroy(gameObject);
    }
}
